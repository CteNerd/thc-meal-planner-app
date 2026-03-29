#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${1:-}"
ENV_NAME="${2:-unknown}"

if [[ -z "$BASE_URL" ]]; then
  echo "Usage: ./scripts/validate-deployment.sh <base-url> [env-name]"
  exit 1
fi

check_status() {
  local path="$1"
  local expected="$2"
  local actual
  actual=$(curl -sS -o /dev/null -w "%{http_code}" "${BASE_URL}${path}")

  if [[ "$actual" != "$expected" ]]; then
    echo "[FAIL] ${path} expected ${expected}, got ${actual}"
    exit 1
  fi

  echo "[PASS] ${path} -> ${actual}"
}

echo "Running automated smoke checks for ${ENV_NAME} at ${BASE_URL}"

check_status "/" "200"
check_status "/api/health" "200"
check_status "/api/profile" "401"

cors_status=$(curl -sS -o /dev/null -w "%{http_code}" -X OPTIONS \
  -H "Origin: https://example.com" \
  -H "Access-Control-Request-Method: GET" \
  -H "Access-Control-Request-Headers: authorization,content-type" \
  "${BASE_URL}/api/health")

if [[ "$cors_status" != "204" ]]; then
  echo "[FAIL] OPTIONS /api/health expected 204, got ${cors_status}"
  exit 1
fi

echo "[PASS] OPTIONS /api/health -> ${cors_status}"
echo "Automated smoke checks completed successfully."
