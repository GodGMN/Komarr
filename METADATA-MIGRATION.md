# Metadata-Source Migration Sketch

> **Superseded by shipped implementation (2026-05-19).** This sketch
> predates the shipped `LegacyMigrationService`, `ReidentifyService`, and
> `BookIdMapping` bridge. See [`ARCHITECTURE.md`](ARCHITECTURE.md)
> § "Librarr fork additions" and [`CHANGELOG.md`](CHANGELOG.md) for the
> as-shipped flow. Kept here for historical interest only — the analysis
> below is what the engineering effort looked like before any code was
> written.

> Sketch only — not a committed plan. Sized as "what would the smallest
> credible engineering effort look like" if someone wanted to revive Readarr
> after the upstream archive on 2025-06-27. Cross-references
> [`ARCHITECTURE.md`](ARCHITECTURE.md) §4.3.5 and the retirement notice in
> [`README.md`](README.md):1-20.

---

## 1. Why this matters

Per the Servarr team's retirement statement, Readarr was shut down for a
combination of three reasons:

1. The book-metadata source it depended on (a Goodreads-derived
   `bookinfo.club`-style service) became unusable.
2. The maintainers had no bandwidth to rebuild it.
3. The community attempt to migrate to **Open Library** stalled.

Any revival is gated on solving #1. This document sketches the smallest
credible path to a working metadata source.

## 2. What's already in our favor

The current code is **already designed around a swappable metadata source**.
The seam is in `src/NzbDrone.Core/MetadataSource/`:

```
IProvideAuthorInfo    .GetAuthorInfo(readarrId, useCache) / GetChangedAuthors(since)
IProvideBookInfo      .GetBookInfo(id) → (workId, Book, List<AuthorMetadata>)
ISearchForNewAuthor   .SearchForNewAuthor(title)
ISearchForNewBook     .SearchForNewBook(title, author, getAllEditions)
                      .SearchByIsbn(isbn) / SearchByAsin(asin)
                      .SearchByGoodreadsBookId(int, bool)
ISearchForNewEntity   .SearchForNewEntity(title) → List<object>
IProvideSeriesInfo    .GetSeriesInfo(int id, useCache) → Goodreads.SeriesResource
IProvideListInfo      .GetListInfo(int id, page, useCache) → Goodreads.ListResource
```

**Single implementation today** (`MetadataSource/BookInfo/BookInfoProxy.cs:27`):

```csharp
public class BookInfoProxy : IProvideAuthorInfo, IProvideBookInfo,
                             ISearchForNewBook, ISearchForNewAuthor,
                             ISearchForNewEntity
```

DryIoc auto-registers it (`NzbDrone.Host/Bootstrap.cs:93` `AutoAddServices`),
so a second `IProvide*` implementation can be added and dispatched without
modifying any consumer. Consumers (callers of the seam) are localised:

- `Books/Services/AddAuthorService.cs`
- `Books/Services/RefreshAuthorService.cs`
- `Books/Services/AddBookService.cs`
- `Books/Services/RefreshBookService.cs`
- `ImportLists/ImportListSyncService.cs`
- `MediaFiles/BookImport/Identification/CandidateService.cs`
- `MediaFiles/BookImport/Manual/ManualImportService.cs`

Foreign IDs in the domain model are **strings** —
`AuthorMetadata.ForeignAuthorId`, `Book.ForeignBookId`,
`Book.ForeignEditionId` — so they accommodate Open Library's
`OL12345A` / `OL98765W` / `OL55555M` formats without a schema change.

## 3. What's NOT in our favor

Three real frictions:

### 3.1 Goodreads types leak into the abstraction layer

```csharp
// MetadataSource/IProvideSeriesInfo.cs
public interface IProvideSeriesInfo
{
    SeriesResource GetSeriesInfo(int id, bool useCache = true);  // ← Goodreads DTO
}
```

`IProvideSeriesInfo` and `IProvideListInfo` return concrete Goodreads
resource types. This violates the abstraction and forces a Goodreads-shaped
DTO on any new provider. **Refactor needed** before a clean second
implementation — either:

- (a) Translate Open Library data into the existing Goodreads-shaped DTOs
  (preserves consumers, weird semantics), or
- (b) Introduce neutral types (`SeriesInfo`, `ListInfo`) and update the ~5
  consumers — cleaner but a one-time invasive change.

### 3.2 `ISearchForNewBook.SearchByGoodreadsBookId(int, bool)`

The interface is **explicitly** tied to a Goodreads integer ID. A new
provider has to either:

- Honour Goodreads IDs (e.g., via a Goodreads↔OL bridge table — slow), or
- Throw / return empty when called with a Goodreads ID (breaks
  back-compat for existing libraries).

Best long-term: rename to `SearchByForeignBookId(string, bool)` and let the
active provider interpret.

