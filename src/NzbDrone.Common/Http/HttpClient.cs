using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http.Dispatchers;
using NzbDrone.Common.TPL;

namespace NzbDrone.Common.Http
{
    public interface IHttpClient
    {
        HttpResponse Execute(HttpRequest request);
        void DownloadFile(string url, string fileName, string userAgent = null);

        // Deliberately an overload rather than a fourth optional parameter on
        // the method above: Moq's Verify/Setup lambdas are expression trees,
        // and C# forbids optional arguments there (CS0854). Adding a default
        // parameter breaks every existing `Verify(c => c.DownloadFile(a, b,
        // null))` across the test suite.
        void DownloadFile(string url, string fileName, string userAgent, TimeSpan? rateLimit);
        HttpResponse Get(HttpRequest request);
        HttpResponse<T> Get<T>(HttpRequest request)
            where T : new();
        HttpResponse Head(HttpRequest request);
        HttpResponse Post(HttpRequest request);
        HttpResponse<T> Post<T>(HttpRequest request)
            where T : new();

        Task<HttpResponse> ExecuteAsync(HttpRequest request);
        Task DownloadFileAsync(string url, string fileName, string userAgent = null);
        Task DownloadFileAsync(string url, string fileName, string userAgent, TimeSpan? rateLimit);
        Task<HttpResponse> GetAsync(HttpRequest request);
        Task<HttpResponse<T>> GetAsync<T>(HttpRequest request)
            where T : new();
        Task<HttpResponse> HeadAsync(HttpRequest request);
        Task<HttpResponse> PostAsync(HttpRequest request);
        Task<HttpResponse<T>> PostAsync<T>(HttpRequest request)
            where T : new();
    }

    public class HttpClient : IHttpClient
    {
        private const int MaxRedirects = 5;

        private readonly Logger _logger;
        private readonly IRateLimitService _rateLimitService;
        private readonly ICached<CookieContainer> _cookieContainerCache;
        private readonly List<IHttpRequestInterceptor> _requestInterceptors;
        private readonly IHttpDispatcher _httpDispatcher;

        public HttpClient(IEnumerable<IHttpRequestInterceptor> requestInterceptors,
            ICacheManager cacheManager,
            IRateLimitService rateLimitService,
            IHttpDispatcher httpDispatcher,
            Logger logger)
        {
            _requestInterceptors = requestInterceptors.ToList();
            _rateLimitService = rateLimitService;
            _httpDispatcher = httpDispatcher;
            _logger = logger;

            // ServicePointManager.DefaultConnectionLimit = 12 used to live here.
            // It was already doing nothing: every request goes through
            // ManagedHttpDispatcher, which builds a SocketsHttpHandler, and
            // ServicePointManager settings have never applied to those. The
            // limit it was trying to express is set for real — same value of
            // 12 — at ManagedHttpDispatcher.CreateHttpClient
            // (MaxConnectionsPerServer). .NET 10 obsoletes the API and the
            // strict build turns that into an error, which is what surfaced it.
            _cookieContainerCache = cacheManager.GetCache<CookieContainer>(typeof(HttpClient));
        }

