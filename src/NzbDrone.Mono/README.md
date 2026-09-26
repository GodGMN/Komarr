# NzbDrone.Mono/ — Linux/macOS platform shim (Readarr.Mono.csproj)

Platform-specific implementations of cross-cutting abstractions for Mono /
Linux / macOS.

## Responsibility

Pairs with `NzbDrone.Windows/`. Selected at runtime by the DI registration
based on `OsInfo.IsWindows`. Implements:

- POSIX signal handling (SIGTERM / SIGINT for clean shutdown).
- Service hosting on systemd / launchd.
- File-permission helpers (chmod / chown) used during install moves.
- Hard-link detection for safe atomic file replacement.

## Dependencies

`Mono.Posix.NETStandard 5.20.1.34-servarr22` — a Servarr fork of the
upstream Posix package. Pinned for ABI stability across the Linux RIDs.

## Tests

See `../NzbDrone.Mono.Test/` (same pattern: NUnit + Moq + Test.Common).
