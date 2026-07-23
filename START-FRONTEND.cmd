@echo off
setlocal
cd /d "%~dp0frontend"
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { $r=Invoke-WebRequest -UseBasicParsing http://127.0.0.1:5173 -TimeoutSec 2; if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 500) { exit 0 } } catch {}; exit 1" >nul 2>nul
if not errorlevel 1 (
  echo DigitMak frontend is already running at http://localhost:5173
  echo You do not need to start it a second time.
  start "" http://localhost:5173
  exit /b 0
)
if not exist node_modules (
  echo Installing frontend dependencies...
  call npx.cmd -y pnpm@11 install
  if errorlevel 1 pause & exit /b 1
)
echo Opening DigitMak frontend at http://localhost:5173
call npm.cmd run dev -- --open