### 3.3 Foreign-ID continuity for existing user libraries

Existing installs have books stored with **Goodreads numeric IDs** as
`ForeignBookId`. A switch to Open Library means every existing library
entry's foreign ID becomes meaningless. Options:

- **One-shot ID-bridge migration**: at first refresh after upgrade, look up
  each Goodreads ID's ISBN(s) on the old data (still cached in
  `MediaFiles` tags / extras?), then resolve to an OL work ID via
  `https://openlibrary.org/isbn/{isbn}.json`. Lossy but mostly works.
- **Carry both IDs**: add a `ForeignBookIdAlt` column for the OL identifier
  while keeping Goodreads as a stable identity. New rows store OL ID in
  the primary slot.
- **Re-identify-from-files**: re-run the existing import-identification
  pipeline (`MediaFiles/BookImport/Identification/`) on each library's
  files using the file-side metadata (ISBN/ASIN/title in EPUB/AZW/audio
  tags) to discover the OL match.

The third option is by far the most robust because it doesn't trust the
old service's ID space at all — it re-discovers identity from the actual
files on disk.

## 4. Source options ranked by feasibility

| Source | Coverage | Auth needed? | Effort | Quality |
|---|---|---|---|---|
| **Open Library** (openlibrary.org) | Excellent for print, weak audiobooks, no series concept | None | Moderate (write `OpenLibraryProxy`) | Free, comprehensive print catalog, Wikipedia-grade noise |
| **rreading-glasses** (community shim) | Whatever Goodreads still answers | None | Trivial (config point-and-shoot) | Depends on a third party with no SLA |
| **Hardcover** (hardcover.app) | Modern Goodreads-like, growing | API key required | Moderate, GraphQL | Smaller catalog, active community |
| **Google Books** | Very broad, ISBN-heavy | API key, strict quotas | Moderate | Inconsistent author/series data |
| **ISBNdb / BookBrainz / Wikidata** | Specialised | Mixed (paid / free) | High | Too patchy alone |
| **Hybrid (OL + Hardcover + bridge)** | Best | Multiple keys | High | Best |

Audiobooks are the gap in every free option. **Audible has no public API**;
the realistic options for audio metadata are (a) `audnex.us` (community
Audible scraper) or (b) ASIN-driven Hardcover lookups.

## 5. Recommended path: dual-track

### Track A — Immediate unbreaker (1-2 days, no code change)

**Endorse rreading-glasses as the configured metadata host.**

`BookInfoProxy` already talks to a configurable base URL via the
`IMetadataRequestBuilder` indirection
(`MetadataSource/MetadataRequestBuilder.cs`). rreading-glasses is, by
design, BookInfo-API-compatible. If you point Readarr at a
self-hosted or community-hosted rreading-glasses instance, the existing
proxy works.

**Code changes required:** zero, in principle.

**Doc / config changes required:**

- Surface `MetadataSource.BaseUrl` (or equivalent) in
  `Settings/General` UI (frontend `Settings/General/`).
- Default it to a known-good public mirror (or leave empty and document a
  Docker `docker-compose` recipe for a sidecar `rreading-glasses`
  container).
- Update `HealthCheck/MetadataConnectivityCheck` (add one if missing) to
  fail loudly if the metadata endpoint is unreachable, with a link to the
  setup doc.

**Caveats:**

- Servarr team explicitly disclaim support for third-party mirrors
  (`README.md:5`). A fork doing this should document it.
- rreading-glasses still depends on Goodreads being reachable — if
  Goodreads tightens scraping, this breaks.

### Track B — Native Open Library provider (1-3 months)

Implement `OpenLibraryProxy` as a second concrete behind the same
`IProvide*` / `ISearchForNew*` interfaces. Make the active provider
**configurable per install** so users can opt into either backend.

The two providers coexist via a thin selector
(`MetadataSource/MetadataSourceFactory`) that reads
`ConfigService.MetadataSource` (`"BookInfo"` | `"OpenLibrary"`) and
returns the right implementation. DryIoc registers both
`BookInfoProxy` and `OpenLibraryProxy`; the selector pulls the chosen
one from the container.

## 6. Phase plan (Track B)

