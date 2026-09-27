# Komarr

Komarr is a planned self-hosted manga acquisition and collection manager, inspired by Sonarr and built for the *arr ecosystem. It will organize manga on the filesystem for readers such as Komga and Kavita to consume.

The project is at an early implementation stage. The [product requirements](docs/PRD.md) describe the proposed v0.1 scope. The Librarr 1.2.2-beta source is imported at a pinned commit. Komarr now boots with its own executable, data folder, and database; the inherited book UI and domain code still need replacement. See [upstream provenance](UPSTREAM.md) before building or modifying the inherited code.

Komarr uses a fresh `komarr.db` in its own application data directory. Existing Readarr and Librarr databases are not migrated. The inherited in-app updater and metadata connectivity probe are disabled while their Komarr replacements are developed.

To run the current build in Docker, see the [Docker instructions](distribution/docker/README.md).

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
