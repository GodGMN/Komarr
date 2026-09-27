using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using NUnit.Framework;

namespace NzbDrone.Playwright.Test
{
    // Phase 12.4 detail-page smoke. Navigates directly to /narrator/{id}
    // with a guaranteed-bogus id so no library seed is required, and
    // asserts the page mounts + renders the "not found" error state
    // without throwing on the console.
    //
    // The full chip → page round-trip (clicking a narrator chip on a
    // seeded book details page) is deliberately out of scope here —
    // it needs a seeded library and lives on the v1.0.0-stable
    // checklist (see docs/release-checklist.md).
    [TestFixture]
    public class NarratorPageTest : PlaywrightTestBase
    {
        [Test]
        public async Task narrator_detail_page_renders_not_found_for_unknown_id()
        {
            // 999999 is a deterministically-unknown id — the Phase 12.1
            // controller returns 404 for unknown narrators, which the
            // detail page's Promise.all error handler maps to the
            // "Narrator not found." copy in NarratorDetailsPage.js.
            await Page.GotoAsync("http://localhost:8787/narrator/999999");
            await _page.WaitForNoSpinner();

            var imageName = MethodBase.GetCurrentMethod().Name;
            await TakeScreenshot(imageName);

            // Text-based locator stays stable across CSS-module hash
            // changes — the className will mutate with every build,
            // but the user-visible copy is the canonical anchor.
            await Page.GetByText("Narrator not found.").First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 10000
            });

            (await Page.GetByText("Narrator not found.").CountAsync()).Should().BeGreaterThan(0);
        }
    }
}
