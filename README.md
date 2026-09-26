# Komarr

Komarr is a planned self-hosted manga acquisition and collection manager, inspired by Sonarr and built for the *arr ecosystem. It will organize manga on the filesystem for readers such as Komga and Kavita to consume.

The project is at the planning stage. The [product requirements](docs/PRD.md) describe the proposed v0.1 scope and the intended Librarr 1.2.2-beta starting point. No application code has been imported yet.

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
