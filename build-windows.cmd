@echo off
cd /d "%~dp0"
dotnet run --project tests/AuditLineage.Tests -c Release
if errorlevel 1 goto failed
dotnet publish src/AuditLineage.Desktop -c Release -r win-x64 --self-contained true -o artifacts/windows-x64
if errorlevel 1 goto failed
echo Built: artifacts\windows-x64\AuditLineage.exe
pause
exit /b 0
:failed
echo Build failed. Install .NET 10 SDK and read the error above.
pause
exit /b 1
