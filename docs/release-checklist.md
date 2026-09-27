# Release checklist

Defines what each Librarr release tag actually requires. Two
sequential gates — beta first, then stable — plus an explicit
deferred-to-1.1+ list so a release decision doesn't accidentally
fall over an unmade tradeoff.

Cross-references:

- Everything still open, tiered in one table:
  [`roadmap.md`](roadmap.md#open-work-at-a-glance)
- Deferral rationale: [`deferred-modernization.md`](deferred-modernization.md)
- OL bulk-data fork position + revisit triggers: [`ol-bulk-data.md`](ol-bulk-data.md)
- Quarterly writeups: [`state-of-the-fork/`](state-of-the-fork/README.md)
- Phase-by-phase delivery log: [`../MASTER-PLAN.md`](../MASTER-PLAN.md)

---

## v1.0.0-beta

The first publishable artefact. Goal: an artefact someone can
install and exercise end-to-end against the Open Library backend
that Phases 2–5 wired up.

### Engineering

- [x] `./test.sh Mac Unit Test` green on local Linux/macOS — verified
  during the finalization pass (3359 passed, 123 skipped, 0 failed
  across nine assemblies; `TEST_DIR=_tests/net10.0` env required, see
  test.sh for the path-resolution quirk).
- [x] `dotnet build src/Readarr.sln -c Debug` clean (0 errors, 0
  warnings).
- [x] `yarn lint` + `yarn stylelint-linux` clean.
- [x] `tests/e2e/smoke.sh` runs in CI as the blocking `e2e-smoke`
  job (`.github/workflows/build.yml:209-242`). Boots the published
  `linux-x64` backend artefact, asserts `/api/v1/health` +
  `/api/v1/system/status`, fires a `ReidentifyLibrary` command.
- [x] Playwright smoke suite, **16 tests** across six fixtures
  (`src/NzbDrone.Playwright.Test/`): page-load smokes (the Phase 10
  port plus the narrator-detail page), root folder form checks, Add
  Author search checks, book-detail checks and seeded library
  round-trips. 10 run without network; 6 are gated on live
  OpenLibrary. Gated by `READARR_RUN_PLAYWRIGHT=1`; opt-in per the
  suite's README, and run per-push in `build.yml`.

  It could not run at all between the .NET 10 migration and
  `5447db9` — a stale driver in the shared `_tests/` output made
  every test die in `CreateAsync`, and Playwright 1.40's Chromium
  crashed on current macOS about one run in three. The gate now
  checks driver against client up front.

  Then, once it ran, it was running against an app tree built before
  the features it tests existed — the recipe below was ordered so that
  the backend build deleted the frontend build. That is what made the
  first genuinely-green run of this suite 2026-08-01, not 2026-07-30:
  the earlier "green" runs served a pre-migration frontend, so the
  React 18.3 `defaultProps` work had never actually been exercised in a
  browser, and neither had the audiobook-profile or work-count UI.
  `NzbDroneRunner` now fails rather than boot a stale tree.
- [x] `docker build -f distribution/docker/Dockerfile -t librarr/librarr:test .`
  builds clean. `docker run` boots,
  serves `/ping`, passes `tests/e2e/smoke.sh`. Eight Phase 9b
  skeleton bugs were caught and fixed in the realisation pass —
  see commit history for the per-bug breakdown.

  Runtime base is `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` since
  the .NET 10 migration. This line used to quote "~180 MB alpine 6.0
  runtime"; that size was measured on the 6.0 base and has not been
  re-measured, so it is dropped rather than carried forward.
- [x] Fresh-install metadata source defaults to OpenLibrary. The
  legacy `BookInfo` default pointed at `api.bookinfo.club` (retired
  upstream 2025-06-27); flipped in `ConfigService.cs:278` so fresh
  installs don't land on a non-functional search. Existing installs
  that explicitly set `MetadataSourceType=BookInfo` migrate via the
  Phase 5 reidentify wizard.
- [ ] **Manual operator walkthrough** (no automated coverage —
  needs a seeded library; partially verified in the realisation
  pass via the smoke container):
  1. [x] Navigate to `/narrator/999999`, confirm "Narrator not
     found." error state. (Verified live against
     `librarr/librarr:test` container.)
  2. [x] Author lookup hits OpenLibrary (Phase 3 proxy), returns
     `foreignAuthorId` like `OL1394865A`. (Verified via
     `/api/v1/author/lookup?term=Brandon%20Sanderson`.)
  3. [ ] Add an author end-to-end (`/add/new`), refresh metadata,
     confirm books + editions + covers populate within 60 s.
  4. [ ] Open a book detail page; confirm "Narrated by …" chips
     render when audiobook editions exist.
  5. [ ] Click a narrator chip; land on `/narrator/:id`; confirm
     book list renders with author links back to `/author/:slug`.

