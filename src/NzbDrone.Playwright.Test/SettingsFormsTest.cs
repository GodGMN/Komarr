using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace NzbDrone.Playwright.Test
{
    // Form-level checks for settings surfaces the fork added. These were
    // previously verified by driving a browser by hand and keeping the
    // screenshot, which proves it worked once on one machine and nothing
    // thereafter.
    //
    // Neither test seeds or touches the network: the root folder modal is
    // reachable on a fresh instance, which is what makes these worth having as
    // ordinary always-on smokes.
    [TestFixture]
    public class SettingsFormsTest : PlaywrightTestBase
    {
        // The dual-format work (#19/#21/#23) put a second quality profile
        // selector on the root folder, so an audiobook can be graded on a
        // different scale from the ebook. If the field silently stops
        // rendering, nothing else in the suite notices -- the API keeps
        // accepting DefaultAudiobookQualityProfileId either way.
        [Test]
        public async Task root_folder_modal_offers_an_audiobook_quality_profile()
        {
            await OpenAddRootFolderModal();

            var modal = Page.Locator("div[class*='ModalContent']");

            (await modal.GetByText("Audiobook Quality Profile").CountAsync())
                .Should().BeGreaterThan(0, "the root folder modal should expose the audiobook default");

            (await modal.Locator("select[name='defaultAudiobookQualityProfileId']").CountAsync())
                .Should().Be(1);

            await CloseModal();
        }

        // The "leave it unset" option is the whole reason the field is
        // optional: an unset audiobook profile means "use the ebook one", and
        // the API validation only accepts the id when it is > 0. If this
        // option disappears the field becomes mandatory in practice.
        [Test]
        public async Task audiobook_quality_profile_can_be_left_unset()
        {
            await OpenAddRootFolderModal();

            var select = Page.Locator("select[name='defaultAudiobookQualityProfileId']");
            var options = await select.Locator("option").AllInnerTextsAsync();

            options.Should().Contain(o => o.Contains("None"),
                "an unset audiobook profile has to stay selectable");

            await CloseModal();
        }

        private async Task OpenAddRootFolderModal()
        {
            await Page.GotoAsync($"http://localhost:{AssemblyGate.Port}/settings/mediamanagement");
            await _page.WaitForNoSpinner();

            // Selected by CSS-module class, not by role and name, because the
            // control has no accessible name to select by: RootFolders.js
            // renders it as a Card containing nothing but a plus Icon, so a
            // screen reader announces it as an unlabelled clickable. Note the
            // selector is not tag-qualified: Card renders through Link, which
            // emits an <a>, not the <div> the class name suggests.
            await Page.Locator("[class*='addRootFolder']").First.ClickAsync();
            await Page.Locator("div[class*='ModalContent']").First.WaitForAsync();
        }

        private async Task CloseModal()
        {
            // Leave the DOM as we found it. The browser is shared across every
            // fixture in the assembly (see AssemblyGate), so a modal left open
            // here is a strict-mode violation somewhere else.
            await Page.Keyboard.PressAsync("Escape");
        }
    }
}
