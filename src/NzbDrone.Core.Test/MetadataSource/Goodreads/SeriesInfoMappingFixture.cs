using System.Xml.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource.Goodreads;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MetadataSource.Goodreads
{
    // Phase 2 mapping coverage. MapSeries / MapList are the boundary that
    // turns Goodreads XML-shaped Resources into the source-neutral
    // SeriesInfo / ListInfo DTOs the rest of the codebase consumes.
    // Driven via XElement.Parse so the fixture also catches any future
    // resource-shape drift, not just the projection step.
    [TestFixture]
    public class SeriesInfoMappingFixture : TestBase
    {
        // Production path: GoodreadsProxy.GetSeriesInfo deserializes the
        // wire response as ShowSeriesResource, which is the only thing that
        // parses series_works → Works. Going through ShowSeriesResource
        // here matches that flow exactly.
        private static SeriesResource ParseSeries(XElement element)
        {
            var show = new ShowSeriesResource();
            show.Parse(element);
            return show.Series;
        }

        private static ListResource ParseList(XElement element)
        {
            var resource = new ListResource();
            resource.Parse(element);
            return resource;
        }

        // ── MapSeries ───────────────────────────────────────────────
        [Test]
        public void MapSeries_should_project_top_level_metadata()
        {
            var element = new XElement("series",
                new XElement("id", "49075"),
                new XElement("title", "The Stormlight Archive"),
                new XElement("description", "Epic fantasy by Brandon Sanderson"));

            var info = GoodreadsProxy.MapSeries(ParseSeries(element));

            info.ForeignSeriesId.Should().Be("49075");
            info.Title.Should().Be("The Stormlight Archive");
            info.Description.Should().Be("Epic fantasy by Brandon Sanderson");
            info.Books.Should().BeEmpty();
        }

        [Test]
        public void MapSeries_should_project_works_with_best_book_details()
        {
            var element = new XElement("series",
                new XElement("id", "49075"),
                new XElement("title", "The Stormlight Archive"),
                new XElement("series_works",
                    new XElement("series_work",
                        new XElement("user_position", "1"),
                        new XElement("work",
                            new XElement("id", "7235533"),
                            new XElement("original_title", "The Way of Kings"),
                            new XElement("best_book",
                                new XElement("id", "8929481"),
                                new XElement("title", "The Way of Kings"),
                                new XElement("author",
                                    new XElement("id", "38550"),
                                    new XElement("name", "Brandon Sanderson")))))));

            var info = GoodreadsProxy.MapSeries(ParseSeries(element));

            info.Books.Should().HaveCount(1);
            var first = info.Books[0];
            first.ForeignBookId.Should().Be("7235533");
            first.Title.Should().Be("The Way of Kings");
            first.ForeignEditionId.Should().Be("8929481");
            first.AuthorName.Should().Be("Brandon Sanderson");
            first.ForeignAuthorId.Should().Be("38550");
        }

        [Test]
        public void MapSeries_should_leave_best_book_fields_null_when_absent()
        {
            // Goodreads occasionally returns a work with no best_book block —
            // mapper must not NRE.
            var element = new XElement("series",
                new XElement("id", "99"),
                new XElement("series_works",
                    new XElement("series_work",
                        new XElement("work",
                            new XElement("id", "777"),
                            new XElement("original_title", "Orphan Work")))));

            var info = GoodreadsProxy.MapSeries(ParseSeries(element));

            info.Books.Should().HaveCount(1);
            var first = info.Books[0];
            first.ForeignBookId.Should().Be("777");
            first.Title.Should().Be("Orphan Work");
            first.ForeignEditionId.Should().BeNull();
            first.AuthorName.Should().BeNull();
            first.ForeignAuthorId.Should().BeNull();
        }

        [Test]
        public void MapSeries_should_not_assign_position()
        {
            // Position is intentionally null on the mapper — series-work
            // ordering isn't exposed through the neutral DTO yet. Locks the
            // current behavior so any future change is intentional.
            var element = new XElement("series",
                new XElement("id", "1"),
                new XElement("series_works",
                    new XElement("series_work",
                        new XElement("user_position", "3"),
                        new XElement("work",
                            new XElement("id", "10"),
                            new XElement("original_title", "Book Three")))));

            var info = GoodreadsProxy.MapSeries(ParseSeries(element));

            info.Books[0].Position.Should().BeNull();
        }

        // ── MapList ─────────────────────────────────────────────────
        [Test]
        public void MapList_should_carry_foreign_list_id_through()
        {
            // foreignListId is supplied by the caller (it's not in the XML
            // response) — pass-through fidelity is the contract.
            var element = new XElement("list",
                new XElement("page", "1"),
                new XElement("per_page", "30"),
                new XElement("total_books", "0"));

            var info = GoodreadsProxy.MapList("listopia-42", ParseList(element));

            info.ForeignListId.Should().Be("listopia-42");
            info.Page.Should().Be(1);
            info.PerPage.Should().Be(30);
            info.TotalBooks.Should().Be(0);
            info.Books.Should().BeEmpty();
        }

        [Test]
        public void MapList_should_project_book_authors_when_present()
        {
            var element = new XElement("list",
                new XElement("page", "2"),
                new XElement("per_page", "30"),
                new XElement("total_books", "60"),
                new XElement("books",
                    new XElement("book",
                        new XElement("id", "8929481"),
                        new XElement("work",
                            new XElement("id", "7235533"),
                            new XElement("original_title", "The Way of Kings")),
                        new XElement("authors",
                            new XElement("author",
                                new XElement("id", "38550"),
                                new XElement("name", "Brandon Sanderson"))))));

            var info = GoodreadsProxy.MapList("epic-fantasy", ParseList(element));

            info.Books.Should().HaveCount(1);
            var first = info.Books[0];
            first.ForeignBookId.Should().Be("7235533");
            first.Title.Should().Be("The Way of Kings");
            first.ForeignEditionId.Should().Be("8929481");
            first.AuthorName.Should().Be("Brandon Sanderson");
            first.ForeignAuthorId.Should().Be("38550");
        }

        [Test]
        public void MapList_should_pick_first_author_when_book_has_multiple()
        {
            // The mapper takes Authors.FirstOrDefault() — primary credit.
            var element = new XElement("list",
                new XElement("page", "1"),
                new XElement("per_page", "30"),
                new XElement("total_books", "1"),
                new XElement("books",
                    new XElement("book",
                        new XElement("id", "1"),
                        new XElement("work",
                            new XElement("id", "10"),
                            new XElement("original_title", "Co-authored")),
                        new XElement("authors",
                            new XElement("author",
                                new XElement("id", "100"),
                                new XElement("name", "Primary Author")),
                            new XElement("author",
                                new XElement("id", "200"),
                                new XElement("name", "Secondary Author"))))));

            var info = GoodreadsProxy.MapList("any", ParseList(element));

            info.Books[0].AuthorName.Should().Be("Primary Author");
            info.Books[0].ForeignAuthorId.Should().Be("100");
        }

        [Test]
        public void MapList_should_handle_book_without_work_or_authors()
        {
            var element = new XElement("list",
                new XElement("page", "1"),
                new XElement("per_page", "30"),
                new XElement("total_books", "1"),
                new XElement("books",
                    new XElement("book",
                        new XElement("id", "555"))));

            var info = GoodreadsProxy.MapList("any", ParseList(element));

            info.Books.Should().HaveCount(1);
            var first = info.Books[0];
            first.ForeignEditionId.Should().Be("555");
            first.ForeignBookId.Should().BeNull();
            first.Title.Should().BeNull();
            first.AuthorName.Should().BeNull();
            first.ForeignAuthorId.Should().BeNull();
        }
    }
}
