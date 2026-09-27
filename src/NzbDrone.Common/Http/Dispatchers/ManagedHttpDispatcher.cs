using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http.Proxy;

namespace NzbDrone.Common.Http.Dispatchers
{
    public class ManagedHttpDispatcher : IHttpDispatcher
    {
        private const string NO_PROXY_KEY = "no-proxy";

        private const int connection_establish_timeout = 2000;
        private static bool useIPv6 = Socket.OSSupportsIPv6;
        private static bool hasResolvedIPv6Availability;

        private readonly IHttpProxySettingsProvider _proxySettingsProvider;
        private readonly ICreateManagedWebProxy _createManagedWebProxy;
        private readonly ICertificateValidationService _certificateValidationService;
        private readonly IUserAgentBuilder _userAgentBuilder;
        private readonly ICached<System.Net.Http.HttpClient> _httpClientCache;
        private readonly ICached<CredentialCache> _credentialCache;

        private readonly Logger _logger;

        public ManagedHttpDispatcher(IHttpProxySettingsProvider proxySettingsProvider,
            ICreateManagedWebProxy createManagedWebProxy,
            ICertificateValidationService certificateValidationService,
            IUserAgentBuilder userAgentBuilder,
            ICacheManager cacheManager,
            Logger logger)
        {
            _proxySettingsProvider = proxySettingsProvider;
            _createManagedWebProxy = createManagedWebProxy;
            _certificateValidationService = certificateValidationService;
            _userAgentBuilder = userAgentBuilder;

            _httpClientCache = cacheManager.GetCache<System.Net.Http.HttpClient>(typeof(ManagedHttpDispatcher), "httpclient");
            _credentialCache = cacheManager.GetCache<CredentialCache>(typeof(ManagedHttpDispatcher), "credentialcache");

            _logger = logger;
        }

        public async Task<HttpResponse> GetResponseAsync(HttpRequest request, CookieContainer cookies)
        {
            var requestMessage = new HttpRequestMessage(request.Method, (Uri)request.Url)
            {
                Version = HttpVersion.Version20,
                VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
            };
            requestMessage.Headers.UserAgent.ParseAdd(_userAgentBuilder.GetUserAgent(request.UseSimplifiedUserAgent));
            requestMessage.Headers.ConnectionClose = !request.ConnectionKeepAlive;

            var cookieHeader = cookies.GetCookieHeader((Uri)request.Url);
            if (cookieHeader.IsNotNullOrWhiteSpace())
            {
                requestMessage.Headers.Add("Cookie", cookieHeader);
            }

            NetworkCredential networkCredential = null;

            if (request.Credentials != null)
            {
                if (request.Credentials is BasicNetworkCredential bc)
                {
                    // Manually set header to avoid initial challenge response
                    var authInfo = bc.UserName + ":" + bc.Password;
                    authInfo = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(authInfo));
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", authInfo);
                }
                else if (request.Credentials is NetworkCredential nc)
                {
                    // Registered against this request's client in GetClient
                    // below, rather than into a process-wide cache here.
                    networkCredential = nc;
                }
            }

            using var cts = new CancellationTokenSource();
            if (request.RequestTimeout != TimeSpan.Zero)
            {
                cts.CancelAfter(request.RequestTimeout);
            }
            else
            {
                // The default for System.Net.Http.HttpClient
                cts.CancelAfter(TimeSpan.FromSeconds(100));
            }

            if (request.ContentData != null)
            {
                requestMessage.Content = new ByteArrayContent(request.ContentData);
            }

            if (request.Headers != null)
            {
                AddRequestHeaders(requestMessage, request.Headers);
            }

            var httpClient = GetClient(request.Url, networkCredential);

            try
            {
                using var responseMessage = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                {
                    byte[] data = null;

                    try
                    {
                        if (request.ResponseStream != null && responseMessage.StatusCode == HttpStatusCode.OK)
                        {
                            await responseMessage.Content.CopyToAsync(request.ResponseStream, null, cts.Token);
                        }
                        else
                        {
                            data = await responseMessage.Content.ReadAsByteArrayAsync(cts.Token);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new WebException("Failed to read complete http response", ex, WebExceptionStatus.ReceiveFailure, null);
                    }

                    var headers = responseMessage.Headers.ToNameValueCollection();

                    headers.Add(responseMessage.Content.Headers.ToNameValueCollection());

                    return new HttpResponse(request, new HttpHeader(headers), data, responseMessage.StatusCode, responseMessage.Version);
                }
            }
            catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
            {
                throw new WebException("Http request timed out", ex.InnerException, WebExceptionStatus.Timeout, null);
            }
        }

