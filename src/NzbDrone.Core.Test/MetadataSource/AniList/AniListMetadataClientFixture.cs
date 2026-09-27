using System;
using System.Net;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.MetadataSource.AniList;

namespace NzbDrone.Core.Test.MetadataSource.AniList
{
    [TestFixture]
    public class AniListMetadataClientFixture
    {
        private Mock<IHttpClient> _http;
        private AniListMetadataClient _client;
        private DateTime _now;

        [SetUp]
        public void SetUp()
        {
            _http = new Mock<IHttpClient>();
            _now = new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);
            _client = new AniListMetadataClient(_http.Object, LogManager.GetCurrentClassLogger(), () => _now);
        }

        [Test]
        public void Search_maps_manga_and_uses_cache()
        {
            Respond("""
                {"data":{"Page":{"media":[{"id":30013,"idMal":16498,"title":{"romaji":"Shingeki no Kyojin","english":"Attack on Titan","native":"進撃の巨人"},"synonyms":["AoT"],"status":"FINISHED","format":"MANGA","chapters":139,"volumes":34,"startDate":{"year":2009,"month":9,"day":9},"coverImage":{"large":"https://example.org/cover.jpg"}}]}}}
                """);

            var first = _client.Search(" Attack on Titan ");
            var second = _client.Search("attack on titan");

            first.Availability.Should().Be(AniListAvailability.Available);
            first.Media.Should().ContainSingle();
            first.Media[0].Id.Should().Be(30013);
            first.Media[0].MalId.Should().Be(16498);
            first.Media[0].Synonyms.Should().Contain("AoT");
            first.Media[0].Volumes.Should().Be(34);
            first.Media[0].StartDate.Should().Be(new DateTime(2009, 9, 9));
            second.Media.Should().ContainSingle();
            _http.Verify(x => x.Post(It.IsAny<HttpRequest>()), Times.Once());
        }

        [Test]
        public void Refresh_returns_stale_details_when_anilist_is_down()
        {
            Respond("""
                {"data":{"Media":{"id":30013,"title":{"romaji":"Shingeki no Kyojin"},"synonyms":[]}}}
                """);
            _client.GetById(30013).Availability.Should().Be(AniListAvailability.Available);
            _now = _now.AddDays(2);
            _http.Setup(x => x.Post(It.IsAny<HttpRequest>())).Throws(new WebException("offline"));

            var result = _client.GetById(30013, true);

            result.Availability.Should().Be(AniListAvailability.Stale);
            result.Media.Should().ContainSingle(x => x.Id == 30013);
            result.Message.Should().Contain("unavailable");
        }

        [Test]
        public void Rate_limit_cooldown_is_reported_without_second_request()
        {
            _http.Setup(x => x.Post(It.IsAny<HttpRequest>())).Returns<HttpRequest>(request =>
            {
                var headers = new HttpHeader();
                headers.Set("Retry-After", "42");
                return new HttpResponse(request, headers, "{}", HttpStatusCode.TooManyRequests);
            });

            var first = _client.GetById(1);
            var second = _client.GetById(2);

            first.Availability.Should().Be(AniListAvailability.RateLimited);
            first.RetryAfterSeconds.Should().Be(42);
            second.Availability.Should().Be(AniListAvailability.RateLimited);
            _http.Verify(x => x.Post(It.IsAny<HttpRequest>()), Times.Once());
        }

        [Test]
        public void Expired_cache_does_not_masquerade_as_current_metadata()
        {
            Respond("""
                {"data":{"Media":{"id":1,"title":{"romaji":"Old title"},"synonyms":[]}}}
                """);
            _client.GetById(1);
            _now = _now.AddDays(8);
            _http.Setup(x => x.Post(It.IsAny<HttpRequest>())).Throws(new WebException("offline"));

            var result = _client.GetById(1);

            result.Availability.Should().Be(AniListAvailability.Unavailable);
            result.Media.Should().BeEmpty();
        }

        private void Respond(string json)
        {
            JObject.Parse(json);
            _http.Setup(x => x.Post(It.IsAny<HttpRequest>())).Returns<HttpRequest>(request =>
                new HttpResponse(request, new HttpHeader(), json));
        }
    }
}
