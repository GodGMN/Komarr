#!/usr/bin/env python3
"""
Le Guin metadata-completeness benchmark.

Scores Ursula K. Le Guin's catalog in the running librarr-smoke
container on a set of binary metadata fields, and compares each
field's actual coverage to its theoretical ceiling (the count of
books where the data actually exists on OpenLibrary).

Output JSON shape:
{
  "timestamp": "...",
  "author_id": 9,
  "total_books": 249,
  "fields": {
    "cover_on_disk":  {"actual": 187, "ceiling": 213, "delta": -26},
    "description":    {"actual":  42, "ceiling": 198, "delta": -156},
    ...
  },
  "overall_mean_actual":   0.62,
  "overall_mean_ceiling":  0.84
}

Usage:
  python3 scripts/bench_le_guin.py --output journal/bench-pre-cycle1.json
"""

import argparse
import datetime
import hashlib
import json
import os
import pathlib
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

CONTAINER = "librarr-smoke"
HOST = "http://localhost:8787"
AUTHOR_ID = 9  # Le Guin in the smoke DB
OL_BASE = "https://openlibrary.org"
CACHE_DIR = pathlib.Path(__file__).parent / ".bench-cache"


def get_api_key() -> str:
    out = subprocess.run(
        ["docker", "exec", CONTAINER, "sed", "-n",
         r"s/.*<ApiKey>\(.*\)<\/ApiKey>.*/\1/p", "/config/config.xml"],
        capture_output=True, text=True, check=True,
    )
    return out.stdout.strip()


def api_get(path: str, api_key: str) -> object:
    req = urllib.request.Request(f"{HOST}{path}", headers={"X-Api-Key": api_key})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.load(r)


def cover_size(book_id: int) -> int:
    """Return bytes of /config/MediaCover/Books/<id>/cover.jpg, or 0 if absent."""
    out = subprocess.run(
        ["docker", "exec", CONTAINER, "sh", "-c",
         f"stat -c %s /config/MediaCover/Books/{book_id}/cover.jpg 2>/dev/null || echo 0"],
        capture_output=True, text=True, check=False,
    )
    try:
        return int(out.stdout.strip())
    except ValueError:
        return 0


def ol_fetch_cached(url: str) -> dict:
    """Cache OL responses on disk. Uses curl for the actual fetch so we
    inherit the system keychain (macOS Python often lacks a working CA
    bundle and breaks on TLS verify)."""
    CACHE_DIR.mkdir(parents=True, exist_ok=True)
    key = hashlib.sha256(url.encode()).hexdigest()[:24]
    cache_path = CACHE_DIR / f"{key}.json"
    if cache_path.exists():
        try:
            return json.loads(cache_path.read_text())
        except json.JSONDecodeError:
            pass  # stale, refetch
    try:
        out = subprocess.run(
            ["curl", "-fsSL", "--max-time", "30", url],
            capture_output=True, text=True, check=True,
        )
        payload = json.loads(out.stdout)
    except (subprocess.CalledProcessError, json.JSONDecodeError) as e:
        print(f"  ol_fetch failed: {url}: {e}", file=sys.stderr)
        return {}
    cache_path.write_text(json.dumps(payload))
    time.sleep(0.05)  # tiny throttle when re-priming the cache
    return payload


