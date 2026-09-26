# Download/ — download lifecycle

Owns everything from "a release was approved" to "a file appeared in the
library".

## High-level pipeline

```
Indexer search → DecisionEngine specs → DownloadDecisionMaker
              → DownloadService → DownloadClient (provider)
              → TrackedDownloadService (polls the client)
              → CompletedDownloadService
              → MediaFiles/BookImport pipeline
              → History + Notifications
```

## Subfolders

- `Clients/` — per-client implementations (see [Clients/README.md](Clients/README.md)).
- `History/` — short-term grab/import tracking complementing `History/`.
- `Pending/` — releases queued for retry / delay.
- `TrackedDownloads/` — synchronisation of client state to internal queue.
- `FailedDownloads/` — handling for unrecoverable downloads.

## Key types

- `IDownloadService` — entry point used by indexer search and RSS sync.
- `DownloadClientBase` — base for all clients; subdivides into
  `TorrentClientBase` and `UsenetClientBase`.
- `TrackedDownloadService` — polls all enabled clients and updates a unified
  view of in-flight downloads.
- `CompletedDownloadService` — invoked when a download finishes; hands off
  to the import pipeline.

## See

`Clients/README.md` for individual client implementations.
