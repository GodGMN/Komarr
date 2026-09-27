# Contributing manga release names

The parser corpus lives in [`tests/Parser/Fixtures/nyaa-public-2026-09-27.jsonl`](../tests/Parser/Fixtures/nyaa-public-2026-09-27.jsonl). Each line is one public release title with a reviewed expected parse. The file contains no torrent links or payloads.

Add a fixture when a real manga release name exposes a missing pattern or a dangerous positive match. Keep the original title punctuation and numbering. Set `source` and `sourceQuery` to explain where and how you found it. Set `reviewed` to `true` only after you have checked whether the number is a volume, chapter, pack range, or something else. For an uncertain title, use `Unknown` with null start/end and low confidence; this is safer than inventing coverage.

Example:

```json
{"input":"Example Manga v03 (Digital)","source":"Nyaa","sourceQuery":"v03","reviewed":true,"expected":{"title":"Example Manga","unitType":"Volume","start":"03","end":"03","isPack":false,"confidence":"High","sourceHint":"Digital"}}
```

Before opening a PR, run:

```bash
python3 scripts/check_manga_fixtures.py
python3 -m unittest discover -s tests/Parser -p 'test_*.py'
```

Run `MangaReleaseParserFixture` in the .NET test suite as well. A change that would interpret an unrelated release as a valid volume or chapter is a false positive and blocks automatic acquisition. Include the intended parse and a short explanation of the naming pattern in the PR.
