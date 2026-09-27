# ImportLists/ — import list providers

External sources that suggest authors/books to add to the library
automatically. Each list runs on a schedule and adds matching authors when
they appear.

## Implementations

- `Goodreads/` — user shelves, lists.
- `LazyLibrarian/` — sync from another LazyLibrarian instance.
- `Readarr/` — sync from another Readarr instance.

## Base hierarchy

```
IImportList
└── ImportListBase
    └── HttpImportListBase  ← most providers
        └── concrete provider
```

## Key services

- `ImportListSyncService` — orchestrates periodic sync runs.
- `ImportListExclusionService` — keeps a deny-list of explicitly-skipped
  authors/books.
- `ImportListStatusService` — health tracking, back-off on failures.

## Adding a list source

1. Pick the base (`HttpImportListBase` for HTTP/REST APIs).
2. Implement `{Source}Settings`, `{Source}Parser`, `{Source}RequestGenerator`.
3. Drop it under `{Source}/` — `ProviderFactory` discovers automatically.
