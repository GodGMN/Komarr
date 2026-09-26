# Indexers/ — release sources

Concrete implementations of the indexer provider model. Each subfolder is
one indexer family.

## Base hierarchy

```
IIndexer
└── IndexerBase
    └── HttpIndexerBase
        ├── Newznab/NewznabIndexerBase
        │   ├── Newznab/ (generic Newznab)
        │   └── Torznab/Torznab
        ├── Gazelle/Gazelle
        ├── FileList/FileList
        ├── Nyaa/Nyaa
        ├── IPTorrents/IPTorrents
        ├── TorrentRss/TorrentRssIndexer
        └── …
```

## Per-indexer folder layout

```
{Indexer}/
├── {Indexer}.cs            Provider entry point
├── {Indexer}Settings.cs    [FieldDefinition]-decorated settings POCO
├── {Indexer}RequestGenerator.cs   builds search/RSS URLs
├── {Indexer}Parser.cs      parses the response into ReleaseInfo
├── {Indexer}Definition.cs  optional, for capability metadata
└── …
```

## Cross-cutting helpers

- `IndexerRepository` — DB storage of indexer instances.
- `IndexerStatusService` — exponential-backoff health tracking.
- `Exceptions/` — `IndexerException`, `RequestLimitReachedException`, etc.

## Adding an indexer

1. Pick the closest base class. Most HTTP-based indexers will derive from
   `HttpIndexerBase`.
2. Implement `{Indexer}Settings`, `{Indexer}RequestGenerator`,
   `{Indexer}Parser`.
3. Drop the files under `{Indexer}/` — `ProviderFactory` finds them via
   reflection.

See `../../../ARCHITECTURE.md` §10 for the broader pattern.
