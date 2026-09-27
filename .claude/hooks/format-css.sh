#!/usr/bin/env bash
# PostToolUse:Edit|Write|MultiEdit — run `stylelint --fix` on a single
# frontend CSS file.
#
# Fail-open. Skipped if `frontend/node_modules/.bin/stylelint` is missing.
set -u

EVENT="$(cat 2>/dev/null || true)"
FILE="$(printf '%s' "$EVENT" | /usr/bin/python3 -c '
import json,sys
try:
    e=json.loads(sys.stdin.read() or "{}")
    print((e.get("tool_input") or {}).get("file_path","") or "")
except Exception:
    print("")
' 2>/dev/null)"

case "$FILE" in
    */node_modules/*) exit 0 ;;
    */frontend/*.css) : ;;
    *) exit 0 ;;
esac
[ -f "$FILE" ] || exit 0

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
STYLELINT="$REPO_ROOT/frontend/node_modules/.bin/stylelint"
if [ ! -x "$STYLELINT" ]; then
    >&2 echo "[format-css] stylelint not installed (run 'yarn install'); skipping."
    exit 0
fi

timeout 25 "$STYLELINT" \
    --config "$REPO_ROOT/frontend/.stylelintrc" \
    --fix \
    "$FILE" >/dev/null 2>&1 || true

exit 0
