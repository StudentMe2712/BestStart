@echo off
chcp 65001 >nul
title Wiki Server - Запуск
color 0A

echo.
echo ============================================
echo    ЗАПУСК WIKI-СЕРВЕРА
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

REM Проверка наличия node_modules
if not exist "node_modules\" (
    echo [ПРЕДУПРЕЖДЕНИЕ] Зависимости не установлены!
    echo.
    echo Запускаю установку зависимостей...
    call install.bat
    if %ERRORLEVEL% NEQ 0 (
        echo.
        echo [ОШИБКА] Не удалось установить зависимости
        pause
        exit /b 1
    )
)

REM Создание .env файла, если его нет
if not exist ".env" (
    echo PORT=3000 > .env
    echo DB_NAME=wiki.db >> .env
    echo NODE_ENV=production >> .env
    echo [✓] Создан файл конфигурации .env
)

echo [✓] Проверки пройдены успешно
echo.
echo Запуск сервера...
echo.

REM Запуск сервера
node server.js

REM Если сервер остановлен
echo.
echo ============================================
echo    СЕРВЕР ОСТАНОВЛЕН
echo ============================================
echo.
pause


