# frontend/build/ — Webpack 5 config

The frontend build pipeline.

## Pieces

- `webpack.config.js` — main config. Reads `../babel.config.js` and
  `../postcss.config.js`. Bundles `frontend/src/index.ts` →
  `_output/UI/` (relative to repo root).
- Loaders:
  - `babel-loader 9.1.3` (with `@babel/preset-env`, `@babel/preset-react`,
    `@babel/preset-typescript`).
  - `ts-loader 9.4.4` (paired with `fork-ts-checker-webpack-plugin` for
    type-checking off the critical path).
  - `css-loader 6.8.1` + CSS Modules.
  - `css-modules-typescript-loader 4.0.1` + `typescript-plugin-css-modules`
    — generates ambient `*.css.d.ts` files for CSS Module class names so TS
    can reference them.
  - `postcss-loader 7.3.0` with custom-properties / nested / mixins /
    color-function.
  - `style-loader 3.3.3` (dev) + `mini-css-extract-plugin 2.7.6` (prod).
  - `file-loader` / `url-loader` for fonts and images.
- Plugins: `html-webpack-plugin`, `mini-css-extract-plugin`,
  `terser-webpack-plugin`, `filemanager-webpack-plugin`,
  `webpack-livereload-plugin` (dev).

## Run

```
yarn start    # webpack --watch
yarn build    # one-shot
```

Both scripts live in the root `package.json` and shell out to
`webpack --config ./frontend/build/webpack.config.js`.

## Output

`_output/UI/` (sibling to the .NET `_output/`). The static-content middleware
in `Readarr.Http` serves it.
