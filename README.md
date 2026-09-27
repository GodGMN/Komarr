# Komarr

Komarr is a self-hosted manga acquisition and collection manager, inspired by Sonarr and built for the *arr ecosystem. It organizes manga on the filesystem for readers such as Komga and Kavita to consume.

The project is an early alpha. The [product requirements](docs/PRD.md) describe the v0.1 scope. Komarr has manga lookup, collections, Wanted, release review, local library scanning and mapping, indexer search, download clients, and seeding-safe import. Parts of the inherited book settings remain while manga-specific settings are built. The Librarr 1.2.2-beta source was imported at a pinned commit; see [upstream provenance](UPSTREAM.md).

Komarr uses a fresh `komarr.db` in its own application data directory. Existing Readarr and Librarr databases are not migrated. The inherited in-app updater and metadata connectivity probe are disabled while their Komarr replacements are developed.

To run Komarr in Docker, see the [Docker instructions](distribution/docker/README.md). Open **Manga → Setup** to check local storage, indexers, download clients, AniList metadata, and backups. Add Prowlarr's Torznab or Newznab feed as an indexer; the [Prowlarr to Nyaa proof](docs/integrations/prowlarr-nyaa.md) documents the integration. Backups contain the database and config, which can include API and download-client credentials, so store them privately.

## Principles

1. Filesystem first.
2. Readers are consumers, not dependencies.
3. Local operation must never depend on a cloud service.
4. Prefer deterministic behavior over clever behavior.
5. Never automatically acquire content from an ambiguous match.
6. Preserve seeding.
7. Explain every decision.
8. Integrate with the *arr ecosystem instead of rebuilding it.
9. Support messy real-world manga naming rather than imposing an ideal model.
10. Scope stays manga acquisition and collection management.

## License

Komarr is licensed under [GNU GPL v3](LICENSE). Code later imported from Librarr will retain its applicable copyright and attribution notices.
