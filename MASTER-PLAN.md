# Master Plan: Reviving Readarr on Open Library

> Expanded version of `~/.claude/plans/analyze-the-project-architechture-generic-dawn.md`
> with concrete code sketches and implementation notes per phase. Originally
> a working blueprint, not committed code — every snippet was hand-written
> against the real interfaces in `src/NzbDrone.Core/MetadataSource/` before
> any of it had been compiled.

## Status at 1.0.0-beta (2026-05-19)

Phases 0-11 plus Phase 12.4 are shipped — see [`CHANGELOG.md`](CHANGELOG.md)
for the mapping of Cycle N commits to phase milestones, and
[`ARCHITECTURE.md`](ARCHITECTURE.md) § "Librarr fork additions" for the
as-shipped code map. The 12-phase body below remains the strategic
blueprint; treat it as a record of the original plan plus the
post-1.0 backlog still ahead (dedupe, normalization, broader indexer
coverage). The sketches were preserved as written — not retrofitted to
match the shipped code, so individual snippets may diverge from
current `src/`. When in doubt, the code is the source of truth.

## Context

Upstream `Readarr/Readarr` archived on **2025-06-27** (commit `0b79d300`).
Maintainer-stated reasons: (1) metadata source unusable, (2) no bandwidth,
(3) community Open Library migration stalled.

This plan describes the **minimum credible engineering program** to revive
Readarr as an independent fork on Open Library as the primary metadata
source — **no rreading-glasses shim**, native implementation only.

**Anchor docs in this work tree:**

- `ARCHITECTURE.md` — full architectural map.
- `METADATA-MIGRATION.md` — technical sketch of the metadata-source swap.
- `CLAUDE.md` — project memory for future Claude sessions.

**Working snapshot:** develop HEAD `0b79d300`, version `0.4.19`, .NET
6.0.427, 41 migrations.

---

## Non-goals (explicit)

- No rreading-glasses adoption — clean break.
- No Goodreads compatibility layer — Goodreads ID columns become legacy.
- No new download clients / indexers / notifications.
- No frontend stack rewrite, no JS→TS push, no class→hooks migration.
- No backport to upstream — the upstream archive is read-only.

---

## Phase 0 — Foundation (1-2 weeks, blocking)

**Goal:** Buildable fork with branding, CI, governance locked in.

### 0.1 Fork governance & licensing

Decisions to write down before touching code:

- **Fork name** — call it `Bookarr` for the rest of this document as a
  placeholder. Replace with the real name once chosen.
- **License** — stay on GPL v3. The current `LICENSE.md` already says so.
- **CLA** — drop. The existing `CLA.md` assigns rights to the Servarr team
  and the fork can't honour that. Replace the file with a short note:

```markdown
# Contributor License

This project is licensed under GPL v3 (see LICENSE.md). All contributions
are accepted on an inbound = outbound basis: by submitting a PR you agree
that your work is licensed under the same GPL v3 terms.

No separate Contributor License Agreement is required.
```

Also rewrite `CONTRIBUTING.md` (currently 13 lines pointing at the
servarr wiki) to point at the fork's own docs/wiki.

### 0.2 Create the fork

```bash
# from the local clone
git remote add bookarr git@github.com:bookarr/Bookarr.git
git checkout -b main 0b79d300            # branch from the last upstream commit
# Phase 0 commits go onto main; preserve full upstream history
git push bookarr main
git push bookarr --tags                  # carry v0.4.18.2805 etc. across
```

Preserve git history. The fork's first commit should be a rename/branding
change, not a `--squash` reset.

### 0.3 Branding pass — files to touch

`src/Directory.Build.props:72-74` (Product / Company / Copyright):

```diff
-    <Product>Readarr</Product>
-    <Company>readarr.com</Company>
-    <Copyright>Copyright 2017-$([System.DateTime]::Now.ToString('yyyy')) readarr.com (GNU General Public v3)</Copyright>
+    <Product>Bookarr</Product>
+    <Company>bookarr.dev</Company>
+    <Copyright>Copyright 2025-$([System.DateTime]::Now.ToString('yyyy')) Bookarr contributors (GNU General Public v3)</Copyright>
```

`azure-pipelines.yml:14-17`:

```diff
-  readarrVersion: '$(majorVersion).$(minorVersion)'
-  buildName: '$(Build.SourceBranchName).$(readarrVersion)'
-  sentryOrg: 'servarr'
-  sentryUrl: 'https://sentry.servarr.com'
+  bookarrVersion: '$(majorVersion).$(minorVersion)'
+  buildName: '$(Build.SourceBranchName).$(bookarrVersion)'
+  sentryOrg: 'bookarr'           # provisioned under the fork's Sentry org
+  sentryUrl: 'https://sentry.bookarr.dev'
```

`frontend/src/index.ejs`, `frontend/src/login.html`,
`frontend/src/oauth.html` — replace literal `"Readarr"` strings.

`distribution/windows/setup/readarr.iss` — rename to `bookarr.iss`,
update `AppName`, `AppPublisher`, `AppUrl`, install dir name.

`distribution/osx/Readarr.app/` — rename folder + update Info.plist
`CFBundleName`, `CFBundleIdentifier` (e.g., `dev.bookarr.app`).

### 0.4 Namespace remap — deferred

The codebase still uses `NzbDrone.*` namespaces
(`Directory.Build.props:97-99`):

```xml
<!-- For now keep the NzbDrone namespace -->
<RootNamespace Condition="'$(ReadarrProject)'=='true'">$(MSBuildProjectName.Replace('Readarr','NzbDrone'))</RootNamespace>
```

**Leave it alone in Phase 0.** A full rename touches every `using` line in
~2000 files. Defer to Phase 10. Document the quirk in the new
`README.md` and `CLAUDE.md`.

### 0.5 CI/CD migration off Azure DevOps

Stand up GitHub Actions. Skeleton for the main build pipeline:

```yaml
# .github/workflows/build.yml
name: Build

on:
  push:
    branches: [main, develop]
    paths-ignore:
      - '.github/**'
      - 'src/NzbDrone.Core/Localization/Core/**'
      - 'src/Readarr.Api.*/openapi.json'
  pull_request:
    branches: [develop]

env:
  DOTNET_VERSION: '6.0.428'  # bump from 6.0.427 to the latest in-support patch
  NODE_VERSION: '20'
  OUTPUT_FOLDER: ./_output
  TESTS_FOLDER: ./_tests

jobs:
  setup:
    runs-on: ubuntu-22.04
    outputs:
      version: ${{ steps.version.outputs.version }}
    steps:
      - uses: actions/checkout@v4
      - id: version
        run: |
          MAJOR=$(grep -E "^\s*majorVersion:" azure-pipelines.yml | awk '{print $2}' | tr -d "'")
          echo "version=${MAJOR}.${{ github.run_number }}" >> $GITHUB_OUTPUT

  backend:
    needs: setup
    strategy:
      fail-fast: false
      matrix:
        include:
          - { os: 'ubuntu-22.04',  enable_analysis: 'true'  }
          - { os: 'macos-13',      enable_analysis: 'false' }
          - { os: 'windows-2022',  enable_analysis: 'false' }
    runs-on: ${{ matrix.os }}
    env:
      EnableAnalyzers: ${{ matrix.enable_analysis }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
      - name: Build
        run: ./build.sh --backend --enable-extra-platforms

  frontend:
    needs: setup
    runs-on: ubuntu-22.04
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: ${{ env.NODE_VERSION }}
          cache: 'yarn'
      - run: yarn install --frozen-lockfile
      - run: yarn lint
      - run: yarn stylelint-linux
      - run: yarn build

  test-unit:
    needs: [backend, frontend]
    runs-on: ubuntu-22.04
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
      - name: Run tests
        run: dotnet test --logger "trx" --settings src/coverlet.runsettings
```

**Note the analyser gating:** `EnableAnalyzers=true` only on Linux, the same
split upstream had. Phase 10 removes this gate so StyleCop runs on every
leg.

Mirror `release.yml` for tagged releases — same matrix plus
`actions/upload-release-asset` to push the per-RID archives, the
InnoSetup installer, and the DMG.

### 0.6 Package source

Edit `src/NuGet.config`. Remove any `feed.servarr.com` source. Keep
`https://api.nuget.org/v3/index.json` and add the fork's GitHub Packages
feed once available:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="bookarr" value="https://nuget.pkg.github.com/bookarr/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <bookarr>
      <add key="Username" value="%GITHUB_ACTOR%" />
      <add key="ClearTextPassword" value="%GITHUB_TOKEN%" />
    </bookarr>
  </packageSourceCredentials>
