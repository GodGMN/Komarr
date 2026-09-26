#!/usr/bin/env bash
# PostToolUse:Edit|Write|MultiEdit — run `eslint --fix` on a single
# frontend JS/TS file.
#
# Fail-open. Skipped if `frontend/node_modules/.bin/eslint` is missing
# (a `yarn install` from repo root will install it).
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

# Only the frontend code extensions; skip build/, __mocks__, node_modules.
case "$FILE" in
    */frontend/build/*|*/node_modules/*|*__mocks__*) exit 0 ;;
    */frontend/*.js|*/frontend/*.jsx|*/frontend/*.ts|*/frontend/*.tsx) : ;;
    *) exit 0 ;;
esac
[ -f "$FILE" ] || exit 0

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ESLINT="$REPO_ROOT/frontend/node_modules/.bin/eslint"
if [ ! -x "$ESLINT" ]; then
    >&2 echo "[format-js-ts] eslint not installed (run 'yarn install'); skipping."
    exit 0
fi

timeout 25 "$ESLINT" \
    --config "$REPO_ROOT/frontend/.eslintrc.js" \
    --ignore-path "$REPO_ROOT/frontend/.eslintignore" \
    --no-error-on-unmatched-pattern \
    --fix \
    "$FILE" >/dev/null 2>&1 || true

exit 0
