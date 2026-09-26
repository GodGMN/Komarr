#!/usr/bin/env bash
# PostToolUse:Edit|Write|MultiEdit — run `dotnet format whitespace` on the
# single .cs file Claude just touched.
#
# Fail-open: any error → exit 0. Skipped if dotnet is missing or the file
# isn't a .cs file. Scoped to the file (not the whole solution) so worst-
# case wall time stays inside the 30s timeout.
set -u

# Read the hook event from stdin and pull file_path.
EVENT="$(cat 2>/dev/null || true)"
FILE="$(printf '%s' "$EVENT" | /usr/bin/python3 -c '
import json,sys
try:
    e=json.loads(sys.stdin.read() or "{}")
    print((e.get("tool_input") or {}).get("file_path","") or "")
except Exception:
    print("")
' 2>/dev/null)"

# Skip when this hook does not apply.
case "$FILE" in
    *.cs) : ;;
    *)    exit 0 ;;
esac
[ -f "$FILE" ] || exit 0
command -v dotnet >/dev/null 2>&1 || exit 0

# Find the project that owns this file by walking up to the nearest .csproj.
PROJECT_DIR="$(dirname "$FILE")"
while [ "$PROJECT_DIR" != "/" ] && ! compgen -G "$PROJECT_DIR/*.csproj" >/dev/null; do
    PROJECT_DIR="$(dirname "$PROJECT_DIR")"
done
CSPROJ="$(compgen -G "$PROJECT_DIR/*.csproj" 2>/dev/null | head -n1 || true)"
[ -n "$CSPROJ" ] || exit 0

# `dotnet format whitespace` is the cheapest leg (no analyzers); style/
# analyzers are enforced by the build itself via EnforceCodeStyleInBuild.
timeout 25 dotnet format whitespace "$CSPROJ" \
    --include "$FILE" \
    --verbosity quiet >/dev/null 2>&1 || true

exit 0
