# Styles/ — global styles + themes

CSS that isn't co-located with a component.

## Files

- `globals.css` — global resets and base rules.
- `scaffolding.css` — base scaffolding styles (fonts, line-heights).

## Subfolders

- `Themes/` — JS objects exporting colour tokens.
  - `dark.js` — dark theme (e.g., `readarrRed: '#ca302d'`).
  - `light.js` — light theme.
- `Variables/` — CSS custom-property declarations consumed by the
  PostCSS pipeline. One file per category: `colors.css`, `fonts.css`,
  `dimensions.css`, etc.
- `Mixins/` — PostCSS mixin definitions (used by `postcss-mixins`).

## Theming flow

1. User picks a theme in Settings → UI.
2. The selection is persisted via `redux-localstorage`.
3. `App.js` applies a body class (`theme-dark` / `theme-light`).
4. The CSS variables in `Variables/` map differently per body class, giving
   themed components without per-component logic.

## No real i18n library

There is no `react-i18next` or `react-intl`. Translation goes through
`Utilities/String/translate.js`, which fetches strings from the backend at
runtime.
