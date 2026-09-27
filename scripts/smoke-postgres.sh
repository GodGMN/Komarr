#!/usr/bin/env bash
set -euo pipefail

image=${1:-komarr:local}
suffix="$$"
network="komarr-pg-smoke-$suffix"
database="komarr-pg-$suffix"
app="komarr-app-$suffix"
data_dir=$(mktemp -d)
postgres_password=$(openssl rand -hex 20)

cleanup() {
  docker stop "$app" "$database" >/dev/null 2>&1 || true
  docker network rm "$network" >/dev/null 2>&1 || true
  rm -rf "$data_dir"
}
trap cleanup EXIT

docker network create "$network" >/dev/null
docker run -d --rm --name "$database" --network "$network" \
  -e POSTGRES_PASSWORD="$postgres_password" postgres:15-alpine >/dev/null
for attempt in $(seq 1 60); do
  if docker exec "$database" pg_isready -U postgres >/dev/null 2>&1; then
    break
  fi
  sleep 1
done
docker exec "$database" pg_isready -U postgres >/dev/null
for name in komarr-main komarr-log komarr-cache; do
  docker exec "$database" createdb -U postgres "$name"
done

docker run -d --rm --name "$app" --network "$network" \
  --user "$(id -u):$(id -g)" -e HOME=/tmp \
  -e Komarr__Postgres__Host="$database" \
  -e Komarr__Postgres__User=postgres \
  -e Komarr__Postgres__Password="$postgres_password" \
  -p 127.0.0.1::8787 -v "$data_dir:/config" "$image" >/dev/null
port=$(docker port "$app" 8787/tcp | sed -n 's/.*://p' | head -1)
for attempt in $(seq 1 120); do
  if curl --silent --fail --output /dev/null "http://127.0.0.1:$port/ping"; then
    break
  fi
  if ! docker inspect "$app" >/dev/null 2>&1; then
    echo 'Komarr exited before PostgreSQL startup completed' >&2
    exit 1
  fi
  sleep 1
done
curl --silent --fail --output /dev/null "http://127.0.0.1:$port/ping"

legacy=$(docker exec "$database" psql -U postgres -d komarr-main -tA -c \
  "SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('Authors','Books','BookFiles','Editions','AuthorMetadata','Series','SeriesBookLink','BookIdMapping','Narrators','EditionNarrators')")
[[ "$legacy" == 0 ]] || { echo "Fresh PostgreSQL schema has $legacy book tables" >&2; exit 1; }
manga=$(docker exec "$database" psql -U postgres -d komarr-main -tA -c \
  "SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('Manga','MangaFiles','MangaDownloads')")
[[ "$manga" == 3 ]] || { echo 'Fresh PostgreSQL manga tables missing' >&2; exit 1; }

python3 - "http://127.0.0.1:$port" "$data_dir/config.xml" <<'PY'
import sys
import urllib.request
import xml.etree.ElementTree as ET

base_url, config_path = sys.argv[1:]
key = ET.parse(config_path).getroot().findtext('ApiKey')
request = urllib.request.Request(base_url + '/api/v1/manga', headers={'X-Api-Key': key})
with urllib.request.urlopen(request, timeout=10) as response:
    assert response.status == 200
PY
node scripts/smoke-browser.mjs "http://127.0.0.1:$port"
echo 'Fresh PostgreSQL boot, manga API, navigation, and schema passed.'
