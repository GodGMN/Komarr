# Settings/ — settings pages

All Settings → * pages in the SPA.

## Subfolders

- `Media Management/` — naming, root folders, file management.
- `Profiles/` — Quality / Metadata / Release / Delay profiles.
- `Quality/` — quality definitions (size ranges per format).
- `CustomFormats/` — custom-format scoring rules.
- `Indexers/` — indexer CRUD UI.
- `DownloadClients/` — download client CRUD UI.
- `ImportLists/` — import list CRUD UI.
- `Connect/` — notification provider CRUD UI.
- `Metadata/` — metadata source settings.
- `Tags/` — user tags.
- `General/` — host bindings, ports, auth, logging, branch / update.
- `UI/` — UI prefs (theme, dates, language, calendar).
- `DevelopmentSettings/` — internal flags.

## Pattern

Each settings page consumes the backend's `provider/schema` endpoints
(`Readarr.Api.V1/{Domain}/Schema/`) to render the configuration form
dynamically. The backend's `[FieldDefinition]` attributes drive field type
selection (input / select / checkbox / password / OAuth / etc.).

## Connection

Settings are typically edited via Redux: load → mutate → save → toast on
success / error. The store namespace is `settings.*` in the root state.
