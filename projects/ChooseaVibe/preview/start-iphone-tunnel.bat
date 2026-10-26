@echo off
chcp 65001 >nul
title Выбери Вайб — Локальный сервер для iPhone
cd /d "%~dp0"

echo ======================================================================
echo    ✨ ВЫБЕРИ ВАЙБ: СВИДАНИЯ ДЛЯ ПАР — ЛОКАЛЬНЫЙ СЕРВЕР IPHONE PWA ✨
echo ======================================================================
echo.

set PORT=8080

rem Определение локального IP-адреса через PowerShell
for /f "tokens=*" %%a in ('powershell -NoProfile -Command "(Get-NetIPAddress -AddressFamily IPv4 -InterfaceAlias 'Wi-Fi*','Ethernet*','Ethernet','Wi-Fi','Беспроводная сеть*','Подключение по локальной сети*' -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } | Select-Object -ExpandProperty IPAddress -First 1)"') do set LOCAL_IP=%%a

if "%LOCAL_IP%"=="" (
    for /f "tokens=*" %%a in ('powershell -NoProfile -Command "(Test-Connection -ComputerName (hostname) -Count 1).IPV4Address.IPAddressToString"') do set LOCAL_IP=%%a
)

if "%LOCAL_IP%"=="" set LOCAL_IP=127.0.0.1

echo [✓] Локальный IP компьютера: %LOCAL_IP%
echo [✓] Рабочий порт: %PORT%
echo.
echo ----------------------------------------------------------------------
echo 📱 ИНСТРУКЦИЯ ДЛЯ ЗАПУСКА НА РЕАЛЬНОМ IPHONE:
echo.
echo   1. Убедитесь, что iPhone подключен к той же Wi-Fi сети.
echo   2. Откройте браузер Safari на iPhone и перейдите по адресу:
echo.
echo          👉  http://%LOCAL_IP%:%PORT%/index.html
echo.
echo   3. В Safari нажмите кнопку «Поделиться» (иконка квадрата со стрелкой).
echo   4. Прокрутите вниз и выберите «На экран "Домой"» (Add to Home Screen).
echo   5. Вы получите нативное PWA-приложение без рамок браузера и адреса!
echo ----------------------------------------------------------------------
echo.
echo 💻 Для открытия в браузере на этом компьютере:
echo       👉  http://localhost:%PORT%/index.html
echo.
echo ======================================================================
echo Запуск веб-сервера... (Нажмите Ctrl+C для остановки)
echo ======================================================================
echo.

where python >nul 2>&1
if %errorlevel% equ 0 (
    python -m http.server %PORT%
    goto end
)

where npx >nul 2>&1
if %errorlevel% equ 0 (
    npx http-server -p %PORT% -c-1
    goto end
)

where node >nul 2>&1
if %errorlevel% equ 0 (
    node -e "const http=require('http'),fs=require('fs'),path=require('path');const mime={'html':'text/html','css':'text/css','js':'application/javascript','svg':'image/svg+xml','json':'application/json'};http.createServer((req,res)=>{let f=path.join(process.cwd(),req.url.split('?')[0]);if(f.endsWith(path.sep)||fs.existsSync(f)&&fs.statSync(f).isDirectory())f=path.join(f,'index.html');fs.readFile(f,(err,data)=>{if(err){res.writeHead(404);res.end('Not Found');return;}const ext=path.extname(f).slice(1);res.writeHead(200,{'Content-Type':mime[ext]||'application/octet-stream'});res.end(data);});}).listen(%PORT%,()=>console.log('Node server running on port %PORT%'));"
    goto end
)

echo [!] Ошибка: Не найден Python или Node.js для запуска сервера.
pause

:end
