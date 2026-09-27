#!/usr/bin/env bash
set -euo pipefail

old_image=${1:-ghcr.io/godgmn/komarr:v0.1.0-alpha.1}
new_image=${2:-komarr:local}
name="komarr-upgrade-$$"
data_dir=$(mktemp -d)

cleanup() {
  docker stop "$name" >/dev/null 2>&1 || true
  rm -rf "$data_dir"
}
trap cleanup EXIT

start_container() {
  docker run -d --rm --platform linux/amd64 --name "$name" --user "$(id -u):$(id -g)" \
    -e HOME=/tmp -p 127.0.0.1::8787 -v "$data_dir:/config" "$1" >/dev/null
  port=$(docker port "$name" 8787/tcp | sed -n 's/.*://p' | head -1)
  for attempt in $(seq 1 120); do
    if curl --silent --fail --output /dev/null "http://127.0.0.1:$port/ping"; then
      return
    fi
    sleep 1
  done
  docker logs --tail 40 "$name" >&2
  exit 1
}

start_container "$old_image"
docker stop "$name" >/dev/null
for attempt in $(seq 1 40); do
  if ! docker inspect "$name" >/dev/null 2>&1; then
    break
  fi
  sleep 0.25
done
python3 - "$data_dir/komarr.db" <<'PY'
import sqlite3
import sys

with sqlite3.connect(sys.argv[1]) as db:
    db.execute('INSERT INTO BookIdMapping (GoodreadsId, Confidence, Source, CreatedUtc) VALUES (?, ?, ?, ?)',
               ('komarr-upgrade-sentinel', 1, 'smoke', '2026-01-01'))
PY
start_container "$new_image"
python3 - "$data_dir" "http://127.0.0.1:$port" <<'PY'
import pathlib
import sqlite3
import sys
import urllib.request
import xml.etree.ElementTree as ET

data_dir = pathlib.Path(sys.argv[1])
base_url = sys.argv[2]
with sqlite3.connect(data_dir / 'komarr.db') as db:
    assert db.execute("SELECT count(*) FROM BookIdMapping WHERE GoodreadsId = 'komarr-upgrade-sentinel'").fetchone()[0] == 1
    assert db.execute("SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'Authors'").fetchone()[0] == 1
    assert db.execute("SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'Manga'").fetchone()[0] == 1
key = ET.parse(data_dir / 'config.xml').getroot().findtext('ApiKey')
request = urllib.request.Request(base_url + '/api/v1/manga', headers={'X-Api-Key': key})
with urllib.request.urlopen(request, timeout=10) as response:
    assert response.status == 200
PY
echo 'Existing Komarr book data and manga API survived the schema upgrade.'
