"""
Shared helpers for Readarr Claude Code hooks.

I/O contract:
  stdin   = JSON envelope from Claude Code (`{tool_name, tool_input, cwd, ...}`)
  exit 0  = allow (silent)
  exit 2  = DENY; stderr is the message Claude will see
  any other exit / exception = fail-open (allow, stderr logged to errors.log)

Mirrors the convention in ~/.claude/hooks/enforce-llm-delegation.py and
read-once.py: a broken hook MUST NEVER brick a session. Wrap every entry
point with `run_safely(...)`.
"""
from __future__ import annotations

import json
import os
import sys
import traceback
from pathlib import Path
from typing import Callable

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
ERROR_LOG = Path(__file__).resolve().parent / "errors.log"


def read_event() -> dict:
    raw = sys.stdin.read() or "{}"
    return json.loads(raw)


def deny(message: str) -> None:
    sys.stderr.write(message.rstrip() + "\n")
    sys.exit(2)


def allow() -> None:
    sys.exit(0)


def log_error(exc: BaseException) -> None:
    try:
        ERROR_LOG.parent.mkdir(parents=True, exist_ok=True)
        with ERROR_LOG.open("a", encoding="utf-8") as fh:
            fh.write(traceback.format_exc())
            fh.write("---\n")
    except Exception:
        pass


def run_safely(entry: Callable[[dict], None]) -> None:
    try:
        event = read_event()
        entry(event)
    except SystemExit:
        raise
    except BaseException as exc:  # fail-open
        log_error(exc)
        sys.exit(0)


def tool_path(event: dict) -> str:
    return (event.get("tool_input") or {}).get("file_path", "") or ""


def tool_command(event: dict) -> str:
    return (event.get("tool_input") or {}).get("command", "") or ""


def new_content(event: dict) -> str:
    """
    Returns the text Claude is about to write to the file:
      - Write: tool_input.content
      - Edit:  tool_input.new_string
      - MultiEdit: concatenation of every edits[].new_string
    """
    ti = event.get("tool_input") or {}
    if "content" in ti and ti["content"] is not None:
        return str(ti["content"])
    if "new_string" in ti and ti["new_string"] is not None:
        return str(ti["new_string"])
    edits = ti.get("edits")
    if isinstance(edits, list):
        return "\n".join(str(e.get("new_string", "")) for e in edits)
    return ""


def under_repo(path: str) -> bool:
    if not path:
        return False
    try:
        Path(path).resolve().relative_to(REPO_ROOT)
        return True
    except ValueError:
        return False


def rel_to_repo(path: str) -> str:
    try:
        return str(Path(path).resolve().relative_to(REPO_ROOT))
    except ValueError:
        return path


def matches_any(path: str, suffixes: tuple[str, ...]) -> bool:
    return path.lower().endswith(suffixes)


def env_flag(name: str) -> bool:
    return os.environ.get(name, "").strip() not in ("", "0", "false", "False")