</configuration>
```

Servarr-forked packages that need an upstream-or-replacement decision
(`src/Directory.Packages.props`):

| Package | Action |
|---|---|
| `Servarr.FluentMigrator.Runner` 3.3.2.9 | Republish under fork ID, or pin to upstream FluentMigrator 3.3.2 + manual SQLite/Postgres dialect plug |
| `Servarr.FluentMigrator.Runner.SQLite` 3.3.2.9 | Same |
| `Servarr.FluentMigrator.Runner.Postgres` 3.3.2.9 | Same |
| `System.Data.SQLite.Core.Servarr` 1.0.115.5-18 | Hardest to replace — Servarr fork was needed for the bundled-platform RIDs (musl, ARM). Republish or vendor the native binaries |
| `Mono.Posix.NETStandard 5.20.1.34-servarr22` | Try upstream `Mono.Posix.NETStandard 5.20.1.34` first; only fork if it breaks |
| `TagLibSharp-Lidarr` 2.2.0.19 | Switch to upstream `taglib-sharp 2.3.0`; verify Audible / FLAC tag reading still works |

### 0.7 Sentry / error reporting

Two surfaces:

- **Backend** — `src/NzbDrone.Common/Instrumentation/SentryTarget.cs` (or
  similar) — point at fork's DSN or strip the Sentry sink if not running
  an org. Sentry DSN is read from config; no code change needed if you
  just change the config value.
- **Frontend** — `frontend/src/Diag/sentry.js` (or wherever
  `@sentry/browser` is initialised) — same; switch DSN.

### 0.8 Translation hub

Upstream uses `translate.servarr.com` (Weblate). Three options:

| Option | Cost | Behaviour |
|---|---|---|
| Freeze translations at HEAD | Free | Strings in `src/NzbDrone.Core/Localization/Core/*.json` stop evolving. Acceptable for Phase 0; revisit |
| Guest on translate.servarr.com | Free, dependent | Risk if Servarr revokes |
| Self-host Weblate | ~$15/mo VPS | Real autonomy |

Pick freeze for Phase 0. Add a `LOCALIZATION.md` note in the repo to set
expectations.

### Exit criteria

- `git clone {fork} && ./build.sh --backend && yarn build` succeeds on
  a clean macOS + Linux + Windows runner.
- README shows new project name; binaries advertise the new product
  name; installer signs cleanly with the fork's cert.
- All tests pass green (Selenium suite tolerated as failing — Phase 1
  quarantines it).

---

## Phase 1 — Code triage (1 week)

**Goal:** Strip dead paths, set a coverage floor, lock the build before
invasive refactors.

### 1.1 Inventory Goodreads-specific code

Run this from the repo root and check the output into
`docs/metadata-removal-plan.md`:

```bash
{
  echo "## Goodreads-bound files (to be deleted in Phase 5)"
  echo
  find src/NzbDrone.Core/MetadataSource/Goodreads -type f
  find src/NzbDrone.Core/MetadataSource/GoodreadsSearchProxy -type f
  echo
  echo "## Files that import a Goodreads type (must be migrated first)"
  echo
  grep -rln "NzbDrone\.Core\.MetadataSource\.Goodreads" src/ \
    | grep -v "/Goodreads/" \
    | grep -v "/GoodreadsSearchProxy/"
  echo
  echo "## Goodreads-shaped public API surface"
  grep -rn "Goodreads" src/Readarr.Api.V1 || true
} > docs/metadata-removal-plan.md
```

This becomes the deletion checklist for Phase 5.

### 1.2 Quarantine the Selenium suite

`NzbDrone.Automation.Test` pins Selenium 3.141 + ChromeDriver 91 — both
years out of date. Don't try to fix Selenium 3; mark the whole project's
tests as explicit so CI skips them:

```csharp
// src/NzbDrone.Automation.Test/AutomationTest.cs (or whatever the base is)
using NUnit.Framework;

[SetUpFixture]
[Explicit("Selenium 3 + ChromeDriver 91 — quarantined until Playwright port (Phase 10)")]
public class AutomationFixtureGate
{
    // empty body; the [Explicit] attribute on a SetUpFixture skips the whole assembly
    // unless --filter is used to opt in
}
```

Alternative if `[Explicit]` on `SetUpFixture` doesn't propagate — add it
to each `[Test]` directly:

```bash
# scripted attribute bump
find src/NzbDrone.Automation.Test -name "*.cs" -exec \
  sed -i '' -e 's/\[Test\]/[Test, Explicit("Selenium 3 quarantined")]/' {} \;
```

Add a comment to `CLAUDE.md` so future sessions know this is deliberate.

### 1.3 Coverage baseline

Capture current numbers:

```bash
dotnet test --collect:"XPlat Code Coverage" --settings src/coverlet.runsettings
# parse the resulting coverage.cobertura.xml for line-rate
```

Then edit `src/coverlet.runsettings` to add a fail-under threshold:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat code coverage">
        <Configuration>
          <Format>cobertura,opencover</Format>
          <Exclude>[Readarr.Test.Common]*,[*]NzbDrone.Test.Dummy.*</Exclude>
          <ExcludeByFile>**/Migration/*.cs</ExcludeByFile>
          <!-- New for Phase 1: fail if line coverage drops below baseline -->
          <Threshold>40</Threshold>   <!-- replace with measured baseline -->
          <ThresholdType>line</ThresholdType>
          <ThresholdStat>total</ThresholdStat>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

### 1.4 Decide on `src/Libraries/Interop.NetFwTypeLib.dll`

It's used by `NzbDrone.Windows/` to add a Windows Firewall rule for the
bind port at first run. Two options:

- **Keep** — add a `LICENSE.txt` next to it documenting that it's
  `tlbimp`-generated from the OS COM type library and that
  redistribution of generated interop assemblies for OS COM types is
  generally permitted.
- **Replace** with `DirectN`-style late-bound dispatch — more work, no
  vendored binary, but pulls in extra runtime cost.

Phase 1 keeps it; flag for Phase 10.

### Exit criteria

- `docs/metadata-removal-plan.md` exists and lists every Goodreads-touch
  file in the repo.
- `dotnet test` runs the unit + integration suites and skips the
  automation suite.
- Coverage threshold gate is enforced in CI.

---

## Phase 2 — Refactor the metadata seam (1-2 weeks)

**Goal:** No Goodreads types in `IProvide*` / `ISearchForNew*`. This is
the prerequisite for any second concrete proxy.

### 2.1 New neutral DTOs

```csharp
// src/NzbDrone.Core/Books/Model/SeriesInfo.cs
using System.Collections.Generic;

namespace NzbDrone.Core.Books
{
    /// <summary>
    /// Provider-neutral series description. Replaces
    /// NzbDrone.Core.MetadataSource.Goodreads.SeriesResource on the
    /// IProvideSeriesInfo contract.
    /// </summary>
    public class SeriesInfo
    {
        // Source identifier as a string so OpenLibrary (e.g. "OL...W")
        // and Wikidata (e.g. "Q12345") IDs both fit.
        public string ForeignSeriesId { get; set; }

        public string Title { get; set; }
        public string Description { get; set; }

        // Each linked book carries its own foreign id (the work id from
        // whichever metadata source is active) plus its ordinal in the
        // series ("Book 3", "0.5", etc — string because half-ordinals
        // like "2.5" are real).
        public List<SeriesBookLink> Books { get; set; } = new();
    }

    public class SeriesBookLink
    {
        public string ForeignBookId { get; set; }
        public string Position { get; set; }
    }
}
```

```csharp
// src/NzbDrone.Core/Books/Model/ListInfo.cs
using System.Collections.Generic;

namespace NzbDrone.Core.Books
{
    /// <summary>
    /// Provider-neutral curated-list description. Replaces the
    /// Goodreads "Listopia" shape on IProvideListInfo.
    /// </summary>
    public class ListInfo
    {
        public string ForeignListId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public List<ListInfoBook> Books { get; set; } = new();
    }

    public class ListInfoBook
    {
        public string ForeignBookId { get; set; }
        public int Rank { get; set; }
    }
}
```

### 2.2 Updated interfaces

```csharp
// src/NzbDrone.Core/MetadataSource/IProvideSeriesInfo.cs
using NzbDrone.Core.Books;

namespace NzbDrone.Core.MetadataSource
{
    public interface IProvideSeriesInfo
    {
        // Before: SeriesResource GetSeriesInfo(int id, bool useCache = true)
        // After: neutral DTO + string id so OL/Wikidata IDs fit.
        SeriesInfo GetSeriesInfo(string foreignSeriesId, bool useCache = true);
    }
}
```

```csharp
// src/NzbDrone.Core/MetadataSource/IProvideListInfo.cs
using NzbDrone.Core.Books;

namespace NzbDrone.Core.MetadataSource
{
    public interface IProvideListInfo
    {
        ListInfo GetListInfo(string foreignListId, int page, bool useCache = true);
    }
}
```

```csharp
// src/NzbDrone.Core/MetadataSource/ISearchForNewBook.cs
using System.Collections.Generic;
using NzbDrone.Core.Books;

namespace NzbDrone.Core.MetadataSource
{
    public interface ISearchForNewBook
    {
        List<Book> SearchForNewBook(string title, string author, bool getAllEditions = true);
        List<Book> SearchByIsbn(string isbn);
        List<Book> SearchByAsin(string asin);

        // RENAMED: was SearchByGoodreadsBookId(int, bool).
        // OpenLibraryProxy parses "OL...W" strings.
        // BookInfoProxy keeps parsing integer Goodreads ids during the
        // transition window.
        List<Book> SearchByForeignBookId(string foreignId, bool getAllEditions);
    }
}
```

### 2.3 `BookInfoProxy` adapter

`BookInfoProxy` keeps working — it absorbs the int → string parsing
internally so callers don't care:

```csharp
// src/NzbDrone.Core/MetadataSource/BookInfo/BookInfoProxy.cs (snippet)
public List<Book> SearchByForeignBookId(string foreignId, bool getAllEditions)
{
    // BookInfo's API still uses Goodreads numeric ids.
    if (!int.TryParse(foreignId, out var goodreadsBookId))
    {
        _logger.Debug("BookInfoProxy received non-numeric foreign id '{0}', returning empty", foreignId);
        return new List<Book>();
    }

    return SearchByGoodreadsBookId(goodreadsBookId, getAllEditions);
}

// Existing method stays private during the transition; remove in Phase 5.
private List<Book> SearchByGoodreadsBookId(int goodreadsId, bool getAllEditions)
{
    // unchanged body
}

public SeriesInfo GetSeriesInfo(string foreignSeriesId, bool useCache = true)
{
    if (!int.TryParse(foreignSeriesId, out var id))
    {
        throw new InvalidOperationException(
            $"BookInfoProxy expects integer Goodreads series ids; got '{foreignSeriesId}'.");
    }

    var goodreadsResource = GetSeriesInfoInternal(id, useCache);
    return SeriesMapper.ToNeutral(goodreadsResource);
}

private Goodreads.SeriesResource GetSeriesInfoInternal(int id, bool useCache)
{
    // existing body that hits the BookInfo API
}
```

And the mapper:

```csharp
// src/NzbDrone.Core/MetadataSource/BookInfo/SeriesMapper.cs
using System.Linq;
using NzbDrone.Core.Books;
using NzbDrone.Core.MetadataSource.Goodreads;

namespace NzbDrone.Core.MetadataSource.BookInfo
{
    internal static class SeriesMapper
    {
        public static SeriesInfo ToNeutral(SeriesResource src)
        {
            if (src == null) return null;
            return new SeriesInfo
            {
                ForeignSeriesId = src.Id.ToString(),
                Title           = src.Title,
                Description     = src.Description,
                Books           = src.Works.Select(w => new SeriesBookLink
                {
                    ForeignBookId = w.Id.ToString(),
                    Position      = w.UserPosition
                }).ToList()
            };
        }
    }
}
```

### 2.4 Update consumers

Seven files reference `IProvideSeriesInfo` / `IProvideListInfo` /
`SearchByGoodreadsBookId`:

- `Books/Services/AddAuthorService.cs`
- `Books/Services/RefreshAuthorService.cs`
- `Books/Services/AddBookService.cs`
- `Books/Services/RefreshBookService.cs`
- `ImportLists/ImportListSyncService.cs`
- `MediaFiles/BookImport/Identification/CandidateService.cs`
- `MediaFiles/BookImport/Manual/ManualImportService.cs`

Each change is mechanical — accept `string` IDs, use the new DTO types.
Pattern:

```diff
-public List<Book> Identify(int goodreadsBookId, bool allEditions)
+public List<Book> Identify(string foreignBookId, bool allEditions)
 {
-    return _bookSearchService.SearchByGoodreadsBookId(goodreadsBookId, allEditions);
+    return _bookSearchService.SearchByForeignBookId(foreignBookId, allEditions);
 }
```

### 2.5 Unit tests for the new shape

`src/NzbDrone.Core.Test/MetadataSource/SeriesInfoMappingFixture.cs`:

```csharp
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource.BookInfo;
using NzbDrone.Core.MetadataSource.Goodreads;

namespace NzbDrone.Core.Test.MetadataSource
{
    [TestFixture]
    public class SeriesInfoMappingFixture
    {
        [Test]
        public void Maps_basic_series_with_books()
        {
            var src = new SeriesResource
            {
                Id = 49075,
                Title = "The Stormlight Archive",
                Works = new()
                {
                    new() { Id = 7235533, UserPosition = "1" },
                    new() { Id = 17332218, UserPosition = "2" }
                }
            };

            var result = SeriesMapper.ToNeutral(src);

            result.ForeignSeriesId.Should().Be("49075");
            result.Books.Should().HaveCount(2);
            result.Books[0].ForeignBookId.Should().Be("7235533");
            result.Books[0].Position.Should().Be("1");
        }
    }
}
```

### Exit criteria

- All consumers compile against neutral DTOs and string IDs.
- `BookInfoProxy` round-trips Goodreads-shaped data through the new
  interfaces without behavior change.
- New mapping fixture passes.

---

## Phase 3 — `OpenLibraryProxy` MVP (2-3 weeks)

**Goal:** Read-only OL provider behind the same interfaces, selectable
via config.

### 3.1 Folder skeleton

```
src/NzbDrone.Core/MetadataSource/OpenLibrary/
├── OpenLibraryProxy.cs
├── OpenLibraryRequestBuilder.cs
├── OpenLibraryException.cs
├── Mappers/
│   ├── OpenLibraryWorkMapper.cs
│   ├── OpenLibraryEditionMapper.cs
│   ├── OpenLibraryAuthorMapper.cs
│   ├── OpenLibrarySearchMapper.cs
│   └── OpenLibraryDateParser.cs
└── Resources/
    ├── OpenLibraryWorkResource.cs
    ├── OpenLibraryEditionResource.cs
    ├── OpenLibraryAuthorResource.cs
    ├── OpenLibraryAuthorWorksResource.cs
    ├── OpenLibrarySearchResource.cs
    └── OpenLibraryIsbnResource.cs
```

### 3.2 Request builder

```csharp
// src/NzbDrone.Core/MetadataSource/OpenLibrary/OpenLibraryRequestBuilder.cs
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.MetadataSource.OpenLibrary
{
    public interface IOpenLibraryRequestBuilder
    {
        HttpRequestBuilder For(string path);
    }

    public class OpenLibraryRequestBuilder : IOpenLibraryRequestBuilder
    {
        private const string BaseUrl = "https://openlibrary.org/";
        private readonly IConfigService _config;

        public OpenLibraryRequestBuilder(IConfigService config) => _config = config;

        public HttpRequestBuilder For(string path)
        {
            // OL asks for a User-Agent that identifies the consumer.
            // See https://openlibrary.org/developers/api#politeness
            var ua = $"Bookarr/{BuildInfo.Version} (+https://bookarr.dev)";

            return new HttpRequestBuilder(BaseUrl + path.TrimStart('/'))
                .Accept(HttpAccept.Json)
                .SetHeader("User-Agent", ua)
                .WithRateLimit(0.6);  // ≈100 req/min — generous for OL
        }
    }
}
```

`.WithRateLimit(...)` reuses the existing per-host rate-limit
infrastructure in `NzbDrone.Common/Http/`. Look at any indexer for the
existing usage pattern.

### 3.3 Resources

Just the JSON DTOs — no business logic:

```csharp
// src/NzbDrone.Core/MetadataSource/OpenLibrary/Resources/OpenLibraryWorkResource.cs
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NzbDrone.Core.MetadataSource.OpenLibrary.Resources
{
    public class OpenLibraryWorkResource
    {
        // OL returns keys as "/works/OL12345W"; strip the prefix at the boundary.
        public string Key { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }

        // Description is sometimes a plain string, sometimes a typed object
        // {"type": "/type/text", "value": "..."} — handle both:
        public OpenLibraryDescription Description { get; set; }

        [JsonProperty("first_publish_date")]
        public string FirstPublishDate { get; set; }
        public List<string> Subjects { get; set; }
        public List<OpenLibraryAuthorLink> Authors { get; set; }
        public List<int> Covers { get; set; }
    }

    public class OpenLibraryAuthorLink
    {
        public OpenLibraryKey Author { get; set; }
    }

    public class OpenLibraryKey
    {
        public string Key { get; set; }  // "/authors/OL...A"
    }

    public class OpenLibraryDescription
    {
        // Custom converter handles `"foo"` vs `{"value":"foo"}`
        public string Value { get; set; }
    }
}
```

`OpenLibraryEditionResource` (`/books/OL...M.json`) — similar shape:
title, subtitle, publishers[], publish_date, isbn_10[], isbn_13[],
number_of_pages, physical_format, covers[], languages[], works[],
identifiers (.amazon[] = ASIN), description.

`OpenLibraryAuthorResource` (`/authors/OL...A.json`): name,
personal_name, bio (string or object), birth_date, death_date,
alternate_names[], links[], photos[].

`OpenLibrarySearchResource` (`/search.json`): docs[] each with key,
title, author_name[], first_publish_year, isbn[], cover_i, edition_count.

`OpenLibraryIsbnResource` — usually a 302 redirect to `/books/OL...M.json`,
follow it.

### 3.4 The proxy

```csharp
// src/NzbDrone.Core/MetadataSource/OpenLibrary/OpenLibraryProxy.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Books;
using NzbDrone.Core.MetadataSource.OpenLibrary.Mappers;
using NzbDrone.Core.MetadataSource.OpenLibrary.Resources;

namespace NzbDrone.Core.MetadataSource.OpenLibrary
{
    public class OpenLibraryProxy
        : IProvideAuthorInfo, IProvideBookInfo,
          ISearchForNewAuthor, ISearchForNewBook, ISearchForNewEntity
    {
        private readonly IHttpClient _http;
        private readonly IOpenLibraryRequestBuilder _req;
        private readonly Logger _logger;

        public OpenLibraryProxy(IHttpClient http, IOpenLibraryRequestBuilder req, Logger logger)
        {
            _http = http;
            _req = req;
            _logger = logger;
        }

        // ── IProvideAuthorInfo ────────────────────────────────────────

        public Author GetAuthorInfo(string foreignAuthorId, bool useCache = true)
        {
            // foreignAuthorId is the OL author key, e.g. "OL5749351A".
            // Two HTTP calls: author detail + first page of works.
            var authorReq = _req.For($"authors/{foreignAuthorId}.json").Build();
            var worksReq  = _req.For($"authors/{foreignAuthorId}/works.json?limit=200").Build();

            var authorResp = _http.Get<OpenLibraryAuthorResource>(authorReq);
            var worksResp  = _http.Get<OpenLibraryAuthorWorksResource>(worksReq);

            if (authorResp.Resource == null)
                throw new AuthorNotFoundException(foreignAuthorId);

            return OpenLibraryAuthorMapper.ToAuthor(authorResp.Resource, worksResp.Resource);
        }

        public HashSet<string> GetChangedAuthors(DateTime startTime)
        {
            // OL has no "changed-since" endpoint. The Phase 4 strategy is to
            // poll each monitored author's /works.json on the per-author refresh
            // schedule (Jobs/TaskManager). Return empty to suppress the
            // "delta refresh" path.
            return new HashSet<string>();
        }

        // ── IProvideBookInfo ───────────────────────────────────────────

        public Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string id)
        {
            // id is an OL work key, e.g. "OL14931151W".
            var workReq = _req.For($"works/{id}.json").Build();
            var work = _http.Get<OpenLibraryWorkResource>(workReq).Resource
                ?? throw new BookNotFoundException(id);

            // Fetch editions for this work; we'll pick one as the "primary"
            // edition and surface the rest.
            var editionsReq = _req.For($"works/{id}/editions.json?limit=50").Build();
            var editions = _http.Get<OpenLibraryEditionListResource>(editionsReq).Resource;

            var (book, authors) = OpenLibraryWorkMapper.ToBook(work, editions);

            // Returns the work id (so callers can identity-match), the book,
            // and any AuthorMetadata records the call learned about along
            // the way.
            return Tuple.Create(id, book, authors);
        }

        // ── ISearchForNewAuthor ────────────────────────────────────────

        public List<Author> SearchForNewAuthor(string title)
        {
            // OL /search/authors.json?q=...
            var req = _req.For($"search/authors.json?q={Uri.EscapeDataString(title)}").Build();
            var resp = _http.Get<OpenLibraryAuthorSearchResource>(req);
            return resp.Resource?.Docs
                .Select(OpenLibrarySearchMapper.ToAuthorSummary)
                .ToList() ?? new();
        }

        // ── ISearchForNewBook ──────────────────────────────────────────

        public List<Book> SearchForNewBook(string title, string author, bool getAllEditions = true)
        {
            var qs = $"?title={Uri.EscapeDataString(title)}";
            if (!string.IsNullOrWhiteSpace(author))
                qs += $"&author={Uri.EscapeDataString(author)}";
            qs += "&limit=20&fields=key,title,author_name,first_publish_year,isbn,cover_i,edition_count";

            var req = _http.Get<OpenLibrarySearchResource>(_req.For($"search.json{qs}").Build());
            return OpenLibrarySearchMapper.ReRankAndMap(req.Resource, title, author);
        }

        public List<Book> SearchByIsbn(string isbn)
        {
            // /isbn/{isbn}.json redirects (302) to /books/OL...M.json
            var resp = _http.Get<OpenLibraryEditionResource>(_req.For($"isbn/{isbn}.json").Build());
            if (resp.Resource == null) return new List<Book>();
            return new List<Book> { OpenLibraryEditionMapper.ToBook(resp.Resource) };
        }

        public List<Book> SearchByAsin(string asin)
        {
            // OL stores ASIN under identifiers.amazon; OL doesn't have a
            // dedicated ASIN endpoint. Use the search index:
            var req = _req.For($"search.json?q=identifier%3A{Uri.EscapeDataString(asin)}&limit=5").Build();
            var resp = _http.Get<OpenLibrarySearchResource>(req);
            return OpenLibrarySearchMapper.ReRankAndMap(resp.Resource, asin, null);
        }

        public List<Book> SearchByForeignBookId(string foreignId, bool getAllEditions)
        {
            // Caller may pass an OL work id or an OL edition id. Distinguish
            // by the trailing letter (W = work, M = manifest/edition).
            if (foreignId.EndsWith("W"))
            {
                var (_, book, _) = GetBookInfo(foreignId);
                return new() { book };
            }
            if (foreignId.EndsWith("M"))
            {
                var resp = _http.Get<OpenLibraryEditionResource>(_req.For($"books/{foreignId}.json").Build());
                return resp.Resource == null
                    ? new()
                    : new() { OpenLibraryEditionMapper.ToBook(resp.Resource) };
            }
            return new List<Book>();
        }

        // ── ISearchForNewEntity ────────────────────────────────────────

        public List<object> SearchForNewEntity(string title)
        {
            // Combined author + book lookup the SPA quick-search uses.
            var authors = SearchForNewAuthor(title).Cast<object>();
            var books   = SearchForNewBook(title, null).Cast<object>();
            return authors.Concat(books).Take(40).ToList();
        }
    }
}
```

### 3.5 Mappers

Field-by-field translation. Source of truth for the mapping table:
`METADATA-MIGRATION.md` §7.

```csharp
// src/NzbDrone.Core/MetadataSource/OpenLibrary/Mappers/OpenLibraryWorkMapper.cs
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Books;
using NzbDrone.Core.MetadataSource.OpenLibrary.Resources;

namespace NzbDrone.Core.MetadataSource.OpenLibrary.Mappers
{
    internal static class OpenLibraryWorkMapper
    {
        public static (Book book, List<AuthorMetadata> authors)
            ToBook(OpenLibraryWorkResource work, OpenLibraryEditionListResource editions)
        {
            // Pick a "primary" edition. Heuristic:
            //   1. English-language paperback or ebook
            //   2. Largest edition_count score (proxy for "popular")
            //   3. Fall back to the first edition.
            var primaryEdition = editions?.Entries?
                .OrderByDescending(e => e.Languages?.Any(l => l.Key == "/languages/eng") == true ? 1 : 0)
                .ThenBy(e => string.Equals(e.PhysicalFormat, "ebook", System.StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .FirstOrDefault();

            var workId = StripPrefix(work.Key, "/works/");
            var editionId = primaryEdition != null ? StripPrefix(primaryEdition.Key, "/books/") : null;

            var book = new Book
            {
                ForeignBookId    = workId,
                ForeignEditionId = editionId,
                Title            = work.Title,
                ReleaseDate      = OpenLibraryDateParser.ParseDate(work.FirstPublishDate ?? primaryEdition?.PublishDate),
                Genres           = work.Subjects?.Take(8).ToList() ?? new(),
                Ratings          = new Ratings(),                 // OL ratings are sparse; populate via /works/{key}/ratings.json in a follow-up
                Links            = new(),                         // populated below
                CleanTitle       = CleanTitle(work.Title),
                AnyEditionOk     = true,
                Monitored        = true
            };

            // Surface as Goodreads-ish "links" so the UI's existing display
            // works without changes.
            book.Links.Add(new Links { Url = $"https://openlibrary.org/works/{workId}", Name = "Open Library" });

            // Translate author refs. The proxy may not have loaded them yet,
            // so produce AuthorMetadata stubs that downstream services can
            // hydrate via GetAuthorInfo on first refresh.
            var authors = work.Authors?
                .Select(a => StripPrefix(a.Author?.Key, "/authors/"))
                .Where(k => !string.IsNullOrEmpty(k))
                .Select(k => new AuthorMetadata { ForeignAuthorId = k })
                .ToList() ?? new();

            return (book, authors);
        }

        private static string StripPrefix(string key, string prefix) =>
            key != null && key.StartsWith(prefix) ? key[prefix.Length..] : key;

        // Reuse the Parser/Parser.cs title-cleaning when feasible to keep
        // CleanTitle deterministic across providers.
        private static string CleanTitle(string title) =>
            NzbDrone.Core.Parser.Parser.CleanAuthorName(title); // ← double-check this is the right helper
    }
}
```

```csharp
// src/NzbDrone.Core/MetadataSource/OpenLibrary/Mappers/OpenLibraryDateParser.cs
using System;
using System.Globalization;

namespace NzbDrone.Core.MetadataSource.OpenLibrary.Mappers
{
    // OL publish dates are aggressively heterogeneous:
    //   "1997"
    //   "Apr 1997"
    //   "April 1997"
    //   "1997-04-15"
    //   "April 15, 1997"
    //   "n.d."
    //   "[1997]"
    internal static class OpenLibraryDateParser
    {
        private static readonly string[] Formats =
        {
            "yyyy-MM-dd", "yyyy-MM", "yyyy",
            "MMM yyyy", "MMMM yyyy",
            "MMM d, yyyy", "MMMM d, yyyy",
            "d MMM yyyy", "d MMMM yyyy"
        };

        public static DateTime? ParseDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            raw = raw.Trim().Trim('[', ']').Trim();
            if (raw.Length == 0 || raw.Equals("n.d.", StringComparison.OrdinalIgnoreCase)) return null;

            return DateTime.TryParseExact(raw, Formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt) ? dt : (DateTime?)null;
        }
    }
}
```

### 3.6 Source selector

```csharp
// src/NzbDrone.Core/MetadataSource/MetadataSourceFactory.cs
using System;
using DryIoc;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.BookInfo;
using NzbDrone.Core.MetadataSource.OpenLibrary;

namespace NzbDrone.Core.MetadataSource
{
    public interface IMetadataSourceFactory
    {
        IProvideAuthorInfo AuthorInfo();
        IProvideBookInfo   BookInfo();
        ISearchForNewBook  BookSearch();
        ISearchForNewAuthor AuthorSearch();
        ISearchForNewEntity EntitySearch();
    }

    public class MetadataSourceFactory : IMetadataSourceFactory
    {
        private readonly IConfigService _config;
        private readonly IResolver _container;

        public MetadataSourceFactory(IConfigService config, IResolver container)
        {
            _config = config;
            _container = container;
        }

        // Resolve a typed instance from the container by source.
        private T Resolve<T>() where T : class
        {
            var source = _config.MetadataSource ?? "BookInfo";
            return source switch
            {
                "OpenLibrary" => _container.Resolve<OpenLibraryProxy>() as T,
                _              => _container.Resolve<BookInfoProxy>()    as T
            } ?? throw new InvalidOperationException(
                $"Provider {source} does not implement {typeof(T).Name}");
        }

        public IProvideAuthorInfo   AuthorInfo()   => Resolve<IProvideAuthorInfo>();
        public IProvideBookInfo     BookInfo()     => Resolve<IProvideBookInfo>();
        public ISearchForNewBook    BookSearch()   => Resolve<ISearchForNewBook>();
        public ISearchForNewAuthor  AuthorSearch() => Resolve<ISearchForNewAuthor>();
        public ISearchForNewEntity  EntitySearch() => Resolve<ISearchForNewEntity>();
    }
}
```

### 3.7 Config addition

```csharp
// src/NzbDrone.Core/Configuration/ConfigService.cs (snippet)
public string MetadataSource
{
    // Keys live in the DB `Config` key/value table; default is "BookInfo"
    // until Phase 5 flips it to "OpenLibrary".
    get { return GetValue("MetadataSource", "BookInfo"); }
    set { SetValue("MetadataSource", value); }
}
```

### 3.8 Re-wire consumers

Inject `IMetadataSourceFactory` and resolve per-call. Example:

```diff
 public class RefreshAuthorService : IExecute<RefreshAuthorCommand>
 {
-    private readonly IProvideAuthorInfo _authorInfo;
+    private readonly IMetadataSourceFactory _metadata;
     ...
     public RefreshAuthorService(
-        IProvideAuthorInfo authorInfo,
+        IMetadataSourceFactory metadata,
         ...)
     {
-        _authorInfo = authorInfo;
+        _metadata = metadata;
         ...
     }

     public void Execute(RefreshAuthorCommand command)
     {
-        var author = _authorInfo.GetAuthorInfo(command.AuthorId);
+        var author = _metadata.AuthorInfo().GetAuthorInfo(command.AuthorId);
         ...
     }
 }
```

### 3.9 Cover-art handling

`MediaCoverService` already pulls arbitrary URLs. Make sure the cover URL
on Book/Author records is the OL one:

```csharp
// In OpenLibraryWorkMapper / OpenLibraryAuthorMapper:
book.Images = work.Covers?
    .Where(id => id > 0)
    .Take(1)
    .Select(id => new MediaCover.MediaCover
    {
        CoverType = MediaCoverTypes.Cover,
        Url = $"https://covers.openlibrary.org/b/id/{id}-L.jpg"
    })
    .ToList() ?? new();
```

No changes needed in `MediaCoverService` itself.

### Exit criteria

- `OpenLibraryProxy` returns valid `Author`, `Book`, `AuthorMetadata`,
  `Edition` objects for ≥100 hand-picked inputs.
- Cassette-style unit tests under
  `NzbDrone.Core.Test/MetadataSource/OpenLibrary/` pass.
- Manual smoke test: flip `Config.MetadataSource = "OpenLibrary"`, add an
  author, see books and covers populate within 60s.

---

## Phase 4 — Hardening (2 weeks)

**Goal:** Production-grade OL proxy.

### 4.1 Caching

Mirror `BookInfoProxy`'s `LazyCache` pattern (`BookInfoProxy.cs:1-65`).
Add `IAppCache` to the constructor:

```csharp
public OpenLibraryProxy(
    IHttpClient http,
    IOpenLibraryRequestBuilder req,
    ICacheManager cacheManager,
    Logger logger)
{
    _http = http;
    _req  = req;
    _logger = logger;

    _authorCache  = new CachingService(new MemoryCacheProvider(
        new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 })));
    _workCache    = new CachingService(new MemoryCacheProvider(
        new MemoryCache(new MemoryCacheOptions { SizeLimit = 5000 })));
    _editionCache = new CachingService(new MemoryCacheProvider(
        new MemoryCache(new MemoryCacheOptions { SizeLimit = 5000 })));
}

private static readonly TimeSpan AuthorTtl  = TimeSpan.FromHours(24);
private static readonly TimeSpan WorkTtl    = TimeSpan.FromDays(7);
private static readonly TimeSpan EditionTtl = TimeSpan.FromDays(30);
private static readonly TimeSpan SearchTtl  = TimeSpan.FromHours(1);
```

Wrap the per-resource fetches:

```csharp
public Author GetAuthorInfo(string foreignAuthorId, bool useCache = true)
{
    if (!useCache)
        _authorCache.Remove(foreignAuthorId);

    return _authorCache.GetOrAdd(foreignAuthorId, () => GetAuthorInfoFresh(foreignAuthorId),
        new MemoryCacheEntryOptions { SlidingExpiration = AuthorTtl, Size = 1 });
}
```

### 4.2 Polly retries

```csharp
// src/NzbDrone.Core/MetadataSource/OpenLibrary/OpenLibraryRetryPolicy.cs
using System;
using System.Net;
using Polly;
using Polly.Retry;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.MetadataSource.OpenLibrary
{
    internal static class OpenLibraryRetryPolicy
    {
        // Polly 8 AsyncRetryPolicy<HttpResponse<T>> isn't a thing because
        // NzbDrone.Common.Http is synchronous; build a sync retry instead.
        public static RetryPolicy<HttpResponse<T>> Build<T>()
            where T : new()
        {
            return Policy
                .HandleResult<HttpResponse<T>>(r =>
                    r.StatusCode == HttpStatusCode.TooManyRequests ||
                    ((int)r.StatusCode >= 500 && (int)r.StatusCode < 600))
                .WaitAndRetry(
                    retryCount: 4,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));
        }
    }
}
```

Apply in the proxy:

```csharp
var resp = OpenLibraryRetryPolicy.Build<OpenLibraryWorkResource>()
    .Execute(() => _http.Get<OpenLibraryWorkResource>(req));
```

### 4.3 Health check

```csharp
// src/NzbDrone.Core/HealthCheck/Checks/MetadataSourceConnectivityCheck.cs
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Localization;

namespace NzbDrone.Core.HealthCheck.Checks
{
    [CheckOn(typeof(ApplicationStartedEvent))]
    [CheckOn(typeof(ConfigSavedEvent))]
    public class MetadataSourceConnectivityCheck : HealthCheckBase
    {
        private readonly IHttpClient _http;
        private readonly IConfigService _config;

        public MetadataSourceConnectivityCheck(IHttpClient http, IConfigService config, ILocalizationService loc)
            : base(loc)
        {
            _http = http;
            _config = config;
        }

        public override HealthCheck Check()
        {
            var (url, sourceName) = _config.MetadataSource switch
            {
                "OpenLibrary" => ("https://openlibrary.org/status",   "Open Library"),
                _              => ("https://api.bookinfo.club/health", "BookInfo")
            };

            try
            {
                var response = _http.Get(new HttpRequest(url) { SuppressHttpError = true });
                if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
                    return new HealthCheck(GetType());
            }
            catch
            {
                // fall through
            }

            return new HealthCheck(GetType(), HealthCheckResult.Error,
                $"Metadata source {sourceName} ({url}) is unreachable. " +
                "Refresh and add operations will fail until connectivity is restored.",
                "#metadata-source-unreachable");
        }
    }
}
```

### 4.4 GetChangedAuthors semantics

OL has no delta API. Strategy: the per-author `RefreshAuthorCommand`
already runs on a 12-hour cadence (`Jobs/TaskManager.cs`); rely on that.
`GetChangedAuthors` returns empty for OL. Add a Logger.Debug when called
so future debugging is easier.

### 4.5 Swagger regeneration

```bash
./docs.sh linux   # invokes Swashbuckle CLI
```

Commits `src/Readarr.Api.V1/openapi.json`.

### Exit criteria

- p99 author-refresh latency on a 100-author test library within 2× the
  legacy `BookInfoProxy` numbers.
- 24h soak under realistic load reports 0 × 429.
- Health check goes red when `openlibrary.org` blocked at firewall.

---

## Phase 5 — ID-bridge & library migration (2-3 weeks)

**Goal:** Existing user libraries migrate without losing data.

### 5.1 Migration: `BookIdMapping` table

```csharp
// src/NzbDrone.Core/Datastore/Migration/042_book_id_mapping.cs
using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(42)]
    public class book_id_mapping : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.Table("BookIdMapping")
                .WithColumn("Id").AsInt32().NotNullable().PrimaryKey().Identity()
                .WithColumn("GoodreadsId").AsString().NotNullable()
                .WithColumn("OpenLibraryWorkId").AsString().Nullable()
                .WithColumn("OpenLibraryEditionId").AsString().Nullable()
                .WithColumn("Confidence").AsDouble().NotNullable().WithDefaultValue(0.0)
                .WithColumn("Source").AsString().NotNullable()
                .WithColumn("CreatedUtc").AsDateTimeOffset().NotNullable();

            Create.Index("IX_BookIdMapping_GoodreadsId")
                  .OnTable("BookIdMapping").OnColumn("GoodreadsId").Ascending();
            Create.Index("IX_BookIdMapping_OpenLibraryWorkId")
                  .OnTable("BookIdMapping").OnColumn("OpenLibraryWorkId").Ascending();
        }
    }
}
```

### 5.2 Domain model & repository

```csharp
// src/NzbDrone.Core/Books/BookIdMapping.cs
using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Books
{
    public enum BookIdMappingSource { Isbn, Asin, TitleAuthor, Manual, FileTag }

    public class BookIdMapping : ModelBase
    {
        public string GoodreadsId { get; set; }
        public string OpenLibraryWorkId { get; set; }
        public string OpenLibraryEditionId { get; set; }
        public double Confidence { get; set; }
        public BookIdMappingSource Source { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}
```

```csharp
// src/NzbDrone.Core/Books/BookIdMappingRepository.cs
public interface IBookIdMappingRepository : IBasicRepository<BookIdMapping>
{
    BookIdMapping FindByGoodreadsId(string goodreadsId);
}

public class BookIdMappingRepository : BasicRepository<BookIdMapping>, IBookIdMappingRepository
{
    public BookIdMappingRepository(IMainDatabase db, IEventAggregator events)
        : base(db, events) { }

    public BookIdMapping FindByGoodreadsId(string goodreadsId)
        => Query(b => b.GoodreadsId == goodreadsId).FirstOrDefault();
}
```

### 5.3 Reidentify command + handler

```csharp
// src/NzbDrone.Core/Books/Commands/ReidentifyLibraryCommand.cs
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Books.Commands
{
    public class ReidentifyLibraryCommand : Command
    {
        public override bool SendUpdatesToClient => true;
        public override bool RequiresDiskAccess => true;

        // If null, reidentify the whole library; otherwise limit to these author ids.
        public int[] AuthorIds { get; set; }
    }
}
```

Handler (sketch — the body is the meat of Phase 5):

```csharp
// src/NzbDrone.Core/Books/Services/ReidentifyService.cs
public class ReidentifyService : IExecute<ReidentifyLibraryCommand>
{
    private readonly IAuthorService _authors;
    private readonly IBookService   _books;
    private readonly IEditionService _editions;
    private readonly IBookIdMappingRepository _mappings;
    private readonly IMetadataSourceFactory   _metadata;
    private readonly IIdentificationService   _identification; // existing file-tag-based identifier
    private readonly Logger _logger;

    public void Execute(ReidentifyLibraryCommand cmd)
    {
        var authors = cmd.AuthorIds?.Any() == true
            ? _authors.GetAuthors(cmd.AuthorIds)
            : _authors.AllAuthors();

        var ol = _metadata; // factory will route to OpenLibraryProxy

        foreach (var author in authors)
        {
            try
            {
                var candidates = ol.AuthorSearch().SearchForNewAuthor(
                    $"{author.Metadata.Value.Name} {author.Metadata.Value.Born?.Year}".Trim());

                var match = candidates.FirstOrDefault();   // top OL match
                if (match == null) continue;

                _mappings.Insert(new BookIdMapping
                {
                    GoodreadsId       = author.Metadata.Value.ForeignAuthorId,
                    OpenLibraryWorkId = match.Metadata.Value.ForeignAuthorId, // technically author id; reuse the column for now
                    Confidence        = ScoreAuthor(author, match),
                    Source            = BookIdMappingSource.TitleAuthor,
                    CreatedUtc        = DateTime.UtcNow
                });

                foreach (var book in _books.GetBooksByAuthor(author.Id))
                {
                    ReidentifyBook(book, ol);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Reidentify failed for author {0}", author.Name);
            }
        }
    }

    private void ReidentifyBook(Book book, IMetadataSourceFactory ol)
    {
        // Preference: file tags → ISBN → ASIN → title+author search.
        // 1. Run the existing identification pipeline against the book's
        //    files; if it returns a confident OL hit, prefer it.
        var files = _editions.GetEditionFilesByBook(book.Id);
        if (files.Any())
        {
            var fromTags = _identification.IdentifyByFiles(files);
            if (fromTags?.Any() == true)
            {
                Record(book, fromTags.First(), BookIdMappingSource.FileTag);
                return;
            }
        }

        // 2-4 fall through to ISBN/ASIN/title.
        var isbn = book.Editions.Value.FirstOrDefault(e => !string.IsNullOrEmpty(e.Isbn13))?.Isbn13;
        if (isbn != null)
        {
            var match = ol.BookSearch().SearchByIsbn(isbn).FirstOrDefault();
            if (match != null) { Record(book, match, BookIdMappingSource.Isbn); return; }
        }

        var asin = book.Editions.Value.FirstOrDefault(e => !string.IsNullOrEmpty(e.Asin))?.Asin;
        if (asin != null)
        {
            var match = ol.BookSearch().SearchByAsin(asin).FirstOrDefault();
            if (match != null) { Record(book, match, BookIdMappingSource.Asin); return; }
        }

        var titleMatch = ol.BookSearch()
            .SearchForNewBook(book.Title, book.Author.Value?.Name, getAllEditions: false)
            .FirstOrDefault();
        if (titleMatch != null) Record(book, titleMatch, BookIdMappingSource.TitleAuthor);
    }

    private void Record(Book existing, Book olMatch, BookIdMappingSource source)
    {
        _mappings.Insert(new BookIdMapping
        {
            GoodreadsId            = existing.ForeignBookId,
            OpenLibraryWorkId      = olMatch.ForeignBookId,
            OpenLibraryEditionId   = olMatch.ForeignEditionId,
            Confidence             = source switch
            {
                BookIdMappingSource.FileTag     => 0.95,
                BookIdMappingSource.Isbn        => 0.90,
                BookIdMappingSource.Asin        => 0.80,
                BookIdMappingSource.TitleAuthor => 0.50, // user confirmation likely needed
                _                                => 0.30
            },
            Source                 = source,
            CreatedUtc             = DateTime.UtcNow
        });
    }

    private static double ScoreAuthor(Author existing, Author candidate)
    {
        // Compare birth year, alias overlap, name similarity. Cheap proxy:
        var nameDist = LevenshteinDistance(existing.Name, candidate.Name) / (double)existing.Name.Length;
        return Math.Clamp(1.0 - nameDist, 0.0, 1.0);
    }

    private static int LevenshteinDistance(string a, string b)
    {
        // Standard impl — omitted for brevity. Reuse one from
        // NzbDrone.Common/Extensions/ if present, otherwise drop in
        // a small helper.
        return 0;
    }
}
```

### 5.4 Frontend wizard

The wizard lives at `frontend/src/Settings/Metadata/MetadataSwitchWizard/`.
The pattern matches the existing `Settings/Indexers/` provider-add flow.

```jsx
// frontend/src/Settings/Metadata/MetadataSwitchWizard/MetadataSwitchWizard.js
import React, { Component } from 'react';
import PropTypes from 'prop-types';
import { connect } from 'react-redux';
import { startReidentify, confirmMapping, commitMigration }
    from 'Store/Actions/metadataMigrationActions';
import MetadataSwitchWizardStep1Intro from './Step1Intro';
import MetadataSwitchWizardStep2Progress from './Step2Progress';
import MetadataSwitchWizardStep3Resolve from './Step3Resolve';
import MetadataSwitchWizardStep4Commit from './Step4Commit';

class MetadataSwitchWizard extends Component {
    state = { step: 1 };

    onNext  = () => this.setState({ step: this.state.step + 1 });
    onBack  = () => this.setState({ step: Math.max(1, this.state.step - 1) });

    onStartReidentify = () => {
        this.props.dispatchStartReidentify();
        this.onNext();
    };

    onConfirmMapping = (mappingId, openLibraryWorkId) => {
        this.props.dispatchConfirmMapping({ mappingId, openLibraryWorkId });
    };

    onCommit = () => {
        this.props.dispatchCommitMigration();
        this.onNext();
    };

    render() {
        const { step } = this.state;
        const { progress, lowConfidenceMappings, isCommitting } = this.props;
        return (
            <div>
                {step === 1 && <MetadataSwitchWizardStep1Intro onStart={this.onStartReidentify} />}
                {step === 2 && <MetadataSwitchWizardStep2Progress progress={progress} onComplete={this.onNext} />}
                {step === 3 && (
                    <MetadataSwitchWizardStep3Resolve
                        mappings={lowConfidenceMappings}
                        onConfirm={this.onConfirmMapping}
                        onNext={this.onNext}
                    />
                )}
                {step === 4 && <MetadataSwitchWizardStep4Commit onCommit={this.onCommit} isCommitting={isCommitting} />}
            </div>
        );
    }
}

MetadataSwitchWizard.propTypes = {
    progress: PropTypes.object.isRequired,
    lowConfidenceMappings: PropTypes.array.isRequired,
    isCommitting: PropTypes.bool.isRequired,
    dispatchStartReidentify: PropTypes.func.isRequired,
    dispatchConfirmMapping: PropTypes.func.isRequired,
    dispatchCommitMigration: PropTypes.func.isRequired
};

const mapStateToProps = state => ({
    progress: state.metadataMigration.progress,
    lowConfidenceMappings: state.metadataMigration.lowConfidence,
    isCommitting: state.metadataMigration.isCommitting
});

const mapDispatchToProps = {
    dispatchStartReidentify: startReidentify,
    dispatchConfirmMapping: confirmMapping,
    dispatchCommitMigration: commitMigration
};

export default connect(mapStateToProps, mapDispatchToProps)(MetadataSwitchWizard);
```

Progress is pushed through SignalR (`NzbDrone.SignalR/MessageHub`) using
the existing `CommandUpdated` event:

```jsx
// frontend/src/Components/SignalRConnector.js — append a handler:
case 'reidentifyLibrary':
    // surface progress in the wizard's redux slice
    store.dispatch({ type: 'metadataMigration/progress', payload: payload.body });
    break;
```

### 5.5 Default flip & retirement

Once Phase 5 is shipped:

- Change the default in `ConfigService.MetadataSource` to `"OpenLibrary"`
  for new installs (existing installs keep their stored value until they
  run the wizard).
- Mark `BookInfoProxy` and the entire `MetadataSource/Goodreads/` +
  `MetadataSource/GoodreadsSearchProxy/` folders `[Obsolete]`. Delete in
  the next minor release after Phase 5.

### Exit criteria

- 500-book test library reidentifies with ≥85% high-confidence matches.
- Low-confidence rows surface in wizard with top-5 candidate picks.
- Post-migration full refresh completes without orphaning monitored
  entities.

---

## Phase 6 — Series, lists, import lists (2-3 weeks)

**Goal:** Restore series + lists on top of OL-shaped data.

### 6.1 `OpenLibrarySeriesProxy` via Wikidata SPARQL

OL works are sometimes linked to a Wikidata item via the
`external_links` field. Wikidata items carry `P179` (part of the series)
and `P1545` (series ordinal).

```csharp
// src/NzbDrone.Core/MetadataSource/OpenLibrary/OpenLibrarySeriesProxy.cs
public class OpenLibrarySeriesProxy : IProvideSeriesInfo
{
    private readonly IWikidataClient _wd;
    private readonly Logger _logger;

    public SeriesInfo GetSeriesInfo(string foreignSeriesId, bool useCache = true)
    {
        // foreignSeriesId is a Wikidata QID ("Q12345") or a local
        // user-curated series id (prefixed "local:")
        if (foreignSeriesId.StartsWith("local:"))
            return GetLocalSeries(foreignSeriesId);

        var sparql = $@"
            SELECT ?bookQid ?bookLabel ?ordinal ?ol WHERE {{
                ?bookQid wdt:P179 wd:{foreignSeriesId} .   # part of the series
                OPTIONAL {{ ?bookQid p:P179 ?stmt . ?stmt pq:P1545 ?ordinal }}
                OPTIONAL {{ ?bookQid wdt:P648 ?ol }}        # Open Library ID
                SERVICE wikibase:label {{ bd:serviceParam wikibase:language 'en'. }}
            }} ORDER BY xsd:decimal(?ordinal)";

        var results = _wd.Query(sparql);

        return new SeriesInfo
        {
            ForeignSeriesId = foreignSeriesId,
            Title           = _wd.GetLabel(foreignSeriesId),
            Books           = results.Rows.Select(r => new SeriesBookLink
            {
                ForeignBookId = NormalizeOl(r["ol"]) ?? r["bookQid"],
                Position      = r["ordinal"]
            }).ToList()
        };
    }
}
```

```csharp
// src/NzbDrone.Core/MetadataSource/OpenLibrary/WikidataClient.cs
public interface IWikidataClient
{
    SparqlResult Query(string sparql);
    string GetLabel(string qid);
}

public class WikidataClient : IWikidataClient
{
    private const string Endpoint = "https://query.wikidata.org/sparql";
    private readonly IHttpClient _http;

    public SparqlResult Query(string sparql)
    {
        var req = new HttpRequest(Endpoint)
        {
            Method = HttpMethod.GET,
            SuppressHttpError = false
        };
        req.AddQueryParam("query", sparql);
        req.AddQueryParam("format", "json");
        req.Headers["Accept"] = "application/sparql-results+json";
        req.Headers["User-Agent"] = $"Bookarr/{BuildInfo.Version} (+https://bookarr.dev)";

        var resp = _http.Get<WikidataSparqlResponse>(req);
        return new SparqlResult(resp.Resource);
    }
}
```

### 6.2 Local fallback series

When Wikidata is missing a series, fall back to a local table populated
manually by users.

```csharp
// FluentMigrator migration 043 (sketch):
Create.Table("LocalSeries")
    .WithColumn("Id").AsInt32().PrimaryKey().Identity()
    .WithColumn("Title").AsString().NotNullable()
    .WithColumn("Description").AsString().Nullable()
    .WithColumn("CreatedUtc").AsDateTimeOffset().NotNullable();

Create.Table("LocalSeriesBook")
    .WithColumn("LocalSeriesId").AsInt32().NotNullable().ForeignKey("LocalSeries", "Id")
    .WithColumn("BookId").AsInt32().NotNullable().ForeignKey("Books", "Id")
    .WithColumn("Position").AsString().Nullable();
```

UI: `frontend/src/Settings/Profiles/Series/` — list, add, edit local
series.

### 6.3 Import lists

```csharp
// src/NzbDrone.Core/ImportLists/OpenLibrary/Subject/OpenLibrarySubjectImportList.cs
public class OpenLibrarySubjectImportList : HttpImportListBase<OpenLibrarySubjectSettings>
{
    public override string Name => "Open Library Subject";
    public override ImportListType ListType => ImportListType.Other;
    public override TimeSpan MinRefreshInterval => TimeSpan.FromHours(12);

    public OpenLibrarySubjectImportList(/* ctor */) : base(/*...*/) { }

    public override IImportListRequestGenerator GetRequestGenerator() =>
        new OpenLibrarySubjectRequestGenerator(Settings);

    public override IParseImportListResponse GetParser() =>
        new OpenLibrarySubjectParser();
}

