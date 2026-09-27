#!/usr/bin/env bash
set -euo pipefail

image=${1:-komarr:local}
run_browser=${2:-}
name="komarr-smoke-$$"
data_dir=$(mktemp -d)
container_running=0

cleanup() {
  if (( container_running )); then
    docker stop "$name" >/dev/null 2>&1 || true
  fi
  rm -rf "$data_dir"
}
trap cleanup EXIT

start_container() {
  docker run -d --rm \
    --name "$name" \
    --user "$(id -u):$(id -g)" \
    -e HOME=/tmp \
    -p 127.0.0.1::8787 \
    -v "$data_dir:/config" \
    "$image" >/dev/null
  container_running=1
  port=$(docker port "$name" 8787/tcp | sed -n 's/.*://p' | head -1)
  base_url="http://127.0.0.1:$port"
  for attempt in $(seq 1 120); do
    if curl --silent --fail --output /dev/null "$base_url/ping"; then
      return
    fi
    if ! docker inspect "$name" >/dev/null 2>&1; then
      echo "Komarr container exited before it became ready" >&2
      exit 1
    fi
    sleep 1
  done
  docker logs --tail 50 "$name" >&2
  echo "Komarr did not become ready within 120 seconds" >&2
  exit 1
}

check_api() {
  python3 - "$base_url" "$data_dir" "$1" <<'PY'
import json
import pathlib
import sys
import urllib.request
import xml.etree.ElementTree as ET

base_url, data_dir, mode = sys.argv[1:]
data_dir = pathlib.Path(data_dir)
key = ET.parse(data_dir / 'config.xml').getroot().findtext('ApiKey')
assert key, 'Fresh config.xml has no API key'

def request(path, method='GET', body=None):
    payload = None if body is None else json.dumps(body).encode()
    headers = {'X-Api-Key': key}
    if payload is not None:
        headers['Content-Type'] = 'application/json'
    req = urllib.request.Request(base_url + path, data=payload, headers=headers, method=method)
    with urllib.request.urlopen(req, timeout=10) as response:
        return response.status, json.load(response)

with urllib.request.urlopen(base_url + '/', timeout=10) as response:
    html = response.read().decode()
    assert response.status == 200 and '<title>Komarr</title>' in html, 'UI did not load'

for path in ('/api/v1/indexer', '/api/v1/command'):
    status, _ = request(path)
    assert status == 200, f'{path} returned {status}'

status, updates = request('/api/v1/update')
assert status == 200 and updates == [], 'Inherited update feed is active'

status, config = request('/api/v1/config/host')
assert status == 200 and config['updateMechanism'].lower() == 'external'
if mode == 'save':
    assert (data_dir / 'komarr.db').is_file(), 'Fresh Komarr database missing'
    config['instanceName'] = 'Smoke Komarr'
    status, _ = request('/api/v1/config/host/1', 'PUT', config)
    assert status == 202, f'Host settings save returned {status}'
else:
    assert config['instanceName'] == 'Smoke Komarr', 'Host setting did not survive restart'
print(f'{mode}: UI, APIs, and settings OK')
PY
}

start_container
check_api save
if [[ "$run_browser" == "--browser" ]]; then
  node scripts/smoke-browser.mjs "$base_url"
fi
docker stop "$name" >/dev/null
container_running=0
for attempt in $(seq 1 40); do
  if ! docker inspect "$name" >/dev/null 2>&1; then
    break
  fi
  sleep 0.25
done
if docker inspect "$name" >/dev/null 2>&1; then
  echo "Stopped Komarr container was not removed before restart" >&2
  exit 1
fi
start_container
check_api verify
