# Deferred modernization work (Phase 10 follow-ups)

The Phase 10 commit landed a subset of the master plan's modernization
items. The rest is documented here, with the reasoning for each
deferral, so a future session (or a future maintainer) can pick them up
without re-discovering the same constraints.

## .NET 6 → .NET 8 LTS

**Status: SUPERSEDED — see ".NET 10 LTS — DONE (2026-07-30)" below.**
Both claims in this section turned out to be false: the forked NuGets
did not need rebuilding, and .NET 8 was the wrong destination anyway.
Kept unedited because the later section's argument is easier to follow
next to the reasoning it overturns.

**Why not now (as written then, and wrong):**

* `Directory.Packages.props` pins several Servarr-forked NuGet packages
  with `net6.0`-specific builds: `System.Data.SQLite.Core.Servarr`,
  `TagLibSharp-Lidarr`, `Mono.Posix.NETStandard...-servarr22`,
  `Servarr.FluentMigrator.*`. Bumping the TFM without rebuilding those
  forks against .NET 8 will fail at restore.
* The custom Dapper ORM under `NzbDrone.Core/Datastore/` makes light use
  of reflection helpers that have changed behavior under .NET 8's
  trim-friendly defaults. A real upgrade needs a regression pass against
  the live integration suite.

**What's needed:**

1. Audit `Directory.Packages.props` — pick a successor for each
   Servarr-forked package (newer fork OR upstream OR replacement OR
   maintain our own).
2. Bump `<TargetFrameworks>net6.0</TargetFrameworks>` →
   `<TargetFrameworks>net8.0</TargetFrameworks>` in
   `src/Directory.Build.props`.
3. Update `dotnetVersion` in CI (`.github/workflows/build.yml` +
   `azure-pipelines.yml`) from `6.0.x` to `8.0.x`.
4. Re-run the full integration suite. Expect surprises in
   `Datastore/Migration/` (FluentMigrator behavior under net8) and
   `NzbDrone.Common/Reflection/` (trim-related warnings).

## `<Nullable>enable</Nullable>` codebase-wide

**Status:** deferred.

**Why not now:**

* The inherited codebase has zero nullable annotations. Enabling
  globally with `TreatWarningsAsErrors=true` (already set in
  `Directory.Build.props:4`) would emit thousands of CS86xx errors at
  build time. Cleaning those up is genuinely person-weeks of work, not
  an LLM-session task.
* Even per-file `#nullable enable` opt-in needs human review to decide
  which references should be nullable — automated guessing is wrong
  often enough to be net-negative.

**What's needed:** roll it out incrementally per project, starting with
the lowest-coupling ones (`NzbDrone.Common.Test`, `Readarr.Http`).

## React 17 → React 18

**Status: the React bump SHIPPED. The dependency cleanup did not.**
Corrected 2026-07-30 — this section said "deferred" long after the work
landed, and the stale status propagated into `CLAUDE.md` and
`ARCHITECTURE.md`, both of which described a React 17 app.

What is actually true today:

* `react` and `react-dom` are **18.3.1** — the same version Sonarr's
  `v5-develop` runs.
* `frontend/src/bootstrap.tsx:3,18` imports `createRoot` from
  `react-dom/client` and uses it. This is real React 18, not the legacy
  `ReactDOM.render` compatibility mode.

The half that did **not** happen is the peer-dependency cleanup this
section was actually worried about. Still pinned at their pre-18
versions: `react-dnd@14.0.4`, `react-dnd-html5-backend@14.0.2`,
`react-popper@1.3.7`, `react-virtualized@9.21.1`. `react-redux` is also
still **7.2.4**, which predates React 18 and does not use
`useSyncExternalStore` — so combined with legacy `createStore` and
`connected-react-router@6`, the app is on React 18 without access to any
concurrent feature.

So the honest framing is: **React 18 is installed and mounted; the
ecosystem around it is still React 17-era.** The remaining work is the
dependency audit below, not the framework bump.

**What's needed:**

1. Audit each `react-*` dep for React 18 compatibility.
2. Replace `react-dnd@14` with `react-dnd@16` (breaking API changes —
   the existing drag handlers need a rewrite).
3. Replace `react-popper@1` with `@popperjs/react@2` or floating-ui.
4. Replace `react-virtualized` with `react-window` or `@tanstack/virtual`.
5. Bump `react` and `react-dom` to `18.x`.
6. Update `bootstrap.tsx` to use `createRoot`.
7. Test under React 18's strict-mode double-render in dev — class
   components with side effects in `componentDidMount` may break.

