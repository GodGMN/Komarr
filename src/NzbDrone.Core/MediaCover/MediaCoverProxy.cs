using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.OpenLibrary.Mappers;

namespace NzbDrone.Core.MediaCover
{
    public interface IMediaCoverProxy
    {
        string RegisterUrl(string url);

        string GetUrl(string hash);
        byte[] GetImage(string hash);
    }

    public class MediaCoverProxy : IMediaCoverProxy
    {
        private readonly IHttpClient _httpClient;
        private readonly IConfigFileProvider _configFileProvider;
        private readonly ICached<string> _cache;

        public MediaCoverProxy(IHttpClient httpClient, IConfigFileProvider configFileProvider, ICacheManager cacheManager)
        {
            _httpClient = httpClient;
            _configFileProvider = configFileProvider;
            _cache = cacheManager.GetCache<string>(GetType());
        }

        public string RegisterUrl(string url)
        {
            if (url.IsNullOrWhiteSpace())
            {
                return null;
            }

            var hash = url.SHA256Hash();

            _cache.Set(hash, url, TimeSpan.FromHours(24));

            _cache.ClearExpired();

            var fileName = Path.GetFileName(url);
            return _configFileProvider.UrlBase + @"/MediaCoverProxy/" + hash + "/" + fileName;
        }

        public string GetUrl(string hash)
        {
            var result = _cache.Find(hash);

            if (result == null)
            {
                throw new KeyNotFoundException("Url no longer in cache");
            }

            return result;
        }

        public byte[] GetImage(string hash)
        {
            var url = GetUrl(hash);

            // Force auto-redirect on. The HttpRequest constructor flips this
            // to false in dev mode (RuntimeInfo.IsProduction == false, which
            // happens any time the assembly version is the 10.0.0.X local
            // placeholder rather than an official build), but cover URLs
            // routinely 302 from covers.openlibrary.org to archive.org —
            // without following we get an empty 302 body and the browser
            // shows a broken image.
            //
            // Suppress 404 logging. We attempt olid-keyed fallback URLs for
            // works without cover_i; OL returns 404 for the (common) case
            // where the work has no canonical cover. Treating each miss as
            // a Fatal stack trace is log spam — return null instead and
            // let the mapper convert to a clean 404 response so React falls
            // back to its placeholder.
            var request = new HttpRequest(url)
            {
                AllowAutoRedirect = true,
                SuppressHttpErrorStatusCodes = new[] { HttpStatusCode.NotFound },
                LogHttpError = false
            };

            // ISBN-keyed cover lookups are the only form OL rate-limits (100 per
            // IP per 5 minutes, then 403). CoverID and OLID lookups are exempt,
            // so this returns null for them and costs nothing. See
            // OpenLibraryCoverUrls.RateLimitFor.
            var rateLimit = OpenLibraryCoverUrls.RateLimitFor(url);

            if (rateLimit.HasValue)
            {
                request.RateLimit = rateLimit.Value;
            }

            var response = _httpClient.Get(request);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            return response.ResponseData;
        }
    }
}
