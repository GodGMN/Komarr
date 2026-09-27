using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using NUnit.Framework;

namespace NzbDrone.Playwright.Test
{
    // Smoke tests for the six top-level pages, ported from the Selenium
    // suite (src/NzbDrone.Automation.Test/MainPagesTest.cs). Each test
    // clicks a nav icon, waits for the spinner to clear, and asserts a
    // page-specific anchor element exists.
    //
    // These are the cheapest possible "the page mounts without throwing"
    // checks — they won't catch visual regressions, only catastrophic
    // failures like a missing route or a crash in the Redux bootstrap.
    [TestFixture]
    public class MainPagesTest : PlaywrightTestBase
    {
        [Test]
        public async Task author_page()
        {
            await _page.LibraryNavIcon.ClickAsync();
            await _page.WaitForNoSpinner();

            var imageName = MethodBase.GetCurrentMethod().Name;
            await TakeScreenshot(imageName);

            (await Page.Locator("div[class*='AuthorIndex']").CountAsync()).Should().BeGreaterThan(0);
        }

        [Test]
        public async Task calendar_page()
        {
            await _page.CalendarNavIcon.ClickAsync();
            await _page.WaitForNoSpinner();

            var imageName = MethodBase.GetCurrentMethod().Name;
            await TakeScreenshot(imageName);

            (await Page.Locator("div[class*='CalendarPage']").CountAsync()).Should().BeGreaterThan(0);
        }

        [Test]
        public async Task activity_page()
        {
            await _page.ActivityNavIcon.ClickAsync();
            await _page.WaitForNoSpinner();

            var imageName = MethodBase.GetCurrentMethod().Name;
            await TakeScreenshot(imageName);

            (await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Queue" }).CountAsync()).Should().BeGreaterThan(0);
            (await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "History" }).CountAsync()).Should().BeGreaterThan(0);
            (await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Blocklist" }).CountAsync()).Should().BeGreaterThan(0);
        }

        [Test]
        public async Task wanted_page()
        {
            await _page.WantedNavIcon.ClickAsync();
            await _page.WaitForNoSpinner();

            (await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Missing" }).CountAsync()).Should().BeGreaterThan(0);
            (await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Cutoff Unmet" }).CountAsync()).Should().BeGreaterThan(0);
        }

        [Test]
        public async Task system_page()
        {
            await _page.SystemNavIcon.ClickAsync();
            await _page.WaitForNoSpinner();

            var imageName = MethodBase.GetCurrentMethod().Name;
            await TakeScreenshot(imageName);

            (await Page.Locator("div[class*='Health']").CountAsync()).Should().BeGreaterThan(0);
        }

        [Test]
        public async Task add_author_page()
        {
            await _page.LibraryNavIcon.ClickAsync();
            await _page.WaitForNoSpinner();

            // Exact, or this also matches the "Add New Author" button that the
            // empty author index renders — two elements, strict mode violation.
            await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Add New", Exact = true }).ClickAsync();
            await _page.WaitForNoSpinner();

            var imageName = MethodBase.GetCurrentMethod().Name;
            await TakeScreenshot(imageName);

            (await Page.Locator("input[class*='AddNewItem-searchInput']").CountAsync()).Should().BeGreaterThan(0);
        }

        // The wizard's own smoke. Worth having beyond "does the route
        // resolve": LibraryImportSelectFolder mounts EditRootFolderModalConnector
        // eagerly, so a broken import or a Redux slice that isn't registered
        // shows up here as a page error via the base class TearDown rather
        // than as a blank panel nobody notices.
        [Test]
        public async Task library_import_page()
        {
            // The sidebar's child links are rendered into the DOM whether or
            // not their section is expanded, but they're only *visible* once
            // it is — so clicking one directly waits forever on actionability.
            // Expand Library first, exactly as add_author_page does.
            await _page.LibraryNavIcon.ClickAsync();
            await _page.WaitForNoSpinner();

            await _page.LibraryImportNavIcon.ClickAsync();
            await _page.WaitForNoSpinner();

            var imageName = MethodBase.GetCurrentMethod().Name;
            await TakeScreenshot(imageName);

            (await Page.Locator("div[class*='LibraryImportSelectFolder']").CountAsync()).Should().BeGreaterThan(0);
        }
    }
}
