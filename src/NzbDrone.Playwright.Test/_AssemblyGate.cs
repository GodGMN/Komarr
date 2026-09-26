using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Newtonsoft.Json.Linq;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Playwright.Test.PageModel;
using NzbDrone.Test.Common;

namespace NzbDrone.Playwright.Test
{
    // Assembly-wide gate and lifecycle for the Playwright smoke suite. Every
    // fixture here drives a headless Chromium against a real Librarr. The
    // default `dotnet test` invocation in a dev sandbox doesn't have a built
    // binary on hand and may not have Playwright's browser bundle installed,
    // so we don't fail it by default — opt in with READARR_RUN_PLAYWRIGHT=1.
    //
    // First-run setup: see README.md for the `playwright install` step.
    //
    // The browser and the Librarr instance live HERE, not on the test base,
    // and this is load-bearing. NzbDroneRunner.KillAll() kills every Readarr
    // process by name rather than only its own, and every fixture wants port
    // 8787 — so with per-fixture lifecycles, one fixture's teardown would
    // shoot down another fixture's instance and the suite failed
    // intermittently with TargetClosedException from OneTimeSetUp. One
    // instance per assembly removes the race by construction, and boots once
    // instead of once per fixture.
    [SetUpFixture]
    public class AssemblyGate
    {
        internal static IPlaywright Playwright;
        internal static IBrowser Browser;
        internal static IBrowserContext Context;
        internal static IPage Page;
        internal static PageBase PageModel;

        private static NzbDroneRunner _runner;

        // For fixtures that seed through the API. See LibrarySeeder.
        internal static int Port => _runner.Port;

        internal static string ApiKey => _runner.ApiKey;

        [OneTimeSetUp]
        public async Task GateAssembly()
        {
            if (Environment.GetEnvironmentVariable("READARR_RUN_PLAYWRIGHT") != "1")
            {
                Assert.Ignore(
                    "Playwright smoke suite is opt-in; set READARR_RUN_PLAYWRIGHT=1 to run. " +
                    "Requires a built backend on disk and `playwright install` to have run once — see README.md.");
            }

            new StartupContext();

            LogManager.Configuration = new LoggingConfiguration();
            var consoleTarget = new ConsoleTarget { Layout = "${level}: ${message} ${exception}" };
            LogManager.Configuration.AddTarget(consoleTarget.GetType().Name, consoleTarget);
            LogManager.Configuration.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Trace, consoleTarget));

            AssertDriverMatchesClient();

            Playwright = await Microsoft.Playwright.Playwright.CreateAsync();

            Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Timeout = (float)TimeSpan.FromMinutes(3).TotalMilliseconds
            });

            Context = await Browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
            });

            Page = await Context.NewPageAsync();

            // Surface what the browser saw. Without this a frontend exception
            // presents only as "the spinner never went away" from
            // WaitForNoSpinner, with the actual error -- the one line that
            // says which component threw -- discarded inside the browser.
            Page.Console += (_, msg) =>
            {
                if (msg.Type == "error" || msg.Type == "warning")
                {
                    TestContext.Progress.WriteLine($"[browser {msg.Type}] {msg.Text}");
                }
            };

            Page.PageError += (_, error) =>
                TestContext.Progress.WriteLine($"[browser exception] {error}");

            Page.RequestFailed += (_, request) =>
                TestContext.Progress.WriteLine($"[request failed] {request.Url} {request.Failure}");

            _runner = new NzbDroneRunner(LogManager.GetCurrentClassLogger(), null);
            _runner.KillAll();
            _runner.Start(true);

            await Page.GotoAsync("http://localhost:8787");

            PageModel = new PageBase(Page);
            await PageModel.WaitForNoSpinner();

            // Match the Selenium suite — the frontend reads this to widen the
            // viewport on certain pages. See window.Readarr in frontend/src/index.ts.
            await Page.EvaluateAsync("() => { window.Readarr.NameViews = true; }");
        }

        // The test output folder is shared and never cleaned, so it
        // accumulates: a driver from an older package version can sit next to
        // a newer Microsoft.Playwright.dll indefinitely. When they disagree
        // the driver exits during the initialize handshake and the only
        // symptom is `TargetClosedException : Process exited` from
        // CreateAsync -- which says nothing about versions and sent this
        // author looking at browsers, Gatekeeper and stdio buffering first.
        //
        // Same disease as the pre-.NET-10 binary NzbDroneRunner used to boot
        // silently. Name it instead.
        private static void AssertDriverMatchesClient()
        {
            var assemblyDirectory = Path.GetDirectoryName(typeof(AssemblyGate).Assembly.Location);
            var driverManifest = Path.Combine(assemblyDirectory, ".playwright", "package", "package.json");

            if (!File.Exists(driverManifest))
            {
                Assert.Fail(
                    $"Playwright driver not found at {driverManifest}. " +
                    "Rebuild the test project so the package's build targets copy it.");
            }

            var client = typeof(IPlaywright).Assembly.GetName().Version;
            var driver = JObject.Parse(File.ReadAllText(driverManifest)).Value<string>("version");

            // The driver carries a prerelease suffix (1.55.0-beta-...) while
            // the assembly version is padded to four parts (1.55.0.0), so
            // compare on major.minor only.
            var clientSeries = $"{client.Major}.{client.Minor}";
            var driverSeries = string.Join(".", driver.Split('.').Take(2));

            if (clientSeries != driverSeries)
            {
                Assert.Fail(
                    $"Playwright version mismatch: Microsoft.Playwright.dll is {client}, " +
                    $"but the driver in {Path.GetDirectoryName(driverManifest)} is {driver}. " +
                    "A stale driver left in the shared test output makes CreateAsync fail " +
                    "with 'Process exited'. Delete the .playwright folder and rebuild.");
            }

            TestContext.Progress.WriteLine($"Playwright client {client}, driver {driver}");
        }

        [OneTimeTearDown]
        public async Task ReleaseAssembly()
        {
            _runner?.KillAll();

            if (Context != null)
            {
                await Context.CloseAsync();
            }

            if (Browser != null)
            {
                await Browser.CloseAsync();
            }

            Playwright?.Dispose();
        }
    }
}
