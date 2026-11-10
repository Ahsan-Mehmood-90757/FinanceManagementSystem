#!/usr/bin/env bash
# Starts the API and the web app, then opens the browser. The macOS counterpart of RUN.cmd.
# Press Ctrl+C to stop both.
#
#   ./run.sh

set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Connection string saved by setup.sh when your 'postgres' password isn't the default.
if [[ -f "$ROOT/.env" ]]; then
  set -a; source "$ROOT/.env"; set +a
fi

# Fail early with a clear message instead of a stack trace when setup hasn't finished.
if [[ ! -d "$ROOT/frontend/node_modules" ]]; then
  echo "Setup hasn't finished yet (frontend packages are missing). Run ./setup.sh and wait for 'Done'." >&2
  exit 1
fi
if ! (exec 3<>/dev/tcp/127.0.0.1/5432) 2>/dev/null; then
  echo "PostgreSQL isn't running on localhost:5432. Run ./setup.sh (or: brew services start postgresql@17)." >&2
  exit 1
fi

pids=()
cleanup() {
  trap - INT TERM EXIT
  echo; echo "Stopping..."
  for pid in "${pids[@]}"; do kill "$pid" 2>/dev/null || true; done
  wait 2>/dev/null || true
}
trap cleanup INT TERM EXIT

(cd "$ROOT/backend" && exec dotnet run --project src/Fms.Api --launch-profile Development) &
pids+=($!)
(cd "$ROOT/frontend" && exec npm run dev) &
pids+=($!)

echo "Waiting for the API and web app to start..."
for _ in {1..60}; do
  curl -s -o /dev/null http://localhost:5173 && break
  sleep 1
done
open http://localhost:5173 2>/dev/null || true
echo "App: http://localhost:5173   API: http://localhost:5080/scalar   (Ctrl+C to stop)"

wait