        public virtual async Task<HttpResponse> ExecuteAsync(HttpRequest request)
        {
            var cookieContainer = InitializeRequestCookies(request);

            var response = await ExecuteRequestAsync(request, cookieContainer);

            if (request.AllowAutoRedirect && response.HasHttpRedirect)
            {
                var autoRedirectChain = new List<string> { request.Url.ToString() };

                do
                {
                    request.Url += new HttpUri(response.Headers.GetSingleValue("Location"));
                    autoRedirectChain.Add(request.Url.ToString());

                    _logger.Trace("Redirected to {0}", request.Url);

                    if (autoRedirectChain.Count > MaxRedirects)
                    {
                        throw new WebException($"Too many automatic redirections were attempted for {autoRedirectChain.Join(" -> ")}", WebExceptionStatus.ProtocolError);
                    }

                    // 302 or 303 should default to GET on redirect even if POST on original
                    if (RequestRequiresForceGet(response.StatusCode, response.Request.Method))
                    {
                        request.Method = HttpMethod.Get;
                        request.ContentData = null;
                        request.ContentSummary = null;
                    }

                    response = await ExecuteRequestAsync(request, cookieContainer);
                }
                while (response.HasHttpRedirect);
            }

            if (response.HasHttpRedirect && !RuntimeInfo.IsProduction)
            {
                _logger.Error("Server requested a redirect to [{0}] while in developer mode. Update the request URL to avoid this redirect.", response.Headers["Location"]);
            }

            if (!request.SuppressHttpError && response.HasHttpError && (request.SuppressHttpErrorStatusCodes == null || !request.SuppressHttpErrorStatusCodes.Contains(response.StatusCode)))
            {
                if (request.LogHttpError)
                {
                    _logger.Warn("HTTP Error - {0}", response);
                }

                if ((int)response.StatusCode == 429)
                {
                    throw new TooManyRequestsException(request, response);
                }
                else
                {
                    throw new HttpException(request, response);
                }
            }

            return response;
        }

        public HttpResponse Execute(HttpRequest request)
        {
            return ExecuteAsync(request).GetAwaiter().GetResult();
        }

        private static bool RequestRequiresForceGet(HttpStatusCode statusCode, HttpMethod requestMethod)
        {
            return statusCode switch
            {
                HttpStatusCode.Moved or HttpStatusCode.Found or HttpStatusCode.MultipleChoices => requestMethod == HttpMethod.Post,
                HttpStatusCode.SeeOther => requestMethod != HttpMethod.Get && requestMethod != HttpMethod.Head,
                _ => false,
            };
        }

        private async Task<HttpResponse> ExecuteRequestAsync(HttpRequest request, CookieContainer cookieContainer)
        {
            foreach (var interceptor in _requestInterceptors)
            {
                request = interceptor.PreRequest(request);
            }

            if (request.RateLimit != TimeSpan.Zero)
            {
                await _rateLimitService.WaitAndPulseAsync(request.Url.Host, request.RateLimitKey, request.RateLimit);
            }

            _logger.Trace(request);

            var stopWatch = Stopwatch.StartNew();

            var response = await _httpDispatcher.GetResponseAsync(request, cookieContainer);

            HandleResponseCookies(response, cookieContainer);

            stopWatch.Stop();

            _logger.Trace("{0} ({1} ms)", response, stopWatch.ElapsedMilliseconds);

            foreach (var interceptor in _requestInterceptors)
            {
                response = interceptor.PostResponse(response);
            }

            if (request.LogResponseContent && response.ResponseData != null)
            {
                _logger.Trace("Response content ({0} bytes): {1}", response.ResponseData.Length, response.Content);
            }

            return response;
        }

        private CookieContainer InitializeRequestCookies(HttpRequest request)
        {
            lock (_cookieContainerCache)
            {
                var sourceContainer = new CookieContainer();

                var presistentContainer = _cookieContainerCache.Get("container", () => new CookieContainer());
                var persistentCookies = presistentContainer.GetCookies((Uri)request.Url);
                sourceContainer.Add(persistentCookies);

                if (request.Cookies.Count != 0)
                {
                    foreach (var pair in request.Cookies)
                    {
                        Cookie cookie;
                        if (pair.Value == null)
                        {
                            cookie = new Cookie(pair.Key, "", "/")
                            {
                                Expires = DateTime.Now.AddDays(-1)
                            };
                        }
                        else
                        {
                            cookie = new Cookie(pair.Key, pair.Value, "/")
                            {
                                // Use Now rather than UtcNow to work around Mono cookie expiry bug.
                                // See https://gist.github.com/ta264/7822b1424f72e5b4c961
                                Expires = DateTime.Now.AddHours(1)
                            };
                        }

                        sourceContainer.Add((Uri)request.Url, cookie);

                        if (request.StoreRequestCookie)
                        {
                            presistentContainer.Add((Uri)request.Url, cookie);
                        }
                    }
                }

                return sourceContainer;
            }
        }

