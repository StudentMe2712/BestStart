@echo off
chcp 65001 >nul
title Wiki Server - Остановка
color 0C

echo.
echo ============================================
echo    ОСТАНОВКА WIKI-СЕРВЕРА
echo ============================================
echo.

REM Остановка всех процессов Node.js
taskkill /F /IM node.exe >nul 2>nul

if %ERRORLEVEL% EQU 0 (
    echo [✓] Wiki-сервер успешно остановлен
) else (
    echo [i] Сервер не запущен или уже остановлен
)

echo.
echo ============================================
echo.
pause

