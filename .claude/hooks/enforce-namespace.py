#!/usr/bin/env python3
"""
PreToolUse:Edit|Write|MultiEdit — blocks `using Readarr.*;` /
`namespace Readarr.*` in C# files.

Why: csprojs are `Readarr.*` but C# namespaces remain `NzbDrone.*`
(Directory.Build.props:97-99 sets RootNamespace=NzbDrone.* deliberately).
`using Readarr.Core;` will not compile. Allowed exceptions are the API
projects where the namespace ALSO is `Readarr.Api.V1` / `Readarr.Api.V3`.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _lib import allow, deny, matches_any, new_content, run_safely, tool_path

ALLOWED_PREFIXES = ("Readarr.Api.V1", "Readarr.Api.V3", "Readarr.Http")

# Catches: `using Readarr.Foo.Bar;`, `using static Readarr.Foo;`,
# `namespace Readarr.Foo.Bar`, `using Readarr.Foo = ...;`
# Group 1 captures the full dotted path (e.g. `Readarr.Api.V1.Books`).
PATTERN = re.compile(
    r"(?m)^\s*(?:using(?:\s+static)?\s+(?:\w+\s*=\s*)?|namespace\s+)(Readarr\.[\w\.]+)"
)


def main(event: dict) -> None:
    path = tool_path(event)
    if not matches_any(path, (".cs",)):
        allow()

    content = new_content(event)
    if not content:
        allow()

    bad: list[str] = []
    for m in PATTERN.finditer(content):
        ns = m.group(1)
        if not any(ns == p or ns.startswith(p + ".") for p in ALLOWED_PREFIXES):
            bad.append(m.group(0).strip())

    if bad:
        deny(
            "Namespace violation. The csproj assemblies are `Readarr.*` but "
            "C# namespaces remain `NzbDrone.*` (Directory.Build.props:97-99).\n"
            "Use `using NzbDrone.Core;` not `using Readarr.Core;`.\n"
            "Allowed Readarr.* prefixes: " + ", ".join(ALLOWED_PREFIXES) + "\n"
            "Offending lines:\n  " + "\n  ".join(bad[:5])
        )
    allow()


if __name__ == "__main__":
    run_safely(main)
