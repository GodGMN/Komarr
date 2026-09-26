# OpenLibrary bulk-data dumps — fork position

**Status:** Not adopted. Continue using the live API + cache layer.

**Scope:** This writeup satisfies the Phase 11 ask in `MASTER-PLAN.md:2281-2283`
("OL partnership: ... evaluate bulk-data dumps from
`https://openlibrary.org/developers/dumps` to cap live API hits"). It
consolidates the technical sketch that previously lived in
`docs/deferred-modernization.md` and adds the Phase 11 sustainability
framing: when does this decision flip?

## What OpenLibrary publishes

archive.org hosts monthly dumps at
[`https://openlibrary.org/developers/dumps`](https://openlibrary.org/developers/dumps):

* `ol_dump_works`, `ol_dump_editions`, `ol_dump_authors`,
  `ol_dump_redirects` — newline-delimited JSON (one record per line),
  tab-separated metadata header.
* Refresh cadence: monthly (typically end-of-month).
* Combined compressed size: in the tens of GB; uncompressed ingest
  on disk lands in the 100+ GB range once indexed. Treat the
  archive.org-published size as authoritative — do not hard-code
  numbers here that will rot.
* Licence: CC0 (matches OL's data policy).

## Why not now

Four reasons, each independent:

1. **Request volume hasn't justified it.** A typical single-instance
   Librarr install issues hundreds of OL requests per day, well inside
   OL's published rate limits and already absorbed by the in-process
   `LazyCache` layer in
   `src/NzbDrone.Core/MetadataSource/BookInfo/BookInfoProxy.cs` and
   its OpenLibrary sibling under
   `src/NzbDrone.Core/MetadataSource/OpenLibrary/`. Bulk ingest is a
   scaling story, and the fork hasn't hit the scale.

2. **Ingest is a real engineering sprint, not a doc deliverable.**
   Adopting dumps requires:
   - An `IngestOpenLibraryDumpCommand` background job (one-shot, not
     periodic — re-runs only when a fresh dump is published).
   - Schema for a local mirror: denormalized works + editions table
     plus an FTS index (OL's own search relies on Solr; replicating
     that without Solr means SQLite FTS5 or Postgres `tsvector`).
   - A streaming JSON reader that handles 50+ GB without holding the
     whole file in memory.
   - Disk-space gating + an opt-in config flag — most users won't
     consent to a 100 GB local index without being asked.
   The Phase 10 ecosystem-upgrade backlog (react-dnd 14→16, react-redux
   7→8, .NET 8 LTS, Nullable enable) needs to land before this can be
   sequenced.

3. **ID reconciliation against `BookIdMapping` is a drift class the
   cassettes don't cover.** Migration 044 added the Phase 5 ID-bridge
   table. Today every row in that table is keyed off a live-API
   response. Reconciling against a stale monthly dump introduces
   "what if the dump's redirect chain disagrees with the live API for
   a work the user already mapped" — a class of bug the cassette
   harness in `Files/OpenLibrary/` doesn't exercise.

4. **archive.org outreach (Phase 11 operational item) hasn't
   happened.** Without that conversation, we don't know whether OL
   *wants* self-hosted ingest from forks or prefers the cached-proxy
   architecture Librarr already uses. Implementing first and asking
   later inverts the partnership.

## Trigger conditions to revisit

Any one of the following should flip this decision:

* **Sustained 429s from OL** despite the existing 10s/60s proxy cache
  layer (see `docs/roadmap.md` "OpenLibrary proxy" item) on a real
  install.
* **archive.org publishes guidance** that explicitly prefers
  dump-based consumers over live-API consumers for federated /
  third-party tooling. This is the most likely trigger.
* **Librarr installation count grows past ~100** (e.g. distributed via
  Linux package repos or NAS app stores). Aggregate request volume
  changes the math even if per-instance volume doesn't.
* **OL live API availability or cost changes materially** — an SLA
  breach, a paywall, an archive.org funding crisis, or sustained
  downtime that the cache layer can't paper over.

If none of the above hits, the live API + cache architecture remains
correct.

## What adoption would cost

Sketch only — not an implementation plan. Each item is one or more
PRs:

1. **Decide fallback vs primary path.** Fallback (network unavailable)
   is cheaper; primary (offline-only deployment) is what justifies
   the FTS index investment.
2. **Pick a storage strategy.** SQLite FTS5 (zero ops, slower search)
   vs. Postgres `tsvector` (faster, requires Postgres) vs. a sidecar
   service (most flexible, most ops). Phase 6's Postgres-compatibility
   work means the answer is no longer "SQLite only."
3. **Stub `IBookInfoDumpReader` interface.** Mirrors the existing
   `IProvideAuthorInfo` / `IProvideBookInfo` seams. Implementation
   against a downloaded dump can land in a Phase 12+ session.
4. **Build the streaming ingest.** Newline-delimited JSON reader,
   record-by-record commit batching, resumable on interrupt. Disk
   gating per the config flag from item (2).
5. **Reconcile with `BookIdMapping`.** Decide policy: dump-wins,
   live-wins, or last-write-wins. Add cassettes that cover the
   drift cases identified above.
6. **Telemetry.** Stale-hit ratio (dump-hit vs live-fallback), dump
   age in days, disk usage. Without this, operators can't tell
   whether the ingest is paying for itself.

Estimate: 2-3 week sprint. Gated on Phase 10 LTS work (.NET 8,
Nullable, ecosystem upgrades) landing first so the codebase is
modernized before we add a second large subsystem.

## References

* OpenLibrary dump downloads: <https://openlibrary.org/developers/dumps>
* OpenLibrary API rate-limit policy: <https://openlibrary.org/developers/api>
* `MASTER-PLAN.md` Phase 11 (the line item this writeup satisfies)
* `src/NzbDrone.Core/MetadataSource/OpenLibrary/` — current OL proxy
  + cache layer (the architecture this decision keeps in place)
* `docs/roadmap.md` — "Later" bucket entry, points to this file
* `docs/deferred-modernization.md` — historical Phase 10 framing of
  the same question, superseded by this doc
