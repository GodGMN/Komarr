# Contributing to Librarr

Thank you for your interest in helping build Librarr.

## Before you start

* **Read [`MASTER-PLAN.md`](MASTER-PLAN.md)** to see where the project
  is in its 12-phase revival roadmap. Pick a task that lines up with
  the current phase.
* **Read [`ARCHITECTURE.md`](ARCHITECTURE.md)** for a map of the
  codebase — namespaces, layers, the metadata seam, and the provider
  plugin model.
* **Read [`CLAUDE.md`](CLAUDE.md)** if you're working alongside an AI
  assistant; it captures the project-specific quirks (identity remap,
  dual SQLite + Postgres, etc.).

## How to contribute

1. **Open an issue first** for non-trivial changes so the design can be
   discussed before code is written. Bug fixes and small cleanups can
   skip this and go straight to a PR.
2. **Fork the repo, branch off `main`**, push your branch, open a
   pull request against `main`. (This said `develop` until 2026-08-02,
   inherited from upstream Readarr's branching model. Librarr has no
   `develop` branch and never has — `main` is the only branch on the
   remote. The first external contribution ignored this instruction
   and targeted `main`, correctly.)
3. **Run `yarn install && ./build.sh` locally** to confirm the build is
   clean before pushing. Backend tests: `./test.sh`. Frontend lint:
   `yarn lint`.

   Use the *full* `./build.sh`, not `./build.sh --backend && yarn
   build` as this file used to say. That recipe is broken in both
   directions: `--backend` opens by deleting `_output`, so it destroys
   any frontend build that preceded it, while `yarn build` writes
   `_output/UI` and never reaches the app, which serves the UI folder
   beside its own binary. Only the full build's packaging step copies
   it across. Getting this wrong produces a tree that boots and serves
   a stale or missing UI, which is a confusing way to lose an
   afternoon.
4. **Follow the existing code style.** StyleCop is enforced on the
   Linux CI leg; running `dotnet build` locally will surface most style
   violations. Frontend code goes through ESLint + Stylelint.
5. **Keep commits focused.** One logical change per commit; squash
   "fixup" commits before requesting review.

## What happens to your PR

Most PRs are merged normally. Some are landed by **cherry-pick onto
current `main`** instead — when the branch has drifted, when the fix
needs a regression test written alongside it, or when the commit
message needs to carry findings the PR discussion surfaced. GitHub
cannot detect a cherry-pick, so in that case **your PR is closed
rather than shown as merged**, which looks like a rejection and isn't.

When that happens you should expect all three of:

* the commit message naming you and linking the PR,
* a `CHANGELOG.md` entry crediting you, and
* a comment on the PR saying it was landed by cherry-pick, before it
  is closed.

If a PR of yours was closed without those, that's a mistake on our
side — say so on the PR and it will be fixed. This has happened once
so far: [#3](https://github.com/Rorqualx/Librarr/pull/3) by
[@KevlarD-67](https://github.com/KevlarD-67), a genuine fix for a
`CredentialCache` race that made Calibre scans report an empty
library, landed as commit `401f8ba` with a regression test.

## Licensing

Contributions are accepted under GPL v3 (inbound = outbound). See
[`CLA.md`](CLA.md) — there is no CLA to sign.

## Documentation

If your change touches public behavior — config keys, API surface,
provider plugin contracts, the metadata source seam — update the
relevant doc:

* `ARCHITECTURE.md` for cross-cutting structure changes.
* `MASTER-PLAN.md` to amend the phase plan.
* Per-directory `README.md` files for narrower changes.

## Getting help

* GitHub Discussions on this repo for design questions.
* GitHub Issues for bug reports and feature requests.
* Wiki articles from upstream Readarr at
  <https://wiki.servarr.com/readarr> are still mostly accurate for
  install / operational topics, but treat the metadata-source guidance
  as obsolete.
