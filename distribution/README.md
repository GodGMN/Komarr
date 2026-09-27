# distribution/ — Packaging

Installer assets for shipping Librarr to end users.

## Subfolders

- **`windows/`** — InnoSetup installer assets and Windows service helpers.
  CI invokes InnoSetup 6.2.0 (`../azure-pipelines.yml:20`) to build the
  signed `.exe`. The Authenticode signing happens in the dedicated CI `Sign`
  stage.
- **`osx/`** — macOS `.app` bundle template and DMG creation scripts. Builds
  produce both Intel (`osx-x64`) and Apple Silicon (`osx-arm64`) variants.

## Other platforms

Linux is shipped as `.tar.gz` archives per RID by the .NET publish step —
there is no `.deb`/`.rpm` packaging in this repo. FreeBSD is published via
the same archive path after the SDK patch in
`../azure-pipelines.yml:102-111` enables `freebsd-x64`.

## Build order

1. `../build.sh --packages` → produces per-RID `_artifacts/` archives.
2. CI's `Package` stage runs InnoSetup / DMG creation for the OS-specific
   installers.
3. CI's `Sign` stage signs the Windows installer. Linux archives are **not**
   signed.
