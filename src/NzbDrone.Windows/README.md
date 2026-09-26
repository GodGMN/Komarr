# NzbDrone.Windows/ — Windows platform shim (Readarr.Windows.csproj)

Platform-specific implementations selected on Windows. Counterpart to
`NzbDrone.Mono/`.

## Responsibility

- Windows-service hosting integration.
- Windows Firewall rule registration via the COM API (uses the vendored
  `../Libraries/Interop.NetFwTypeLib.dll`).
- Windows process privilege / registry helpers.
- Hard-link and reparse-point handling specific to NTFS.

## Why a separate project?

The Servarr family separates Windows-only code so the Linux/macOS build
doesn't drag in `Microsoft.Win32.Registry` or the firewall interop assembly.
At DI registration time, only one of `NzbDrone.Windows` or `NzbDrone.Mono` is
active based on `OsInfo.IsWindows`.

## Dependencies

`Microsoft.Win32.Registry 5.0.0`,
`System.Security.Principal.Windows 5.0.0`,
`System.IO.FileSystem.AccessControl 5.0.0`,
`System.ServiceProcess.ServiceController 6.0.1`.

## Tests

See `../NzbDrone.Windows.Test/` — only meaningful when running on Windows.
