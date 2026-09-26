# NzbDrone.Update/ — self-updater (Readarr.Update.csproj)

Separate executable that performs in-place updates of the main Readarr
binary. Lives in `_output/Readarr.Update/` (see `../Directory.Build.props:52`).

## Why a separate exe?

Self-updating an executable that is currently running is not portable. The
main process unpacks the new release, then spawns `Readarr.Update.exe`,
which:

1. Waits for the main process to exit.
2. Copies the new binaries into the install folder.
3. Restarts the main process.

## Output

`OutputPath` is set to `_output/Readarr.Update/` so the update package can
include this binary alongside the main app.

## Related

- `NzbDrone.Core/Update/` — orchestration: download release, verify hash,
  spawn this updater.
- `NzbDrone.Common/Processes/` — process-spawn helpers.
