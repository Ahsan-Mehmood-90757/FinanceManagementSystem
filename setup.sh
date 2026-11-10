#!/usr/bin/env bash
# One-time setup for the Financial Management System on macOS (Intel or Apple Silicon).
# The macOS counterpart of START-HERE.cmd / install.ps1. Safe to run again: anything already installed is skipped.
#
#   ./setup.sh
#
# 1. Installs .NET 10 SDK, Node.js and PostgreSQL 17 with Homebrew (only if missing)
# 2. Creates the 'postgres' database user and the fms_dev database
# 3. Restores packages, builds, applies migrations, runs tests, npm install
#
# If your PostgreSQL 'postgres' user has a password other than 'postgres', the connection string is saved to
# ./.env (git-ignored) instead of editing appsettings.Development.json, so nothing machine-specific gets committed.

set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT"

step() { printf '\n\033[36m==> %s\033[0m\n' "$1"; }
ok()   { printf '    \033[32m%s\033[0m\n' "$1"; }
warn() { printf '    \033[33m%s\033[0m\n' "$1"; }
fail() { printf '\n\033[31mSetup failed: %s\033[0m\n' "$1" >&2; exit 1; }
has()  { command -v "$1" >/dev/null 2>&1; }

# ---------------------------------------------------------------- Homebrew
step "Checking Homebrew"
if ! has brew; then
  for b in /opt/homebrew/bin/brew /usr/local/bin/brew; do [[ -x $b ]] && eval "$("$b" shellenv)" && break; done
fi
has brew || fail "Homebrew was not found. Install it from https://brew.sh, then run ./setup.sh again."
ok "Homebrew found"

# ---------------------------------------------------------------- .NET 10 SDK
step "Checking .NET 10 SDK"
if ! (has dotnet && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'); then
  warn "Installing .NET 10 SDK (macOS may ask for your password)..."
  brew install --cask dotnet-sdk
  export PATH="/usr/local/share/dotnet:$PATH"
fi
dotnet --list-sdks 2>/dev/null | grep -q '^10\.' || fail ".NET 10 SDK is still missing. Install it from https://dotnet.microsoft.com/download and run ./setup.sh again."
ok ".NET $(dotnet --version)"

# ---------------------------------------------------------------- Node.js
step "Checking Node.js"
node_major=0
has node && node_major="$(node --version | sed 's/^v//; s/\..*//')"
if (( node_major < 20 )); then
  warn "Installing Node.js LTS..."
  brew install node
fi
has npm || fail "Node.js is still missing after install."
ok "Node $(node --version)"

# ---------------------------------------------------------------- PostgreSQL
step "Checking PostgreSQL"
PG_PASSWORD="postgres"
if has psql; then
  PSQL="$(command -v psql)"
else
  if ! brew list postgresql@17 >/dev/null 2>&1; then
    warn "Installing PostgreSQL 17..."
    # Homebrew can exit non-zero on warnings (e.g. Tier 3 / outdated Xcode) even when the install worked,
    # so check for psql afterwards instead of trusting the exit code.
    brew install postgresql@17 || warn "Homebrew reported a problem; checking whether PostgreSQL installed anyway..."
  fi
  PSQL="$(brew --prefix postgresql@17)/bin/psql"
  [[ -x "$PSQL" ]] || fail "PostgreSQL 17 did not install. See the Homebrew output above."
fi

if ! pg_isready_out="$("$(dirname "$PSQL")/pg_isready" -h localhost -p 5432 2>&1)"; then
  warn "Starting PostgreSQL ($pg_isready_out)..."
  brew services start postgresql@17 || true
  for _ in {1..20}; do "$(dirname "$PSQL")/pg_isready" -h localhost -p 5432 >/dev/null 2>&1 && break; sleep 1; done
fi
"$(dirname "$PSQL")/pg_isready" -h localhost -p 5432 >/dev/null 2>&1 || fail "PostgreSQL is not running on localhost:5432."
ok "PostgreSQL is running"

can_login() { PGPASSWORD="$PG_PASSWORD" "$PSQL" -h localhost -U postgres -d postgres -tAc "select 1" >/dev/null 2>&1; }

if ! can_login; then
  # Homebrew's PostgreSQL only creates a superuser named after your macOS account (no password).
  if "$PSQL" -h localhost -d postgres -tAc "select 1" >/dev/null 2>&1; then
    if [[ "$("$PSQL" -h localhost -d postgres -tAc "select 1 from pg_roles where rolname='postgres'")" != "1" ]]; then
      "$PSQL" -h localhost -d postgres -c "create role postgres with superuser login password 'postgres'" >/dev/null
      ok "Created database user 'postgres' (password 'postgres')"
    fi
  fi
fi
if ! can_login; then
  read -r -s -p "    Enter the password of your existing 'postgres' database user: " PG_PASSWORD; echo
  can_login || fail "Can't sign in to PostgreSQL on localhost:5432 as 'postgres'. Check the password."
fi
ok "Connected to PostgreSQL"

if [[ "$(PGPASSWORD="$PG_PASSWORD" "$PSQL" -h localhost -U postgres -d postgres -tAc "select 1 from pg_database where datname='fms_dev'")" != "1" ]]; then
  PGPASSWORD="$PG_PASSWORD" "$PSQL" -h localhost -U postgres -d postgres -c "create database fms_dev" >/dev/null
  ok "Created database fms_dev"
else
  ok "Database fms_dev already exists"
fi

CONNECTION="Host=localhost;Port=5432;Database=fms_dev;Username=postgres;Password=$PG_PASSWORD"
export FMS_DESIGN_CONNECTION="$CONNECTION"
export ConnectionStrings__Default="$CONNECTION"
if [[ "$PG_PASSWORD" != "postgres" ]]; then
  printf 'ConnectionStrings__Default=%q\n' "$CONNECTION" > "$ROOT/.env"
  ok "Saved your connection string to .env (git-ignored)"
fi

# ---------------------------------------------------------------- backend
cd "$ROOT/backend"
step "Restoring backend packages"
dotnet tool restore
dotnet restore

step "Building backend"
dotnet build --no-restore -c Debug || fail "Backend build failed. See the errors above."

step "Applying migrations to fms_dev"
dotnet ef database update -p src/Fms.Infrastructure -s src/Fms.Api || fail "Applying the migration failed. See the errors above."

step "Running domain tests"
dotnet test tests/Fms.Domain.Tests --no-restore || warn "Some tests failed (the app will still start)."

# ---------------------------------------------------------------- frontend
cd "$ROOT/frontend"
step "Installing frontend packages"
npm install

printf '\n\033[32mDone. Start the system with:  ./run.sh\033[0m\n'
echo "Sign in as: owner / Owner@12345  (change it under Settings > My password after first login)"
