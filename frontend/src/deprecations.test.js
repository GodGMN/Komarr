import fs from 'fs';
import path from 'path';
import { describe, expect, it } from 'vitest';

// React 18.3 warns on `defaultProps` for function components and React 19
// removes it: the props simply arrive undefined, silently, wherever a caller
// relied on the default. Class components are unaffected and keep it.
//
// Grandfathering is deliberate: an empty list here means the codebase is clean
// today, so a new function component with defaultProps fails this test rather
// than adding one more warning to a wall of them.
const srcFolder = path.join(__dirname);

function sourceFiles(dir) {
  const found = [];

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      found.push(...sourceFiles(full));
    } else if ((/\.(js|jsx|ts|tsx)$/).test(entry.name) && !(/\.test\./).test(entry.name)) {
      found.push(full);
    }
  }

  return found;
}

describe('deprecations', () => {
  it('has no defaultProps on function components', () => {
    const offenders = [];

    for (const file of sourceFiles(srcFolder)) {
      const source = fs.readFileSync(file, 'utf8');

      for (const match of source.matchAll(/^(\w+)\.defaultProps\s*=/gm)) {
        const component = match[1];

        // A class component may legitimately keep defaultProps.
        if (!new RegExp(`class\\s+${component}\\b`).test(source)) {
          offenders.push(`${path.relative(srcFolder, file)} (${component})`);
        }
      }
    }

    expect(
      offenders,
      'Use destructuring defaults instead: function Foo({ bar = 1 }). ' +
      'Note that propTypes are checked before defaults are applied, so a ' +
      'defaulted prop must not be marked isRequired.'
    ).toEqual([]);
  });
});
