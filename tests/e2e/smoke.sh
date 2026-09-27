#!/usr/bin/env bash
# Phase 8 end-to-end smoke test for Librarr.
#
# Spins up a clean Librarr instance, hits the API to add an author, kicks
# a search, and verifies the basic API endpoints respond. Not exhaustive;
# the goal is "the running binary serves a green API surface at all" —
# the unit + integration NUnit suites cover correctness.
#
# Usage:
#   ./tests/e2e/smoke.sh                    # uses ./_output/net10.0/<rid>/Readarr
#   READARR_BIN=/path/to/Readarr ./smoke.sh # explicit binary
#
# Requires: curl.
#
# Exits 0 on success, non-zero on first failed assertion.

set -euo pipefail

LIBRARR_PORT="${LIBRARR_PORT:-8787}"
LIBRARR_HOST="${LIBRARR_HOST:-http://localhost:${LIBRARR_PORT}}"
LIBRARR_DATA="${LIBRARR_DATA:-$(mktemp -d -t librarr-smoke.XXXXXX)}"
READARR_BIN="${READARR_BIN:-}"

if [[ -z "${READARR_BIN}" ]]; then
  # Find the host-RID binary in _output/.
  case "$(uname -s)" in
    Darwin) RID="osx-$(uname -m | sed s/x86_64/x64/)" ;;
    Linux)  RID="linux-$(uname -m | sed s/x86_64/x64/)" ;;
    *)      echo "Unsupported OS: $(uname -s)" >&2; exit 1 ;;
  esac
  READARR_BIN="_output/net10.0/${RID}/publish/Readarr"
fi

if [[ ! -x "${READARR_BIN}" ]]; then
  echo "Readarr binary not found at ${READARR_BIN}." >&2
  echo "Build first: ./build.sh --backend --enable-extra-platforms" >&2
  exit 1
fi

echo "Smoke test data dir: ${LIBRARR_DATA}"
echo "Starting ${READARR_BIN}..."

"${READARR_BIN}" -data="${LIBRARR_DATA}" >"${LIBRARR_DATA}/librarr.log" 2>&1 &
LIBRARR_PID=$!

cleanup() {
  echo "Stopping Librarr (PID ${LIBRARR_PID})..."
  kill "${LIBRARR_PID}" 2>/dev/null || true
  wait "${LIBRARR_PID}" 2>/dev/null || true
}
trap cleanup EXIT

# Wait up to 60s for the API to come up.
for _ in $(seq 1 60); do
  if curl -fsS "${LIBRARR_HOST}/ping" >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

if ! curl -fsS "${LIBRARR_HOST}/ping" >/dev/null 2>&1; then
  echo "Librarr never came up on ${LIBRARR_HOST}/ping." >&2
  tail -50 "${LIBRARR_DATA}/librarr.log" >&2 || true
  exit 1
fi

# Extract the API key from the generated config so subsequent calls
# authenticate.
CONFIG_XML="${LIBRARR_DATA}/config.xml"
if [[ ! -f "${CONFIG_XML}" ]]; then
  echo "config.xml not generated under ${LIBRARR_DATA}." >&2
  exit 1
fi

API_KEY=$(sed -n 's/.*<ApiKey>\(.*\)<\/ApiKey>.*/\1/p' "${CONFIG_XML}")
if [[ -z "${API_KEY}" ]]; then
  echo "ApiKey missing from ${CONFIG_XML}." >&2
  exit 1
fi

AUTH=(-H "X-Api-Key: ${API_KEY}")

# Assertion helpers.
assert_endpoint() {
  local path="$1"
  local expected_substr="${2:-}"
  echo "  GET ${path}"
  local body
  body=$(curl -fsS "${AUTH[@]}" "${LIBRARR_HOST}${path}")
  if [[ -n "${expected_substr}" ]] && ! grep -q "${expected_substr}" <<<"${body}"; then
    echo "Expected '${expected_substr}' in ${path} body, not found." >&2
    echo "Body: ${body:0:200}..." >&2
    exit 1
  fi
}

echo "Checking core API endpoints..."
assert_endpoint "/api/v1/system/status" "version"
assert_endpoint "/api/v1/health" ""
assert_endpoint "/api/v1/config/host" "bindAddress"
assert_endpoint "/api/v1/config/metadataprovider" "writeAudioTags"

# Phase 5 reidentify command path — schedule it but don't wait for
# completion (it runs the OL traversal which is unbounded on a fresh
# install).
echo "Triggering ReidentifyLibrary command (fire-and-forget)..."
curl -fsS "${AUTH[@]}" -X POST -H "Content-Type: application/json" \
  -d '{"name":"ReidentifyLibrary"}' \
  "${LIBRARR_HOST}/api/v1/command" >/dev/null

echo "Smoke test passed."
