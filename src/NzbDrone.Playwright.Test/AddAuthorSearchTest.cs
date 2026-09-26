using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using NUnit.Framework;

namespace NzbDrone.Playwright.Test
{
    // The Add Author search surface, which the fork changed twice: results are
    // re-ranked (#15) and each one carries a work count (#28) so a stub record
    // can be told apart from the real author at a glance.
    //
    // Until now that was covered by vitest against a mocked store, plus a
    // by-hand browser check. The vitest side proves the component renders a
    // count it was handed; it cannot prove the API hands one over, which is
    // the half that actually broke during #24 (work counts came back 0 on the
    // id path, so a correct match was labelled "No works").
    //
    // This talks to real OpenLibrary, so it degrades to Inconclusive rather
    // than red when the network is missing. It is not the only one that does
    // -- SeededLibraryTest and BookDetailsTest seed through the API too, which
    // is a live metadata fetch. All three carry RequiresOpenLibrary so CI can
    // run the ten network-free tests as a blocking job and leave these six to
    // a separate one that is allowed to fail.
    [TestFixture]
    [Category("RequiresOpenLibrary")]
    public class AddAuthorSearchTest : PlaywrightTestBase
    {
        [Test]
        public async Task search_results_carry_a_work_count()
        {
            await Search("Tolkien");

            var results = AuthorResults();

            if (await results.CountAsync() == 0)
            {
                Assert.Inconclusive(
                    "No search results — OpenLibrary is unreachable or rate-limiting this IP.");
            }

            // "{count} work(s)" or "No works" — every result gets one or the
            // other. A result with neither means the API stopped returning
            // workCount and the UI silently dropped the chip.
            var text = await results.First.InnerTextAsync();

            Regex.IsMatch(text, @"\d+ work|No works")
                .Should().BeTrue($"the first result should carry a work count, got:\n{text}");
        }

        // The disambiguation path from #24: pasting an author's OpenLibrary id
        // has to resolve that exact author. OpenLibrary's own author search
        // cannot do this (q=OL1422008A returns nothing), so it is entirely on
        // our lookup to special-case it, and nothing else in this suite would
        // notice if that regressed.
        [Test]
        public async Task an_openlibrary_id_resolves_to_one_author()
        {
            await Search(LibrarySeeder.AuthorId);

            var results = AuthorResults();

            if (await results.CountAsync() == 0)
            {
                Assert.Inconclusive(
                    $"No result for {LibrarySeeder.AuthorId} — OpenLibrary is unreachable or rate-limiting this IP.");
            }

            (await results.CountAsync()).Should().Be(1, "an id lookup resolves exactly one author");
            (await results.First.InnerTextAsync()).Should().Contain(LibrarySeeder.AuthorName);
        }

        // The -searchResult suffix matters. Matching on the bare component
        // name matches every nested div the card renders (overlay, content,
        // nameRow, ...), so one result counts as eight.
        private ILocator AuthorResults()
            => Page.Locator("div[class*='AddNewAuthorSearchResult-searchResult']");

        private async Task Search(string term)
        {
            await Page.GotoAsync($"http://localhost:{AssemblyGate.Port}/add/search");
            await _page.WaitForNoSpinner();

            var input = Page.Locator("input[class*='AddNewItem-searchInput']");
            await input.WaitForAsync();
            await input.FillAsync(term);

            // The search is debounced and then has a real network round trip
            // behind it, so wait on the result list rather than on a timeout.
            await Page.Locator("div[class*='SearchResult-searchResult'], div[class*='noResults']")
                      .First
                      .WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        }
    }
}