public class OpenLibrarySubjectSettings : IImportListSettings
{
    [FieldDefinition(0, Label = "Subject", HelpText = "Open Library subject tag, e.g. 'fantasy_fiction'")]
    public string Subject { get; set; }

    [FieldDefinition(1, Label = "Limit", Type = FieldType.Number)]
    public int Limit { get; set; } = 50;
}

public class OpenLibrarySubjectRequestGenerator : IImportListRequestGenerator
{
    public ImportListPageableRequestChain GetListItems()
    {
        var url = $"https://openlibrary.org/subjects/{Settings.Subject}.json?limit={Settings.Limit}";
        var req = new HttpRequest(url) { Method = HttpMethod.GET };
        return new ImportListPageableRequestChain { Pages = new() { new ImportListRequest(req) } };
    }
}

public class OpenLibrarySubjectParser : IParseImportListResponse
{
    public IList<ImportListItemInfo> ParseResponse(ImportListResponse response)
    {
        var page = response.Deserialize<OpenLibrarySubjectPage>();
        return page.Works.Select(w => new ImportListItemInfo
        {
            ForeignBookId = w.Key.Replace("/works/", ""),
            Title         = w.Title,
            Author        = w.Authors?.FirstOrDefault()?.Name
        }).ToList();
    }
}
```

`OpenLibraryAuthorImportList` (all works by one author) and
`OpenLibraryTrendingImportList` (`/trending/daily.json`) follow the same
shape — different URLs and parsers.

### 6.4 Remove Goodreads import lists

Delete `src/NzbDrone.Core/ImportLists/Goodreads/` and any UI registration
in the Settings → Import Lists provider catalog.

### Exit criteria

- Top-50 series via Wikidata return populated ordinals.
- New import list providers add 100 books from a public OL list without
  errors.

---

## Phase 7 — Audiobook supplement (2 weeks, optional)

**Goal:** Audiobook metadata coverage via an opt-in augmenter.

### 7.1 New interface

```csharp
// src/NzbDrone.Core/MetadataSource/IAugmentAudiobookInfo.cs
using NzbDrone.Core.Books;