def score_book(book: dict, api_key: str) -> tuple[dict, dict]:
    """Return (actual_scores, ceiling_scores) — each a dict of field→bool.
    Fetches editions for this book via /api/v1/edition?bookId=X (the
    /book and /book?authorId= endpoints don't populate editions in the
    response body — confirmed empirically against the running container)."""
    fbi = book.get("foreignBookId") or ""
    # Per-book editions endpoint; the list endpoint omits editions
    editions = api_get(f"/api/v1/edition?bookId={book['id']}", api_key) or []
    monitored = next((e for e in editions if e.get("monitored")), None)

    # Pull OL-side truth
    work = ol_fetch_cached(f"{OL_BASE}/works/{fbi}.json") if fbi else {}
    ol_eds = ol_fetch_cached(f"{OL_BASE}/works/{fbi}/editions.json?limit=200") if fbi else {}
    ol_ed_entries = (ol_eds or {}).get("entries", []) or []

    cover_bytes = cover_size(book["id"])

    actual = {
        "cover_on_disk": cover_bytes > 5_000,
        "description":   bool((monitored or {}).get("overview")),
        "release_date":  bool(book.get("releaseDate")),
        "page_count":    (monitored or {}).get("pageCount", 0) > 0,
        "publisher":     bool((monitored or {}).get("publisher")),
        "isbn_13":       bool((monitored or {}).get("isbn13")),
        "genres":        len(book.get("genres") or []) > 0,
        "format":        bool((monitored or {}).get("format")),
        "rating":        ((book.get("ratings") or {}).get("value") or 0) > 0,
        "language":      bool((monitored or {}).get("language")),
    }

    def ed_any(field: str) -> bool:
        return any(e.get(field) for e in ol_ed_entries)

    def ed_any_truthy_list(field: str) -> bool:
        return any((e.get(field) or []) for e in ol_ed_entries)

    def ed_any_num(field: str) -> bool:
        return any((e.get(field) or 0) > 0 for e in ol_ed_entries)

    ceiling = {
        "cover_on_disk": bool(work.get("covers")) or any(
            any((c or 0) > 0 for c in (e.get("covers") or [])) for e in ol_ed_entries),
        "description":   bool(work.get("description")) or ed_any("description"),
        "release_date":  bool(work.get("first_publish_date")) or ed_any("publish_date"),
        "page_count":    ed_any_num("number_of_pages"),
        "publisher":     ed_any_truthy_list("publishers"),
        "isbn_13":       ed_any_truthy_list("isbn_13"),
        "genres":        bool(work.get("subjects")),
        "format":        ed_any("physical_format"),
        "rating":        False,  # ratings_average not exposed on /works/{id}.json
        "language":      ed_any_truthy_list("languages"),
    }
    return actual, ceiling


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--output", required=True, help="Path to JSON output")
    ap.add_argument("--limit", type=int, default=0,
                    help="Optional cap on number of books (for fast smoke tests)")
    args = ap.parse_args()

    api_key = get_api_key()
    books = api_get(f"/api/v1/book?authorId={AUTHOR_ID}", api_key)
    if args.limit > 0:
        books = books[:args.limit]
    total = len(books)
    print(f"Scoring {total} books for Le Guin (authorId={AUTHOR_ID})...")

    fields = [
        "cover_on_disk", "description", "release_date", "page_count",
        "publisher", "isbn_13", "genres", "format", "rating", "language",
    ]
    tally_actual = {f: 0 for f in fields}
    tally_ceiling = {f: 0 for f in fields}

    for i, b in enumerate(books, 1):
        actual, ceiling = score_book(b, api_key)
        for f in fields:
            if actual.get(f):
                tally_actual[f] += 1
            if ceiling.get(f):
                tally_ceiling[f] += 1
        if i % 25 == 0 or i == total:
            print(f"  [{i}/{total}] {b.get('title','')[:50]}")

    field_summary = {
        f: {
            "actual": tally_actual[f],
            "ceiling": tally_ceiling[f],
            "delta": tally_actual[f] - tally_ceiling[f],
        } for f in fields
    }

    overall_mean_actual = sum(tally_actual.values()) / (len(fields) * total) if total else 0
    overall_mean_ceiling = sum(tally_ceiling.values()) / (len(fields) * total) if total else 0

    result = {
        "timestamp": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "author_id": AUTHOR_ID,
        "total_books": total,
        "fields": field_summary,
        "overall_mean_actual": round(overall_mean_actual, 4),
        "overall_mean_ceiling": round(overall_mean_ceiling, 4),
    }

    out_path = pathlib.Path(args.output)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(result, indent=2))

    print()
    print(f"{'field':16}  {'actual':>10}  {'ceiling':>10}  {'delta':>6}")
    for f in fields:
        s = field_summary[f]
        print(f"{f:16}  {s['actual']:>6}/{total:<3}  {s['ceiling']:>6}/{total:<3}  {s['delta']:>+6}")
    print()
    print(f"overall mean — actual: {overall_mean_actual:.3f}  ceiling: {overall_mean_ceiling:.3f}")
    print(f"wrote {out_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
