using System;
using System.Collections.Generic;
using System.Net;
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
using NzbDrone.Core.Test.MetadataSource.OpenLibrary.Fixtures;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.ImportListTests
{
    // Phase 6 coverage for the OL Subject import list. Two interesting
    // axes: (a) per-work key + first-author extraction from the subjects
    // response shape, and (b) the cassette-corpus regression — exercising
    // the Phase 3 `subject_fantasy.json` capture against the production
    // mapper.
    [TestFixture]
    public class OpenLibrarySubjectImportListFixture : CoreTest<OpenLibrarySubjectImportList>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<IOpenLibraryRequestBuilder>(new OpenLibraryRequestBuilder());

            Subject.Definition = new ImportListDefinition
            {
                Id = 1,
                Settings = new OpenLibrarySubjectImportListSettings
                {
                    Subject = "fantasy",
                    Limit = 50
                }
            };
        }

        private void GivenSubjectResponse(OpenLibrarySubjectResource response)
        {
            var json = JsonConvert.SerializeObject(response);
            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get<OpenLibrarySubjectResource>(It.IsAny<HttpRequest>()))
                  .Returns<HttpRequest>(r => new HttpResponse<OpenLibrarySubjectResource>(
                      new HttpResponse(r, new HttpHeader(), json)));
        }

        private void GivenRawJson(string json)
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get<OpenLibrarySubjectResource>(It.IsAny<HttpRequest>()))
                  .Returns<HttpRequest>(r => new HttpResponse<OpenLibrarySubjectResource>(
                      new HttpResponse(r, new HttpHeader(), json)));
        }

        [Test]
        public void Fetch_should_emit_one_item_per_work_with_extracted_keys()
        {
            GivenSubjectResponse(new OpenLibrarySubjectResource
            {
                Name = "fantasy",
                Works = new List<OpenLibrarySubjectWork>
                {
                    new OpenLibrarySubjectWork
                    {
                        Key = "/works/OL27513W",
                        Title = "The Fellowship of the Ring",
                        Authors = new List<OpenLibrarySubjectAuthor>
                        {
                            new OpenLibrarySubjectAuthor { Key = "/authors/OL26320A", Name = "J.R.R. Tolkien" }
                        }
                    },
                    new OpenLibrarySubjectWork
                    {
                        Key = "/works/OL45883W",
                        Title = "Foundation",
                        Authors = new List<OpenLibrarySubjectAuthor>
                        {
                            new OpenLibrarySubjectAuthor { Key = "/authors/OL34221A", Name = "Isaac Asimov" }
                        }
                    }
                }
            });

            var result = Subject.Fetch();

            result.Should().HaveCount(2);
            result[0].BookGoodreadsId.Should().Be("OL27513W");
            result[0].Book.Should().Be("The Fellowship of the Ring");
            result[0].Author.Should().Be("J.R.R. Tolkien");
            result[0].AuthorGoodreadsId.Should().Be("OL26320A");
            result[1].BookGoodreadsId.Should().Be("OL45883W");
        }

        [Test]
        public void Fetch_should_handle_works_with_no_authors()
        {
            // OL occasionally returns a work with an empty/null authors
            // array — the mapper must not NRE.
            GivenSubjectResponse(new OpenLibrarySubjectResource
            {
                Works = new List<OpenLibrarySubjectWork>
                {
                    new OpenLibrarySubjectWork
                    {
                        Key = "/works/OL999W",
                        Title = "Anonymous",
                        Authors = null
                    },
                    new OpenLibrarySubjectWork
                    {
                        Key = "/works/OL1000W",
                        Title = "Empty Authors",
                        Authors = new List<OpenLibrarySubjectAuthor>()
                    }
                }
            });

            var result = Subject.Fetch();

            result.Should().HaveCount(2);
            result[0].Author.Should().BeNull();
            result[0].AuthorGoodreadsId.Should().BeNull();
            result[1].Author.Should().BeNull();
            result[1].AuthorGoodreadsId.Should().BeNull();
        }

        [Test]
        public void Fetch_should_return_empty_when_works_array_null()
        {
            GivenSubjectResponse(new OpenLibrarySubjectResource { Works = null });

            var result = Subject.Fetch();

            result.Should().BeEmpty();
        }

        [Test]
        public void Fetch_should_record_failure_and_return_empty_when_http_throws()
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get<OpenLibrarySubjectResource>(It.IsAny<HttpRequest>()))
                  .Throws(new WebException("No route to host"));

            var result = Subject.Fetch();

            result.Should().BeEmpty();
            Mocker.GetMock<IImportListStatusService>()
                  .Verify(s => s.RecordFailure(It.IsAny<int>(), It.IsAny<TimeSpan>()), Times.Once);

            // The SUT logs a Warn explaining the failure; absorb it so
            // AssertNoUnexpectedLogs in the teardown doesn't trip.
            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void Fetch_should_round_trip_real_subject_cassette()
        {
            // Ties the import list to the Phase 3 cassette corpus. If OL
            // changes the /subjects/{subject}.json shape, this case fails
            // pointing at the offending capture.
            var json = OpenLibraryFixtureLoader.LoadJson("subject_fantasy.json");
            GivenRawJson(json);

            var result = Subject.Fetch();

            result.Should().NotBeEmpty("subject_fantasy.json carries 10 works");
            result.Should().OnlyContain(item => !string.IsNullOrEmpty(item.Book));
            result.Should().OnlyContain(item => !string.IsNullOrEmpty(item.BookGoodreadsId));
            result.Should().OnlyContain(item => !item.BookGoodreadsId.StartsWith("/", StringComparison.Ordinal),
                "mapper must strip the /works/ prefix");
        }
    }
}
