using System;
using System.Net;
using System.Text;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.MetadataSource.OpenLibrary;
using NzbDrone.Core.MetadataSource.OpenLibrary.Resources;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource.OpenLibrary
{
    // Issue #7. The /isbn/ endpoint returns author keys but never names, and an
    // authorless candidate scores maximum author-distance rather than "unknown"
    // — a fixed 0.1875 against a 0.20 accept gate. The name has to come from a
    // second lookup, and where that lookup sits relative to the 30-day response
    // cache is the whole design: inside it, one OL hiccup persists a nameless
    // book for a month.
    [TestFixture]
    public class OpenLibraryAuthorNameFixture : CoreTest<OpenLibraryProxy>
    {
        private const string Isbn = "9780345391803";

        private const string EditionJson = @"{
            ""key"": ""/books/OL123M"",
            ""title"": ""Neuromancer"",
            ""isbn_13"": [""9780345391803""],
            ""works"": [{ ""key"": ""/works/OL456W"" }],
            ""authors"": [{ ""key"": ""/authors/OL26320A"" }]
        }";

        private const string AuthorlessEditionJson = @"{
            ""key"": ""/books/OL123M"",
            ""title"": ""Neuromancer"",
            ""works"": [{ ""key"": ""/works/OL456W"" }]
        }";

        private const string AuthorJson = @"{
            ""key"": ""/authors/OL26320A"",
            ""name"": ""William Gibson""
        }";

        private int _editionCalls;
        private int _authorCalls;

        [SetUp]
        public void Setup()
        {
            _editionCalls = 0;
            _authorCalls = 0;

            // The request builder is pure URL construction — use the real one so
            // the URLs the proxy asks for are the URLs under test.
            Mocker.SetConstant<IOpenLibraryRequestBuilder>(new OpenLibraryRequestBuilder());
        }

        private static HttpResponse Response(HttpRequest request, string json, HttpStatusCode status)
        {
            return new HttpResponse(request, new HttpHeader(), Encoding.UTF8.GetBytes(json ?? string.Empty), status);
        }

        private void GivenEdition(string json)
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get<OpenLibraryEditionResource>(It.IsAny<HttpRequest>()))
                .Returns((HttpRequest r) =>
                {
                    _editionCalls++;
                    return new HttpResponse<OpenLibraryEditionResource>(Response(r, json, HttpStatusCode.OK));
                });
        }

        // Two distinct editions sharing one author key, keyed off the requested URL.
        private void GivenEditionPerIsbn()
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get<OpenLibraryEditionResource>(It.IsAny<HttpRequest>()))
                .Returns((HttpRequest r) =>
                {
                    _editionCalls++;

                    var json = r.Url.ToString().Contains("9780441569595")
                        ? EditionJson.Replace("OL123M", "OL999M").Replace("9780345391803", "9780441569595")
                        : EditionJson;

                    return new HttpResponse<OpenLibraryEditionResource>(Response(r, json, HttpStatusCode.OK));
                });
        }

        private void GivenAuthorLookup(Func<HttpRequest, HttpResponse> behaviour)
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get<OpenLibraryAuthorResource>(It.IsAny<HttpRequest>()))
                .Returns((HttpRequest r) =>
                {
                    _authorCalls++;
                    return new HttpResponse<OpenLibraryAuthorResource>(behaviour(r));
                });
        }

        private static HttpResponse AuthorOk(HttpRequest r) => Response(r, AuthorJson, HttpStatusCode.OK);

        // A 404 rather than a 5xx: both reach the same catch, but 5xx is
        // retryable so it drags the proxy's 2s/4s/8s backoff into the test.
        private static HttpResponse AuthorFailure(HttpRequest r) => Response(r, null, HttpStatusCode.NotFound);

        [Test]
        public void should_resolve_the_author_name_for_an_isbn_lookup()
        {
            GivenEdition(EditionJson);
            GivenAuthorLookup(AuthorOk);

            var books = Subject.SearchByIsbn(Isbn);

            books.Should().HaveCount(1);
            books[0].AuthorMetadata.Value.ForeignAuthorId.Should().Be("OL26320A");
            books[0].AuthorMetadata.Value.Name.Should().Be("William Gibson");
            books[0].Author.Value.CleanName.Should().NotBeNullOrEmpty();
        }

        // The point of the design. A failed name lookup must not be what gets
        // stored for the next 30 days.
        [Test]
        public void should_not_cache_a_nameless_book_when_the_author_lookup_fails()
        {
            var authorShouldFail = true;

            GivenEdition(EditionJson);
            GivenAuthorLookup(r => authorShouldFail ? AuthorFailure(r) : AuthorOk(r));

            var first = Subject.SearchByIsbn(Isbn);
            first[0].AuthorMetadata.Value.Name.Should().BeEmpty("the author lookup failed");

            // OL recovers. The edition response is legitimately still cached, but
            // the missing name must be retried rather than served from it.
            authorShouldFail = false;

            var second = Subject.SearchByIsbn(Isbn);
            second[0].AuthorMetadata.Value.Name.Should().Be("William Gibson");

            _editionCalls.Should().Be(1, "the edition response is cached for 30 days");
        }

        [Test]
        public void should_not_refetch_the_name_once_resolved()
        {
            GivenEdition(EditionJson);
            GivenAuthorLookup(AuthorOk);

            Subject.SearchByIsbn(Isbn);
            var afterFirst = _authorCalls;

            Subject.SearchByIsbn(Isbn);

            _authorCalls.Should().Be(afterFirst, "the resolved name is written onto the cached book");
        }

        // The test above is satisfied by the 30-day book cache alone and never
        // reaches the name cache. Two different ISBNs by one author do: the
        // second book is a cache miss, so the name must come from the author
        // cache rather than a second round trip.
        [Test]
        public void should_reuse_a_resolved_name_across_books_by_the_same_author()
        {
            const string OtherIsbn = "9780441569595";

            GivenEditionPerIsbn();
            GivenAuthorLookup(AuthorOk);

            Subject.SearchByIsbn(Isbn)[0].AuthorMetadata.Value.Name.Should().Be("William Gibson");
            Subject.SearchByIsbn(OtherIsbn)[0].AuthorMetadata.Value.Name.Should().Be("William Gibson");

            _editionCalls.Should().Be(2, "different ISBNs are different cache entries");
            _authorCalls.Should().Be(1, "the author name is cached independently of the book");
        }

        // Proxy-wide short-circuit. Once the breaker is open the point is to stop
        // sending — continuing to hammer an endpoint that is rate-limiting us is
        // what extends the ban.
        [Test]
        public void should_not_send_anything_while_the_breaker_is_open()
        {
            GivenEdition(EditionJson);
            GivenAuthorLookup(AuthorOk);

            Mocker.GetMock<IMetadataSourceStatusService>()
                .Setup(s => s.EnsureAvailable())
                .Throws(new MetadataSourceUnavailableException("open"));

            Assert.Throws<MetadataSourceUnavailableException>(() => Subject.SearchByIsbn(Isbn));

            _editionCalls.Should().Be(0);
            _authorCalls.Should().Be(0);
        }

        [Test]
        public void should_not_look_up_a_name_for_an_edition_with_no_author_key()
        {
            GivenEdition(AuthorlessEditionJson);
            GivenAuthorLookup(AuthorOk);

            var books = Subject.SearchByIsbn(Isbn);

            books.Should().HaveCount(1);
            books[0].AuthorMetadata.Should().BeNull("OL gave us no author key to ask about");
            _authorCalls.Should().Be(0, "there is nothing to look up");
        }
    }
}
