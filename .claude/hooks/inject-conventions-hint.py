#!/usr/bin/env python3
"""
UserPromptSubmit — if the user's prompt mentions a topic that has a known
gotcha, inject a one-line reminder.

Output contract: stdout is appended to the prompt as additional context
(per Claude Code UserPromptSubmit hook spec). We emit at most ONE
<readarr-hint> block to avoid clutter — matches are deduped.

Trigger phrases are short and case-insensitive. Designed to be quiet:
unrelated prompts produce no output.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _lib import allow, run_safely

# (regex, hint) pairs — first match per category wins, then dedup.
RULES: list[tuple[re.Pattern, str]] = [
    (re.compile(r"\b(?:rename|namespace|using\s+Readarr)\b", re.I),
     "Namespaces are `NzbDrone.*` even though csprojs are `Readarr.*` "
     "(Directory.Build.props:97-99). Exception: `Readarr.Api.V1`."),
    (re.compile(r"\b(?:search|find|grep|look\s+for)\b", re.I),
     "Use `ast-grep --pattern '...'` for code searches; grep/rg is for "
     "logs/configs."),
    (re.compile(r"\b(?:fetch\b|axios|XMLHttpRequest|new\s+Request\()", re.I),
     "Frontend HTTP must go through `createAjaxRequest` "
     "(frontend/src/Utilities/createAjaxRequest.js)."),
    (re.compile(r"\b(?:migration|new\s+column|fluentmigrator|alter\s+table)\b", re.I),
     "Dual SQLite + Postgres — use WhereBuilderSqlite / WhereBuilderPostgres "
     "and check Datastore/Migration/PostgresOnly/ for the matching PG fix."),
    (re.compile(r"--no-verify|force.?push|reset\s+--hard|branch\s+-D", re.I),
     "Destructive git op — get explicit user OK and set "
     "ALLOW_DESTRUCTIVE_GIT=1 only for that single command."),
    (re.compile(r"\bParser\.cs\b", re.I),
     "Parser.cs is ~905 lines of regex with no golden corpus — edits "
     "require CLAUDE_ACK_FRAGILE=1 and great care."),
    (re.compile(r"\bBookInfoProxy\b", re.I),
     "BookInfoProxy is the active metadata seam being refactored in "
     "MASTER-PLAN.md Phase 2-5. Coordinate with the migration plan."),
    (re.compile(r"\bwin-arm64\b", re.I),
     "win-arm64 is intentionally excluded from the RID list "
     "(Directory.Build.props:11)."),
]


def main(event: dict) -> None:
    prompt = (event.get("prompt") or "").strip()
    if not prompt:
        allow()

    hints: list[str] = []
    seen: set[str] = set()
    for pat, hint in RULES:
        if pat.search(prompt) and hint not in seen:
            hints.append(hint)
            seen.add(hint)
        if len(hints) >= 3:  # cap noise
            break

    if hints:
        sys.stdout.write("<readarr-hint>\n")
        for h in hints:
            sys.stdout.write(f"- {h}\n")
        sys.stdout.write("</readarr-hint>\n")
    allow()


if __name__ == "__main__":
    run_safely(main)
