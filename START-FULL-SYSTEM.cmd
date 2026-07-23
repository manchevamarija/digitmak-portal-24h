@echo off
setlocal
cd /d "%~dp0"

where docker >nul 2>nul
if errorlevel 1 (
  echo Docker Desktop is required for the full PostgreSQL environment.
  echo Install or start Docker Desktop, then run this file again.
  pause
  exit /b 1
)

docker info >nul 2>nul
if errorlevel 1 (
  echo Docker Desktop is installed but is not running.
  echo Start Docker Desktop and wait until it is ready.
  pause
  exit /b 1
)

echo Building and starting PostgreSQL, API and frontend...
docker compose up -d --build
if errorlevel 1 (
  echo The full DigitMak environment could not be started.
  docker compose ps
  pause
  exit /b 1
)

echo Waiting for the API health check...
powershell -NoProfile -Command "$deadline=(Get-Date).AddMinutes(2); do { try { $r=Invoke-WebRequest -UseBasicParsing http://localhost:8080/health -TimeoutSec 2; if ($r.StatusCode -eq 200) { exit 0 } } catch {}; Start-Sleep -Seconds 2 } while ((Get-Date) -lt $deadline); exit 1"
if errorlevel 1 (
  echo API health check did not become ready. Run: docker compose logs api
  pause
  exit /b 1
)

echo DigitMak is ready at http://localhost:3000
start "" http://localhost:3000
docker compose ps
pause
