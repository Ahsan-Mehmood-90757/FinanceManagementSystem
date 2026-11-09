# One-time developer setup for the Financial Management System (Windows PowerShell).
# Prerequisites: .NET 10 SDK, Node.js 20+, and PostgreSQL running (docker compose up -d, or a local install).
#
#   powershell -ExecutionPolicy Bypass -File .\setup.ps1
#
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "==> Restoring .NET tools and packages" -ForegroundColor Cyan
Push-Location "$root\backend"
dotnet tool restore
dotnet restore

$migrations = "src\Fms.Infrastructure\Persistence\Migrations"
if (-not (Test-Path $migrations)) {
    Write-Host "==> Creating initial EF Core migration" -ForegroundColor Cyan
    dotnet ef migrations add InitialCreate -p src\Fms.Infrastructure -s src\Fms.Api -o Persistence\Migrations
}

Write-Host "==> Applying migrations to fms_dev" -ForegroundColor Cyan
dotnet ef database update -p src\Fms.Infrastructure -s src\Fms.Api

Write-Host "==> Running domain tests" -ForegroundColor Cyan
dotnet test tests\Fms.Domain.Tests
Pop-Location

Write-Host "==> Installing frontend packages" -ForegroundColor Cyan
Push-Location "$root\frontend"
npm install
Pop-Location

Write-Host ""
Write-Host "Done. Start the system in two terminals:" -ForegroundColor Green
Write-Host "  1) cd backend;  dotnet run --project src\Fms.Api      (API on http://localhost:5080, docs at /scalar)"
Write-Host "  2) cd frontend; npm run dev                           (app on http://localhost:5173)"
Write-Host "Sign in as: owner / Owner@12345  (change it under Settings > Users after first login)"
