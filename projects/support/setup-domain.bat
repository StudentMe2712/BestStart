@echo off
chcp 65001 >nul
title Настройка доменного имени для Wiki
color 0E

echo.
echo ============================================
echo    НАСТРОЙКА ДОМЕННОГО ИМЕНИ
echo ============================================
echo.
echo Этот скрипт добавит запись в файл hosts
echo чтобы использовать its-kom.kz вместо IP
echo.

REM Проверка прав администратора
net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ОШИБКА] Требуются права администратора!
    echo.
    echo Запустите этот файл от имени администратора:
    echo 1. ПКМ на setup-domain.bat
    echo 2. "Запуск от имени администратора"
    echo.
    pause
    exit /b 1
)

echo [✓] Права администратора подтверждены
echo.

REM Проверка, не добавлено ли уже
findstr /C:"its-kom.kz" %SystemRoot%\System32\drivers\etc\hosts >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [i] Запись its-kom.kz уже существует в hosts
    echo.
    echo Хотите перезаписать? (Y/N)
    set /p choice=
    if /i "%choice%" NEQ "Y" (
        echo Отменено.
        pause
        exit /b 0
    )
    
    REM Удаляем старую запись
    findstr /V /C:"its-kom.kz" %SystemRoot%\System32\drivers\etc\hosts > %TEMP%\hosts_temp
    move /Y %TEMP%\hosts_temp %SystemRoot%\System32\drivers\etc\hosts >nul
)

REM Добавляем новую запись
echo. >> %SystemRoot%\System32\drivers\etc\hosts
echo 10.10.9.29    its-kom.kz >> %SystemRoot%\System32\drivers\etc\hosts

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ============================================
    echo    ✓ НАСТРОЙКА ЗАВЕРШЕНА!
    echo ============================================
    echo.
    echo Теперь можно использовать:
    echo.
    echo     http://its-kom.kz:3000
    echo.
    echo вместо:
    echo.
    echo     http://10.10.9.29:3000
    echo.
    
    REM Очистка DNS кеша
    ipconfig /flushdns >nul 2>&1
    echo [✓] DNS кеш очищен
    echo.
    
    REM Проверка
    echo [i] Проверка настройки...
    ping -n 1 its-kom.kz | findstr "10.10.9.29" >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        echo [✓] Проверка успешна! its-kom.kz → 10.10.9.29
    ) else (
        echo [!] Проверка не удалась, но настройка добавлена
    )
    echo.
    echo Откройте браузер и перейдите:
    echo http://its-kom.kz:3000
    echo.
) else (
    echo.
    echo [✗] ОШИБКА при добавлении записи
    echo.
)

pause

