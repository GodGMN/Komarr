#!/usr/bin/env python3
"""
PreToolUse:Edit|Write|MultiEdit — blocks `fetch(`, `axios`, raw
`XMLHttpRequest` in frontend code.

Why: jQuery `$.ajax` via `frontend/src/Utilities/createAjaxRequest.js` is
the sole HTTP client. Introducing fetch/axios fragments the auth +
api-base-url plumbing. Allowed in `frontend/build/`, the helper itself,
and `__mocks__/`.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _lib import allow, deny, new_content, rel_to_repo, run_safely, tool_path

ALLOW_PATH_FRAGMENTS = (
    "frontend/build/",
    "frontend/src/Utilities/createAjaxRequest.js",
    "__mocks__/",
    "node_modules/",
)
PATTERNS = [
    (re.compile(r"\bfetch\s*\("), "fetch("),
    (re.compile(r"""\bimport\b[^;]*\bfrom\s*['\"]axios['\"]"""), "axios import"),
    (re.compile(r"""\brequire\s*\(\s*['\"]axios['\"]"""), "axios require"),
    (re.compile(r"\bnew\s+XMLHttpRequest\b"), "new XMLHttpRequest"),
    (re.compile(r"\bwindow\.fetch\b"), "window.fetch"),
]
EXTS = (".js", ".jsx", ".ts", ".tsx")


def main(event: dict) -> None:
    path = tool_path(event)
    rel = rel_to_repo(path)
    if not rel.startswith("frontend/"):
        allow()
    if not path.lower().endswith(EXTS):
        allow()
    if any(frag in rel for frag in ALLOW_PATH_FRAGMENTS):
        allow()

    content = new_content(event)
    if not content:
        allow()

    hits = [label for pat, label in PATTERNS if pat.search(content)]
    if hits:
        deny(
            "Frontend HTTP must go through `createAjaxRequest` "
            "(`frontend/src/Utilities/createAjaxRequest.js`). "
            "Detected: " + ", ".join(hits) + ".\n"
            "Refactor the call to `createAjaxRequest({ url, method, ... })` "
            "or wrap it in a Redux thunk that uses the existing helper."
        )
    allow()


if __name__ == "__main__":
    run_safely(main)
