import '@testing-library/jest-dom/vitest';
// index.ts loads this first thing, and components rely on what it installs --
// AddNewAuthorSearchResult calls authorName.contains(), which is not a real
// String method, only the one polyfills.js defines. Without this every such
// component throws at render time in tests but works fine in the browser.
import '../src/polyfills';
import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';

afterEach(() => {
  cleanup();
});

// index.ejs ships these two divs and the app assumes they exist, so create
// them before the tests run.
//
// This used to be strictly ordered -- it had to happen before any test module
// was imported, because Portal.js resolved
// `document.getElementById('portal-root')` into defaultProps at module load
// time, and an element created inside a test was already too late ("Target
// container is not a DOM element", thrown from deep inside react-dom). The
// defaultProps migration turned that into a default parameter, which is
// evaluated per render, so the ordering no longer matters. Doing it here is
// still the clearest place for it.
for (const id of ['root', 'portal-root']) {
  const div = document.createElement('div');
  div.id = id;
  document.body.appendChild(div);
}

// jsdom ships no canvas implementation, so getContext() returns null and
// react-text-truncate -- which every search-result and detail card renders --
// dies in componentDidMount on `this.canvas.font = font`. A stub is enough:
// nothing here asserts on truncation, it just has to not throw. measureText
// returns a plausible width so the truncation maths does not divide by zero.
HTMLCanvasElement.prototype.getContext = () => ({
  font: '',
  measureText: (text) => ({ width: String(text).length * 6 })
});

// index.ts replaces window.Readarr with the initialize.json payload before
// anything else loads, and modules read it at import time -- createAjaxRequest
// does `const apiRoot = window.Readarr.apiRoot` at module scope, so merely
// importing a component that reaches an action file throws without it.
// Mirrors InitializeJsonController.cs:48-58.
window.Readarr = {
  apiRoot: '/api/v1',
  apiKey: 'test-api-key',
  release: 'test',
  version: '1.1.0-beta',
  instanceName: 'Librarr',
  theme: 'auto',
  branch: 'develop',
  analytics: false,
  userHash: 'test',
  urlBase: '',
  isProduction: true
};

// translate() reads from a fetched translation bundle at runtime and returns
// the raw key until that resolves. Tests assert on rendered copy, so stub it
// to interpolate the same way the real one does -- {count}, {title} -- and
// return the key when a component asks for a string that has no arguments.
//
// The alternative, loading en.json, would make every assertion depend on
// exact marketing copy. This way a test says "the work count is rendered
// with 355" and stays true when the wording changes.
vi.mock('Utilities/String/translate', () => ({
  default: (key, args) => {
    if (!args) {
      return key;
    }

    return `${key}(${Object.entries(args)
      .map(([name, value]) => `${name}=${value}`)
      .join(',')})`;
  }
}));
