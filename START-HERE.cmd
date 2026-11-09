@echo off
rem Double-click to install everything and start the Financial Management System.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
