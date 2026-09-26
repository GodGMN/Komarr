# Open Library JSON cassettes

This directory will hold real OL API responses captured as JSON
fixtures. The Phase 8 fixtures (in code) hand-construct
`OpenLibraryWorkResource` / `OpenLibraryEditionResource` instances;
those are good enough to verify mapper logic but won't surface schema
drift in the actual OL JSON.

## What the corpus needs to cover

Per the master plan the golden corpus is 100+ works, sampling:

- Fiction (canonical) — e.g. Foundation, Dune, 1984
- Non-fiction — biographies, histories, technical books
- Audiobook editions — narrators populated, format=AudioBook
- Foreign-language works — non-English `languages.key`
- Pseudonymous authors — Richard Bachman, Robert Galbraith, Mary Westmacott
- Prolific authors — Asimov, King, Patterson (high edition counts)
- Edge cases — works with no title, dupes, soft-deleted records,
  array-of-strings descriptions, missing covers, `cover_i = -1`

## Capturing real responses

```bash
# Example: a single work + its editions list
curl -sL 'https://openlibrary.org/works/OL45883W.json' \
  > work_foundation.json
curl -sL 'https://openlibrary.org/works/OL45883W/editions.json?limit=50' \
  > work_foundation_editions.json

# Author + their works
curl -sL 'https://openlibrary.org/authors/OL26320A.json' \
  > author_asimov.json
curl -sL 'https://openlibrary.org/authors/OL26320A/works.json?limit=50' \
  > author_asimov_works.json

# ISBN / ASIN lookup
curl -sL 'https://openlibrary.org/isbn/9780553293357.json' \
  > edition_foundation_isbn.json
```

OL is rate-limited around ~100 req/min for anonymous traffic — be
nice. Run captures sequentially with a half-second sleep between
calls.

## Loading fixtures in tests

The `OpenLibraryFixtureLoader` helper next to this README finds
JSON files relative to the test assembly directory. Use it like:

```csharp
var work = OpenLibraryFixtureLoader.Load<OpenLibraryWorkResource>(
    "work_foundation.json");
```

## Status

The harness is in place but no real cassettes are checked in yet —
that requires live network. Capture the corpus from a host with OL
access, commit the JSON, then add fixtures that exercise the
mappers against each shape.
