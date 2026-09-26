# distribution/docker — Librarr docker images

Docker build definitions for Librarr. Images are published automatically
by [`.github/workflows/release.yml`](../../.github/workflows/release.yml)
on every `v*` tag push, as a multi-arch manifest covering
`linux/amd64`, `linux/arm64` and `linux/arm/v7`:

```bash
docker pull ghcr.io/rorqualx/librarr:latest    # GitHub Container Registry
docker pull rorqualx/librarr:latest            # Docker Hub
```

Tags: `:<version>` exact, `:<major>.<minor>`, `:beta` on beta tags, and
`:latest` only on non-prerelease tags. Docker will select the right
architecture for your host automatically.

You can also build locally from this directory — see **Build** below.

## Files

| File | Purpose |
|---|---|
| `Dockerfile` | Self-contained multi-stage build: compiles backend (`dotnet publish`) + frontend (`yarn build`) inside the image, then assembles a small runtime layer on `aspnet:10.0-alpine`. No local toolchain required. |
| `Dockerfile.prebuilt` | Thin packaging layer for when you already ran `./build.sh` and `yarn build` locally — copies `_output/` straight into the runtime image. Fast iteration, but requires the host toolchain. |

## Build

From the repo root — builds for the host's architecture:

```bash
docker build \
  -f distribution/docker/Dockerfile \
  -t librarr/librarr:1.0.0-beta \
  .
```

### Multi-arch

Supported platforms: **`linux/amd64`**, **`linux/arm64`**,
**`linux/arm/v7`**. The Dockerfile maps BuildKit's `TARGETARCH` to the
matching musl RID itself, so nothing needs to be passed per-arch:

| Platform | .NET RID |
|---|---|
| `linux/amd64` | `linux-musl-x64` |
| `linux/arm64` | `linux-musl-arm64` |
| `linux/arm/v7` | `linux-musl-arm` |

```bash
docker buildx create --use --name librarr   # once
docker buildx build \
  -f distribution/docker/Dockerfile \
  --platform linux/amd64,linux/arm64,linux/arm/v7 \
  -t librarr/librarr:1.0.0-beta \
  --push \
  .
```

A multi-platform build cannot load into the local daemon — use `--push`
to a registry, or build one platform at a time with `--load`.

Both build stages are pinned to `$BUILDPLATFORM` and **cross-compile**:
`dotnet publish -r <rid> --self-contained false` only needs the target's
apphost from the runtime pack, and the webpack bundle is
arch-independent. This is deliberate — the .NET SDK under QEMU is both
drastically slower and prone to Roslyn segfaults on x86_64-on-arm64.
QEMU is still required (the runtime stage's `apk add` runs on the
target), hence `docker/setup-qemu-action` in `release.yml`.

`--build-arg DOTNET_RID=...` still overrides the mapping for a
single-platform build — e.g. to target a glibc base (`linux-x64`,
`linux-arm64`) after swapping the runtime image. `win-arm64` is **not**
in the RID list and is unsupported (`src/Directory.Build.props:11`).

`Dockerfile.prebuilt` accepts the same `--platform` set, but only for
RIDs you have already built into `_output/net10.0/` — a missing RID fails
that platform's build with an explicit error rather than silently
shipping the wrong binaries. Populate it with:

```bash
READARRVERSION=1.0.0-beta.4 ./build.sh --backend --frontend
```

`READARRVERSION` matters: the prebuilt variant compiles nothing, so it
cannot stamp a version the way `Dockerfile` does. Without it the binaries
keep the `10.0.0.*` placeholder, and since
`RuntimeInfo.InternalIsOfficialBuild()` rejects `Major >= 10` the image
runs with `IsProduction == false` — changing Sentry DSN selection,
analytics and `initialize.json` caching.

Note `build.sh` begins with `rm -rf _output`, and passing `-r` a
semicolon-separated RID list does not work (MSBuild reads `;` as a
property separator, and `RuntimeIdentifiers` is consumed as singular
downstream). Build the full RID set, or one RID per run.

## Run

Minimum invocation:

```bash
docker run -d \
  --name librarr \
  --restart unless-stopped \
  -e PUID=1000 -e PGID=1000 -e TZ=Etc/UTC -e UMASK_SET=002 \
  -p 8787:8787 \
  -v /path/to/librarr/config:/config \
  -v /path/to/your/library:/books \
  -v /path/to/downloads/completed:/downloads/completed \
  librarr/librarr:1.0.0-beta
```

Volume layout the container expects:

| Mount | Purpose |
|---|---|
| `/config` | Persistent app data — `config.xml`, `librarr.db`, `Logs/`. |
| `/books` (or whatever you choose) | Your library root. Configure the root folder inside Librarr's UI to match. |
| `/downloads/completed` | Where your download client(s) drop completed grabs. Configure Remote Path Mappings if the path-as-seen-by-the-download-client differs from the path-as-seen-by-Librarr. |

Default ports: **8787** HTTP, **6868** HTTPS
(`src/NzbDrone.Host/Bootstrap.cs:135-136`). Override via `config.xml`
(`Port`, `SslPort`, `BindAddress`); `config.xml` reload-on-change is
disabled (`Bootstrap.cs:237`), so changes require a restart.

## Migrating from Readarr

Point `/config` at a copy of your old Readarr `config/` directory and
start the container. The first-boot `LegacyMigrationService` handles
the rest — see ["Migrating from Readarr"](../../README.md#migrating-from-readarr).
A persisted marker in `config.xml` (`LegacyMigrationCompleted`)
prevents re-runs on subsequent restarts.
