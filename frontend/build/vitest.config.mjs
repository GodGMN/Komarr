import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { transformWithEsbuild } from 'vite';
import { defineConfig } from 'vitest/config';

const buildFolder = path.dirname(fileURLToPath(import.meta.url));
const srcFolder = path.resolve(buildFolder, '..', 'src');

// webpack.config.js resolves bare specifiers against frontend/src, so the
// codebase is full of imports like `Components/Label` and `Helpers/Props`.
// Vite has no equivalent of webpack's resolve.modules, so map each top-level
// directory under src instead -- read off disk rather than hardcoded, so a
// new feature folder does not silently fail to resolve in tests only.
//
// Shims is spread in flat because webpack lists it as a second modules root:
// `import ...  from 'Diacritical'` resolves to src/Shims/Diacritical.js.
function moduleAliases() {
  const roots = [srcFolder, path.join(srcFolder, 'Shims')];
  const aliases = {};

  for (const root of roots) {
    if (!fs.existsSync(root)) {
      continue;
    }

    for (const entry of fs.readdirSync(root, { withFileTypes: true })) {
      const name = entry.isDirectory() ? entry.name : path.parse(entry.name).name;

      // First root wins, matching webpack's left-to-right module resolution.
      if (!(name in aliases)) {
        aliases[name] = path.join(root, entry.name);
      }
    }
  }

  return aliases;
}

// 1004 of the ~1070 hand-written frontend source files are .js containing
// JSX, which webpack handles through babel-loader and preset-react. Vite only
// parses JSX out of .jsx/.tsx, so without this every component import dies
// with "content contains invalid JS syntax".
//
// This is a plugin rather than the `esbuild: { loader, include }` option that
// looks like it should do the job. That option's `include` REPLACES Vite's
// default (/\.(m?ts|[jt]sx)$/), so setting it to /\.js$/ stops .ts and .tsx
// being transformed at all -- and the failure surfaces far away, as a parse
// error on the first `interface` in some component three imports deep. There
// is no way to express "jsx loader for .js, leave everything else alone"
// through that option, because `loader` is a single value for all matches.
function jsxInJs() {
  return {
    name: 'librarr:jsx-in-js',
    enforce: 'pre',
    async transform(code, id) {
      const file = id.split('?')[0];

      if (!file.endsWith('.js') || !file.startsWith(srcFolder)) {
        return null;
      }

      return transformWithEsbuild(code, file, { loader: 'jsx' });
    }
  };
}

// Every component does `className={styles.foo}` against a CSS Modules import.
// Vite only treats *.module.css as modules, and this codebase names them all
// plain .css, so without help `styles` is an empty object and each class name
// comes out undefined -- which is mostly harmless but makes React shout
// "prop `className` is marked as required" on almost every render.
//
// Returning the key as its own class name keeps that quiet and lets a test
// query by class if it ever needs to. `then` is excluded because a module
// namespace with a callable `then` is treated as a thenable by the ESM
// loader, which would hang the import.
function cssIdentityModules() {
  const virtualId = '\0librarr:css-identity';

  return {
    name: 'librarr:css-identity',
    enforce: 'pre',
    resolveId(source) {
      return source.endsWith('.css') ? virtualId : null;
    },
    load(id) {
      if (id !== virtualId) {
        return null;
      }

      return `export default new Proxy({}, {
        get: (target, key) => (typeof key === 'string' && key !== 'then' ? key : undefined)
      });`;
    }
  };
}

export default defineConfig({
  plugins: [jsxInJs(), cssIdentityModules()],
  optimizeDeps: {
    esbuildOptions: {
      loader: {
        '.js': 'jsx'
      }
    }
  },
  resolve: {
    alias: {
      ...moduleAliases(),
      // Mirrors the two webpack aliases so a test importing a component that
      // pulls in jQuery or react-middle-truncate resolves the same file the
      // browser bundle does.
      jquery: 'jquery/dist/jquery.min',
      'react-middle-truncate': 'react-middle-truncate/lib/react-middle-truncate'
    }
  },
  test: {
    // No injected globals: tests import describe/it/expect from vitest. The
    // eslint config has no test environment configured, so ambient globals
    // would lint as undefined variables -- and being explicit costs one line
    // per file.
    globals: false,
    environment: 'jsdom',
    setupFiles: [path.join(buildFolder, 'vitest.setup.mjs')],
    include: [path.join(srcFolder, '**/*.test.{js,jsx,ts,tsx}')],
    // The generated *.css.d.ts files sit next to their components and are
    // not tests; exclude explicitly so a stray glob change cannot pick them
    // up as one.
    exclude: ['**/node_modules/**', '**/*.css.d.ts'],
    // CSS is left unprocessed. Every component here does
    // `className={styles.foo}`, which becomes undefined -- harmless, because
    // testing-library queries by role and text, not by class. Turning CSS on
    // would mean teaching Vite that every .css file is a module (webpack's
    // config does that; Vite reserves it for *.module.css), for no gain.
    css: false,
    clearMocks: true,
    restoreMocks: true
  }
});
