# Contributing to Komarr

Komarr is an early manga-focused fork with inherited *arr infrastructure. Read the [principles](README.md#principles), [PRD](docs/PRD.md), and [getting-started guide](docs/getting-started.md) before changing behavior. Keep acquisition decisions deterministic and explain why a release was accepted, rejected, or sent to review. Do not add cloud dependencies for local collection management or change torrent payloads in ways that disrupt seeding.

Open a GitHub issue for a bug or feature, or use the manga release-name template for parser problems. Never post credentials, private tracker URLs, backup ZIPs, or downloaded content. For parser changes, follow the [fixture contribution guide](docs/contributing-fixtures.md).

## Build and checks

```bash
docker build -f distribution/docker/Dockerfile -t komarr:local .
./scripts/smoke-docker.sh komarr:local --browser
python3 scripts/check_manga_fixtures.py
python3 -m unittest discover -s tests/Parser -p 'test_*.py'
```

The Docker smoke script checks a fresh install, settings persistence, browser navigation, Basic authentication, and API key access. The CI workflow also builds the .NET solution and runs focused manga tests. If you change parsing, decisions, import paths, or backup restore, add a regression test that shows the real failure mode.

Describe user-visible changes and verification in the PR. Point out database migrations, filesystem behavior, download-client compatibility, and uncertain matches. Screenshots help with UI changes.

Contributions use the project's [GPL v3 license](LICENSE). There is [no CLA to sign](CLA.md).
