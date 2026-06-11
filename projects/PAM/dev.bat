@echo off
REM ============================================================
REM  PAM dev launcher.
REM  backend + extension -> run HIDDEN in background (logs to files).
REM  web (frontend)      -> opens in its own visible window.
REM  Stop background services with stop-dev.bat.
REM  ASCII only on purpose (Cyrillic + chcp breaks cmd .bat).
REM ============================================================
setlocal
set "ROOT=%~dp0"

if not exist "%ROOT%backend\.venv\Scripts\python.exe" goto :noenv

echo [PAM] Starting backend (hidden) -> backend\dev-backend.log
powershell -NoProfile -Command "Start-Process -WindowStyle Hidden -WorkingDirectory '%ROOT%backend' -FilePath '%ROOT%backend\.venv\Scripts\python.exe' -ArgumentList '-m','uvicorn','app.main:app','--host','127.0.0.1','--port','8000','--reload' -RedirectStandardOutput '%ROOT%backend\dev-backend.log' -RedirectStandardError '%ROOT%backend\dev-backend.err.log'"

echo [PAM] Starting extension (hidden) -> extension\dev-extension.log
powershell -NoProfile -Command "Start-Process -WindowStyle Hidden -WorkingDirectory '%ROOT%extension' -FilePath 'cmd.exe' -ArgumentList '/c','npm run dev' -RedirectStandardOutput '%ROOT%extension\dev-extension.log' -RedirectStandardError '%ROOT%extension\dev-extension.err.log'"

echo.
echo [PAM] Backend  : http://localhost:8000/docs
echo [PAM] Extension: building to extension\build\chrome-mv3-dev
echo [PAM] Web UI   : opening in its own window (http://localhost:3000)
echo [PAM] Stop background services: run stop-dev.bat
echo.
start "PAM web :3000" /D "%ROOT%web" cmd /k "npm run dev"
goto :eof

:noenv
echo [PAM] backend\.venv not found. Create it first:
echo       py -3.11 -m venv backend\.venv
echo       backend\.venv\Scripts\python -m pip install -e backend
pause
