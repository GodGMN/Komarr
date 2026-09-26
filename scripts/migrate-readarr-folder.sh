#!/usr/bin/env bash
# migrate-readarr-folder.sh
#
# Automates Recipe A from docs/migrating-from-readarr.md: copy an existing
# Readarr AppData folder to the Librarr default location, leaving the
# Readarr folder untouched so the user can roll back by just starting
# the old binary.
#
# Usage:
#   ./scripts/migrate-readarr-folder.sh                 # use default paths
#   ./scripts/migrate-readarr-folder.sh --src=...       # override source
#   ./scripts/migrate-readarr-folder.sh --dst=...       # override destination
#   ./scripts/migrate-readarr-folder.sh --dry-run       # show what would happen, copy nothing
#   ./scripts/migrate-readarr-folder.sh --force         # skip the safety pause + the running-process check
#
# Tested on macOS (zsh + bash) and Linux. Refuses to run on Windows;
# use migrate-readarr-folder.ps1 there.

set -euo pipefail

# ── Defaults ────────────────────────────────────────────────────────────
# On both Linux and macOS, Readarr/Librarr resolve SpecialFolder.ApplicationData
# to $XDG_CONFIG_HOME (falling back to ~/.config). The folder name is the
# only thing that changed between Readarr and Librarr.
default_root="${XDG_CONFIG_HOME:-$HOME/.config}"
src="$default_root/Readarr"
dst="$default_root/Librarr"
dry_run=0
force=0

# ── Argv parsing ────────────────────────────────────────────────────────
for arg in "$@"; do
    case "$arg" in
        --src=*)   src="${arg#--src=}" ;;
        --dst=*)   dst="${arg#--dst=}" ;;
        --dry-run) dry_run=1 ;;
        --force)   force=1 ;;
        -h|--help)
            sed -n '2,18p' "$0"
            exit 0
            ;;
        *)
            echo "error: unknown argument '$arg'" >&2
            exit 2
            ;;
    esac
done

# ── Platform guard ──────────────────────────────────────────────────────
case "$(uname -s)" in
    Linux|Darwin) ;;
    *) echo "error: this script targets Linux/macOS. On Windows, use scripts/migrate-readarr-folder.ps1." >&2
       exit 1 ;;
esac

# ── Pre-flight ──────────────────────────────────────────────────────────
echo "Source:      $src"
echo "Destination: $dst"
echo

if [[ ! -d "$src" ]]; then
    echo "error: source folder '$src' does not exist." >&2
    echo "  Tip: pass --src=/path/to/your/Readarr if your data folder is elsewhere." >&2
    exit 1
fi

if [[ ! -f "$src/config.xml" ]]; then
    echo "warning: '$src/config.xml' not found — the source may not be a Readarr/Librarr data folder." >&2
    echo "  Continuing anyway since you pointed me here." >&2
fi

if [[ -e "$dst" ]]; then
    echo "error: destination '$dst' already exists." >&2
    echo "  Refusing to merge into an existing folder. Move/rename '$dst' first, or pass --dst=... to use a different target." >&2
    exit 1
fi

# Best-effort running-process check. Both Readarr and Librarr binaries
# are named Readarr.* (binary names were intentionally kept at Phase 0),
# so the same probe covers both.
if [[ $force -eq 0 ]]; then
    if pgrep -f "Readarr\\.(exe|Console)" >/dev/null 2>&1 \
       || pgrep -x "Readarr" >/dev/null 2>&1; then
        echo "error: a Readarr/Librarr process is running. Stop it before migrating, or rerun with --force." >&2
        exit 1
    fi
fi

# Final confirmation unless --force or --dry-run.
if [[ $force -eq 0 && $dry_run -eq 0 ]]; then
    echo "About to copy '$src' to '$dst' (Readarr folder untouched)."
    printf "Continue? [y/N] "
    read -r reply
    case "$reply" in
        y|Y|yes|YES) ;;
        *) echo "Aborted."; exit 0 ;;
    esac
fi

# ── Copy ────────────────────────────────────────────────────────────────
# cp -a (alias for -dpR) preserves symlinks, permissions, timestamps. Use
# it on both Linux (GNU cp) and macOS (BSD cp) — the flag has equivalent
# semantics on both. The trailing /. inside cp -a is intentional: it
# means "contents of $src into a freshly-created $dst", regardless of
# whether $src ends with a slash.

if [[ $dry_run -eq 1 ]]; then
    echo "[dry-run] would: cp -a '$src/.' '$dst'"
    echo "[dry-run] no files copied."
    exit 0
fi

mkdir -p "$dst"
cp -a "$src/." "$dst"

# ── Sanity check the copy ───────────────────────────────────────────────
src_size=$(du -sk "$src" 2>/dev/null | awk '{print $1}')
dst_size=$(du -sk "$dst" 2>/dev/null | awk '{print $1}')

if [[ "${src_size:-0}" -ne 0 && "${dst_size:-0}" -ne 0 ]]; then
    pct=$(( (dst_size * 100) / src_size ))
    echo
    echo "Copy complete. Source: ${src_size}K, destination: ${dst_size}K (~${pct}% match)."
    if [[ $pct -lt 95 ]]; then
        echo "warning: destination is noticeably smaller than source. Inspect '$dst' manually." >&2
    fi
fi

cat <<EOF

Done. Next steps:
  1. Start Librarr — it picks up the copy at '$dst' on first launch.
  2. Verify the library, indexers, and download clients look right.
  3. Run Settings → Metadata → Switch Metadata Source.
  4. Once you're satisfied, you can delete '$src' — but the Readarr
     folder is your rollback parachute, so consider keeping it for a
     few days.

See docs/migrating-from-readarr.md for the full migration guide.
EOF
