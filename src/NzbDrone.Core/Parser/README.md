# Parser/ — release name parsing

Pure-regex parsing of release names, file names, and feed metadata into
`ParsedBookInfo` records.

## Key files

- `Parser.cs` (~905 LoC) — the central regex parser. Handles author/title
  extraction, year detection, edition markers, audiobook flags.
- `QualityParser.cs` (~302 LoC) — derives `Quality` (PDF / EPUB / AZW3 /
  MP3 / FLAC / …) from the same release strings.
- `LanguageParser.cs` — language detection.
- `IsoLanguages.cs` — ISO 639 code table.
- `Model/ParsedBookInfo.cs` — parser output DTO.

## Fragility

This is the most-touched and most-fragile file in the codebase. Each indexer
flavour names releases differently, so `Parser.cs` accretes regex over
time. A single careless change can break many release patterns at once.
There is no curated golden-corpus test fixture — extension happens in
response to user-reported breakage.

## Where the parsed info goes

- `Indexers/*` use it to turn RSS / search results into `ReleaseInfo`.
- `MediaFiles/BookImport/Identification/` uses it to match imported files
  to library books.

See `../../../ARCHITECTURE.md` §8.5 (god classes) and §9 (open challenges).
