# Configuration/ — split config (XML file + database)

Application configuration is split across two stores.

## `config.xml` — bootstrap-time only

Handled by `ConfigFileProvider.cs` (~431 LoC). Stores values needed before
the database is available:

- `BindAddress`, `Port`, `SslPort`, `EnableSsl`, `SslCertPath`,
  `SslCertPassword`.
- `ApiKey`.
- `AuthenticationMethod` (`None` / `Basic` / `Forms`).
- `LogLevel`.
- `UrlBase` (for reverse proxies).
- `Branch`, `UpdateMechanism`, `UpdateAutomatically`.
- `Theme` (default UI theme).
- Postgres connection (overridden by `Readarr:Postgres` env vars in
  `Bootstrap.cs:102,161`).

Loaded by `Bootstrap.GetConfiguration` (`../../NzbDrone.Host/Bootstrap.cs:229-248`).

## Database `Config` table — runtime preferences

Handled by `ConfigService.cs` (~494 LoC) + `ConfigRepository.cs`. Key/value
JSON store for everything else — quality definitions, default profiles,
calendar settings, naming preferences, etc.

## Why two stores?

Bootstrap-time settings (port, TLS, API key) must be available *before*
DryIoc, the database, or migrations run. They can't live in the DB. Runtime
preferences can — and do — so the UI can write them back without restart.

## Reload

`ConfigFileProvider` watches the XML file for changes via
`AddXmlFile(..., reloadOnChange: false)` — currently reload-on-change is
**disabled** (`Bootstrap.cs:237`). Changes require a restart.
