using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource.OpenLibrary.Mappers;
using NzbDrone.Core.MetadataSource.OpenLibrary.Resources;

namespace NzbDrone.Core.Test.MetadataSource.OpenLibrary
{
    // Phase 8 fixtures for the OL mapping layer. Mappers are pure functions
    // (Resource → domain), so they don't need IHttpClient mocking — we can
    // hand-construct the Resource shape and assert on the result. This is
    // the lowest-friction path to a coverage baseline; HTTP-mocking-based
    // tests for the full OpenLibraryProxy come in Phase 8b alongside
    // canned JSON cassettes from real OL endpoints.
    [TestFixture]
    public class OpenLibraryWorkMapperFixture
    {
        [Test]
        public void ToBook_should_strip_olid_prefix_from_work_key()
        {
            var work = new OpenLibraryWorkResource
            {
                Key = "/works/OL45883W",
                Title = "Foundation",
                FirstPublishDate = "1951"
            };

            var (book, _) = OpenLibraryWorkMapper.ToBook(work, NoEditions());

            book.ForeignBookId.Should().Be("OL45883W");
            book.TitleSlug.Should().Be("OL45883W");
            book.Title.Should().Be("Foundation");
            book.ReleaseDate?.Year.Should().Be(1951);
        }

        [Test]
        public void ToBook_should_carry_subjects_into_genres_capped_at_ten()
        {
            var subjects = Enumerable.Range(1, 20).Select(i => $"Subject {i}").ToList();
            var work = new OpenLibraryWorkResource
            {
                Key = "OL45883W",
                Title = "Foundation",
                Subjects = subjects
            };

            var (book, _) = OpenLibraryWorkMapper.ToBook(work, NoEditions());

            book.Genres.Should().HaveCount(10);
            book.Genres.First().Should().Be("Subject 1");
        }

        [Test]
        public void ToBook_should_surface_authors_with_extracted_keys()
        {
            var work = new OpenLibraryWorkResource
            {
                Key = "OL45883W",
                Title = "Foundation",
                Authors = new List<OpenLibraryAuthorLink>
                {
                    new OpenLibraryAuthorLink { Author = new OpenLibraryKey { Key = "/authors/OL26320A" } }
                }
            };

            var (_, authors) = OpenLibraryWorkMapper.ToBook(work, NoEditions());

            authors.Should().HaveCount(1);
            authors[0].ForeignAuthorId.Should().Be("OL26320A");
            authors[0].TitleSlug.Should().Be("OL26320A");
        }

        [Test]
        public void ToBook_should_prefer_english_isbn13_edition_as_primary()
        {
            var work = new OpenLibraryWorkResource
            {
                Key = "OL45883W",
                Title = "Foundation"
            };

            var editions = new OpenLibraryEditionListResource
            {
                Size = 3,
                Entries = new List<OpenLibraryEditionResource>
                {
                    new OpenLibraryEditionResource
                    {
                        Key = "/books/OL_FR_M",
                        Title = "Fondation",
                        Languages = new List<OpenLibraryKey> { new OpenLibraryKey { Key = "/languages/fre" } }
                    },
                    new OpenLibraryEditionResource
                    {
                        Key = "/books/OL_EN_ISBN_M",
                        Title = "Foundation",
                        Languages = new List<OpenLibraryKey> { new OpenLibraryKey { Key = "/languages/eng" } },
                        Isbn13 = new List<string> { "9780553293357" }
                    },
                    new OpenLibraryEditionResource
                    {
                        Key = "/books/OL_EN_M",
                        Title = "Foundation",
                        Languages = new List<OpenLibraryKey> { new OpenLibraryKey { Key = "/languages/eng" } }
                    }
                }
            };

            var (book, _) = OpenLibraryWorkMapper.ToBook(work, editions);

            book.ForeignEditionId.Should().Be("OL_EN_ISBN_M");
            book.Editions.Value.Single(e => e.Monitored).ForeignEditionId.Should().Be("OL_EN_ISBN_M");
        }

        // A caller that hands a freshly-mapped book to import identification gets
        // no DB lazy-load, so the mapper has to attach the author itself or
        // Book.Author/Book.AuthorMetadata dereference to null downstream.
        [Test]
        public void ToBook_should_attach_primary_author_to_the_book()
        {
            var work = new OpenLibraryWorkResource
            {
                Key = "/works/OL45883W",
                Title = "Foundation",
                Authors = new List<OpenLibraryAuthorLink>
                {
                    new OpenLibraryAuthorLink { Author = new OpenLibraryKey { Key = "/authors/OL26320A" } },
                    new OpenLibraryAuthorLink { Author = new OpenLibraryKey { Key = "/authors/OL99999A" } }
                }
            };

            var (book, _) = OpenLibraryWorkMapper.ToBook(work, NoEditions());

            book.AuthorMetadata.Should().NotBeNull();
            book.AuthorMetadata.Value.ForeignAuthorId.Should().Be("OL26320A");
            book.Author.Should().NotBeNull();
            book.Author.Value.Metadata.Value.ForeignAuthorId.Should().Be("OL26320A");

            // Empty rather than null, matching the stub CandidateService.ToCandidates
            // builds for the same situation.
            book.AuthorMetadata.Value.Name.Should().BeEmpty();
        }

        [Test]
        public void ToBook_should_not_attach_an_author_when_the_work_has_none()
        {
            var work = new OpenLibraryWorkResource
            {
                Key = "/works/OL45883W",
                Title = "Foundation",
                Authors = new List<OpenLibraryAuthorLink>()
            };

            var (book, authors) = OpenLibraryWorkMapper.ToBook(work, NoEditions());

            authors.Should().BeEmpty();
            book.AuthorMetadata.Should().BeNull();
        }

        private static OpenLibraryEditionListResource NoEditions()
        {
            return new OpenLibraryEditionListResource
            {
                Size = 0,
                Entries = new List<OpenLibraryEditionResource>()
            };
        }
    }
}
