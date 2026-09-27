using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using NUnit.Framework;
using NzbDrone.Playwright.Test.PageModel;

namespace NzbDrone.Playwright.Test
{
    // Equivalent of the legacy Selenium AutomationTest base class, ported to
    // Playwright. The browser and the Librarr instance are owned by
    // AssemblyGate and shared across every fixture — see the comment there for
    // why they can't be per-fixture. This type is now just the accessors plus
    // the per-test error assertion.
    //
    // NzbDroneRunner picks up the most-recently-built backend in
    // _output/net10.0 — run a full `./build.sh` before invoking the suite, or
    // the runner will throw at assembly setup. `--backend` alone is not
    // enough and is actively worse than nothing: it opens with
    // `rm -rf _output`, so it deletes the frontend, and only the packaging
    // step copies the UI next to the binary. See AssertAppIsNotStale.
    public abstract class PlaywrightTestBase
    {
        protected IPage Page => AssemblyGate.Page;

        protected PageBase _page => AssemblyGate.PageModel;

        protected async Task<string[]> GetPageErrors()
        {
            return (await Page.Locator("#errors div").AllInnerTextsAsync()).ToArray();
        }

        protected async Task TakeScreenshot(string name)
        {
            try
            {
                await Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = $"./{name}_test_screenshot.png",
                    FullPage = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save screenshot {name}, {ex.Message}");
            }
        }

        // Every test asserts the UI's own error panel is empty when it leaves.
        // This is what turns a page-load smoke into something that catches a
        // crash in the Redux bootstrap rather than only a missing route.
        [TearDown]
        public async Task AutomationTearDown()
        {
            (await GetPageErrors()).Should().BeEmpty();
        }
    }
}
