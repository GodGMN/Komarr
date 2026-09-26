# Datastore/ — custom Dapper ORM + migrations

The persistence layer. **Hand-rolled Dapper-based ORM** with dual SQLite /
PostgreSQL support and a FluentMigrator migration system.

## Key types

- `BasicRepository<TModel>` — generic CRUD repo. Concrete repos inherit and
  add domain-specific queries.
- `SqlBuilder` — fluent SQL composition.
- `WhereBuilderSqlite` / `WhereBuilderPostgres` — dialect-specific predicate
  translation. Every new query must be valid on both backends.
- `DbFactory` + `ConnectionStringFactory` — pick SQLite (default) or
  Postgres from `config.xml` / `Readarr:Postgres` env vars.
- `MigrationController` — runs all migrations under `Migration/` on startup.
- `Converters/` — custom JSON column converters (`CustomFormat`,
  `EmbeddedDocument`, `Quality`, `TimeSpan`, etc.).
- `Extensions/` — composition extensions registered by
  `Bootstrap.AddDatabase()`.

## Adding a column

1. Add field to the POCO model under `{Domain}/Model/`.
2. Add a `Migration/0XX_DescriptiveName.cs` class inheriting
   `NzbDroneMigrationBase`.
3. Make sure SQLite *and* Postgres semantics are correct (types like
   `DateTime` need care — Postgres has dedicated migrations for
   `timestamptz` conversion).
4. Update affected repositories / mapping where needed.

## Forked dependencies

- `Servarr.FluentMigrator.Runner` 3.3.2.9 + `.SQLite` + `.Postgres`
  (`../../Directory.Packages.props:12-14`).
- `System.Data.SQLite.Core.Servarr 1.0.115.5-18` — Servarr fork of the
  SQLite ADO.NET provider; necessary for the bundled-platform RIDs.
- `Npgsql 7.0.7` — Postgres driver.

## Why hand-rolled instead of EF?

Servarr predates modern EF Core, and the data access patterns (heavy joins,
custom JSON columns, dual-dialect support) don't map cleanly to EF
conventions. The cost is that every new feature requires hand-written SQL.
