# Prowlarr to Nyaa compatibility proof

As of 2026-09-27, Komarr can receive a Nyaa indexer from Prowlarr's **Readarr** application integration and query it through the inherited Torznab stack. This was verified with Prowlarr 2.6.5.5623, Nyaa.si, and the Komarr image from this checkout. Prowlarr uses Books category `7000` for the synced indexer.

## Reproduce

From a clean checkout with Docker and Python 3:

```bash
docker build -f distribution/docker/Dockerfile -t komarr:local .
./scripts/prowlarr-nyaa-smoke.py --image komarr:local --query 'One Piece'
```

The script starts fresh temporary Komarr and Prowlarr containers on a private Docker network. It pins Prowlarr to the tested image digest, configures Nyaa through Prowlarr's API, configures Komarr as a Readarr application, waits for Prowlarr to sync the Torznab indexer, then calls Komarr's raw search endpoint. It removes the containers, network, and temporary app data afterward. It does not download or import any content. The live Nyaa query requires internet access; the ordinary Komarr boot does not.

The observed result was:

```text
Prowlarr synced Nyaa to Komarr; 1 raw releases for 'One Piece'.
  One Piece 1194 | Nyaa.si (Prowlarr) | [7000, 156719]
```

The count and title are live tracker data, so they can change. The proof checks for at least one result in the Books category.

To check a saved manga through the title-only Torznab search path, run:

```bash
./scripts/prowlarr-nyaa-smoke.py --image komarr:local --manga-id 30149
```

This adds BLAME! by its explicit AniList ID, searches at most three distinct preferred, English, and romaji titles per indexer, and checks that Nyaa returns a Books release. `GET /api/v1/manga/{id}/search` returns the query list, deduplicated release metadata, and per-indexer errors. It does not download anything. The search uses one page of `t=search` per title with category `7000` plus configured Books categories.

The 2026-09-27 live check sent one distinct BLAME! query and returned 15 Books releases. Some results were related artbooks or editions; the decision endpoint explains why they do not match the saved title and requested item.

`GET /api/v1/manga/{id}/search/decisions` runs the same bounded search through the manga parser, alias matcher, item coverage, and saved quality policy. An optional `itemId` limits the decision to a known volume or chapter. Each result includes its parsed title and confidence, matched alias, covered item IDs, quality hints, and separate rejection and manual-review reasons. This endpoint only evaluates releases; it does not grab them.

The manga detail page can send a reviewed release to a configured download client. Eligible releases need an explicit click; releases with manual-review reasons also need confirmation. `POST /api/v1/manga/{id}/grab` searches the indexer again and requires the selected GUID, title, and indexer to match a fresh result. It reruns matching and quality checks before sending the release. The response and `GET /api/v1/manga/{id}/downloads` show its client tracking ID and covered manga items. Rejected releases cannot be sent. Duplicate pending or sent releases are blocked.

Komarr uses the inherited qBittorrent, SABnzbd, NZBGet, and Transmission client integrations. New client configurations default to the `komarr` category; existing configured categories stay as saved. The client retains the download for seeding.

When a client reports completion, Komarr inspects each file in the payload and stores a separate report for CBZ, CBR, EPUB, PDF, and ZIP candidates. The manga detail page shows ready files, manual-review reasons, and unsupported files. A named volume range can cover several known items; each file in a multi-file pack is checked independently. Overlapping coverage, unqualified numbers, and conflicting titles require review. The inherited book import path skips downloads tracked as manga. File identification does not move or import source files; the following import task handles that step.

In the live BLAME! check, AniList's volume count created 10 known items. Nyaa returned 15 Books results; none qualified for automatic grabbing. The related artbook and movie edition results lacked an explicit volume or chapter token, so the endpoint explained that manual identification was required. A focused integration fixture also verifies an eligible `BLAME! v01` result and rejects a wrong title and requested-volume mismatch.

## Raw search endpoint

`GET /api/v1/indexer/rawsearch?query=<term>` uses normal Komarr API authentication. It searches enabled interactive indexers through their existing provider implementation and returns at most 50 release summaries plus the full result count. It neither matches releases to the library nor starts a download. It reports the count and error, if any, for each queried indexer.

This endpoint is an early acquisition diagnostic. The inherited `/api/v1/release` endpoint searches by saved author or book ID, or fetches RSS; it has no free-text manga path on a fresh database.

## Compatibility gaps for the manga search work

- Prowlarr currently connects through its Readarr application type. Komarr keeps the relevant Readarr-compatible indexer API for this integration.
- The older raw query uses inherited author search criteria. The saved manga search uses a manga title-only request through the Torznab/Newznab provider and Books categories.
- Nyaa reports `7000` (Books) plus a tracker-specific category ID in this proof. The saved manga search includes the Books parent category.
- Search results are observational. Komarr does not grab a result from this endpoint, especially when a match is ambiguous.
