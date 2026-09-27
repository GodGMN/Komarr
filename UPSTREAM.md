# Upstream provenance

Komarr imports source from [Rorqualx/Librarr](https://github.com/Rorqualx/Librarr), release tag `v1.2.2-beta`.

- Resolved upstream commit: `4cbee9df11489ff469a8b1d99cb75c73008e75a4`.
- The source was merged into Komarr with unrelated histories, retaining all 235 upstream commits. The original upstream tag is stored locally as `librarr-v1.2.2-beta`; the commit SHA is the durable reference.
- Komarr's README, PRD, GPLv3 `LICENSE`, and local-secret ignore rules were retained. The upstream README is preserved at [`docs/upstream/LIBRARR_README.md`](docs/upstream/LIBRARR_README.md).
- Upstream `LICENSE.md`, `CLA.md`, `CODE_OF_CONDUCT.md`, `CONTRIBUTING.md`, source-level notices, and history remain in the imported tree. Librarr descends from Readarr and Sonarr; their existing notices and license obligations remain applicable.
- This import has not renamed the running application or replaced the book domain. Those changes are separate MVP tasks.
- The inherited lock-threads and live OpenLibrary integration workflows are manual-only until Komarr's CI is adapted.

To inspect the exact imported source later, run `git show 4cbee9df11489ff469a8b1d99cb75c73008e75a4:<path>` or compare that commit with the Komarr merge commit.

## Import build check

The inherited application source in `src/` and `frontend/` matches the pinned upstream commit. The following checks passed before domain changes:

- Node 24.13.0 and Yarn 1.22.22: `corepack yarn install --frozen-lockfile --network-timeout 120000`, then `corepack yarn build --env production`.
- A fresh full clone with .NET SDK 10.0.401: `dotnet build src/Readarr.sln -c Release -p:Platform=Posix -v quiet`. It finished with zero errors and 180 warnings, including inherited NuGet vulnerability advisories.

The local import checkout uses Git's partial-clone repository format 1, which the inherited SourceLink 1.1.1 package cannot read. The backend check therefore used a fresh full clone from the pushed Komarr branch, whose repository format is 0.
