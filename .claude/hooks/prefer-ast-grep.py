#!/usr/bin/env python3
"""
PreToolUse:Bash — redirect grep/rg over code files to ast-grep.

Why: global rule in ~/.claude/CLAUDE.md says "Always use `ast-grep` for
code searches". Text grep on code produces false positives from comments
and strings; ast-grep is AST-aware.

Bypass: ALLOW_TEXT_GREP=1 in env. Skip if the command appears to target
log/text files (fixtures, .json, .csv, .log).
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _lib import allow, deny, env_flag, run_safely, tool_command

# Identify a grep/rg invocation at the start of the command or after a pipe/&&.
TOOL = re.compile(r"(?:^|[|&;]\s*)(?P<tool>grep|rg|egrep|fgrep)\b")
CODE_TOKEN = re.compile(
    r"""(?P<tok>
        \*\.(?:cs|ts|tsx|js|jsx|cshtml|razor)\b   # *.cs etc.
      | \bsrc/[^\s'"]*\.(?:cs|ts|tsx|js|jsx)\b    # specific code files
      | \bfrontend/src/                            # any frontend code path
      | \bsrc/NzbDrone\.                           # backend project root
      | --include=['"]?\*\.(?:cs|ts|tsx|js|jsx)['"]?
      | --type[= ](?:cs|ts|tsx|js|jsx|csharp|typescript|javascript)\b
    )""",
    re.VERBOSE,
)
TEXT_TOKEN = re.compile(
    r"\.(?:log|json|csv|md|yml|yaml|xml|txt|sql|conf|env|css)\b"
)


def main(event: dict) -> None:
    cmd = tool_command(event)
    if not cmd:
        allow()
    if env_flag("ALLOW_TEXT_GREP"):
        allow()

    tool_match = TOOL.search(cmd)
    if not tool_match:
        allow()

    if not CODE_TOKEN.search(cmd):
        allow()  # not targeting code

    # If the command also lists explicit non-code targets, allow it.
    code_hits  = len(CODE_TOKEN.findall(cmd))
    text_hits  = len(TEXT_TOKEN.findall(cmd))
    if text_hits and text_hits >= code_hits:
        allow()

    tool = tool_match.group("tool")
    deny(
        f"`{tool}` is being used over code files. Use `ast-grep` instead:\n"
        "  ast-grep --pattern 'function $NAME($$$) { $$$ }' src/\n"
        "  ast-grep --pattern 'public class $C : $$$BASE { $$$ }' src/\n"
        "  ast-grep --pattern 'using NzbDrone.Core.$$$' src/\n"
        "Reason: ast-grep matches structural patterns, not text — no false "
        "positives from comments/strings.\n"
        "If a plain text search is actually what you want (CSV/logs/fixtures), "
        "set ALLOW_TEXT_GREP=1 in env for that single call."
    )


if __name__ == "__main__":
    run_safely(main)