namespace NzbDrone.Core.MetadataSource
{
    /// <summary>
    /// Composable add-on metadata source. Runs AFTER IProvideBookInfo to
    /// fill audiobook-specific fields (narrator, duration, ASIN cover).
    /// Each augmenter must degrade gracefully — if it fails, the book
    /// keeps the print-side metadata it already has.
    /// </summary>
    public interface IAugmentAudiobookInfo
    {
        bool CanAugment(Book book);
        Book Augment(Book book);
    }
}
```

### 7.2 `AudnexProxy`

```csharp
// src/NzbDrone.Core/MetadataSource/Audnex/AudnexProxy.cs
public class AudnexProxy : IAugmentAudiobookInfo
{
    private const string BaseUrl = "https://api.audnex.us/";
    private readonly IHttpClient _http;
    private readonly Logger _logger;

    public bool CanAugment(Book book)
    {
        // Run only when the book has an audiobook edition with an ASIN.
        return book.Editions.Value
            .Any(e => e.IsAudiobook() && !string.IsNullOrWhiteSpace(e.Asin));
    }

    public Book Augment(Book book)
    {
        var audiobookEditions = book.Editions.Value.Where(e => e.IsAudiobook() && !string.IsNullOrWhiteSpace(e.Asin));

        foreach (var ed in audiobookEditions)
        {
            try
            {
                var req = new HttpRequest($"{BaseUrl}books/{ed.Asin}");
                req.Headers["User-Agent"] = $"Bookarr/{BuildInfo.Version}";
                var resp = _http.Get<AudnexBookResource>(req);
                if (resp.Resource == null) continue;

                ed.Narrators   = resp.Resource.Narrators?.Select(n => n.Name).ToList();
                ed.RunTime     = TimeSpan.FromSeconds(resp.Resource.RuntimeLengthMin * 60);
                ed.PublishDate = OpenLibraryDateParser.ParseDate(resp.Resource.ReleaseDate);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Audnex augment failed for ASIN {0}; keeping print metadata", ed.Asin);
            }
        }
        return book;
    }
}
```

### 7.3 Wiring

In `Books/Services/RefreshBookService.cs`:

```csharp
public class RefreshBookService : IExecute<RefreshBookCommand>
{
    private readonly IMetadataSourceFactory _metadata;
    private readonly IEnumerable<IAugmentAudiobookInfo> _augmenters;
    private readonly IConfigService _config;

