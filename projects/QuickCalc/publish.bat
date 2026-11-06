@echo off
setlocal
echo ========================================
echo   QuickCalc Standalone Executable Build
echo ========================================

set SCRIPT_DIR=%~dp0
set PROJECT_PATH=%SCRIPT_DIR%QuickCalc.csproj
set OUTPUT_DIR=%SCRIPT_DIR%dist

echo Project: %PROJECT_PATH%
echo Output:  %OUTPUT_DIR%
echo.

rem Close any running QuickCalc instances to prevent file lock
taskkill /F /IM QuickCalc.exe 2>nul

dotnet publish "%PROJECT_PATH%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "%OUTPUT_DIR%"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Publish failed with error code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

if exist "%OUTPUT_DIR%\QuickCalc.exe" (
    echo.
    echo [SUCCESS] QuickCalc.exe built successfully!
    echo Location: %OUTPUT_DIR%\QuickCalc.exe
    exit /b 0
) else (
    echo.
    echo [ERROR] QuickCalc.exe was not found in %OUTPUT_DIR%
    exit /b 1
)
