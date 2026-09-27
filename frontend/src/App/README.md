# App/ — SPA shell

The top-level React shell.

## Files

- `App.js` — wraps the tree in `<Provider store={store}>` and
  `<ConnectedRouter history={history}>`.
- `AppRoutes.js` — static route table. **No lazy loading or code splitting**
  — every page is imported up-front. React Router 5's `<Route component=…>`
  pattern, no `useRoutes`.
- `ColorImpairedContext.js` — accessibility colour-mode context (provides a
  flag the rest of the UI uses to swap to colour-impaired-friendly palettes).
- `PageConnector.js` — connects the `Page` shell from `Components/Page/` to
  Redux (disconnect detection, dimension tracking, sidebar state).

## Auth

There is no client-side auth guard. Anonymous users hit the API; the
backend (`Readarr.Http` middleware) returns 401 if auth is required. The
login page is server-rendered.

## Entry chain

`../bootstrap.tsx` builds the store + history, then renders `<App />` from
this folder.
