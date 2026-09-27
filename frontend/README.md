# frontend/ — React SPA

The web UI. See [`../ARCHITECTURE.md`](../ARCHITECTURE.md) §5 for the full map.

## Stack

- React 17 + react-dom 17 (no React 18 features).
- Redux 4 with `createStore` (legacy), custom `Store/thunks.js` thunk pattern,
  `reselect`, `connected-react-router`.
- React Router 5, history 4.10 (browser history).
- CSS Modules via PostCSS (autoprefixer, mixins, nested, simple-vars,
  color-function).
- jQuery 3.7 — **only** for `src/Utilities/createAjaxRequest.js`.
- `@microsoft/signalr 6.0.25` for real-time push.
- TypeScript 5.1 — partial migration (~29% of files are `.ts`/`.tsx`).
- Webpack 5 + Babel + ts-loader; PostCSS pipeline.
- ESLint 8 (`.eslintrc.js`) + Prettier 2 + Stylelint 15.

## Layout

```
frontend/
├── babel.config.js
├── postcss.config.js
├── tsconfig.json        TS config used by webpack ts-loader
├── jsconfig.json
├── typings/             Hand-written ambient .d.ts files
├── .eslintrc.js         393-line house style (PropTypes on .js, off on .ts/.tsx)
├── .stylelintrc
├── .prettierrc.json
├── build/               Webpack 5 config + loaders   → see build/README.md
└── src/                 SPA source                    → see src/README.md
```

## Dev commands

`package.json` is at the **repo root**, not here. Run from the repo root:

```
yarn install
yarn start     # watch mode (webpack --watch)
yarn build     # one-shot build → ./_output/UI
yarn lint      # ESLint over frontend/
yarn stylelint-linux   # Stylelint over CSS files
```

## Conventions

- PascalCase folder per component, `Component.js` + `Component.css` +
  `ComponentConnector.js`.
- Class components dominate; hooks adoption is partial (~141 callsites).
- PropTypes enforced on `.js`, off on `.ts/.tsx` (`react/prop-types` in
  `.eslintrc.js:317,365`).
- `react-hooks/rules-of-hooks` and `exhaustive-deps` are `error` — new code
  is expected to be hook-aware.
