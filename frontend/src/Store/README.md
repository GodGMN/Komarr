# Store/ — Redux store

Where every piece of UI state lives.

## Files

- `createAppStore.js` — store factory. Uses **legacy `createStore`** (not
  RTK's `configureStore`). Composes the root reducer, middleware,
  `routerMiddleware`, and `redux-batched-actions`.
- `thunks.js` — **custom** thunk pattern via `createThunk` and
  `handleThunks`. Action creators register a thunk by string key; a
  middleware looks the key up at dispatch time. Most async work uses this,
  not `redux-thunk` (which is still in `package.json` but largely unused).

## Subfolders

| Folder         | Purpose                                                   |
|----------------|-----------------------------------------------------------|
| `Actions/`     | Per-feature action creators (`authorActions.js`, …)        |
| `Actions/Creators/` | Generator functions: `createFetchHandler`,           |
|                | `createHandleActions`, `createSetReducerValueReducer` —    |
|                | factor out reducer boilerplate                             |
| `Middleware/`  | `appMiddleware`, `sentryMiddleware`, etc.                  |
| `Migrators/`   | One-shot localStorage schema migrators                     |
| `Reducers/`    | Root reducer + `connectRouter`                             |
| `Selectors/`   | `reselect`-based selectors; `createDeepEqualSelector.js`   |

## Conventions

- FSA-style actions (`{ type, payload, error }`) via `redux-actions`.
- `redux-batched-actions` for grouping dispatches.
- `redux-localstorage` for selected slices (UI preferences, last-route).
- Selectors live next to where they're consumed when feature-specific;
  shared ones live here.

## SignalR

`Components/SignalRConnector.js` dispatches into this store directly. It
does not route through thunks.
