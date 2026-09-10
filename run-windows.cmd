@echo off
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo Install .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0
  echo Or download the standalone application from GitHub Actions.
  pause
  exit /b 1
)
dotnet run --project src/AuditLineage.Desktop -c Release
if errorlevel 1 pause
