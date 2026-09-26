#!/usr/bin/env bash
# SessionStart — print the top-5 gotchas once per session so Claude has
# the right priors before touching the tree. Written to stderr (visible
# to the model per the hook spec).
set -u

>&2 cat <<'EOF'
[readarr-hooks] active — top gotchas:
  1. Namespaces are NzbDrone.* even though csprojs are Readarr.* (Directory.Build.props:97-99).
  2. Every query must work on BOTH SQLite and Postgres — use WhereBuilderSqlite/Postgres.
  3. Parser.cs (~905 lines of regex) and BookInfoProxy.cs are FRAGILE — env CLAUDE_ACK_FRAGILE=1 required to edit.
  4. Frontend HTTP only via createAjaxRequest (no fetch / axios / XMLHttpRequest).
  5. Upstream is ARCHIVED (2025-06-27). This is a fork; CI = GitHub Actions, not Azure.
  Bypass envs (use sparingly, with user OK):
    CLAUDE_ACK_FRAGILE=1        edit Parser.cs / Stylecop.ruleset / Directory.Build.props / BookInfoProxy.cs
    ALLOW_DESTRUCTIVE_GIT=1     allow one force-push / reset --hard / --no-verify
    ALLOW_TEXT_GREP=1           allow one grep/rg over code files
EOF

exit 0
