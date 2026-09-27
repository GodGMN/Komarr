#!/usr/bin/env python3
"""
PreToolUse:Edit|Write|MultiEdit — blocks adding `win-arm64` to
src/Directory.Build.props.

Why: Directory.Build.props:11 lists the supported RIDs. win-arm64 is
intentionally excluded — Windows-on-ARM is unsupported. Adding it
breaks `./build.sh --enable-extra-platforms` on CI.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _lib import allow, deny, new_content, rel_to_repo, run_safely, tool_path

PATTERN = re.compile(r"\bwin-arm64\b")


def main(event: dict) -> None:
    rel = rel_to_repo(tool_path(event))
    if rel != "src/Directory.Build.props":
        allow()
    if PATTERN.search(new_content(event)):
        deny(
            "win-arm64 is intentionally excluded from the RID list "
            "(Directory.Build.props:11). Windows-on-ARM is unsupported. "
            "If support is being added, that's a multi-PR change requiring "
            "test infra updates — confirm with the user first."
        )
    allow()


if __name__ == "__main__":
    run_safely(main)
