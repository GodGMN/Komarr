# MediaFiles/ — file scan, parse, tag, import

File-system-facing layer. Reads ebook / audiobook files, extracts metadata,
matches releases to known books, and imports them into the library.

## Subfolders

- `BookImport/` — the import pipeline, including:
  - `Identification/` — release-to-book candidate matching.
  - `Specifications/` — accept/reject rules in the specification pattern.
  - `Manual/` — interactive-import support (UI side picks specific files).
  - `ImportApprovedBooks.cs` (~575 LoC) — finalises an accepted import.
- `Calibre/` — Calibre Content Server integration. `CalibreProxy.cs` (~682
  LoC) speaks Calibre's HTTP API for add/convert.
- `EBook/` — ebook-format-specific tag readers.
- `Audio/` — audio-format-specific tag readers.
- `Events/` — file-change events broadcast across the app.

## Tag readers

- `EpubTag.cs` — extracts metadata from EPUB OPF.
- `AzwTag.cs` — extracts metadata from AZW/MOBI.
- `AudioTag.cs` (~553 LoC) — wraps `TagLibSharp-Lidarr 2.2.0.19` for MP3,
  M4B, FLAC, etc.

## Other

- `FileNameBuilder.cs` (~578 LoC, in `../Organizer/`) — applies user-defined
  rename templates.
- `DownloadedBooksImportService.cs` (~370 LoC) — entry point invoked when a
  download finishes.

## Gotcha

Hard-link detection lives in `NzbDrone.Common/Disk/` and is selected per
platform — Windows vs Linux/macOS implementations differ (`NzbDrone.Windows/`
vs `NzbDrone.Mono/`).