```
Phase 1   Refactor the seam (1-2 weeks)
─────────
  • Replace Goodreads.SeriesResource / Goodreads.ListResource leaks in
    IProvideSeriesInfo / IProvideListInfo with neutral DTOs
    (Books/Model/SeriesInfo.cs, Books/Model/ListInfo.cs).
  • Update the 5 consumer files; adapt the existing BookInfoProxy to map
    its Goodreads response → neutral DTO.
  • Rename ISearchForNewBook.SearchByGoodreadsBookId →
    SearchByForeignBookId(string, bool). Add a default impl on
    BookInfoProxy that parses the integer; OpenLibraryProxy will accept
    "OL...W" strings.
  • All consumers still call the existing BookInfoProxy — zero functional
    change shipped at end of phase 1.

Phase 2   OpenLibraryProxy MVP (2-3 weeks)
─────────
  • New folder: src/NzbDrone.Core/MetadataSource/OpenLibrary/
      ├── OpenLibraryProxy.cs              implements IProvide* + ISearchFor*
      ├── OpenLibraryRequestBuilder.cs     base URL + rate limit (10 rps)
      ├── OpenLibraryException.cs
      └── Resources/
          ├── OpenLibraryWorkResource.cs        /works/{OL...W}.json
          ├── OpenLibraryEditionResource.cs     /books/{OL...M}.json
          ├── OpenLibraryAuthorResource.cs      /authors/{OL...A}.json
          ├── OpenLibraryAuthorWorksResource.cs /authors/{OL...A}/works.json
          ├── OpenLibrarySearchResource.cs      /search.json
          └── OpenLibraryIsbnResource.cs        /isbn/{isbn}.json
  • Map OL JSON → domain models (Author, Book, AuthorMetadata, Edition).
    Mapping table in §7 below.
  • Cover art: covers.openlibrary.org/b/id/{cover_id}-L.jpg →
    NzbDrone.Core.MediaCover (existing infra).
  • Wire into the selector but keep default = BookInfo so existing
    installs are unaffected.

Phase 3   ID-bridge & migration tooling (1-2 weeks)
─────────
  • New command: ReidentifyAuthorCommand / ReidentifyLibraryCommand under
    NzbDrone.Core/Books/Commands/.
  • Logic: for each Author and Book in the library, attempt resolution
    against OpenLibrary using (a) ISBN if present on any edition,
    (b) ASIN, (c) title+author fuzzy match. Cache results in a new table
    BookIdMapping(GoodreadsId, OpenLibraryWorkId, Confidence).
  • Settings → Metadata UI: "Switch metadata source" wizard that runs the
    reidentify command, presents conflicts, lets the user pick edition.
  • Health check: warn when migrated entries have Confidence < threshold.

Phase 4   Series + lists (1 week, may slip)
─────────
  • Open Library lacks a strong "series" notion. Two pragmatic fallbacks:
      (a) Wikidata via SPARQL — works are linked to Wikidata items which
          carry "part of the series" properties. Slow but free.
      (b) Author page subjects + manual user-curated series in a new
          local table.
  • Import lists from Goodreads "Listopia" cannot be carried over
    cleanly. Replace with "Open Library subject" and "Open Library
    trending" list providers under
    NzbDrone.Core/ImportLists/OpenLibrary/.

Phase 5   Audiobook supplement (TBD)
─────────
  • Optional second provider chain that augments OL metadata with
    audnex.us audiobook info for entries with audio-format quality
    profiles. Implements only the audiobook-specific fields (narrator,
    duration, ASIN). Slots in via a new IAugmentAudiobookInfo interface.
```

## 7. Open Library → Readarr field mapping (sketch)

| Readarr field | Open Library source | Notes |
|---|---|---|
| `Author.ForeignAuthorId` | OL author key, e.g. `OL5749351A` | Stored as string |
| `AuthorMetadata.Name` | `/authors/{key}.json` `.name` | |
| `AuthorMetadata.SortName` | `.personal_name` ?? derived `last, first` | OL has both `name` and `personal_name` |
| `AuthorMetadata.Aliases` | `.alternate_names[]` | |
| `AuthorMetadata.Overview` | `.bio` (string or `{value}`) | OL `bio` is sometimes plain, sometimes object |
| `AuthorMetadata.Born` / `Died` | `.birth_date`, `.death_date` | Free-text → parse to `DateTime?` |
| `AuthorMetadata.Images` | `https://covers.openlibrary.org/a/olid/{key}-L.jpg` | |
| `AuthorMetadata.Genres` | n/a — derive from aggregated work subjects | Cap to N |
| `AuthorMetadata.Links` | `.links[]` | |
| `Book.ForeignBookId` | OL work key, e.g. `OL14931151W` | The "work" is the abstract book |
| `Book.ForeignEditionId` | OL edition key, e.g. `OL45804535M` | The "edition" is the printed instance |
| `Book.Title` | work `.title` | |
| `Book.ReleaseDate` | min(`first_publish_date`, editions[].publish_date) | OL dates are often "1997", parse permissively |
| `Book.Ratings` | `/works/{key}/ratings.json` `.summary.average` | Optional — OL ratings sparse |
| `Edition.Isbn13` / `Isbn10` | edition `.isbn_13[]` / `.isbn_10[]` | |
| `Edition.Asin` | edition `.identifiers.amazon[]` | Sometimes present |
| `Edition.PageCount` | edition `.number_of_pages` | |
| `Edition.Format` | edition `.physical_format` + `.format` | Map: "paperback" → `Paperback`, "ebook" → `Ebook`, etc. |
| `Edition.Language` | edition `.languages[].key` (`/languages/eng`) | |
| `Edition.Publisher` | edition `.publishers[0]` | |
| `Edition.Cover` | `https://covers.openlibrary.org/b/olid/{edition_key}-L.jpg` | |

