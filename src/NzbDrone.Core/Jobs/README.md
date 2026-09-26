# Jobs/ — task scheduling

Schedules recurring `ICommand` dispatches.

## Key types

- `TaskManager.cs` (~224 LoC) — owns the schedule table. On startup, seeds
  default tasks (RSS sync, refresh metadata, refresh monitored downloads,
  housekeeping, backup, etc.) and persists per-user-customised intervals.
- `Scheduler.cs` (~71 LoC) — timer that wakes every 30 s, checks for
  due tasks, and enqueues their commands via `CommandQueueManager`.
- `ScheduledTask.cs` — DB-stored task row.
- `IExecute<TCommand>` handlers across `../` are what actually runs.

## Default tasks (illustrative)

- `RssSyncCommand` — every 15 min.
- `RefreshMonitoredDownloadsCommand` — every 1 min.
- `ApplicationUpdateCommand` — every 6 hours (if auto-update enabled).
- `HousekeepingCommand` — every 24 hours.
- `RefreshAuthorCommand` — every 12 hours per author.
- `BackupCommand` — every week.
- `CheckHealthCommand` — every hour.

## UI surface

System → Tasks (in the SPA) shows the schedule with last-run / next-run
timestamps. Users can change intervals or trigger "Run now" via the API
(`Readarr.Api.V1/System/Tasks/`).
