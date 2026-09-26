#!/usr/bin/env python3
"""
PreToolUse:Edit|Write|MultiEdit — blocks `using NzbDrone.Windows;` /
`using NzbDrone.Mono;` from inside NzbDrone.Core/.

Why: only one platform shim is active at runtime. Core/ stays
platform-neutral; DryIoc picks the impl via OsInfo.IsWindows in
Bootstrap.cs.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _lib import allow, deny, new_content, rel_to_repo, run_safely, tool_path

PATTERN = re.compile(r"(?m)^\s*using\s+NzbDrone\.(Windows|Mono)\b")


def main(event: dict) -> None:
    rel = rel_to_repo(tool_path(event))
    if not rel.startswith("src/NzbDrone.Core/"):
        allow()
    if not rel.endswith(".cs"):
        allow()

    content = new_content(event)
    if not content:
        allow()

    m = PATTERN.search(content)
    if m:
        deny(
            f"Core/ must not bind to a platform shim "
            f"(`{m.group(0).strip()}`). Inject the abstraction and let "
            "DryIoc pick the impl via `OsInfo.IsWindows` in "
            "src/NzbDrone.Host/Bootstrap.cs."
        )
    allow()


if __name__ == "__main__":
    run_safely(main)
