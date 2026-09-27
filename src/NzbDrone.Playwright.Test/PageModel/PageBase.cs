using System;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace NzbDrone.Playwright.Test.PageModel
{
    // Page-object wrapper modeled after the legacy Selenium PageBase.
    // The locators are the same DOM hooks the React frontend exposes;
    // when the frontend is restructured (e.g. the Phase 10+ React 18
    // refresh), this is the single place that needs updating.
    public class PageBase
    {
        private readonly IPage _page;
        private readonly float _defaultTimeoutMs = (float)TimeSpan.FromSeconds(5).TotalMilliseconds;

        public PageBase(IPage page)
        {
            _page = page;
        }

        // Selenium's `WebDriverWait.Until(d => d.FindElement(by))` maps to
        // Playwright's locator with a configurable timeout. The legacy
        // suite returned IWebElement; here we hand back the locator handle
        // so the caller can do further actions (Click, InnerText, etc.).
        public ILocator Find(string selector, int timeoutSeconds = 5)
        {
            return _page.Locator(selector).Nth(0);
        }

        public async Task<ILocator> FindAndWait(string selector, int timeoutSeconds = 5)
        {
            var locator = _page.Locator(selector);
            await locator.First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Attached,
                Timeout = (float)TimeSpan.FromSeconds(timeoutSeconds).TotalMilliseconds
            });
            return locator.First;
        }

        public async Task WaitForNoSpinner(int timeoutSeconds = 30)
        {
            // Give the spinner some time to appear before we start waiting
            // for it to disappear (matches the legacy Thread.Sleep(200)).
            await Task.Delay(200);

            try
            {
                await _page.Locator(".followingBalls").WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Hidden,
                    Timeout = (float)TimeSpan.FromSeconds(timeoutSeconds).TotalMilliseconds
                });
            }
            catch (PlaywrightException ex)
            {
                // Same forgiving behaviour as the Selenium version: if the
                // spinner never appeared at all, that's fine — page is ready.
                //
                // Catch PlaywrightException, not TimeoutException. Playwright
                // throws Microsoft.Playwright.TimeoutException, and `using
                // System;` is also in scope here, so the unqualified name is
                // not the type you think it is -- this catch used to miss
                // every timeout it was written to swallow. TimeoutException
                // derives from PlaywrightException, so this covers both.
                //
                // Log rather than swallow silently. A spinner that never
                // clears means the page is wedged, and the whole point of the
                // smoke suite is to notice that.
                NUnit.Framework.TestContext.Progress.WriteLine(
                    $"WaitForNoSpinner gave up after {timeoutSeconds}s: {ex.Message}");
            }
        }

        // Navigation icons. Selenium used By.LinkText; Playwright's role
        // selector matches the same nav-link semantics across the React
        // sidebar.
        // Exact, unlike the others. Playwright's Name match is a
        // case-insensitive substring by default, and the sidebar's Library
        // section now has a "Library Import" child rendered into the DOM
        // whether or not the section is expanded — so a loose "Library"
        // matches two links and every Click on it fails strict mode.
        public ILocator LibraryNavIcon => _page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Library", Exact = true });

        public ILocator LibraryImportNavIcon => _page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Library Import", Exact = true });

        public ILocator CalendarNavIcon => _page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Calendar" });

        public ILocator ActivityNavIcon => _page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Activity" });

        public ILocator WantedNavIcon => _page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Wanted" });

        public ILocator SettingNavIcon => _page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Settings" });

        public ILocator SystemNavIcon => _page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { NameRegex = new System.Text.RegularExpressions.Regex("System") });
    }
}