        private void HandleResponseCookies(HttpResponse response, CookieContainer container)
        {
            foreach (Cookie cookie in container.GetCookies((Uri)response.Request.Url))
            {
                cookie.Expired = true;
            }

            var cookieHeaders = response.GetCookieHeaders();

            if (cookieHeaders.Empty())
            {
                return;
            }

            AddCookiesToContainer(response.Request.Url, cookieHeaders, container);

            if (response.Request.StoreResponseCookie)
            {
                lock (_cookieContainerCache)
                {
                    var persistentCookieContainer = _cookieContainerCache.Get("container", () => new CookieContainer());

                    AddCookiesToContainer(response.Request.Url, cookieHeaders, persistentCookieContainer);
                }
            }
        }

        private void AddCookiesToContainer(HttpUri url, string[] cookieHeaders, CookieContainer container)
        {
            foreach (var cookieHeader in cookieHeaders)
            {
                try
                {
                    container.SetCookies((Uri)url, cookieHeader);

                    // SetCookies does not always throw on a cookie it declines:
                    // an already-expired one is dropped silently, which is
                    // correct behaviour but indistinguishable from success here.
                    // The catch below therefore cannot be relied on to notice,
                    // and a missing session cookie surfaces much later as an
                    // unexplained re-login or 403 against the origin.
                    //
                    // Look for the cookie by name rather than watching the
                    // container's count. Count is unchanged when a cookie
                    // REPLACES one already held under the same name -- which is
                    // the ordinary case for a refreshed session cookie -- so
                    // counting reports a drop on nearly every re-issue.
                    var name = ParseCookieName(cookieHeader);

                    if (name != null && container.GetCookies((Uri)url)[name] == null)
                    {
                        _logger.Debug("Cookie '{0}' was declined without error by {1}: {2}", name, url, cookieHeader);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Invalid cookie in {0}", url);
                }
            }
        }

        // The name of the first cookie in a Set-Cookie header, or null when
        // there is nothing worth reporting on. Only feeds the diagnostic above,
        // so every uncertain case returns null rather than risk a false alarm.
        //
        // Null for a deletion — `my=; Expires=Thu, 01-Jan-1970 ...; Max-Age=0`,
        // the conventional way to clear a cookie. Nothing is stored afterwards,
        // which is the whole point, and reporting it as declined would mean the
        // diagnostic fired on correct behaviour every time a server logged
        // someone out.
        private static string ParseCookieName(string cookieHeader)
        {
            var equals = cookieHeader.IndexOf('=');

            if (equals <= 0)
            {
                return null;
            }

            var name = cookieHeader.Substring(0, equals).Trim();

            if (name.Length == 0 || name.Contains(';'))
            {
                return null;
            }

            var value = cookieHeader.Substring(equals + 1);
            var end = value.IndexOf(';');

            if (end >= 0)
            {
                value = value.Substring(0, end);
            }

            return value.Trim().Length == 0 ? null : name;
        }

        public Task DownloadFileAsync(string url, string fileName, string userAgent = null)
        {
            return DownloadFileAsync(url, fileName, userAgent, null);
        }

        public async Task DownloadFileAsync(string url, string fileName, string userAgent, TimeSpan? rateLimit)
        {
            var fileNamePart = fileName + ".part";

            try
            {
                var fileInfo = new FileInfo(fileName);
                if (fileInfo.Directory != null && !fileInfo.Directory.Exists)
                {
                    fileInfo.Directory.Create();
                }

                _logger.Debug("Downloading [{0}] to [{1}]", url, fileName);

                var stopWatch = Stopwatch.StartNew();
                await using (var fileStream = new FileStream(fileNamePart, FileMode.Create, FileAccess.ReadWrite))
                {
                    var request = new HttpRequest(url);
                    request.AllowAutoRedirect = true;
                    request.ResponseStream = fileStream;
                    request.RequestTimeout = TimeSpan.FromSeconds(300);

                    // The userAgent parameter had been accepted and then
                    // silently dropped, so every caller that bothered to pass
                    // one still went out under the default UserAgentBuilder
                    // string. Honour it.
                    if (userAgent.IsNotNullOrWhiteSpace())
                    {
                        request.Headers.Add("User-Agent", userAgent);
                    }

                    // Opt-in per-host throttling, enforced by IRateLimitService
                    // keyed on request.Url.Host. Used for OpenLibrary's covers
                    // API, which publishes a hard 100-requests-per-IP-per-5-minutes
                    // limit and answers 403 past it.
                    if (rateLimit.HasValue)
                    {
                        request.RateLimit = rateLimit.Value;
                    }

                    var response = await GetAsync(request);

                    if (response.Headers.ContentType != null && response.Headers.ContentType.Contains("text/html"))
                    {
                        throw new HttpException(request, response, "Site responded with html content.");
                    }
                }

                stopWatch.Stop();

                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                File.Move(fileNamePart, fileName);
                _logger.Debug("Downloading Completed. took {0:0}s", stopWatch.Elapsed.Seconds);
            }
            finally
            {
                if (File.Exists(fileNamePart))
                {
                    File.Delete(fileNamePart);
                }
            }
        }

        public void DownloadFile(string url, string fileName, string userAgent = null)
        {
            DownloadFile(url, fileName, userAgent, null);
        }

        public void DownloadFile(string url, string fileName, string userAgent, TimeSpan? rateLimit)
        {
            // https://docs.microsoft.com/en-us/archive/msdn-magazine/2015/july/async-programming-brownfield-async-development#the-thread-pool-hack
            Task.Run(() => DownloadFileAsync(url, fileName, userAgent, rateLimit)).GetAwaiter().GetResult();
        }

        public Task<HttpResponse> GetAsync(HttpRequest request)
        {
            request.Method = HttpMethod.Get;
            return ExecuteAsync(request);
        }

        public HttpResponse Get(HttpRequest request)
        {
            return Task.Run(() => GetAsync(request)).GetAwaiter().GetResult();
        }

        public async Task<HttpResponse<T>> GetAsync<T>(HttpRequest request)
            where T : new()
        {
            var response = await GetAsync(request);
            CheckResponseContentType(response);
            return new HttpResponse<T>(response);
        }

        public HttpResponse<T> Get<T>(HttpRequest request)
            where T : new()
        {
            return Task.Run(() => GetAsync<T>(request)).GetAwaiter().GetResult();
        }

        public Task<HttpResponse> HeadAsync(HttpRequest request)
        {
            request.Method = HttpMethod.Head;
            return ExecuteAsync(request);
        }

        public HttpResponse Head(HttpRequest request)
        {
            return Task.Run(() => HeadAsync(request)).GetAwaiter().GetResult();
        }

        public Task<HttpResponse> PostAsync(HttpRequest request)
        {
            request.Method = HttpMethod.Post;
            return ExecuteAsync(request);
        }

        public HttpResponse Post(HttpRequest request)
        {
            return Task.Run(() => PostAsync(request)).GetAwaiter().GetResult();
        }

        public async Task<HttpResponse<T>> PostAsync<T>(HttpRequest request)
            where T : new()
        {
            var response = await PostAsync(request);
            CheckResponseContentType(response);
            return new HttpResponse<T>(response);
        }

        public HttpResponse<T> Post<T>(HttpRequest request)
            where T : new()
        {
            return Task.Run(() => PostAsync<T>(request)).GetAwaiter().GetResult();
        }

        private void CheckResponseContentType(HttpResponse response)
        {
            if (response.Headers.ContentType != null && response.Headers.ContentType.Contains("text/html"))
            {
                throw new UnexpectedHtmlContentException(response);
            }
        }
    }
}