### Docs

- [x] Roadmap `[~]` items reconciled to `[x]` (cassettes + reidentify
  regression — both were already landed; the roadmap had not been
  updated post-Phase 5).
- [x] React 17 → 18 "Later" entry split into core ✅ (Phase 10) +
  ecosystem deps ❌ (deferred).
- [x] Deferred-modernization dispositions confirmed per the table
  in this checklist's "Deferred-to-1.1+" section below.
- [x] CHANGELOG entry covering Phases 0–12. Shipped as two
  Keep-a-Changelog releases rather than the per-phase one-liners
  originally sketched here — `## [1.0.0-beta] — 2026-05-19` covers
  Phases 0–12, `## [1.1.0-beta] — 2026-07-30` the Library Import
  wizard and the first ARM images, and `## [1.2.0-beta] — 2026-08-03`
  the .NET 10 move and what followed it. Grouping by release rather
  than by internal phase number is the more useful shape for a reader
  who was never inside the plan.

  This line used to credit the .NET 10 move to 1.1.0-beta. It is not
  in that tag: `v1.1.0-beta` was cut at 12:59 on 2026-07-30 and the
  migration landed at 16:46, so the 1.2.x line is the first .NET 10
  build the project ever shipped. Worth stating plainly, because a
  release everyone believed was proven on .NET 10 was nothing of the
  kind, and the self-contained regression below is what that bought.

### Out-of-engineering (manual user-action)

These cannot be executed from an LLM session; they are gates the
human maintainer must pass through before tagging the beta. **All of
them have since been passed** — the boxes below went unticked for a
while after the fact, which is how this section came to read as
blocking work when nothing in it was.

- [x] Fork remote configured. `origin` is
  `https://github.com/Rorqualx/Librarr.git`; it no longer points at
  the archived upstream.
- [x] Release secrets configured for `release.yml`. Confirmed by the
  workflow having run to completion on tag push — the GHCR and Docker
  Hub login steps are unconditional and fail loudly on a missing
  secret, so a successful release run is itself the evidence. The
  Windows signing secrets are the deliberate exception: optional, and
  absent by design unless a certificate is configured (see below).
- [x] **Decided: the Windows build is signable but unsigned by
  default.** Retained below because the decision is a live one for
  any fork — the mechanism is opt-in, not removed. The reasoning that
  produced the decision, kept verbatim:

  With no certificate the pipeline still produces the zips and the
  installers, just unsigned, and `distribution/windows/sign.ps1`
  says so and exits 0. Unsigned means every user meets a SmartScreen
  "unrecognised app" warning on first run, and a fresh certificate
  earns reputation slowly, so signing only stops being painful some
  weeks after the first signed release. To turn it on, add two
  repository secrets:
  - `WINDOWS_CERT_PFX` — the code-signing certificate as a base64
    PKCS#12 blob: `base64 -w0 cert.pfx` (macOS: `base64 -i cert.pfx`).
  - `WINDOWS_CERT_PASSWORD` — that .pfx's password.

  An EV certificate on a hardware token cannot be used this way; it
  needs a signing service the runner can call instead. Once both
  secrets exist nothing else changes — the `build-windows` and
  `installer` jobs pick them up, and the draft release notes switch
  from the SmartScreen warning to stating the build is signed.
