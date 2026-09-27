using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.MetadataSource.AniList
{
    public enum AniListAvailability
    {
        Available,
        Stale,
        RateLimited,
        Unavailable
    }

    public class AniListMedia
    {
        public int Id { get; set; }
        public int? MalId { get; set; }
        public string TitleRomaji { get; set; }
        public string TitleEnglish { get; set; }
        public string TitleNative { get; set; }
        public List<string> Synonyms { get; set; } = new ();
        public string Description { get; set; }
        public string Status { get; set; }
        public string Format { get; set; }
        public string CountryOfOrigin { get; set; }
        public int? Chapters { get; set; }
        public int? Volumes { get; set; }
        public string CoverUrl { get; set; }
        public string BannerUrl { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class AniListResult
    {
        public AniListAvailability Availability { get; set; }
        public List<AniListMedia> Media { get; set; } = new ();
        public int? RetryAfterSeconds { get; set; }
        public string Message { get; set; }
    }

    public interface IAniListMetadataClient
    {
        AniListResult Search(string term);
        AniListResult GetById(int id, bool refresh = false);
    }

    // AniList is discovery metadata. Existing manga remain usable from the local database
    // when AniList is down; callers should persist successful lookups, never error results.
    public class AniListMetadataClient : IAniListMetadataClient
    {
        private const string Endpoint = "https://graphql.anilist.co";
        private const int CacheCapacity = 128;
        private const string Fields = "id idMal title { romaji english native } synonyms description(asHtml: false) status format countryOfOrigin chapters volumes coverImage { large } bannerImage startDate { year month day } endDate { year month day } updatedAt";
        private static readonly TimeSpan FreshLife = TimeSpan.FromHours(24);
        private static readonly TimeSpan SearchLife = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan StaleLife = TimeSpan.FromDays(7);
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;
        private readonly Func<DateTime> _utcNow;
        private readonly Dictionary<string, CacheEntry> _cache = new ();
        private readonly object _gate = new ();
        private DateTime _retryAt;

        private class CacheEntry
        {
            public AniListResult Result { get; set; }
            public DateTime StoredAt { get; set; }
        }

        public AniListMetadataClient(IHttpClient httpClient, Logger logger)
            : this(httpClient, logger, () => DateTime.UtcNow)
        {
        }

        internal AniListMetadataClient(IHttpClient httpClient, Logger logger, Func<DateTime> utcNow)
        {
            _httpClient = httpClient;
            _logger = logger;
            _utcNow = utcNow;
        }

        public AniListResult Search(string term)
        {
            term = term?.Trim();
            if (string.IsNullOrWhiteSpace(term))
            {
                return new AniListResult { Availability = AniListAvailability.Available };
            }

            if (term.Length > 200)
            {
                term = term.Substring(0, 200);
            }

            const string query = "query ($search: String!) { Page(page: 1, perPage: 20) { media(search: $search, type: MANGA) { " + Fields + " } } }";
            return Fetch("search:" + term.ToUpperInvariant(), query, new { search = term }, false, SearchLife, root => root["data"]?["Page"]?["media"] as JArray);
        }

        public AniListResult GetById(int id, bool refresh = false)
        {
            if (id <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            const string query = "query ($id: Int!) { Media(id: $id, type: MANGA) { " + Fields + " } }";
            return Fetch(
                "id:" + id.ToString(CultureInfo.InvariantCulture),
                query,
                new { id },
                refresh,
                FreshLife,
                root => root["data"]?["Media"] is JObject media ? new JArray(media) : new JArray());
        }

        private AniListResult Fetch(string key, string query, object variables, bool refresh, TimeSpan freshLife, Func<JObject, JArray> select)
        {
            var now = _utcNow();
            CacheEntry cached;
            lock (_gate)
            {
                _cache.TryGetValue(key, out cached);
                if (!refresh && cached != null && now - cached.StoredAt < freshLife)
                {
                    return cached.Result;
                }

                if (now < _retryAt)
                {
                    return Fallback(cached, now, AniListAvailability.RateLimited, (int)Math.Ceiling((_retryAt - now).TotalSeconds), "AniList rate limit is active.");
                }
            }

            try
            {
                var request = new HttpRequest(Endpoint)
                {
                    Method = HttpMethod.Post,
                    SuppressHttpError = true,
                    RateLimit = TimeSpan.FromMilliseconds(2100),
                    RateLimitKey = "komarr-anilist",
                    RequestTimeout = TimeSpan.FromSeconds(15)
                };
                request.Headers.ContentType = "application/json";
                request.Headers.Accept = "application/json";
                request.SetContent(JsonConvert.SerializeObject(new { query, variables }));
                var response = _httpClient.Post(request);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retry = ParseRetryAfter(response.Headers.GetSingleValue("Retry-After"));
                    lock (_gate)
                    {
                        _retryAt = now.AddSeconds(retry);
                    }

                    return Fallback(cached, now, AniListAvailability.RateLimited, retry, "AniList rate limit is active.");
                }

                if (response.HasHttpError)
                {
                    return Fallback(cached, now, AniListAvailability.Unavailable, null, $"AniList returned HTTP {(int)response.StatusCode}.");
                }

                var root = JObject.Parse(response.Content);
                if (root["errors"] is JArray errors && errors.Count > 0)
                {
                    return Fallback(cached, now, AniListAvailability.Unavailable, null, "AniList returned a GraphQL error.");
                }

                var items = select(root);
                if (items == null)
                {
                    return Fallback(cached, now, AniListAvailability.Unavailable, null, "AniList returned an incomplete response.");
                }

                var result = new AniListResult
                {
                    Availability = AniListAvailability.Available,
                    Media = items.OfType<JObject>().Select(Map).ToList()
                };
                lock (_gate)
                {
                    if (_cache.Count >= CacheCapacity)
                    {
                        var oldest = _cache.OrderBy(x => x.Value.StoredAt).First().Key;
                        _cache.Remove(oldest);
                    }

                    _cache[key] = new CacheEntry { Result = result, StoredAt = now };
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "AniList request failed");
                return Fallback(cached, now, AniListAvailability.Unavailable, null, "AniList is unavailable.");
            }
        }

        private static AniListResult Fallback(CacheEntry cached, DateTime now, AniListAvailability unavailable, int? retry, string message)
        {
            return new AniListResult
            {
                Availability = cached != null && now - cached.StoredAt < StaleLife ? AniListAvailability.Stale : unavailable,
                Media = cached != null && now - cached.StoredAt < StaleLife ? cached.Result.Media : new List<AniListMedia>(),
                RetryAfterSeconds = retry,
                Message = message
            };
        }

        private static int ParseRetryAfter(string value)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            {
                return Math.Clamp(seconds, 1, 300);
            }

            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
            {
                return Math.Clamp((int)Math.Ceiling((date - DateTimeOffset.UtcNow).TotalSeconds), 1, 300);
            }

            return 60;
        }

        private static AniListMedia Map(JObject item)
        {
            return new AniListMedia
            {
                Id = item.Value<int>("id"),
                MalId = item.Value<int?>("idMal"),
                TitleRomaji = item["title"]?.Value<string>("romaji"),
                TitleEnglish = item["title"]?.Value<string>("english"),
                TitleNative = item["title"]?.Value<string>("native"),
                Synonyms = item["synonyms"]?.Values<string>().Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>(),
                Description = item.Value<string>("description"),
                Status = item.Value<string>("status"),
                Format = item.Value<string>("format"),
                CountryOfOrigin = item.Value<string>("countryOfOrigin"),
                Chapters = item.Value<int?>("chapters"),
                Volumes = item.Value<int?>("volumes"),
                CoverUrl = item["coverImage"]?.Value<string>("large"),
                BannerUrl = item.Value<string>("bannerImage"),
                StartDate = ReadDate(item["startDate"]),
                EndDate = ReadDate(item["endDate"]),
                UpdatedAt = item.Value<long?>("updatedAt") is long timestamp ? DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime : null
            };
        }

        private static DateTime? ReadDate(JToken value)
        {
            var year = value?.Value<int?>("year");
            var month = value?.Value<int?>("month");
            var day = value?.Value<int?>("day");
            return year is > 0 && month is >= 1 and <= 12 && day is >= 1 and <= 31 &&
                   DateTime.TryParseExact($"{year:0000}-{month:00}-{day:00}", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : null;
        }
    }
}
