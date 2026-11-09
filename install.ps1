# One-click installer for the Financial Management System (Windows 10/11).
# Started by START-HERE.cmd. Safe to run again: anything already installed is skipped.
#
# 1. Installs .NET 10 SDK, Node.js LTS and PostgreSQL 17 with winget (only if missing)
# 2. Creates the fms_dev database
# 3. Restores packages, creates + applies the EF Core migration, runs tests, npm install
# 4. Starts the API and the web app in two windows and opens the browser

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$devSettings = Join-Path $root "backend\src\Fms.Api\appsettings.Development.json"

function Step($text) { Write-Host ""; Write-Host "==> $text" -ForegroundColor Cyan }
function Ok($text) { Write-Host "    $text" -ForegroundColor Green }
function Warn($text) { Write-Host "    $text" -ForegroundColor Yellow }
function Refresh-Path {
    $env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [Environment]::GetEnvironmentVariable("Path", "User")
}
function Has($cmd) { [bool](Get-Command $cmd -ErrorAction SilentlyContinue) }

try {
    # ------------------------------------------------------------------ winget
    Step "Checking winget (Windows package manager)"
    if (-not (Has "winget")) {
        throw "winget was not found. Install 'App Installer' from the Microsoft Store, then run START-HERE.cmd again."
    }
    Ok "winget found"

    # ------------------------------------------------------------------ .NET 10 SDK
    Step "Checking .NET 10 SDK"
    $hasNet10 = (Has "dotnet") -and ((dotnet --list-sdks 2>$null) -match "^10\.")
    if (-not $hasNet10) {
        Warn "Installing .NET 10 SDK (approve the Windows prompt if asked)..."
        winget install --id Microsoft.DotNet.SDK.10 -e --accept-source-agreements --accept-package-agreements --silent
        Refresh-Path
    }
    if (-not ((dotnet --list-sdks 2>$null) -match "^10\.")) { throw ".NET 10 SDK is still missing after install. Restart the computer and run START-HERE.cmd again." }
    Ok ".NET $(dotnet --version)"

    # ------------------------------------------------------------------ Node.js
    Step "Checking Node.js"
    $nodeOk = $false
    if (Has "node") { $nodeOk = [int]((node --version).TrimStart("v").Split(".")[0]) -ge 20 }
    if (-not $nodeOk) {
        Warn "Installing Node.js LTS..."
        winget install --id OpenJS.NodeJS.LTS -e --accept-source-agreements --accept-package-agreements --silent
        Refresh-Path
    }
    if (-not (Has "npm")) { throw "Node.js is still missing after install. Restart the computer and run START-HERE.cmd again." }
    Ok "Node $(node --version)"

    # ------------------------------------------------------------------ PostgreSQL
    Step "Checking PostgreSQL"
    $pgBin = Get-ChildItem "C:\Program Files\PostgreSQL" -Directory -ErrorAction SilentlyContinue |
        Sort-Object { [int]($_.Name -replace '\D', '') } -Descending |
        ForEach-Object { Join-Path $_.FullName "bin" } |
        Where-Object { Test-Path (Join-Path $_ "psql.exe") } | Select-Object -First 1
    $pgPassword = "postgres"

    if (-not $pgBin) {
        Warn "Installing PostgreSQL 17 (superuser 'postgres', password 'postgres', port 5432)..."
        winget install --id PostgreSQL.PostgreSQL.17 -e --accept-source-agreements --accept-package-agreements `
            --override "--mode unattended --unattendedmodeui minimal --superpassword postgres --serverport 5432 --disable-components stackbuilder"
        $pgBin = "C:\Program Files\PostgreSQL\17\bin"
        if (-not (Test-Path (Join-Path $pgBin "psql.exe"))) { throw "PostgreSQL install did not finish. Run START-HERE.cmd again." }
        Start-Sleep -Seconds 5
    } else {
        Ok "Found PostgreSQL at $pgBin"
        $env:PGPASSWORD = $pgPassword
        & (Join-Path $pgBin "psql.exe") -h localhost -U postgres -d postgres -c "select 1" *> $null
        if ($LASTEXITCODE -ne 0) {
            $secure = Read-Host "    Enter the password of your existing 'postgres' database user" -AsSecureString
            $pgPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
        }
    }
    $env:PGPASSWORD = $pgPassword
    $psql = Join-Path $pgBin "psql.exe"
    & $psql -h localhost -U postgres -d postgres -c "select 1" *> $null
    if ($LASTEXITCODE -ne 0) { throw "Can't sign in to PostgreSQL on localhost:5432 as 'postgres'. Check the service is running and the password is right." }
    Ok "Connected to PostgreSQL"

    $exists = & $psql -h localhost -U postgres -d postgres -tAc "select 1 from pg_database where datname='fms_dev'"
    if ($exists -ne "1") {
        & $psql -h localhost -U postgres -d postgres -c "create database fms_dev" | Out-Null
        Ok "Created database fms_dev"
    } else { Ok "Database fms_dev already exists" }

    $connection = "Host=localhost;Port=5432;Database=fms_dev;Username=postgres;Password=$pgPassword"
    $json = Get-Content $devSettings -Raw
    # Only touch ConnectionStrings:Default (Logging:LogLevel also has a "Default" key).
    $json = $json -replace '("ConnectionStrings"\s*:\s*\{\s*"Default"\s*:\s*)"[^"]*"', ('$1"' + $connection + '"')
    Set-Content -Path $devSettings -Value $json -Encoding UTF8
    $env:FMS_DESIGN_CONNECTION = $connection

    # ------------------------------------------------------------------ backend
    Step "Restoring backend packages"
    Push-Location (Join-Path $root "backend")
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed" }
    dotnet restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }

    Step "Building backend"
    dotnet build --no-restore -c Debug
    if ($LASTEXITCODE -ne 0) { throw "Backend build failed. Copy the errors above and send them to Claude." }

    $migrations = "src\Fms.Infrastructure\Persistence\Migrations"
    if (-not (Test-Path $migrations)) {
        Step "Creating the database schema migration"
        dotnet ef migrations add InitialCreate -p src\Fms.Infrastructure -s src\Fms.Api -o Persistence\Migrations
        if ($LASTEXITCODE -ne 0) { throw "Creating the migration failed. Send the errors above to Claude." }
    }
    Step "Applying migrations to fms_dev"
    dotnet ef database update -p src\Fms.Infrastructure -s src\Fms.Api
    if ($LASTEXITCODE -ne 0) { throw "Applying the migration failed. Send the errors above to Claude." }

    Step "Running domain tests"
    dotnet test tests\Fms.Domain.Tests --no-restore
    if ($LASTEXITCODE -ne 0) { Warn "Some tests failed (the app will still start). Send the output to Claude." }
    Pop-Location

    # ------------------------------------------------------------------ frontend
    Step "Installing frontend packages"
    Push-Location (Join-Path $root "frontend")
    npm install
    if ($LASTEXITCODE -ne 0) { throw "npm install failed" }
    Pop-Location

    # ------------------------------------------------------------------ start
    Step "Starting the system"
    & (Join-Path $root "RUN.cmd")

    Write-Host ""
    Write-Host "All set." -ForegroundColor Green
    Write-Host "  App:       http://localhost:5173   (sign in: owner / Owner@12345)"
    Write-Host "  API docs:  http://localhost:5080/scalar"
    Write-Host "  Next time: double-click RUN.cmd"
}
catch {
    Write-Host ""
    Write-Host "Setup stopped: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Scroll up for details. Copy the red/error lines and send them to Claude." -ForegroundColor Red
}
finally {
    Read-Host "Press Enter to close this window"
}
