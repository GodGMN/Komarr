# NzbDrone.Common/ — utilities (Readarr.Common.csproj)

The lowest production layer. **No business logic** — only cross-cutting
helpers used by every other backend project.

## Notable folders

- `Cache/` — in-memory caching primitives (built on `LazyCache 2.4.0`).
- `Composition/` — DryIoc rule extensions, `WithNzbDroneRules()`,
  `AutoAddServices` discovery helpers.
- `Disk/` — disk-IO abstractions (`IDiskProvider`) and the in-memory test
  double.
- `EnvironmentInfo/` — `IAppFolderInfo`, OS detection, `AppFolderInfo`.
- `Exceptions/` — base exception types (`ReadarrStartupException` etc.).
- `Extensions/` — string/path/enumerable extension methods.
- `Http/` — `IHttpClient`, request/response abstractions, cookie & rate-limit
  helpers (built on RestSharp 106).
- `Instrumentation/` — NLog targets (`DatabaseTarget`,
  `SlowRunningAsyncTargetWrapper`), runtime reconfig
  (`ReconfigureLogging`), Sentry hooks.
- `Processes/` — `IProcessProvider` wrappers for OS process spawning.
- `Serializer/` — Newtonsoft.Json + System.Text.Json helpers.
- `TPL/` — task/parallelism helpers.

## Dependencies

NLog, Newtonsoft.Json, RestSharp 106 (legacy major), Polly, Sentry,
System.IO.Abstractions.

## Conventions

- Pure utility — never reference `NzbDrone.Core`.
- Interfaces live next to implementations (`IDiskProvider` /
  `DiskProvider`).
- Heavy use of `[Mockable]` for test-double generation.
