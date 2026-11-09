@echo off
rem Thin wrapper: the full implementation lives in build.ps1.
rem Prefers pwsh (PowerShell 7), falls back to Windows PowerShell 5.1.
setlocal
set "PS1=%~dp0build.ps1"
set "PSCMD="
where pwsh >nul 2>nul
if not errorlevel 1 set "PSCMD=pwsh"
if not defined PSCMD set "PSCMD=powershell"
"%PSCMD%" -NoProfile -ExecutionPolicy Bypass -File "%PS1%" %*
set "RC=%ERRORLEVEL%"
endlocal & exit /b %RC%
