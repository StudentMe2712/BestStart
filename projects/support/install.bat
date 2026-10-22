@echo off
chcp 65001 >nul
title Wiki Server - Установка зависимостей
color 0B

echo.
echo ============================================
echo    УСТАНОВКА ЗАВИСИМОСТЕЙ
echo ============================================
echo.

REM Проверка наличия Node.js
where node >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [ОШИБКА] Node.js не установлен!
    echo.
    echo Пожалуйста, установите Node.js с https://nodejs.org/
    echo.
    pause
    exit /b 1
)

REM Вывод версий
echo [i] Информация о системе:
node --version
npm --version
echo.

REM Установка зависимостей
echo [►] Начинаю установку зависимостей...
echo [►] Это может занять несколько минут...
echo.

npm install

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ============================================
    echo    ✓ УСТАНОВКА ЗАВЕРШЕНА УСПЕШНО!
    echo ============================================
    echo.
    echo Теперь вы можете запустить сервер через start.bat
    echo.
) else (
    echo.
    echo ============================================
    echo    ✗ ОШИБКА УСТАНОВКИ!
    echo ============================================
    echo.
    echo Пожалуйста, проверьте подключение к интернету
    echo и попробуйте снова.
    echo.
)

pause


