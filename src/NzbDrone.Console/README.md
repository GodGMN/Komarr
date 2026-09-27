# NzbDrone.Console/ — console-mode entry (Readarr.Console.csproj)

Companion to `NzbDrone/` that runs Readarr from a non-Windows terminal /
without the tray. Cross-platform alternative to the WinForms entry.

## Role

- Output type: `Exe` (per `../Directory.Build.props:21`).
- Hosts the same `NzbDrone.Host.Bootstrap` pipeline as the tray exe, minus
  the tray callback.

## Use case

This is the binary launched on Linux / Mac and inside Docker images. On
Windows it can be used to run the app in a console window for debugging.
