using System;
using NUnit.Framework;

namespace NzbDrone.Automation.Test
{
    // Assembly-wide gate for the Selenium suite. ChromeDriver 91 is years out
    // of date and won't start against modern Chrome; without it every fixture
    // here fails the default `dotnet test` run. Run manually with
    // READARR_RUN_AUTOMATION=1 once the driver + ChromeDriver versions are
    // aligned (or the suite is migrated to Playwright per the master plan).
    [SetUpFixture]
    public class AssemblyGate
    {
        [OneTimeSetUp]
        public void GateAssembly()
        {
            if (Environment.GetEnvironmentVariable("READARR_RUN_AUTOMATION") != "1")
            {
                Assert.Ignore(
                    "Automation.Test (Selenium + ChromeDriver 91) is opt-in; set READARR_RUN_AUTOMATION=1 to run. " +
                    "Suite is historical — see CLAUDE.md and the Phase plan for the Playwright migration.");
            }
        }
    }
}
