# ThingiProvider/ — provider plugin model

The plugin abstraction shared by Indexers, Download Clients, Notifications,
Import Lists, and Metadata sources.

## Key types

- `IProvider` — marker interface every provider implements.
- `ProviderFactory<TProvider, TDefinition>` — discovers concrete provider
  types via reflection, builds them with configured settings, caches
  instances.
- `IProviderRepository<TDefinition>` — stores per-instance configuration
  (settings JSON blob, base URL, API key, tags, etc.) in the database.
- `ProviderStatusServiceBase` — tracks per-provider health, applies
  exponential back-off after repeated failures so a broken indexer doesn't
  spam the queue.
- `*Definition` — DB-stored settings + provider type discriminator.

## How "adding a new provider" works

1. Implement the domain-specific base (e.g., `IndexerBase`,
   `DownloadClientBase`, `NotificationBase`).
2. The `ProviderFactory` discovers it automatically — no manual registration.
3. UI lookup endpoints in `Readarr.Api.V1/{Domain}/Schema/` use reflection on
   `[FieldDefinition]` attributes to render the settings form.
4. Once enabled in the UI, the factory instantiates the provider with its
   stored settings.

## See also

[`../../../ARCHITECTURE.md`](../../../ARCHITECTURE.md) §4.3.5 for the broader
picture and §8.4 for the duplication smell across concrete providers.
