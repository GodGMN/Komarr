# NzbDrone.Core/ — business logic (Readarr.Core.csproj)

The largest project — almost every domain rule lives here. See
[`../../ARCHITECTURE.md`](../../ARCHITECTURE.md) §4 for cross-cutting patterns.

## Top-level folders

| Folder              | Purpose                                                                  |
|---------------------|--------------------------------------------------------------------------|
| `Analytics/`        | Anonymous usage reporting                                                |
| `Annotations/`      | Custom attributes (e.g., `[FieldDefinition]` for provider settings UI)   |
| `Authentication/`   | API-key and form authentication helpers                                  |
| `AuthorStats/`      | Aggregated stats per author                                              |
| `Backup/`           | Database/config backup creation and restore                              |
| `Blocklisting/`     | Release blocklist                                                         |
| `Books/`            | Author / Book / Edition / Series domain                                   |
| `Configuration/`    | XML and DB-backed config (`ConfigFileProvider`, `ConfigService`)          |
| `CustomFilters/`    | User-defined UI filters                                                   |
| `CustomFormats/`    | Custom-format quality scoring rules                                       |
| `Datastore/`        | Custom Dapper ORM + FluentMigrator migrations + SQLite/Postgres dialect  |
| `DecisionEngine/`   | Specification-pattern release decisions                                   |
| `DiskSpace/`        | Per-root disk-space monitoring                                            |
| `Download/`         | Download lifecycle + per-protocol client implementations                  |
| `Exceptions/`       | Domain exceptions                                                         |
| `Extras/`           | Companion files (subtitles, info, art)                                    |
| `HealthCheck/`      | Pluggable health checks shown in System → Status                          |
| `History/`          | Grab/import/failed history                                                |
| `Housekeeping/`     | ~20 cleanup jobs (orphan rows, expired blocklist, etc.)                   |
| `Http/`             | Web-app HTTP helpers (proxy settings, public address)                     |
| `ImportLists/`      | Goodreads / LazyLibrarian / Readarr import list providers                 |
| `Indexers/`         | Indexer providers (Newznab/Torznab/Gazelle/FileList/Nyaa/IPTorrents/RSS)  |
| `IndexerSearch/`    | Cross-indexer search orchestration                                        |
| `Instrumentation/`  | Log-level reconfig + diagnostic events                                    |
| `Jobs/`             | `TaskManager`, `Scheduler`, recurring command dispatch                    |
| `Languages/`        | Language codes / parsing                                                  |
| `Lifecycle/`        | App start/shutdown events                                                  |
| `Localization/`     | i18n string tables (Weblate-managed, excluded from CI triggers)           |
| `MediaCover/`       | Cover image fetch + cache                                                 |
| `MediaFiles/`       | File scanning, parsing, tagging, import pipeline                          |
| `Messaging/`        | `EventAggregator` (pub/sub) + `CommandQueueManager`                       |
| `MetadataSource/`   | External metadata providers (BookInfo proxy)                              |
| `Notifications/`    | Discord / Slack / Email / Telegram / Plex / Webhook providers             |
| `Organizer/`        | File naming rules and rename engine                                       |
| `Parser/`           | Release-name regex parser, quality parser, language parser                |
| `Profiles/`         | Quality / metadata / release / delay profiles                             |
| `ProgressMessaging/`| SignalR-bound command progress wrappers                                    |
| `Properties/`       | Assembly metadata                                                         |
| `Qualities/`        | Quality definitions and ranking                                           |
| `Queue/`            | Active download queue                                                     |
| `RemotePathMappings/` | Host↔download-client path translation                                   |
| `RootFolders/`      | Library root folder management                                            |
| `Security/`         | Crypto helpers                                                            |
| `Tags/`             | User tags                                                                 |
| `ThingiProvider/`   | Generic provider/factory base for indexers/clients/notifications/etc.    |
| `Update/`           | Self-update orchestration                                                 |
| `Validation/`       | FluentValidation extensions                                               |

## Conventions

Per-domain layout (e.g., `Books/`):

```
{Domain}/
├── Model/                   POCOs (Book.cs, Author.cs, Edition.cs)
├── {Entity}Repository.cs
├── {Entity}Service.cs
├── Commands/                ICommand impls
├── Events/                  IEvent impls
│   └── Handlers/            IHandle<*> classes
└── …
```

Heavy use of constructor injection. New services are auto-discovered by the
DryIoc `AutoAddServices` scan (`NzbDrone.Host/Bootstrap.cs`).

## Big files to know about (often-touched)

- `Parser/Parser.cs` (~905 LoC) — regex-driven release-name parser.
- `MetadataSource/BookInfo/BookInfoProxy.cs` (~993 LoC) — metadata proxy.
- `MediaFiles/Calibre/CalibreProxy.cs` (~682 LoC) — Calibre content-server
  client.
- `MediaFiles/BookImport/ImportApprovedBooks.cs` (~575 LoC).
- `Organizer/FileNameBuilder.cs` (~578 LoC).
- `Download/Clients/QBittorrent/QBittorrent.cs` (~725 LoC).
