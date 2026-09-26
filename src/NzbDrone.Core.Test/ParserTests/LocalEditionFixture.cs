using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.ParserTests
{
    // PopulateMatch clones the matched edition/book so identification does not
    // hold a reference to every candidate it saw. It was written against books
    // that came out of the database, where Author, AuthorMetadata and
    // SeriesLinks are all lazy-load proxies and therefore never null. A
    // candidate mapped straight from a metadata source has none of that, and
    // dereferencing it unconditionally fails the whole import run rather than
    // skipping the one book.
    [TestFixture]
    public class LocalEditionFixture : TestBase
    {
        private static LocalEdition WithMatchedBook(Book book)
        {
            var edition = new Edition
            {
                ForeignEditionId = "OL123M",
                Title = "Neuromancer",
                Book = book
            };

            book.Editions = new List<Edition> { edition };

            return new LocalEdition
            {
                LocalBooks = new List<LocalBook>
                {
                    new LocalBook { Path = "/books/neuromancer.epub" }
                },
                ExistingTracks = new List<LocalBook>(),
                Edition = edition
            };
        }

        [Test]
        public void should_populate_match_for_candidate_with_no_author_metadata()
        {
            // As built by OpenLibraryEditionMapper.ToBook for an ISBN lookup:
            // a slim book with no author and no series.
            var localEdition = WithMatchedBook(new Book
            {
                ForeignBookId = "OL456W",
                Title = "Neuromancer",
                AuthorMetadata = null,
                SeriesLinks = null
            });

            localEdition.PopulateMatch(false);

            localEdition.Edition.Book.Value.AuthorMetadata.Should().NotBeNull();
            localEdition.LocalBooks[0].Book.Should().NotBeNull();
        }

        [Test]
        public void should_treat_null_series_links_as_no_series()
        {
            var localEdition = WithMatchedBook(new Book
            {
                ForeignBookId = "OL456W",
                Title = "Neuromancer",
                AuthorMetadata = new AuthorMetadata { Name = "William Gibson" },
                SeriesLinks = null
            });

            localEdition.PopulateMatch(false);

            var seriesLinks = localEdition.Edition.Book.Value.SeriesLinks;
            seriesLinks.Should().NotBeNull();
            seriesLinks.Value.Should().BeEmpty();
        }

        [Test]
        public void should_skip_series_links_whose_series_did_not_resolve()
        {
            var book = new Book
            {
                ForeignBookId = "OL456W",
                Title = "Neuromancer",
                AuthorMetadata = new AuthorMetadata { Name = "William Gibson" }
            };

            book.SeriesLinks = new List<SeriesBookLink>
            {
                new SeriesBookLink { Series = null, Position = "1" },
                new SeriesBookLink
                {
                    Series = new Series { ForeignSeriesId = "OL9S", Title = "Sprawl" },
                    Position = "2"
                }
            };

            var localEdition = WithMatchedBook(book);

            localEdition.PopulateMatch(false);

            var seriesLinks = localEdition.Edition.Book.Value.SeriesLinks.Value;
            seriesLinks.Should().HaveCount(1);
            seriesLinks[0].Series.Value.Title.Should().Be("Sprawl");
        }
    }
}
