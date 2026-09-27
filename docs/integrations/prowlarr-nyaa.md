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

## Raw search endpoint

`GET /api/v1/indexer/rawsearch?query=<term>` uses normal Komarr API authentication. It searches enabled interactive indexers through their existing provider implementation and returns at most 50 release summaries plus the full result count. It neither matches releases to the library nor starts a download. It reports the count and error, if any, for each queried indexer.

This endpoint is an early acquisition diagnostic. The inherited `/api/v1/release` endpoint searches by saved author or book ID, or fetches RSS; it has no free-text manga path on a fresh database.

## Compatibility gaps for the manga search work

- Prowlarr currently connects through its Readarr application type. Komarr keeps the relevant Readarr-compatible indexer API for this integration.
- The query uses the inherited author search criteria to reach Torznab's book/generic search requests. Manga title parsing, exact matching, edition/volume coverage, and decision explanations still need manga-domain work.
- Nyaa reports `7000` (Books) plus a tracker-specific category ID in this proof. A reliable manga category and query policy belongs to the later Torznab/Newznab adaptation task.
- Search results are observational. Komarr does not grab a result from this endpoint, especially when a match is ambiguous.