## Selenium → Playwright

**Status: SUPERSEDED — see "Selenium → Playwright (partially done)"
below.** The suite exists and runs; what remains is the keep-or-delete
call on `NzbDrone.Automation.Test`. Kept for the same reason as the
.NET section above.

**Why not now (as written then):** Phase 1 already quarantined the existing Selenium 3
suite with `[Explicit]`. The port itself is a separate effort that
deserves its own session — Playwright .NET has a different API surface
(`IPage`, `IBrowserContext`) and the existing page-object pattern
(`src/NzbDrone.Automation.Test/PageModel/`) would need a rewrite.

**What's needed:**

1. Add `Microsoft.Playwright` (~80MB nupkg, downloads browser binaries
   at install time) to `Directory.Packages.props` for the test project.
2. Rewrite `AutomationTest.cs` setup using `IBrowserContext` instead of
   `ChromeDriver`.
3. Port `PageModel/PageBase.cs` and friends.
4. Remove the `[Explicit]` attribute once the suite passes again.

## Namespace cleanup (NzbDrone.* → Librarr.*)

**Status:** explicitly NOT recommended.

**Why not:**

* `src/Directory.Build.props:97-99` deliberately remaps `Readarr.*`
  csproj names back to `NzbDrone.*` namespaces. This is intentional —
  the codebase is partly upstream Servarr (Sonarr) heritage and the
  namespaces serve as a historical signal for which subsystems came
  from where.
* A full rename is ~2000 file touches. The risk/reward is bad: every
  `using` and every fully-qualified reference must change, and the
  payoff is purely cosmetic (the assembly names already say `Readarr`).
* If you really want this, do it with `dotnet format` or a Roslyn
  rewriter, NOT by hand or `sed`. And run the full integration suite
  after.

**Recommendation:** leave `NzbDrone.*` namespaces alone. The
`Directory.Build.props` remap stays.

## SBOM step

**Status:** ✅ done in Phase 10. See `.github/workflows/build.yml` `sbom`
job. Artifacts attached to each successful build (90-day retention).

## OL bulk-data dump fallback

**Status:** deferred to 1.1+. The standalone Phase 11 writeup at
[`docs/ol-bulk-data.md`](ol-bulk-data.md) is now the authoritative source —
it covers the technical sketch that used to live here plus the trigger
conditions that would flip the decision.

## Reidentify regression test (Soon → blocked on cassettes)

**Status: mostly done — this section was stale in two places.**
`ReidentifyRegressionFixture` is `[TestFixture]`, not `[Explicit]`
(`src/NzbDrone.Core.Test/MusicTests/ReidentifyRegressionFixture.cs:32`),
so it runs in the default suite. The cassettes it was "blocked on"
are in tree — 117 real OL captures under
`src/NzbDrone.Core.Test/Files/OpenLibrary/`, re-capturable via
`scripts/capture-ol-cassettes.sh`.

**What is actually still open:** only the 500-book snapshot. The
current seed is 10 books built in memory (5 ISBN-13s + 5 title+author
shapes) driven through the real `OpenLibraryProxy` against a
cassette-backed `IHttpClient`. That is enough to assert the 0.85
threshold; it is not enough to make the assertion statistically
meaningful. Capturing a larger one still needs a running install with
a real library — see the 1.0-stable gate in
[`release-checklist.md`](release-checklist.md).

## React 17 → 18 (superseded — see the corrected section above)

**Status: obsolete as written.** React 18.3.1 shipped and is mounted via
`createRoot`. This entry's claim that the bump was blocked *on* the
dependency replacements turned out to be backwards: the bump happened
first, and `react-dnd@14`, `react-virtualized@9`, `react-popper@1` and
`react-redux@7.2.4` are all still on their pre-18 pins. They are
follow-up work, not a precondition.

## .NET 10 LTS — DONE (2026-07-30) + Nullable enable (still deferred)

**The target was wrong and the blocker was overstated. Both corrected
below.**

### The target is .NET 10, not .NET 8

.NET 8 **and** .NET 9 both reach end of support on **2026-11-10**.
Landing on .NET 8 now would buy about one quarter. .NET 6 — what we run
today — has been out of support since **2024-11-12**. .NET 10 LTS is
supported to **2028-11-14** and is the only sensible destination.

### "The forks are pinned to net6.0, so this cannot start" — falsified

Each of the four named packages was checked against the rest of the
Servarr family on 2026-07-30. Every one has a resolved path, and two of
them have been solved twice:

