#!/usr/bin/env python3
"""
PostToolUse:Write — informational reminder when a new
src/NzbDrone.Core/Datastore/Migration/*.cs file is created.

Always allows; prints to stderr so Claude sees it. The reminder covers
the two things that bite when adding a FluentMigrator file:
  1. Update the matching domain model class.
  2. Verify the migration runs cleanly on both SQLite and Postgres.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _lib import allow, rel_to_repo, run_safely, tool_path


def main(event: dict) -> None:
    rel = rel_to_repo(tool_path(event))
    if not rel.startswith("src/NzbDrone.Core/Datastore/Migration/"):
        allow()
    if not rel.endswith(".cs"):
        allow()

    sys.stderr.write(
        "[migration-pair-reminder] New migration detected: " + rel + "\n"
        "  - Update the matching model class under "
        "src/NzbDrone.Core/{Domain}/Model/.\n"
        "  - Verify the migration runs on BOTH SQLite and Postgres "
        "(date/time and string-default columns have separate Postgres "
        "migrations under Datastore/Migration/PostgresOnly/).\n"
        "  - Bump the migration number to match the file name; gaps "
        "will be flagged at startup by NzbDroneMigrationBase.\n"
    )
    allow()


if __name__ == "__main__":
    run_safely(main)
