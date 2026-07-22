@echo off
setlocal
call "%~dp0src\build.cmd"
if errorlevel 1 (
  echo.
  echo Budowanie nie powiodlo sie.
  pause
  exit /b 1
)
start "" "%~dp0Wdrozyciel.exe"
