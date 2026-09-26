using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.ImportLists.OpenLibrary;
using NzbDrone.Core.MetadataSource.OpenLibrary;
using NzbDrone.Core.MetadataSource.OpenLibrary.Resources;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.ImportListTests
{
    // Phase 6 coverage for the OL Trending list. Two interesting axes:
    // (a) period whitelist enforcement (now/daily/weekly/...), and
    // (b) key + author extraction from the trending response shape
    // (search-doc-like, not the canonical work shape).
    [TestFixture]
    public class OpenLibraryTrendingImportListFixture : CoreTest<OpenLibraryTrendingImportList>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<IOpenLibraryRequestBuilder>(new OpenLibraryRequestBuilder());

            Subject.Definition = new ImportListDefinition
            {
                Id = 1,
                Settings = new OpenLibraryTrendingImportListSettings
                {
                    Period = "weekly",
                    Limit = 10
                }
            };
        }

        private void GivenTrendingResponse(OpenLibraryTrendingResource response)
        {
            var json = JsonConvert.SerializeObject(response);
            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get<OpenLibraryTrendingResource>(It.IsAny<HttpRequest>()))
                .Returns<HttpRequest>(r => new HttpResponse<OpenLibraryTrendingResource>(
                    new HttpResponse(r, new HttpHeader(), json)));
        }

        [Test]
        public void Fetch_should_reject_invalid_period_without_calling_http()
        {
            ((OpenLibraryTrendingImportListSettings)Subject.Definition.Settings).Period = "fortnightly";

            var result = Subject.Fetch();

            result.Should().BeEmpty();
            Mocker.GetMock<IHttpClient>()
                .Verify(c => c.Get<OpenLibraryTrendingResource>(It.IsAny<HttpRequest>()), Times.Never);

            // The implementation logs a Warn explaining why the fetch was skipped.
            // Count varies between isolated and full-suite runs (logger state is
            // not fully reset between tests), so don't pin it — just allow it.
            ExceptionVerification.IgnoreWarns();
        }

        [TestCase("now")]
        [TestCase("daily")]
        [TestCase("weekly")]
        [TestCase("monthly")]
        [TestCase("yearly")]
        [TestCase("forever")]
        public void Fetch_should_accept_documented_periods(string period)
        {
            ((OpenLibraryTrendingImportListSettings)Subject.Definition.Settings).Period = period;
            GivenTrendingResponse(new OpenLibraryTrendingResource
            {
                Works = new System.Collections.Generic.List<OpenLibraryTrendingWork>
                {
                    new OpenLibraryTrendingWork { Key = "/works/OL1W", Title = "x" }
                }
            });

            var result = Subject.Fetch();

            result.Should().HaveCount(1);
        }

        [Test]
        public void Fetch_should_extract_keys_and_first_author()
        {
            GivenTrendingResponse(new OpenLibraryTrendingResource
            {
                Works = new System.Collections.Generic.List<OpenLibraryTrendingWork>
                {
                    new OpenLibraryTrendingWork
                    {
                        Key = "/works/OL45883W",
                        Title = "Foundation",
                        AuthorName = new System.Collections.Generic.List<string> { "Isaac Asimov", "Some Editor" },
                        AuthorKey = new System.Collections.Generic.List<string> { "OL26320A", "OL2A" }
                    }
                }
            });

            var result = Subject.Fetch();

            result.Should().HaveCount(1);
            result[0].BookGoodreadsId.Should().Be("OL45883W");
            result[0].Book.Should().Be("Foundation");
            result[0].Author.Should().Be("Isaac Asimov");
            result[0].AuthorGoodreadsId.Should().Be("OL26320A");
        }

        [Test]
        public void Fetch_should_handle_works_with_no_author()
        {
            GivenTrendingResponse(new OpenLibraryTrendingResource
            {
                Works = new System.Collections.Generic.List<OpenLibraryTrendingWork>
                {
                    new OpenLibraryTrendingWork
                    {
                        Key = "/works/OL45883W",
                        Title = "Anonymous Work",
                        AuthorName = null,
                        AuthorKey = null
                    }
                }
            });

            var result = Subject.Fetch();

            result.Should().HaveCount(1);
            result[0].Author.Should().BeNull();
            result[0].AuthorGoodreadsId.Should().BeNull();
        }

        [Test]
        public void Fetch_should_return_empty_when_works_array_null()
        {
            GivenTrendingResponse(new OpenLibraryTrendingResource { Works = null });

            var result = Subject.Fetch();

            result.Should().BeEmpty();
        }
    }
}