    public void Execute(RefreshBookCommand cmd)
    {
        var (id, book, authors) = _metadata.BookInfo().GetBookInfo(cmd.ForeignBookId);

        if (_config.AudiobookAugmentationEnabled)
        {
            foreach (var aug in _augmenters)
                if (aug.CanAugment(book)) book = aug.Augment(book);
        }

        // persist as before
    }
}
```

DryIoc auto-discovers `IAugmentAudiobookInfo` implementations and injects
them as `IEnumerable<>` without any explicit registration.

### Exit criteria

- 90% of top-100 Audible audiobooks have populated narrator + duration
  after augment.
- Augmenter failures degrade gracefully (no exception escapes).

---

## Phase 8 — Testing & QA (2-3 weeks, can overlap with Phases 3-7)

### 8.1 Golden-corpus tests

Fixture format — cassette-style JSON with both request and response:

```json
// src/NzbDrone.Core.Test/MetadataSource/OpenLibrary/Fixtures/work_OL14931151W.json
{
  "request": {
    "url": "https://openlibrary.org/works/OL14931151W.json",
    "method": "GET"
  },
  "response": {
    "status": 200,
    "body": {
      "key": "/works/OL14931151W",
      "title": "The Way of Kings",
      "first_publish_date": "2010-08-31",
      "authors": [{ "author": { "key": "/authors/OL5749351A" } }],
      "subjects": ["Fantasy fiction", "Epic fantasy"],
      "covers": [10520116]
    }
  }
}
```

Test runner:

```csharp
// src/NzbDrone.Core.Test/MetadataSource/OpenLibrary/OpenLibraryWorkMapperFixture.cs
[TestFixture]
public class OpenLibraryWorkMapperFixture : CoreTest
{
    private OpenLibraryProxy _proxy;
    private CassetteHttpClient _http;

