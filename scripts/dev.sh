#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://127.0.0.1:47221}"
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"

cd "$ROOT/src/GisDashboard.Api"
dotnet run --urls "$ASPNETCORE_URLS" &
API_PID=$!

cd "$ROOT/client"
npm run dev -- --host 127.0.0.1 --port 47222 &
UI_PID=$!

cleanup() {
  kill "$API_PID" "$UI_PID" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

echo "API  http://127.0.0.1:47221"
echo "SPA  http://127.0.0.1:47222"
wait
