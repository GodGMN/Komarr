# HealthCheck/ — system health checks

Pluggable health checks shown in System → Status in the UI.

## Pattern

Each check implements `IProvideHealthCheck`. They are auto-discovered by the
DI scan. Categories:

- **Errors** — block features (e.g., no root folder configured, write
  permission missing).
- **Warnings** — operational issues (e.g., update available, indexer
  unreachable).
- **Notices** — informational.

## Example checks

- `RootFolderCheck` — every monitored author has a writable root.
- `IndexerStatusCheck` — at least one indexer is enabled and healthy.
- `DownloadClientCheck` — at least one configured + reachable.
- `RemotePathMappingCheck` — host vs download-client path mapping is sane.
- `UpdateCheck` — newer release available.
- `MonoNotNetCoreCheck` — pre-flight legacy check.
- `AppDataLocationCheck` — appdata is on the user's path.

## Wiring

- `HealthCheckService` — runs checks on a schedule and on events.
- `HealthCheckEvent` — published when a check's status changes; UI listens
  via SignalR.