    [SetUp]
    public void SetUp()
    {
        _http = new CassetteHttpClient("Fixtures/work_OL14931151W.json");
        _proxy = new OpenLibraryProxy(_http, Mocker.Resolve<IOpenLibraryRequestBuilder>(),
                                      Mocker.Resolve<ICacheManager>(), TestLogger);
    }

    [Test]
    public void GetBookInfo_maps_basic_fields()
    {
        var (workId, book, authors) = _proxy.GetBookInfo("OL14931151W");

        workId.Should().Be("OL14931151W");
        book.Title.Should().Be("The Way of Kings");
        book.ReleaseDate.Should().Be(new DateTime(2010, 8, 31));
        authors.Should().HaveCount(1);
        authors[0].ForeignAuthorId.Should().Be("OL5749351A");
    }
}
```

### 8.2 Reidentify regression suite

```bash
# tests/regression/reidentify.sh
#!/usr/bin/env bash
set -euo pipefail
# Restore a known starting state.
cp tests/regression/fixtures/goodreads_500book_library.db /tmp/readarr.db

# Spin up the backend with Config.MetadataSource forced to OpenLibrary.
READARR_DB=/tmp/readarr.db dotnet run --project src/NzbDrone.Console -- \
  --config tests/regression/fixtures/test-config.xml &
