@echo off
setlocal enabledelayedexpansion

echo =======================================================
echo          TrendScanner Analytical Terminal
echo =======================================================
echo.

where docker >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [INFO] Docker detected. Starting services with Docker Compose...
    docker compose up --build
) else (
    echo [WARNING] Docker not found in PATH.
    echo [INFO] Attempting local startup...
    
    echo Starting Backend on http://127.0.0.1:8000 ...
    start "TrendScanner Backend" cmd /k "cd backend && python -m uvicorn main:app --host 0.0.0.0 --port 8000 --reload"
    
    echo Starting Frontend on http://localhost:3000 ...
    start "TrendScanner Frontend" cmd /k "cd frontend && npm run dev"
    
    echo [SUCCESS] Backend and Frontend started in separate windows.
)
pause
