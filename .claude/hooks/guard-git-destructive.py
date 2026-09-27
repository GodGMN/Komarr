#!/usr/bin/env python3
"""
PreToolUse:Bash — blocks destructive git operations.

Why: the global Bash tool docs already say "NEVER run destructive git
commands unless the user explicitly requests them". This hook enforces
that mechanically so a careless completion can't slip through.

Bypass: ALLOW_DESTRUCTIVE_GIT=1 in env (after explicit user OK).

Denies:
  - git push --force / -f / +<ref>   to main|master|develop
  - git reset --hard
  - git commit ... --no-verify
  - git push ... --no-verify
  - git checkout .
  - git restore .
  - git clean -f / -fd / -fx
  - git branch -D <name>     (use -d for safe delete)
  - git rebase -i            (interactive; not supported in this harness)
  - git update-ref -d
  - git reflog expire --expire=now / --all
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _lib import allow, deny, env_flag, run_safely, tool_command

PROTECTED_BRANCHES = ("main", "master", "develop")

RULES: list[tuple[re.Pattern, str]] = [
    (
        re.compile(
            r"\bgit\s+push\b[^&;|]*\s(?:--force(?:-with-lease)?|-f|\+\S+)\b"
            r"[^&;|]*\b(?:" + "|".join(PROTECTED_BRANCHES) + r")\b"
        ),
        "force-push to a protected branch (main/master/develop)",
    ),
    (re.compile(r"\bgit\s+reset\s+--hard\b"),               "git reset --hard"),
    (re.compile(r"\bgit\s+commit\b[^&;|]*--no-verify\b"),   "git commit --no-verify"),
    (re.compile(r"\bgit\s+push\b[^&;|]*--no-verify\b"),     "git push --no-verify"),
    (re.compile(r"\bgit\s+checkout\s+\.(?:\s|$)"),          "git checkout ."),
    (re.compile(r"\bgit\s+restore\s+\.(?:\s|$)"),           "git restore ."),
    (re.compile(r"\bgit\s+clean\s+-[a-zA-Z]*f"),            "git clean -f"),
    (re.compile(r"\bgit\s+branch\s+-D\b"),                  "git branch -D (force delete)"),
    (re.compile(r"\bgit\s+rebase\s+-i\b"),                  "git rebase -i (interactive)"),
    (re.compile(r"\bgit\s+update-ref\s+-d\b"),              "git update-ref -d"),
    (re.compile(r"\bgit\s+reflog\s+expire\b[^&;|]*--expire=now"),
        "git reflog expire --expire=now"),
]


def main(event: dict) -> None:
    cmd = tool_command(event)
    if not cmd:
        allow()
    if env_flag("ALLOW_DESTRUCTIVE_GIT"):
        allow()

    for pat, label in RULES:
        if pat.search(cmd):
            deny(
                f"Destructive git operation blocked: {label}\n"
                f"Command: {cmd.strip()}\n"
                "These operations can lose work. Ask the user for explicit "
                "confirmation; once granted, re-run with "
                "ALLOW_DESTRUCTIVE_GIT=1 in env for that single command. "
                "Prefer a safer alternative when possible (e.g. `git "
                "branch -d` instead of `-D`, `git revert` instead of "
                "`reset --hard`, a new commit instead of `--amend`)."
            )
    allow()


if __name__ == "__main__":
    run_safely(main)
