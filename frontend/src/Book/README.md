# Book/ — Book pages

UI for book-level browsing and editing.

## Subfolders / files

- `Index/` — paginated, filterable book list. `BookIndex.js` is ~500 LoC
  (one of the largest components).
- `Details/` — book detail page (`BookDetails.js` ~300 LoC). Tabs for
  editions, history, releases, files.
- `Edit/` — `EditBookModal` for monitoring + quality profile changes.
- `Delete/` — `DeleteBookModal`.
- `Calendar/` — calendar-view rendering for releases.
- `Search/` — interactive search entrypoints scoped to a book.

## Patterns

- Class components for the page-level views.
- Redux container suffixed `Connector` (`BookDetailsConnector.js`).
- CSS Module per component.

## Redux interactions

Action types live in `../Store/Actions/bookActions.js` (~400 LoC, one of the
larger action files). Uses `createFetchHandler` / `createHandleActions` from
`../Store/Actions/Creators/`.
