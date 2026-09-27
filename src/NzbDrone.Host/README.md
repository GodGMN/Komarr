# NzbDrone.Host/ — bootstrap & DI (Readarr.Host.csproj)

Hosts the ASP.NET Core pipeline and wires the DryIoc container.

## Entry points

- `Bootstrap.cs` — picks the application mode (Service / Interactive /
  Utility / Help / RegisterUrl / InstallService / UninstallService) and starts
  the appropriate host. The DI container is created with
  `WithNzbDroneRules()` and `AutoAddServices` scans the assembly list
  `Bootstrap.ASSEMBLIES` (`Bootstrap.cs:37-44`).
- `Startup.cs` — the ASP.NET Core `Startup` class. Configures Kestrel,
  routing, middleware (auth, error handling), MVC, SignalR.

## Process modes

`Bootstrap.GetApplicationMode` (`Bootstrap.cs:186-227`):

| Mode              | When                                                       |
|-------------------|------------------------------------------------------------|
| `Help`            | `--help` flag                                              |
| `RegisterUrl`     | `--registerurl` (Windows only)                             |
| `InstallService`  | `--install-service` (Windows only)                         |
| `UninstallService`| `--uninstall-service` (Windows only)                       |
| `Service`         | Detected to be running as a Windows service                |
| `Interactive`     | Default — tray or console                                  |

## Network defaults

- HTTP port `8787`, HTTPS port `6868` (`Bootstrap.cs:135-136`).
- TLS cert path / password read from XML config; SSL is opt-in.
- `Kestrel.AllowSynchronousIO = false` (`Bootstrap.cs:179`).

## Configuration sources (merged in order)

1. `config.xml` (`AppFolderInfo.GetConfigPath`).
2. In-memory keys (e.g., `dataProtectionFolder`).
3. Environment variables (e.g., `Readarr__Postgres__Host`).

## Dependencies

DryIoc 5.4.3 + `DryIoc.Microsoft.DependencyInjection 6.2.0`,
`Microsoft.AspNetCore.*` 6.x, NLog, Npgsql 7.0.7.