PID=$!
sleep 5

# Kick off the reidentify command via API.
curl -s -X POST http://localhost:8787/api/v1/command \
  -H "X-Api-Key: $TEST_API_KEY" \
  -d '{"name":"ReidentifyLibrary"}'

# Wait up to 10 minutes.
for i in $(seq 1 60); do
  STATE=$(curl -s http://localhost:8787/api/v1/command -H "X-Api-Key: $TEST_API_KEY" | jq -r '.[] | select(.name=="ReidentifyLibrary") | .state')
  [[ "$STATE" == "completed" ]] && break
  sleep 10
done

# Check the result.
HIGH=$(curl -s http://localhost:8787/api/v1/bookIdMapping -H "X-Api-Key: $TEST_API_KEY" \
  | jq '[.[] | select(.confidence >= 0.7)] | length')
echo "High-confidence: $HIGH / 500"

kill $PID
[[ $HIGH -ge 425 ]] || { echo "FAIL: <85% high-confidence"; exit 1; }
```

### 8.3 E2E smoke

Already sketched in METADATA-MIGRATION.md; promote to CI.

### 8.4 Coverage gate

Already wired in Phase 1 — ensure it doesn't regress as new code lands.

### Exit criteria

- All unit + integration suites green on the fork's CI matrix.
- Reidentify regression passes.
- One e2e smoke run per CI build.

---

## Phase 9 — Beta release (1 week)

### 9.1 Version reset

`azure-pipelines.yml` (or `.github/workflows/release.yml`):

```yaml
env:
  majorVersion: '1.0.0-beta'
  minorVersion: ${{ github.run_number }}
  bookarrVersion: ${{ env.majorVersion }}.${{ env.minorVersion }}
```

### 9.2 Migration guide

```markdown
# docs/migrating-from-readarr.md

## Quick start

1. Stop your existing Readarr install.
2. Back up `config.xml` and the SQLite DB file (default
   `~/.config/Readarr/readarr.db`).
3. Install Bookarr from the GitHub release page.
4. Point Bookarr at the same config directory.
5. Open Settings → Metadata → "Switch to Open Library" and run the
   wizard.
6. Inspect any low-confidence matches; pick the right Open Library work
   manually if needed.
7. Trigger a full library refresh and verify covers appear.

## What changes

- Foreign IDs in the database move from Goodreads numeric ids to Open
  Library `OL...W` / `OL...M` strings.
- Series metadata comes from Wikidata; ordinals may differ from
  Goodreads-sourced ones.
- Audiobook narrator/duration only populates if you enable the optional
  Audnex augmenter (Settings → Metadata → Audiobook augmenter).

## Known limitations

- Some pre-ISBN works can't be auto-bridged. Use manual lookup.
- Open Library has weaker coverage for academic textbooks and self-pub.
- Goodreads ListImport sources are gone; replace with OL Subject /
  OL Trending / OL Author import lists.

## Rollback

Restore the backup and reinstall original Readarr from
`https://github.com/Readarr/Readarr/releases/tag/v0.4.18.2805` (note:
upstream is archived, no support available).
```

### 9.3 Release artifacts

`.github/workflows/release.yml` — same 10-RID matrix as upstream
(`Directory.Build.props:11`), plus Authenticode + macOS notarization.

### 9.4 Docker

```dockerfile
# Dockerfile (fork's own; not in upstream Readarr/Readarr)
FROM mcr.microsoft.com/dotnet/runtime:6.0-alpine AS runtime
WORKDIR /app

# Multi-stage build: bring in published Linux-musl x64 output.
COPY _output/linux-musl-x64/ ./

EXPOSE 8787
ENTRYPOINT ["dotnet", "Bookarr.dll"]
```

Multi-arch via `docker buildx`. Push to the fork's Docker Hub org.

### Exit criteria

- Tagged release `v1.0.0-beta.1` on the fork's GitHub.
- Installer + tarball + Docker image reachable and signed.
- At least one independent tester runs the migration wizard end-to-end
  without data loss.

---

## Phase 10 — Stable & modernisation (4-6 weeks)

### 10.1 .NET 8 LTS upgrade

```diff
- <TargetFramework>net6.0</TargetFramework>
+ <TargetFramework>net8.0</TargetFramework>
```

Sweep `src/Directory.Packages.props`:

```diff
- <PackageVersion Include="Microsoft.AspNetCore.SignalR.Client" Version="6.0.29" />
+ <PackageVersion Include="Microsoft.AspNetCore.SignalR.Client" Version="8.0.10" />
- <PackageVersion Include="Microsoft.Extensions.Caching.Memory" Version="6.0.1" />
+ <PackageVersion Include="Microsoft.Extensions.Caching.Memory" Version="8.0.0" />
... (all Microsoft.* 6.x → 8.x)
```

Verify the custom Dapper ORM (`src/NzbDrone.Core/Datastore/`) runs on
.NET 8. The known landmine is
`SqliteSchemaDumper`'s reflection over `System.Data.SQLite.Core.Servarr` —
test thoroughly.

### 10.2 Nullable + ImplicitUsings

```diff
  <PropertyGroup>
+   <ImplicitUsings>enable</ImplicitUsings>
+   <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    ...
  </PropertyGroup>
```

This will generate thousands of warnings as errors. Plan: ship as
multiple PRs — one project at a time, starting with `NzbDrone.Common`
(smallest, most leaf-y) and working up.

### 10.3 StyleCop on every CI leg

Remove the `EnableAnalyzers` gating in
`.github/workflows/build.yml`. Run StyleCop on Linux, Mac, Windows.

### 10.4 SBOM

```yaml
# .github/workflows/release.yml — append
- name: Generate .NET SBOM
  run: |
    dotnet tool install --global Microsoft.Sbom.DotNetTool
    sbom-tool generate -b _output -bc src -pn Bookarr -pv ${{ env.bookarrVersion }} -nsb https://bookarr.dev
- name: Generate Node SBOM
  uses: anchore/sbom-action@v0
  with:
    path: frontend
    format: spdx-json
```

### 10.5 Selenium → Playwright

```bash
# Roughly:
dotnet remove src/NzbDrone.Automation.Test/Readarr.Automation.Test.csproj \
  package Selenium.Support Selenium.WebDriver.ChromeDriver
dotnet add    src/NzbDrone.Automation.Test/Readarr.Automation.Test.csproj \
  package Microsoft.Playwright
```

Port test fixtures. The shape of "open the SPA, click a button, assert
DOM" maps cleanly.

### 10.6 React 17 → 18

```diff
- "react": "17.0.2",
- "react-dom": "17.0.2",
+ "react": "18.3.1",
+ "react-dom": "18.3.1",
```

Replace `ReactDOM.render` in `frontend/src/bootstrap.tsx`:

```diff
- import ReactDOM from 'react-dom';
- ReactDOM.render(<App store={store} history={history} />, document.getElementById('root'));
+ import { createRoot } from 'react-dom/client';
+ const root = createRoot(document.getElementById('root'));
+ root.render(<App store={store} history={history} />);
```

That's nearly the entire React 17 → 18 surface for this codebase.

### 10.7 Namespace cleanup (optional)

```bash
# global rename, single PR
find src -name "*.cs" -exec sed -i '' 's/namespace NzbDrone\./namespace Bookarr./g' {} \;
find src -name "*.cs" -exec sed -i '' 's/using NzbDrone\./using Bookarr./g' {} \;
# also update Directory.Build.props:97-99 to remove the RootNamespace rewrite
```

Run all tests. Likely produces a few thousand mechanical diffs, no
behaviour change.

### Exit criteria

- `v1.0.0` stable tag.
- All CI legs run StyleCop.
- SBOM published as a CI artifact.

---

## Phase 11 — Long-term sustainability (ongoing)

> **Retired 2026-08-03 — the first two bullets.** Expanding them into
> `docs/governance.md` produced a governance model for an organization
> that never existed: four roles for one person, an approval threshold
> no change ever met, and a recruitment countdown enforced by
> publishing a maintenance-mode notice that would have been false.
> Librarr is a single-maintainer project by choice. The document is
> deleted; the bullets are kept below as the record of where it came
> from. The quarterly writeups survived on their own merits — see
> `docs/state-of-the-fork/`.

- **Bus-factor:** ≥2 active committers with merge rights.
- **Funding:** Open Collective / GitHub Sponsors. Publish operating
  budget (CI, certs, Docker, Sentry).
- **OL partnership:** contact archive.org about traffic shape; evaluate
  bulk-data dumps from `https://openlibrary.org/developers/dumps` to cap
  live API hits.
- **Roadmap:** `docs/roadmap.md` kept current.
- **Quarterly writeups** of project state.

---

## Phase 12 — Post-1.0 backlog (catch-all)

Concrete follow-ups that are *known* and *scoped* but deliberately
deferred past the 1.0 cut. Items here have a clear shape — they're
small enough to be one or two commits, but each one is unblocked work
not blocking work, so they don't need to gate the release. Add new
entries as they come up; promote an item to the active roadmap when
it stops being optional.

### 12.1 Narrator API surface

The migration-043 + 044 work landed the `Narrators` /
`EditionNarrators` schema and wired the audnex augmenter → join
pipeline end-to-end. What it did *not* ship is a public read surface
for narrators as first-class entities. Right now the only way the
join is queried is via the lazy-loaded `Edition.NarratorList`, which
the frontend consumes as a comma-joined string in
`BookDetailsHeader.js`.

To unblock per-narrator UX (e.g. "show me every audiobook narrated
by George Guidall"):

* Add `src/Readarr.Api.V1/Narrator/NarratorController.cs` with
  `GET /api/v1/narrator/{id}` and `GET /api/v1/narrator?editionId=X`.
* Add `NarratorResource` DTO + mapper. Keep the shape minimal
  (`Id`, `Name`, `ForeignNarratorId`) until there's a UI need for
  more.
* Service backing is already in place — `INarratorService` exposes
  `GetNarratorsForEdition` and the underlying repos cover the
  lookups needed.

Acceptance: an integration test asserts `GET /narrator/{id}` returns
the expected `NarratorResource` JSON for a seeded narrator row.

### 12.2 Frontend narrator chips

Depends on 12.1. Today `BookDetailsHeader.js` renders narrators as
`{`Narrated by ${narrators}`}` against a string prop. Once the API
surface above is in place, the same component can fetch the
structured list and render each narrator as a clickable chip
(`<Link to={/narrator/${id}}>{name}</Link>`).

* Update `BookDetailsHeaderConnector.js` to pass the structured
  list rather than the joined string.
* Add a `<NarratorChip />` component under
  `frontend/src/Book/Details/`.
* PropTypes + i18n key: `Narrated by <chips>` rather than
  `Narrated by <string>`.

A narrator detail page (browsable per-narrator works list) is a
separate item — call it 12.4 if/when it gets scoped.

### 12.4 Per-narrator detail page

Landed alongside 12.1–12.3 to give the chips an actual route target.
Scope is intentionally narrow — this is the minimum thing that turns
the chip from a static label into a working hyperlink.

Backend:

* `EditionNarratorRepository.FindByNarratorId(int)` — new repo
  method, mirror of `FindByEditionId`.
* `INarratorService.GetBooksForNarrator(int narratorId)` — walks
  EditionNarrators → Editions → Books, deduped by BookId so a
  narrator on the abridged + unabridged editions of the same work
  surfaces once. Depends on `IEditionService` + `IBookService`,
  injected via constructor.
* `NarratorBookResource` (`src/Readarr.Api.V1/Narrator/`) — six
  fields: `Id`, `Title`, `TitleSlug`, `ForeignBookId`, `AuthorId`,
  `AuthorName`, `AuthorTitleSlug`. Deliberately *not* the full
  `BookResource` shape — the page only needs enough to render
  clickable rows; the existing `/api/v1/book/{slug}` endpoint covers
  the detail click-through.
* `GET /api/v1/narrator/{id}/book` — new route on `NarratorController`.
  Returns `200 OK` + `[]` for unknown ids (not `404`) so the page
  can render an empty state without special-casing the error path.

Frontend:

* `frontend/src/Narrator/NarratorDetailsPage.js` — class component
  using `createAjaxRequest` directly (no Redux store; ephemeral data,
  no shared state to coordinate). Mirrors the pattern in
  `Settings/Metadata/LowConfidenceMappings.js`.
* `/narrator/:id` route registered in `AppRoutes.js`.
* `NarratorChip.js` now wraps the chip in `<Link to={`/narrator/${id}`}>`
  when `id` is present, falling back to a plain chip otherwise so
  stale payloads or test stubs don't render a dead link.

Acceptance: integration fixture `NarratorFixture` asserts the new
endpoint returns `200 OK` + empty list for an unknown narrator id.
Unit tests in `NarratorServiceFixture` cover the dedup-across-editions
and missing-edition cases.

### 12.3 Down-migration policy decision (not a feature)

Recorded as a deliberate non-feature so future contributors don't
write a down-migration for `044_drop_editions_narrators.cs` and
think they're being helpful.

* Re-creating the dropped `Editions.Narrators` column is trivial.
* Re-derivation from the join is a single `SELECT ... GROUP_CONCAT`
  query.
* But: the audnex augmenter no longer writes the column, so a
  downgraded binary running against a re-created column would never
  refill it. The column would go immediately stale.

The migration's own header comment makes this argument; documenting
the decision here too so it survives unrelated refactors. Rollback
recipe (restore a pre-044 SQLite backup) lives in
`docs/migrating-from-readarr.md`.

---

## Critical files & references summary

| Layer | Key files |
|---|---|
| Build / CI | `azure-pipelines.yml` (→ `.github/workflows/`), `build.sh`, `test.sh`, `src/Directory.Build.props`, `src/Directory.Packages.props` |
| Identity / branding | `LICENSE.md`, `CLA.md`, `CONTRIBUTING.md`, `README.md`, `distribution/windows/setup/`, `distribution/osx/` |
| Metadata seam (Phase 2) | `src/NzbDrone.Core/MetadataSource/IProvide{Author,Book,Series,List}Info.cs`, `ISearchForNew{Author,Book,Entity}.cs` |
| Current proxy | `src/NzbDrone.Core/MetadataSource/BookInfo/BookInfoProxy.cs` |
| New proxy (Phase 3+) | `src/NzbDrone.Core/MetadataSource/OpenLibrary/` (new folder) |
| Source selector (Phase 3) | `src/NzbDrone.Core/MetadataSource/MetadataSourceFactory.cs` (new) |
| Consumers (Phase 2) | `Books/Services/{Add,Refresh}{Author,Book}Service.cs`, `ImportLists/ImportListSyncService.cs`, `MediaFiles/BookImport/Identification/CandidateService.cs`, `MediaFiles/BookImport/Manual/ManualImportService.cs` |
| Domain model | `src/NzbDrone.Core/Books/Model/{Author,AuthorMetadata,Book,Edition}.cs` |
| Datastore (migration) | `src/NzbDrone.Core/Datastore/Migration/042_book_id_mapping.cs` (new), `BasicRepository.cs`, `DbFactory.cs` |
| Health (Phase 4) | `src/NzbDrone.Core/HealthCheck/Checks/MetadataSourceConnectivityCheck.cs` (new) |
| SignalR (Phase 5) | `src/NzbDrone.SignalR/MessageHub.cs` |
| Frontend wizard (Phase 5) | `frontend/src/Settings/Metadata/MetadataSwitchWizard/` (new) |

Patterns already in the codebase that this plan reuses:

- **DryIoc auto-registration** (`NzbDrone.Host/Bootstrap.cs:90,93`,
  `WithNzbDroneRules`).
- **`HttpIndexerBase` rate-limiting** in `Indexers/`.
- **`LazyCache` pattern** in `BookInfoProxy.cs:1-65`.
- **`MediaCoverService`** for arbitrary cover URLs.
- **`ProviderFactory<TProvider, TDefinition>` & `ThingiProvider`** for
  new import list / metadata providers.
- **`Parser/Parser.cs` title-cleaning** helpers for OL search re-ranking.

---

## Verification

End-to-end program passes when:

1. **Phase 0:** `git clone {fork} && ./build.sh && yarn build` succeeds on
   clean macOS + Linux + Windows runners.
2. **Phase 2:** `dotnet test src/NzbDrone.Core.Test/ --filter
   "FullyQualifiedName~MetadataSource"` is green after the seam refactor.
3. **Phase 3:** Manual smoke: flip `Config.MetadataSource` to
   `"OpenLibrary"` on a dev install, add an author, verify
   books + editions + covers populate within 60s.
4. **Phase 4:** 24h soak (10 monitored authors, refresh every 12h, RSS
   sync every 15min) reports zero 429s and zero health-check failures.
5. **Phase 5:** Reidentify regression on a 500-book Goodreads-ID library
   produces ≥85% high-confidence matches.
6. **Phase 6:** Top-50 series via Wikidata return populated ordinals.
7. **Phase 7:** Top-100 Audible audiobooks have populated narrator +
   duration after augment.
8. **Phase 8:** CI green on all four legs (Linux unit, Linux
   integration, Mac unit, Windows unit); coverage threshold gate passes.
9. **Phase 9:** Third-party tester migrates a stock Readarr install onto
   the fork without losing data; tagged beta release published.
10. **Phase 10:** Stable `v1.0.0` tag, .NET 8 LTS, StyleCop on every CI
    leg, SBOM published.

---

## Risks & exit criteria

| Risk | Likelihood | Mitigation |
|---|---|---|
| Open Library rate-limits or blocks the fork's traffic | Medium | Negotiate with archive.org early; have bulk-data-dump fallback ready |
| Reidentify match rate <85% on real libraries | High for thin metadata libraries | Phase 5 wizard surfaces low-confidence rows for manual pick — accept some user effort |
| .NET 8 upgrade breaks the custom Dapper ORM | Low | Phase 10 only after Phase 9 ships; can roll back to .NET 6 |
| Servarr team objects to the fork's branding | Medium | Phase 0 picks a new name; no Servarr trademarks reused |
| Audnex / audiobook augmenter dies | Medium | Opt-in and degrades gracefully — no hard dependency |
| Volunteer attrition between phases | High | Phase 11 funding + bus-factor work; phases 3+ designed to be parallelisable |

**Hard stop / exit criteria.** Abandon the program if, at the end of
Phase 5, the reidentify regression rate is <50% on representative
libraries. At that point, OL coverage is too sparse for the migration
story to work and the project needs either a Hardcover-only or hybrid
approach instead — meaning a different master plan.
