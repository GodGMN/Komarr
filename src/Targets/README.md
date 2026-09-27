# Targets/ — custom MSBuild targets

Auxiliary MSBuild logic imported by the .NET build.

## Files

- `PublishAllRids.targets` — defines the full Runtime Identifier matrix and
  orchestrates multi-RID publishing. The canonical RID list also appears
  in `../Directory.Build.props:11`; this targets file is what the build
  script uses when iterating.

## Adding a RID

1. Add the RID to `../Directory.Build.props:11`.
2. Add a matching entry in `PublishAllRids.targets`.
3. Confirm CI's `build_backend` matrix can publish for it.
4. Add packaging support under `../../distribution/{os}/` if needed.

> Note: `win-arm64` is intentionally NOT in the RID list — there is no
> Windows-on-ARM build yet.

## Caveat

`../../azure-pipelines.yml:102-111` patches the .NET SDK's
`Microsoft.NETCoreSdk.BundledVersions.props` file at runtime to add
`freebsd-x64` and `linux-x86` to the bundled RIDs — those two are not in
the base SDK list.
