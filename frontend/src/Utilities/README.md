# Utilities/ — shared helpers

Generic utility modules used across the SPA.

## Subfolders

- `Api/` — API-related helpers.
- `Date/` — `formatDate`, `formatDateTime`, `formatRelativeDate` (built on
  `moment 2.29.4`).
- `Number/` — number formatting (`formatBytes`, `formatNumber`).
- `String/` — `translate`, string transforms.
- `Object/` — `getRelativeDate`, `selectAll`-style helpers.
- `Array/` — `arrayMove`, `sortByName`.
- `Component/` — minor component utilities.

## Key files

- **`createAjaxRequest.js`** — the sole HTTP client. Wraps jQuery `$.ajax`,
  injects API-key headers, normalises errors. *The only place jQuery is used
  in the codebase.*
- **`String/translate.js`** — the lightweight i18n shim. Looks up keys in a
  table fetched from the backend (`Readarr.Api.V1/Localization`). No
  pluralisation, no formatters — just key → string.
- **`getStatusStyle.js`** — maps backend status enums to UI colour classes.

## Why is jQuery here?

History. The `$.ajax` wrapper predates `fetch` and was kept across the
Servarr family rather than rewritten. See
[`../../../ARCHITECTURE.md`](../../../ARCHITECTURE.md) §8.6.
