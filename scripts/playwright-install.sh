#!/usr/bin/env bash
# playwright-install.sh
#
# One-shot bootstrap for the Readarr.Playwright.Test browser bundle.
# Restores NuGet packages so the bundled Playwright CLI is on disk,
# then invokes it to download Chromium.
#
# Idempotent — safe to re-run. Cached at ~/.cache/ms-playwright/
# (Linux) or ~/Library/Caches/ms-playwright/ (macOS).

set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
project="$repo_root/src/NzbDrone.Playwright.Test/Readarr.Playwright.Test.csproj"

if [[ ! -f "$project" ]]; then
    echo "error: Playwright test project not found at $project" >&2
    exit 1
fi

case "$(uname -s)" in
    Linux|Darwin) ;;
    *) echo "error: this script targets Linux/macOS. On Windows, use scripts/playwright-install.ps1." >&2
       exit 1 ;;
esac

echo "Restoring Playwright test project so the CLI is on disk..."
dotnet restore "$project"

# Which driver we hand the `install` command to decides which Chromium
# revision lands in the cache, so it has to be the same driver the tests
# will later launch through.
#
# That is not a given. _tests/ is shared, never cleaned, and keyed by target
# framework and RID, so it accumulates: after the .NET 6 -> 10 migration this
# tree held four drivers, two of them 1.40.0 leftovers wanting chromium-1091.
# The old `find -print -quit` took whichever the walk reached first, which was
# a stale one — it installed 1091 while the tests asked their 1.55.0 driver
# for 1187, and the run died with "Executable doesn't exist". The README used
# to write this up as a Playwright packaging quirk. It was this function.
#
# So: pick by version, matching the pin, and refuse rather than guess.
pinned_version=$(
    sed -n 's/.*PackageVersion Include="Microsoft.Playwright" Version="\([^"]*\)".*/\1/p' \
        "$repo_root/src/Directory.Packages.props" | head -1
)

if [[ -z "$pinned_version" ]]; then
    echo "error: could not read the Microsoft.Playwright pin from src/Directory.Packages.props." >&2
    exit 1
fi

# The driver carries a prerelease suffix (1.55.0-beta-...) against an assembly
# version of 1.55.0, so compare on major.minor — same rule as the runtime
# check in _AssemblyGate.AssertDriverMatchesClient.
pinned_series="${pinned_version%.*}"

driver_series() {
    sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([0-9]*\.[0-9]*\).*/\1/p' \
        "$(dirname "$1")/package.json" | head -1
}

find_cli() {
    local candidate
    while IFS= read -r candidate; do
        if [[ "$(driver_series "$candidate")" == "$pinned_series" ]]; then
            echo "$candidate"
            return
        fi
    done < <(find "$repo_root/_tests" "$repo_root/src/NzbDrone.Playwright.Test/bin" \
                  -path "*/.playwright/package/cli.js" 2>/dev/null)
}

echo "Locating the Playwright $pinned_version driver..."
playwright_cli=$(find_cli)
if [[ -z "$playwright_cli" ]]; then
    # The bundle isn't laid down until at least one build runs.
    echo "Triggering a build so Playwright drops its CLI..."
    dotnet build "$project" -nologo --verbosity quiet
    playwright_cli=$(find_cli)
fi

if [[ -z "$playwright_cli" ]]; then
    echo "error: no .playwright driver matching the pinned $pinned_version found under _tests/ or bin/." >&2
    echo "       Drivers present:" >&2
    while IFS= read -r candidate; do
        echo "         $(driver_series "$candidate")  $candidate" >&2
    done < <(find "$repo_root/_tests" "$repo_root/src/NzbDrone.Playwright.Test/bin" \
                  -path "*/.playwright/package/cli.js" 2>/dev/null)
    echo "       If they are all stale, delete _tests/ and rebuild." >&2
    exit 1
fi

echo "Using $playwright_cli"

# Drive the Node CLI directly rather than the playwright.ps1 wrapper: that
# wrapper needs PowerShell, which is not a reasonable thing to require on a
# Linux or macOS dev box. Playwright ships its own Node alongside the CLI, so
# prefer that and fall back to whatever node is on PATH.
bundled_node=$(find "$(dirname "$playwright_cli")/../node" -maxdepth 2 -name node -type f -print -quit 2>/dev/null || true)
node_bin="${bundled_node:-$(command -v node || true)}"

if [[ -z "$node_bin" ]]; then
    echo "error: no node binary found (neither bundled nor on PATH)." >&2
    exit 1
fi

# A dev box that has ever opened a browser already has Chromium's shared
# libraries; a CI container does not, and the failure there is a launch
# that dies naming a missing .so rather than anything about Playwright.
# `install-deps` needs root, so it is opt-in rather than the default --
# asking for a sudo password on someone's laptop to run a test suite is
# not a reasonable default.
if [[ "${1:-}" == "--with-deps" ]]; then
    echo "Installing Chromium's system dependencies (needs root)..."
    "$node_bin" "$playwright_cli" install-deps chromium
fi

echo "Installing Chromium..."
"$node_bin" "$playwright_cli" install chromium

echo
echo "Done. You can now run:"
echo "  READARR_RUN_PLAYWRIGHT=1 dotnet test src/NzbDrone.Playwright.Test/"