| Package | Librarr (net6.0) | Lidarr `develop` (net8.0) | Sonarr `v5-develop` (net10.0) |
|---|---|---|---|
| FluentMigrator | `Servarr.FluentMigrator.* 3.3.2.9` | upstream **6.2.0** | upstream **8.0.1** |
| `System.Data.SQLite.Core.Servarr` | `1.0.115.5-18` | **not referenced** | **not referenced** |
| `TagLibSharp-Lidarr` | `2.2.0.19` | **`2.2.0.27`** on net8 | n/a (no audio tagging) |
| `Mono.Posix.NETStandard` | `-servarr22` | `-servarr20` | **`-servarr24`** |

So: the FluentMigrator fork is *dropped* for upstream, the SQLite fork is
*dropped entirely*, TagLibSharp has a newer build that runs on net8, and
Mono.Posix's fork was updated rather than abandoned. Nothing here fails
at restore for want of a successor.

### What it actually cost, once done

**The SQLite provider swap was not needed.** `System.Data.SQLite.Core.Servarr
1.0.115.5-18` restores and runs fine on .NET 10, as do
`Servarr.FluentMigrator.Runner 3.3.2.9`, `TagLibSharp-Lidarr 2.2.0.19` and
`Mono.Posix.NETStandard ...-servarr22`. All 47 migrations apply to a fresh
database and an existing one upgrades in place. The prediction below was
wrong in a useful direction — it named the hardest-looking thing, and that
thing turned out to be a non-issue.

The compile-time triage was ~11 unique issues, not thousands: obsolete
`ServicePointManager` (already a no-op), four formatter-based serialization
constructors, three `X509Certificate2` loads, `ForwardedHeadersOptions.KnownNetworks`,
`ISystemClock` on three auth handlers, two `IHeaderDictionary.Add` calls,
and three `CA2022` inexact reads — of which the `DiskProvider` copy-verification
one was a real latent bug.

**The expensive part was nothing anyone predicted:** ASP.NET Core 10 stopped
inferring `[FromBody]` for complex parameters on controllers that opt in via
`IApiBehaviorMetadata`. Every write endpoint silently bound an all-default
model and failed validation — the app could read but not write. 39 actions
needed explicit `[FromBody]`/`[FromQuery]`. Only a running instance caught
it; the 2764 unit tests were all green at the time.

Two further breakages only a test run would find:

* `WhereBuilder{Sqlite,Postgres}` rejected `array.Contains(x)` because C# 13
  binds it to `MemoryExtensions` (first-class spans) instead of `Enumerable`,
  in two overload shapes, wrapped in an `op_Implicit` call rather than a
  `Convert` node.
* `TimeSpan.FromSeconds` gained a `long` overload that throws
  `ArgumentOutOfRangeException` where the `double` one throws
  `OverflowException`, silently disabling a fallback in the Transmission
  client.

### Original prediction, kept for the record

Not the TFM bump — the **SQLite provider swap**.
`System.Data.SQLite` is referenced across 22 files, and the coupling is
to concrete types, not just a connection string: `SQLiteParameter` (20
uses), `SQLiteErrorCode` (11), `SQLiteException` (7), `SQLiteConnection`
(7), plus FluentMigrator's `SQLiteProcessor`/`SQLiteQuoter`/
`SQLiteGenerator`/`SQLiteResolver`. Roughly half the files are test
fixtures. Both reference projects removed this dependency, so the
destination is known — but this is the step to schedule real time for,
and every change has to stay valid on **both** SQLite and PostgreSQL.

### Nullable enable

Still deferred, and unchanged: the codebase has zero annotations, so
flipping the switch produces a several-thousand-error build. Worth
noting that neither Lidarr nor Sonarr v5 sets `Nullable` either — Sonarr
v5 sets `AnalysisLevel 6.0-all` and stops there. This is not a
prerequisite for the runtime move and should not be bundled with it.

## Selenium → Playwright (partially done — status corrected 2026-07-30)

**Status: a Playwright suite exists and runs green.**
`src/NzbDrone.Playwright.Test` drives headless Chromium against a real
Librarr instance, opt-in via `READARR_RUN_PLAYWRIGHT=1`. It is small
(main-pages smoke, narrator detail page, library-import wizard) rather
than a full port of the Selenium suite, and `NzbDrone.Automation.Test`
still exists alongside it with its years-old Selenium + ChromeDriver
pins.

Remaining: decide whether to finish porting the Selenium cases or delete
that project outright. Leaving both in the tree is the worst of the three
options — it implies coverage that only one of them provides.
