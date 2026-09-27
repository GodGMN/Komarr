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

namespace NzbDrone.Core.Test.ImportListTests
{
    // Phase 6 coverage for the OL Author import list. The list itself is
    // a thin shell — extract OLID keys, attach the configured author key
    // as the parent, surface failures. These tests gate the key
    // extraction + the "skip empty key" short circuit.
    [TestFixture]
    public class OpenLibraryAuthorImportListFixture : CoreTest<OpenLibraryAuthorImportList>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<IOpenLibraryRequestBuilder>(new OpenLibraryRequestBuilder());

            Subject.Definition = new ImportListDefinition
            {
                Id = 1,
                Settings = new OpenLibraryAuthorImportListSettings
                {
                    AuthorKey = "OL26320A",
                    Limit = 50
                }
            };
        }

        private void GivenWorksResponse(OpenLibraryAuthorWorksResource response)
        {
            var json = JsonConvert.SerializeObject(response);
            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get<OpenLibraryAuthorWorksResource>(It.IsAny<HttpRequest>()))
                .Returns<HttpRequest>(r => new HttpResponse<OpenLibraryAuthorWorksResource>(
                    new HttpResponse(r, new HttpHeader(), json)));
        }

        [Test]
        public void Fetch_should_return_empty_when_author_key_blank()
        {
            ((OpenLibraryAuthorImportListSettings)Subject.Definition.Settings).AuthorKey = "  ";

            var result = Subject.Fetch();

            result.Should().BeEmpty();
            Mocker.GetMock<IHttpClient>()
                .Verify(c => c.Get<OpenLibraryAuthorWorksResource>(It.IsAny<HttpRequest>()), Times.Never);
        }

        [Test]
        public void Fetch_should_emit_one_item_per_work_with_extracted_key()
        {
            GivenWorksResponse(new OpenLibraryAuthorWorksResource
            {
                Size = 2,
                Entries = new System.Collections.Generic.List<OpenLibraryWorkResource>
                {
                    new OpenLibraryWorkResource { Key = "/works/OL45883W", Title = "Foundation" },
                    new OpenLibraryWorkResource { Key = "/works/OL45884W", Title = "Foundation and Empire" }
                }
            });

            var result = Subject.Fetch();

            result.Should().HaveCount(2);
            result[0].BookGoodreadsId.Should().Be("OL45883W");
            result[0].Book.Should().Be("Foundation");
            result[0].AuthorGoodreadsId.Should().Be("OL26320A");
            result[1].BookGoodreadsId.Should().Be("OL45884W");
        }

        [Test]
        public void Fetch_should_skip_entries_with_blank_keys()
        {
            GivenWorksResponse(new OpenLibraryAuthorWorksResource
            {
                Entries = new System.Collections.Generic.List<OpenLibraryWorkResource>
                {
                    new OpenLibraryWorkResource { Key = "/works/OL45883W", Title = "Real" },
                    new OpenLibraryWorkResource { Key = null, Title = "Bogus" },
                    new OpenLibraryWorkResource { Key = "  ", Title = "Also Bogus" }
                }
            });

            var result = Subject.Fetch();

            result.Should().HaveCount(1);
            result[0].Book.Should().Be("Real");
        }

        [Test]
        public void Fetch_should_return_empty_when_response_entries_null()
        {
            GivenWorksResponse(new OpenLibraryAuthorWorksResource { Entries = null });

            var result = Subject.Fetch();

            result.Should().BeEmpty();
        }
    }
}