- [x] `v1.0.0-beta` tag pushed, then `v1.1.0-beta`, then
  `v1.2.0-beta` and `v1.2.1-beta` (both 2026-08-03 — the first tags
  to carry Windows installers, and the first .NET 10 builds).

  **`v1.2.0-beta`'s downloads were never published.** Its artifacts
  came out framework-dependent, because .NET 7 had dropped the rule
  that a RuntimeIdentifier implies a self-contained build and nothing
  here set the property explicitly. The draft was deleted unpublished
  at zero downloads and superseded by `v1.2.1-beta`; its Docker images
  were unaffected and remain published. The release pipeline now
  refuses to package a build whose publish output has no host CLR in
  it, so a future release cannot repeat this quietly.

  All four tags exist; the release pipeline
  (`.github/workflows/release.yml`) fires on `v*`:
  ```bash
  git tag -a v1.0.0-beta -m "Librarr 1.0.0 beta"
  git push origin v1.0.0-beta
  ```
  Pipeline produces multi-RID binary tarballs, the two Windows
  installers, and a Docker build that now genuinely pushes
  (`release.yml:530` is `push: true` — the `push: false` this line
  used to describe was the Phase 9b placeholder). A draft GitHub
  release is created automatically; **publishing it is manual**, and
  until you do, nobody without push access can see it.

  If you re-push a tag to re-run the pipeline, the job now deletes any
  existing *draft* for that tag before creating the new one, and stops
  outright if the tag has already been published. It did neither
  before 2026-08-03: every re-run left its predecessor behind as an
  invisible orphan holding a full ~880 MB asset set, which is how
  `v1.0.0-beta` ended up with three releases — one published and two
  drafts, the later of which was created 22 minutes *after* the
  publish. Both drafts were deleted 2026-08-03; neither had ever been
  downloaded.

---

## v1.0.0-stable

Promotes the beta to stable. Not actionable until the beta artefact
has been in real use and the remaining engineering coverage gaps are
closed. It used to be gated on governance commitments as well; those
were retired 2026-08-03 along with the document that invented them.

### Engineering

- [ ] Beta has been in the wild for at least 30 days with no
  critical regressions reported in `docs/state-of-the-fork/`.
- [ ] **Integration suite runnable unattended.** Partially addressed:
  since 2026-08-02 `nightly-integration.yml` runs the 94 tests one
  fixture per invocation with a pause between them, so they are no
  longer running nowhere. That is a workaround, not the fix — the
  scheduled job is the only place they can run, and a push-time
  pipeline still cannot execute them. The original diagnosis, still
  accurate:

  The fixtures work —
  the Goodreads identifiers are gone, `EnsureAuthor` resolves an
  OpenLibrary author id, and the six fixtures that were marked
  "Waiting for metadata to be back again" each pass against live
  OpenLibrary. What does *not* work is running them in one pass:
  every fixture starts from an empty appdata, so nothing is cached
  between them, and one full run of `ApiTests` was enough to have
  the source IP refused (`Connection refused (openlibrary.org:443)`
  on 26 of 88 tests). Until that is solved they have to be run one
  fixture at a time, which is not something CI can do usefully.

  Two plausible routes, neither attempted: share one appdata (and
  therefore one warm cache) across the assembly the way
  `NzbDrone.Playwright.Test` shares its instance, or record the
  OpenLibrary responses as cassettes — `OpenLibraryCassetteFixture`
  in the unit suite already has the machinery.
- [x] **Playwright chip → page round-trip test.** Done via the
  API-based seed (`LibrarySeeder`), not a checked-in SQLite file:
  a captured database pins the schema at whatever migration count
  it was taken on and rots the next time somebody adds one of the
  48. `SeededLibraryTest` adds one small author, then clicks
  library → author and asserts on the book rows themselves, so it
  cannot pass on an empty table. Verified it fails with seeding
  disabled.

  The seed is the suite's only OpenLibrary dependency, so it
  reports Inconclusive rather than red when the network is
  missing or throttling.
- [ ] **Cross-browser Playwright.** Firefox + WebKit launchers
  are Playwright one-liners; wire when needed for theme/CSS
  regression coverage.
- [ ] **500-book reidentify regression seed.** The current 10-book
  in-memory seed is sufficient to assert the 0.85 threshold; a
  larger snapshot makes the assertion statistically meaningful.
  Lives under `tests/regression/`; capture via
  `sqlite3 readarr.db .dump` from a populated install.

### Project state

This section used to gate the stable tag on two governance
commitments: a second maintainer, and a quarterly writeup. The first
is gone — `docs/governance.md` was deleted 2026-08-03 as a description
of an organization that never existed, and a release should not be
blocked on whether a stranger volunteers. Librarr is a
single-maintainer project and that is not a defect to be cleared
before 1.0.

- [x] First state-of-the-fork writeup published:
  [`2026-Q2.md`](state-of-the-fork/2026-Q2.md), 2026-08-02. It was due
  2026-07-14 and shipped 19 days late; the post says so in its own
  opening rather than backdating itself. Q2 is the first quarter owed
  — Q1 2026 predates the fork's first commit (2026-05-16) — so no
  earlier writeup is missing.

