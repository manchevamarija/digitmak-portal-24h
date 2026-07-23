@echo off
setlocal
cd /d "%~dp0"
set "ASPNETCORE_ENVIRONMENT=Development"
set "LocalDatabasePath=%~dp0backend\src\DigitMak.Portal.Api\data\digitmak-dev.db"
set "UPLOADS_ROOT=%~dp0backend\src\DigitMak.Portal.Api\data\uploads"
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { $r=Invoke-WebRequest -UseBasicParsing http://127.0.0.1:5241/health -TimeoutSec 2; if ($r.StatusCode -eq 200) { exit 0 } } catch {}; exit 1" >nul 2>nul
if not errorlevel 1 (
  echo DigitMak API is already running at http://localhost:5241
  echo You do not need to start it a second time.
  exit /b 0
)
powershell -NoProfile -ExecutionPolicy Bypass -Command "if (Get-NetTCPConnection -State Listen -LocalPort 5241 -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }" >nul 2>nul
if not errorlevel 1 (
  echo Port 5241 is being used by another application.
  echo Close that application and run START-BACKEND.cmd again.
  pause
  exit /b 1
)
echo Starting DigitMak API at http://localhost:5241
echo Local data is stored persistently in backend\src\DigitMak.Portal.Api\data\digitmak-dev.db
echo Uploaded documents are stored persistently in backend\src\DigitMak.Portal.Api\data\uploads
dotnet run --project backend\src\DigitMak.Portal.Api
if errorlevel 1 pause