        // Keyed by credentials as well as proxy, which is what keeps one set of
        // credentials out of another's requests.
        //
        // System.Net.CredentialCache prefix-matches, and .NET truncates the
        // prefix at its last '/', so an entry registered for
        // /ajax/books/lib1 also answers a request for /ajax/books/lib2. With a
        // single process-wide cache, two root folders on one Calibre server
        // under different accounts would resolve to whichever was registered
        // first -- verified: adding lib1 as user-lib1 and lib2 as user-lib2,
        // lib2 then resolves to user-lib1. With PreAuthenticate=true those
        // credentials are sent proactively, so this was one account's password
        // going out on another account's request.
        //
        // Giving each (proxy, credential) pair its own client and its own cache
        // makes prefix collisions harmless: every entry inside one cache holds
        // the same credential, so a sloppy match still returns the right
        // answer. It also removes the per-request rewriting of a shared object
        // that the two preceding fixes were working around.
        //
        // Cost: one HttpClient per distinct credential rather than per proxy.
        // That is bounded by the number of configured services, and the clients
        // are cached for the life of the process exactly as before -- a changed
        // password strands the old client until restart, which is acceptable
        // for something that changes about never.
        // No default for `credentials`: this is virtual, and default argument
        // values bind from the static type at the call site rather than the
        // override, which is a trap worth not laying.
        protected virtual System.Net.Http.HttpClient GetClient(HttpUri uri, NetworkCredential credentials)
        {
            var proxySettings = _proxySettingsProvider.GetProxySettings(uri);

            var key = ClientKey(proxySettings, credentials);

            var client = _httpClientCache.Get(key, () => CreateHttpClient(proxySettings, key));

            if (credentials != null)
            {
                RegisterCredential(key, (Uri)uri, credentials);
            }

            return client;
        }

        private static string ClientKey(HttpProxySettings proxySettings, NetworkCredential credentials)
        {
            var proxyKey = proxySettings?.Key ?? NO_PROXY_KEY;

            if (credentials == null)
            {
                return proxyKey;
            }

            // Hashed rather than concatenated: this string is a dictionary key
            // living for the process lifetime, and there is no reason for a
            // password to be sitting in one in the clear.
            var material = $"{credentials.Domain} {credentials.UserName} {credentials.Password}";
            var fingerprint = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(material)));

