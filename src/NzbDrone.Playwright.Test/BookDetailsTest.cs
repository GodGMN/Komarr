using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using NUnit.Framework;

namespace NzbDrone.Playwright.Test
{
    // The book detail page had no automated coverage of any kind -- no
    // Playwright smoke, no vitest -- which was noticed while scoping the
    // react-tabs 4 -> 6 bump. Both detail pages drive react-tabs, and this one
    // was the half nothing would have caught a regression on.
    //
    // Written and proven green against react-tabs 4.3.0 BEFORE the bump, so it
    // is an oracle for that change rather than a test shaped to fit whatever
    // the new version happens to render.
    [TestFixture]
    [Category("RequiresOpenLibrary")]
    public class BookDetailsTest : PlaywrightTestBase
    {
        private string _titleSlug;

        [OneTimeSetUp]
        public void Seed()
        {
            LibrarySeeder.EnsureSeeded(AssemblyGate.Port, AssemblyGate.ApiKey);
            _titleSlug = LibrarySeeder.FirstBookTitleSlug(AssemblyGate.Port, AssemblyGate.ApiKey);
        }

        // role and module class together, and neither alone.
        //
        // role='tab' alone would also match any other tab strip the page
        // mounts. The class alone cannot be pinned tightly: webpack builds
        // CSS-module names as `[name]/[local]` in development but
        // `[name]/[local]/[hash]` in production (webpack.config.js:206), so
        // `BookDetails-tab-` with a trailing separator matches only production
        // bundles, and without it the prefix also swallows `tabList` and
        // `tabContent`. Pairing the two is stable across both builds.
        //
        // Not tag-qualified: react-tabs chooses the element, and pinning it
        // would make this fail on a markup change that is not a bug.
        private ILocator Tabs() => Page.Locator("[role='tab'][class*='BookDetails-tab']");

        private ILocator FilterMenu() => Page.Locator("[class*='BookDetails-filterIcon']");

        private async Task OpenBookPage()
        {
            await Page.GotoAsync($"http://localhost:{AssemblyGate.Port}/book/{_titleSlug}");
            await _page.WaitForNoSpinner();

            // The tab strip mounts on its own fetch, after the page spinner
            // clears -- same shape as the book table in SeededLibraryTest. Wait
            // rather than count, or this reads zero on a page about to render
            // three.
            await Tabs().First.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        }

        [Test]
        public async Task book_page_renders_its_three_tabs()
        {
            await OpenBookPage();

            (await Tabs().CountAsync())
                .Should().Be(3, "the book detail page has History, Search and Files tabs");

            // A tab strip that renders with every label empty still counts
            // three, and looks fine to a smoke test that only counts.
            (await Tabs().First.InnerTextAsync())
                .Should().NotBeNullOrWhiteSpace("a tab with no label is a rendering failure");
        }

        // The interesting one for the react-tabs bump. TabList holds a
        // conditional non-Tab child -- the filter menu, rendered only while the
        // Search tab is selected -- so the number of TabList children changes
        // with selection. react-tabs counts Tab children specifically rather
        // than by position, and this asserts that it still does: if a version
        // ever counted positionally, the extra div would shift the indices and
        // selecting a tab would show the wrong panel.
        [Test]
        public async Task selecting_the_search_tab_reveals_the_filter_menu_without_shifting_the_tabs()
        {
            await OpenBookPage();

            (await FilterMenu().CountAsync())
                .Should().Be(0, "the filter menu belongs to the Search tab, which is not selected on load");

            await Tabs().Nth(1).ClickAsync();

            await FilterMenu().First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });

            // Still three. The filter menu is a TabList child but not a Tab, so
            // it must not be counted as one.
            (await Tabs().CountAsync())
                .Should().Be(3, "revealing the filter menu must not add a tab");

            // And the tab that was clicked is the one now marked selected --
            // the check that would fail if indices had shifted by one.
            (await Tabs().Nth(1).GetAttributeAsync("class"))
                .Should().Contain("selectedTab", "clicking the Search tab should select it");
        }
    }
}
