# Housekeeping/ — cleanup jobs

Recurring maintenance tasks that keep the database tidy.

## Pattern

Each housekeeper implements `IHousekeepingTask`. They run on a schedule
driven by the `HousekeepingCommand` (a recurring command set up in
`../Jobs/`). `HousekeepingService` (~50 LoC) is the runner.

## What gets cleaned

Typical housekeepers (~20 in total):

- Orphaned blocklist entries.
- Stale logs.
- Old history records.
- Missing-author / missing-book rows whose parent went away.
- Pending downloads stuck longer than the configured retention.
- Expired backups.
- Orphan tags.
- Cached cover art for deleted authors.

## Adding a housekeeper

1. Implement `IHousekeepingTask`.
2. Drop the file in this folder — auto-discovered.
3. Optional: log start/finish at info level; otherwise stay silent.

## Cadence

The default cadence is once per day, configured in
`../Jobs/TaskManager.cs`.
