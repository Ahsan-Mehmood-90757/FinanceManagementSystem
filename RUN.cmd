@echo off
rem Starts the API and the web app in their own windows, then opens the browser.
cd /d "%~dp0"
start "FMS API (close to stop)" cmd /k "cd /d "%~dp0backend" && dotnet run --project src\Fms.Api --launch-profile Development"
start "FMS Web (close to stop)" cmd /k "cd /d "%~dp0frontend" && npm run dev"
echo Waiting for the API and web app to start...
timeout /t 15 /nobreak >nul
start "" http://localhost:5173