            return $"{proxyKey}|{fingerprint}";
        }

        // Still guarded, and still conditional — but note what that now costs,
        // because it is close to nothing. Every entry inside one client's cache
        // carries the same credential, so Matches answers true for any URL
        // already covered (including by a prefix), and the write is skipped.
        // A given entry is therefore added once and never replaced, which means
        // Remove is only ever a no-op and the entry-briefly-absent window that
        // the previous commit was closing does not arise here at all.
        //
        // The lock stays regardless: first contact with two different URLs on
        // one client can still land concurrently, and the reader — the handler
        // resolving credentials — never takes it.
        private void RegisterCredential(string clientKey, Uri uri, NetworkCredential credentials)
        {
            var creds = GetCredentialCache(clientKey);

            lock (creds)
            {
                foreach (var authtype in new[] { "Basic", "Digest" })
                {
                    if (Matches(creds.GetCredential(uri, authtype), credentials))
                    {
                        continue;
                    }

                    creds.Remove(uri, authtype);
                    creds.Add(uri, authtype, credentials);
                }
            }
        }

        protected virtual System.Net.Http.HttpClient CreateHttpClient(HttpProxySettings proxySettings, string clientKey)
        {
            var handler = new SocketsHttpHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Brotli,
                UseCookies = false, // sic - we don't want to use a shared cookie container
                AllowAutoRedirect = false,
                Credentials = GetCredentialCache(clientKey),
                PreAuthenticate = true,
                MaxConnectionsPerServer = 12,
                ConnectCallback = onConnect,
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = _certificateValidationService.ShouldByPassValidationError
                }
            };

            if (proxySettings != null)
            {
                handler.Proxy = _createManagedWebProxy.GetWebProxy(proxySettings);
            }

            var client = new System.Net.Http.HttpClient(handler)
            {
                DefaultRequestVersion = HttpVersion.Version20,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
                Timeout = Timeout.InfiniteTimeSpan
            };

            return client;
        }

        protected virtual void AddRequestHeaders(HttpRequestMessage webRequest, HttpHeader headers)
        {
            foreach (var header in headers)
            {
                switch (header.Key)
                {
                    case "Accept":
                        webRequest.Headers.Accept.ParseAdd(header.Value);
                        break;
                    case "Connection":
                        webRequest.Headers.Connection.Clear();
                        webRequest.Headers.Connection.Add(header.Value);
                        break;
                    case "Content-Length":
                        AddContentHeader(webRequest, "Content-Length", header.Value);
                        break;
                    case "Content-Type":
                        AddContentHeader(webRequest, "Content-Type", header.Value);
                        break;
                    case "Content-Encoding":
                        AddContentHeader(webRequest, "Content-Encoding", header.Value);
                        break;
                    case "Date":
                        webRequest.Headers.Remove("Date");
                        webRequest.Headers.Date = HttpHeader.ParseDateTime(header.Value);
                        break;
                    case "Expect":
                        webRequest.Headers.Expect.ParseAdd(header.Value);
                        break;
                    case "Host":
                        webRequest.Headers.Host = header.Value;
                        break;
                    case "If-Modified-Since":
                        webRequest.Headers.IfModifiedSince = HttpHeader.ParseDateTime(header.Value);
                        break;
                    case "Referer":
                        webRequest.Headers.Add("Referer", header.Value);
                        break;
                    case "Transfer-Encoding":
                        webRequest.Headers.TransferEncoding.ParseAdd(header.Value);
                        break;
                    case "User-Agent":
                        webRequest.Headers.UserAgent.ParseAdd(header.Value);
                        break;
                    case "Proxy-Connection":
                        throw new NotImplementedException();
                    default:
                        webRequest.Headers.Add(header.Key, header.Value);
                        break;
                }
            }
        }

        private static void AddContentHeader(HttpRequestMessage request, string header, string value)
        {
            var headers = request.Content?.Headers;
            if (headers == null)
            {
                return;
            }

            headers.Remove(header);
            headers.Add(header, value);
        }

        // One cache per client, keyed identically, so the cache a handler was
        // built with is always the one its requests register into.
        //
        // That identity survives a race, and it is Cached<T>.Get doing the
        // work: on a miss it finishes with ConcurrentDictionary.GetOrAdd, which
        // returns the winner's instance rather than the caller's. So when two
        // threads build a client for the same key at once, both handlers are
        // constructed around the same CredentialCache and it does not matter
        // which client wins. Were it to hand each caller its own instance, the
        // surviving handler could be holding a cache that RegisterCredential
        // never writes to, and every request through it would go out
        // unauthenticated.
        private CredentialCache GetCredentialCache(string clientKey)
        {
            return _credentialCache.Get(clientKey, () => new CredentialCache());
        }

        // Note this is asked of GetCredential, which prefix-matches rather than
        // looking up an exact URI, so `existing` may be an entry registered for
        // a parent or sibling path. That is fine for the question being asked:
        // "will a request for this URL already resolve to these credentials?"
        // If a neighbouring entry later changes, the comparison fails on the
        // next request and the entry is written then — it self-heals.
        private static bool Matches(NetworkCredential existing, NetworkCredential candidate)
        {
            return existing != null &&
                   string.Equals(existing.UserName, candidate.UserName, StringComparison.Ordinal) &&
                   string.Equals(existing.Password, candidate.Password, StringComparison.Ordinal) &&
                   string.Equals(existing.Domain, candidate.Domain, StringComparison.Ordinal);
        }

        private bool HasRoutableIPv4Address()
        {
            // Get all IPv4 addresses from all interfaces and return true if there are any with non-loopback addresses
            try
            {
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

                return networkInterfaces.Any(ni =>
                    ni.OperationalStatus == OperationalStatus.Up &&
                    ni.GetIPProperties().UnicastAddresses.Any(ip =>
                        ip.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(ip.Address)));
            }
            catch (Exception e)
            {
                _logger.Debug(e, "Caught exception while GetAllNetworkInterfaces assuming IPv4 connectivity: {0}", e.Message);
                return true;
            }
        }

        private async ValueTask<Stream> onConnect(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            // Until .NET supports an implementation of Happy Eyeballs (https://tools.ietf.org/html/rfc8305#section-2), let's make IPv4 fallback work in a simple way.
            // This issue is being tracked at https://github.com/dotnet/runtime/issues/26177 and expected to be fixed in .NET 6.
            if (useIPv6)
            {
                try
                {
                    var localToken = cancellationToken;

                    if (!hasResolvedIPv6Availability)
                    {
                        // to make things move fast, use a very low timeout for the initial ipv6 attempt.
                        var quickFailCts = new CancellationTokenSource(connection_establish_timeout);
                        var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, quickFailCts.Token);

                        localToken = linkedTokenSource.Token;
                    }

                    return await attemptConnection(AddressFamily.InterNetworkV6, context, localToken);
                }
                catch
                {
                    // Do not retry IPv6 if a routable IPv4 address is available, otherwise continue to attempt IPv6 connections.
                    var routableIPv4 = HasRoutableIPv4Address();
                    _logger.Info("IPv4 is available: {0}, IPv6 will be {1}", routableIPv4, routableIPv4 ? "disabled" : "left enabled");
                    useIPv6 = !routableIPv4;
                }
                finally
                {
                    hasResolvedIPv6Availability = true;
                }
            }

            // fallback to IPv4.
            return await attemptConnection(AddressFamily.InterNetwork, context, cancellationToken);
        }

        private static async ValueTask<Stream> attemptConnection(AddressFamily addressFamily, SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            // The following socket constructor will create a dual-mode socket on systems where IPV6 is available.
            var socket = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                // Turn off Nagle's algorithm since it degrades performance in most HttpClient scenarios.
                NoDelay = true
            };

            try
            {
                await socket.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);

                // The stream should take the ownership of the underlying socket,
                // closing it when it's disposed.
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    }
}
