# NzbDrone/ — main entry exe (Readarr.csproj)

Builds the **main Readarr executable** for Windows (and the tray
integration). The csproj is named `Readarr.csproj` but the folder retains
`NzbDrone` for historical reasons.

## Role

- Provides the tray application / Windows entry that calls
  `NzbDrone.Host.Bootstrap.Start(args, trayCallback)`.
- Output type: `Exe` (set conditionally in `../Directory.Build.props:20`).

## Key references

References `Readarr.Host`, `Readarr.Core`, `Readarr.Common`, `Readarr.Windows`.

## Process modes

`Bootstrap.GetApplicationMode` picks one of:

- `Service` — Windows service.
- `Interactive` — tray or console.
- `Help`, `RegisterUrl`, `InstallService`, `UninstallService` — utility modes.

See `../NzbDrone.Host/Bootstrap.cs:186-227` for the selection logic.
