#!/usr/bin/env python3
"""
PreToolUse:Edit|Write|MultiEdit — requires explicit ack to edit files
flagged as fragile in ARCHITECTURE.md §8 and CLAUDE.md.

Bypass: set CLAUDE_ACK_FRAGILE=1 in env for that session.

Files guarded:
  - Parser/Parser.cs            ~905 lines of regex; no golden corpus.
  - Stylecop.ruleset            File header literally says "Rules for Radarr".
  - Directory.Build.props       RootNamespace remap + RID list + strict flags.
  - BookInfoProxy.cs            Metadata seam being actively refactored in
                                MASTER-PLAN.md Phase 2-5.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _lib import allow, deny, env_flag, rel_to_repo, run_safely, tool_path

FRAGILE = {
    "src/NzbDrone.Core/Parser/Parser.cs":
        "Parser.cs is ~905 lines of regex with no golden-corpus tests. "
        "Changes silently break release-name parsing.",
    "src/Stylecop.ruleset":
        "Stylecop.ruleset header says 'Rules for Radarr' on purpose "
        "(fork heritage). Don't 'fix' the title.",
    "src/Directory.Build.props":
        "Directory.Build.props holds the RootNamespace remap "
        "(Readarr.* → NzbDrone.*), the strict build flags "
        "(TreatWarningsAsErrors), and the intentionally-narrow RID list "
        "(no win-arm64). Touch with care.",
    "src/NzbDrone.Core/MetadataSource/BookInfo/BookInfoProxy.cs":
        "BookInfoProxy.cs is the active metadata seam being refactored in "
        "MASTER-PLAN.md Phase 2-5. Changes here may collide with the "
        "neutral-DTO migration.",
}


def main(event: dict) -> None:
    rel = rel_to_repo(tool_path(event))
    if rel in FRAGILE:
        if env_flag("CLAUDE_ACK_FRAGILE"):
            allow()
        deny(
            f"FRAGILE FILE: {rel}\n{FRAGILE[rel]}\n"
            "If you really mean to edit this, ask the user; once "
            "they confirm, re-run with CLAUDE_ACK_FRAGILE=1 in env."
        )
    allow()


if __name__ == "__main__":
    run_safely(main)
