@echo off
chcp 65001 > nul
set DB_STUB=true
set PORT=3000
echo Запуск локального сервера Wiki (логин: admin / admin)...
node server.js
pause