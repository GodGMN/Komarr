# src/ — .NET solution

The whole backend. See [`../ARCHITECTURE.md`](../ARCHITECTURE.md) for the full
project map.

## Solution layout

29 projects in `Readarr.sln`:

- **15 production projects** (`NzbDrone`, `NzbDrone.Common`, `NzbDrone.Console`,
  `NzbDrone.Core`, `NzbDrone.Host`, `NzbDrone.SignalR`, `NzbDrone.Mono`,
  `NzbDrone.Windows`, `NzbDrone.Update`, `Readarr.Api.V1`, `Readarr.Http`,
  `ServiceHelpers/ServiceInstall`, `ServiceHelpers/ServiceUninstall`).
- **11 test projects** named `*.Test` (NUnit + Moq + FluentAssertions).
- **2 helpers**: `NzbDrone.Test.Common` (test-fixture library, not a runner)
  and `NzbDrone.Test.Dummy` (tiny assembly used by tests).
- **`Libraries/`** — vendored binary deps. **`Targets/`** — custom MSBuild.

> **Identity quirk.** The csproj/assembly names are `Readarr.*` but the C#
> namespaces are still `NzbDrone.*`. The remap is set in
> `Directory.Build.props:97-99`. So `using Readarr.Core;` won't compile —
> `using NzbDrone.Core;` will.

## Central build config

- `Directory.Build.props` — applied to every project. `TreatWarningsAsErrors=true`,
  `EnforceCodeStyleInBuild=true`, central package management on, 10 RIDs.
- `Directory.Packages.props` — single source of truth for NuGet versions
  (CPM with `<PackageVersion>` entries).
- `stylecop.json` + `Stylecop.ruleset` — StyleCop settings + rule actions.
  Note: `Stylecop.ruleset:1` still calls itself "Rules for Radarr".
- `coverlet.runsettings` — coverage collector config.
- `postgres.runsettings` — Postgres-mode test connection.

## Build & test

```
./build.sh --backend              # full backend build (uses 10-RID matrix)
./test.sh                         # dotnet test wrapper
dotnet test src/NzbDrone.Core.Test/  # one project
```

CI uses the same scripts (`../azure-pipelines.yml`). Note that StyleCop
analysis is **only enabled on the Linux CI leg** (`enableAnalysis: 'true'`);
Windows/Mac legs skip it.
