@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo ======================================================================
echo                     TgOrderSniper Windows Launcher                    
echo ======================================================================

set "PYTHON_EXE="

if exist ".venv\Scripts\python.exe" (
    set "PYTHON_EXE=.venv\Scripts\python.exe"
    echo [Launcher] Using virtual environment: .venv\Scripts\python.exe
) else if exist "venv\Scripts\python.exe" (
    set "PYTHON_EXE=venv\Scripts\python.exe"
    echo [Launcher] Using virtual environment: venv\Scripts\python.exe
) else (
    python -c "import sys" >nul 2>&1
    if errorlevel 1 (
        echo [ERROR] Python not found in PATH or virtual environments!
        echo Please install Python 3.10+ from https://www.python.org or create .venv
        pause
        exit /b 1
    )
    set "PYTHON_EXE=python"
    echo [Launcher] Using system Python from PATH
)

echo [Launcher] Launching: %PYTHON_EXE% run.py %*
echo ======================================================================
echo.

%PYTHON_EXE% run.py %*
set "APP_EXIT_CODE=%errorlevel%"

if not "%APP_EXIT_CODE%"=="0" (
    echo.
    echo ======================================================================
    echo [Launcher] Application exited with error code: %APP_EXIT_CODE%
    echo ======================================================================
    pause
    exit /b %APP_EXIT_CODE%
)

exit /b 0
