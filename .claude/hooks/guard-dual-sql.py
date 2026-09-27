#!/usr/bin/env python3
"""
PreToolUse:Edit|Write|MultiEdit — blocks raw `SELECT ... FROM` /
`db.Query<T>("...")` / `db.Execute("...")` style SQL in .cs that doesn't
go through WhereBuilderSqlite / WhereBuilderPostgres.

Why: Readarr runs on both SQLite and Postgres. String-SQL that uses
SQLite-only syntax (LIMIT/OFFSET dialect, double-quoted identifiers,
strftime, etc.) silently breaks on Postgres. WhereBuilder* abstracts
the predicate translation.

Heuristic — denies if BOTH:
  1. The new content has a `db.Query<...>("...")` / `db.Execute("...")`
     style call OR a raw string literal containing `SELECT ... FROM`.
  2. The same file/diff has NO mention of `WhereBuilderSqlite`,
     `WhereBuilderPostgres`, `SqlBuilder`, or `IDatabase.Query` (the
     Servarr ORM wrapper which routes through the right dialect).

Skip-list: migration files (Datastore/Migration/*.cs) since FluentMigrator
emits dialect-aware SQL on its own.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _lib import allow, deny, new_content, rel_to_repo, run_safely, tool_path

# Catch: db.Query<T>("..."), db.Execute("..."), conn.Query("...")
# Look for Dapper-style call sites with a string literal first arg.
DAPPER_CALL = re.compile(
    r"\.(Query(?:Async)?(?:<[^>]+>)?|Execute(?:Async)?(?:Scalar(?:Async)?)?)"
    r"\s*\(\s*[$@]?\""
)
# Catch: standalone string literals containing SELECT ... FROM.
RAW_SELECT = re.compile(r"""\"[^\"]*\bSELECT\b[^\"]*\bFROM\b[^\"]*\"""", re.IGNORECASE)

SAFE_MARKERS = (
    "WhereBuilderSqlite",
    "WhereBuilderPostgres",
    "SqlBuilder",
    "IDatabase",       # NzbDrone.Core/Datastore wrapper
    "BasicRepository", # base repo type
)


def main(event: dict) -> None:
    rel = rel_to_repo(tool_path(event))
    if not rel.endswith(".cs"):
        allow()
    # FluentMigrator owns dialect translation in migration files.
    if "/Datastore/Migration/" in rel:
        allow()

    content = new_content(event)
    if not content:
        allow()

    has_raw = bool(DAPPER_CALL.search(content) or RAW_SELECT.search(content))
    if not has_raw:
        allow()

    if any(marker in content for marker in SAFE_MARKERS):
        allow()

    deny(
        "Raw SQL detected without WhereBuilder/SqlBuilder context.\n"
        "Readarr runs on BOTH SQLite and Postgres. Wrap predicate "
        "translation through `WhereBuilderSqlite` / `WhereBuilderPostgres` "
        "or use `IDatabase.Query` from BasicRepository — see "
        "`src/NzbDrone.Core/Datastore/SqlBuilderExtensions.cs` and any "
        "existing `*Repository.cs` for the pattern.\n"
        "If this is intentional (e.g. a one-shot diagnostic), re-issue "
        "with the SqlBuilder import in the same diff to silence the guard."
    )


if __name__ == "__main__":
    run_safely(main)
