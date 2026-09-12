@echo off
setlocal
cd /d "%~dp0\.."
where python >nul 2>&1
if errorlevel 1 (
  echo Python not found in PATH
  exit /b 1
)
REM Git Bash bash.exe
where bash >nul 2>&1
if errorlevel 1 (
  echo Git Bash not found. Run from "Git Bash": ./scripts/smoke-test.sh
  exit /b 1
)
bash "%~dp0smoke-test.sh" %*
