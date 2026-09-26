using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using NUnit.Framework;

namespace NzbDrone.Playwright.Test
{
    // The round-trip the release checklist has been carrying as an open item:
    // library -> author -> book, on an instance that actually has an author in
    // it. Every other fixture in this suite runs against an empty library, so
    // they all assert on empty states -- an author index that renders nothing
    // but "Add New Author" passes author_page today.
    //
    // Seeding is one small author via the API. See LibrarySeeder for why that
    // rather than a checked-in SQLite file, and for how it degrades when
    // OpenLibrary is unreachable.
    [TestFixture]
    [Category("RequiresOpenLibrary")]
    public class SeededLibraryTest : PlaywrightTestBase
    {
        [OneTimeSetUp]
        public void Seed()
        {
            LibrarySeeder.EnsureSeeded(AssemblyGate.Port, AssemblyGate.ApiKey);
        }

        [Test]
        public async Task library_lists_the_seeded_author()
        {
            await Page.GotoAsync($"http://localhost:{AssemblyGate.Port}/");
            await _page.WaitForNoSpinner();

            (await Page.GetByText(LibrarySeeder.AuthorName).First.IsVisibleAsync())
                .Should().BeTrue("the seeded author should appear on the author index");
        }

        // The round trip proper. Clicking through has broken before in ways a
        // page-load smoke cannot see: the link is built from the author's
        // titleSlug, so a metadata change that empties the slug yields a link
        // to /author/ and a blank page, while every direct-navigation test
        // keeps passing.
        [Test]
        public async Task author_page_reached_by_clicking_through_lists_books()
        {
            await Page.GotoAsync($"http://localhost:{AssemblyGate.Port}/");
            await _page.WaitForNoSpinner();

            await Page.GetByText(LibrarySeeder.AuthorName).First.ClickAsync();
            await _page.WaitForNoSpinner();

            // The route is keyed by the OpenLibrary id, so this also catches a
            // link built from an empty slug -- which lands on /author/ and
            // renders nothing.
            Page.Url.Should().Contain($"/author/{LibrarySeeder.AuthorId}");

            (await Page.GetByText(LibrarySeeder.AuthorName).First.IsVisibleAsync()).Should().BeTrue();

            // Assert on the book rows themselves. Matching the page container
            // (AuthorDetails*) instead would pass on an author whose book
            // table came back empty, which is precisely the failure worth
            // catching here.
            var titles = Page.Locator("[class*='BookRow-title']");

            // Not tag-qualified: the class sits on a table cell, not a div.
            // Wait, don't count. The book table mounts after the page spinner
            // clears -- the rows arrive on their own fetch -- so counting
            // immediately reads zero on a page that is about to render four.
            await titles.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });

            (await titles.CountAsync())
                .Should().BeGreaterThan(0, "the seeded author's books should be listed");

            (await titles.First.InnerTextAsync())
                .Should().NotBeNullOrWhiteSpace("a book row with an empty title is a rendering failure");
        }
    }
}