## 8. Hard problems left open

1. **Identity bridging for legacy libraries.** ISBN bridge works for
   anything with a known ISBN. Public-domain pre-ISBN works, academic
   editions without ISBNs, audiobook-only ASINs not on Open Library —
   these can't bridge automatically. Pragmatic answer: do best-effort,
   flag low-confidence rows, let the user re-pick manually via the
   existing manual-import UI (`MediaFiles/BookImport/Manual/`).
2. **Series.** OL has nothing equivalent to Goodreads' first-class series
   model. Wikidata works for ~70% of major fiction series. The other
   ~30% need either a user-curated local table or a community-maintained
   sidecar dataset. The existing `Books/SeriesBookLinkService` is the
   right place to land whatever solution you pick.
3. **Audiobooks.** OL metadata for audio is weak. Audible has no public
   API. Community scrapers (audnex) work but are fragile to Audible site
   changes. Plan for it being a separate optional augmenter, not a
   primary source.
4. **Rate limiting & politeness.** OL asks API consumers to stay under
   ~100 requests/minute and identify themselves via `User-Agent`. Wire
   that into `NzbDrone.Common/Http` (existing `IHttpClient` infra
   already supports per-host rate limits — see `Indexers/` for the
   pattern). Set the UA to something like `Readarr/0.4.19
   (https://readarr.com)` — though for a fork, use the fork's identity.
5. **Bulk-refresh storms.** Existing `BookInfoProxy` has a `Limited` flag
   that throttles `GetChangedAuthors` (see `BookInfoProxy.cs:83`). Open
   Library doesn't expose a "changed-since" endpoint — you'd need to
   poll author work-lists and diff against local state. The current
   24-hour refresh cadence (`Jobs/TaskManager.cs`) is probably the right
   ceiling.
6. **Search quality.** OL `search.json` is keyword-soup; it does not rank
   "best match" the way Goodreads does. For the `ISearchForNewBook` UX,
   you'll want a post-query re-rank using Levenshtein over title+author.
   `Parser/Parser.cs` already does heuristic title cleaning — reuse it.
7. **Existing Goodreads code paths.** `MetadataSource/Goodreads/` and
   `MetadataSource/GoodreadsSearchProxy/` are still wired in. Decide
   whether to delete them or keep them as a `BookInfoProxy` fallback for
   installs that can't / won't migrate.

## 9. What this DOESN'T solve

- **Existing third-party download clients & indexers** are unaffected — they
  are orthogonal to metadata. Migration changes nothing in `Indexers/` or
  `Download/Clients/`.
- **The retirement disclaimer in `README.md:1-20`** still applies. A fork
  doing this work is publishing its own product; it does not unretire the
  upstream project.
- **Operational support cost.** Open Library is a free service. If usage
  from a revived Readarr fork puts measurable load on
  archive.org's infrastructure, somebody needs to talk to them — see
  `https://openlibrary.org/developers/api` and their `Bulk Data Dumps`
  alternative.
- **The CLA at the top of this repo (`CLA.md`)** assigns rights to the
  Servarr team. A fork that takes contributions will need to either
  re-execute the CLA under the fork's name or drop CLA requirements and
  rely on the inbound=outbound default of GPL v3.

## 10. Concrete first PR (if anyone is reaching for the keyboard)

If you wanted to land a single, reviewable PR that proves the seam is
real:

1. Add `src/NzbDrone.Core/MetadataSource/OpenLibrary/OpenLibraryProxy.cs`
   that implements **only** `ISearchForNewBook.SearchForNewBook(title,
   author)` and `SearchByIsbn(isbn)`. Stub everything else with
   `NotImplementedException`.
2. Add `OpenLibraryRequestBuilder.cs` pointing at
   `https://openlibrary.org/` with the politeness UA.
3. Add `MetadataSourceFactory` that picks `BookInfoProxy` by default and
   `OpenLibraryProxy` when `Config.MetadataSource == "OpenLibrary"`.
4. Add a debug-only setting in `Settings/General/` UI for the toggle.
5. Add a unit test in `NzbDrone.Core.Test/MetadataSource/OpenLibrary/`
   that hits the live API with VCR-style cassettes
   (`NzbDrone.Test.Common` already has a recording pattern — see how
   `Indexers` tests use canned response JSON).

That PR is ~600 lines, touches no consumer code, ships an opt-in
"search-only OL" mode that proves the rest of the plan is feasible
without committing to the schema refactor in §3.1. Everything from
§§5-9 follows from that beachhead.
