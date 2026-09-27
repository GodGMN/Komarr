# Author/ — Author pages

UI for author-level browsing and editing.

## Subfolders / files

- `Index/` — paginated, filterable author list with view modes (Posters /
  Banners / Overview / Table). `AuthorIndex.js` is ~500 LoC.
- `Details/` — author detail page with tabs for books, monitoring, history.
- `Edit/` — `EditAuthorModal`.
- `Delete/` — `DeleteAuthorModal`.
- `Editor/` — bulk-edit table (`AuthorEditor`) for multi-select operations.
- `History/` — author-scoped history.

## Patterns

Class components. Redux containers suffixed `Connector`. CSS Module per
component. PropTypes for every prop.

## Redux interactions

Action types live in `../Store/Actions/authorActions.js` (~300 LoC).
Selectors in `../Store/Selectors/createAuthorClientSideCollectionItemsSelector.js`
provide filtered + sorted views.