### Out-of-engineering

- [x] Docker registry login + `push: true`. Both registries were
  chosen rather than one: `release.yml:479-486` logs in to GHCR with
  the built-in `GITHUB_TOKEN`, `:495-498` logs in to Docker Hub with
  `DOCKERHUB_USERNAME` + `DOCKERHUB_TOKEN`, and `:530` is `push: true`.
  The Docker Hub step is deliberately unconditional — an
  `if: secrets.X != ''` gate broke workflow parsing, so a missing
  secret fails the step loudly instead of silently skipping the push.

---

## Deferred-to-1.1+ (do NOT block v1.0)

Each item has a written disposition; future contributors should
not "fix" any of these in service of shipping 1.0.

| Item | Disposition | Reference |
|---|---|---|
| ~~.NET 8 LTS~~ | **Done, and the target was wrong.** Landed on **.NET 10 LTS** 2026-07-30 — .NET 8 and 9 both go EOL 2026-11-10, so 8 would have bought a quarter. The deferral reason stated here was also false: every Servarr-forked NuGet named in it runs on .NET 10 unchanged, and none needed replacing. Row kept struck-through so the bad reasoning stays visible. | [`deferred-modernization.md`](deferred-modernization.md) |
| Nullable enable | **Defer** as a multi-PR sprint post-1.0. Per-file gradual enable is the recommended approach — global enable with `TreatWarningsAsErrors` emits thousands of CS86xx errors. | [`deferred-modernization.md`](deferred-modernization.md) |
| react-dnd / react-virtualized / react-popper | **Defer**. React core is at 18.3.1 (Phase 10, `ae4261b`); ecosystem deps still work on 18, the replacements would be breaking diffs better done after Playwright has interaction coverage. | [`deferred-modernization.md`](deferred-modernization.md) + roadmap "Later" |
| Full Selenium → Playwright parity | **Defer beyond scaffold**, but the scaffold has grown: 16 tests across six fixtures, not the seven this row was written against. What is still open is a *decision* rather than a port — `NzbDrone.Automation.Test` remains in the tree on Selenium 3 pins, running in no job, and should be either finished or deleted. | [`deferred-modernization.md`](deferred-modernization.md) + roadmap "Later" |
| OL bulk-data dump fallback | **Defer** until any of the four trigger conditions in [`ol-bulk-data.md`](ol-bulk-data.md) fires: sustained 429s, archive.org guidance shift toward dump-based consumers, install count >100, or OL live-API availability/cost change. | [`ol-bulk-data.md`](ol-bulk-data.md) |

The "won't (until persuaded otherwise)" items from
[`roadmap.md`](roadmap.md#wont-until-persuaded-otherwise) — namespace
rename, CLA reintroduction, rreading-glasses adoption — are also
explicitly not 1.0 blockers.

---

## Verification recipe

To re-verify the engineering gates (the ones marked `[x]` above):

```bash
# Unit suite
TEST_DIR=_tests/net10.0 ./test.sh Mac Unit Test

# Full solution build
dotnet build src/Readarr.sln -c Debug

# Lint
yarn lint && yarn stylelint-linux

# Playwright smoke. Use a FULL ./build.sh -- the recipe here used to read
# `./build.sh --backend` then `yarn build`, which is broken in both
# directions: --backend opens with `rm -rf _output` and so deletes the
# frontend, while `yarn build` writes _output/UI and never reaches the
# app, which serves the UI folder beside its own binary. Only the full
# build's packaging step copies it across.
yarn install && ./build.sh
./scripts/playwright-install.sh
READARR_RUN_PLAYWRIGHT=1 dotnet test src/NzbDrone.Playwright.Test/
```

Items intentionally not in the recipe:

- Integration suite (`READARR_RUN_INTEGRATION=1`) — **not dormant
  any more, just not runnable in one pass.** The old reason given
  here (would fail setup against retired `api.bookinfo.club`) is
  obsolete: the fixtures identify authors by OpenLibrary id now and
  pass against live OL. They run in `nightly-integration.yml`, one
  fixture per invocation. Running the whole assembly locally will get
  your IP refused by openlibrary.org — see the stable gate above.
- Selenium suite (`READARR_RUN_AUTOMATION=1`) — quarantined since
  Phase 1, kept for git-archaeology only, and pending a
  keep-or-delete decision.
