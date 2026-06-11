@echo off
REM Stops the HIDDEN PAM backend (uvicorn) and extension (plasmo) from dev.bat.
REM The web window (visible) you can just close yourself.
echo [PAM] Stopping hidden backend + extension...
powershell -NoProfile -Command "Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'uvicorn app.main' -or $_.CommandLine -match 'plasmo' } | ForEach-Object { Write-Host ('  stopping PID ' + $_.ProcessId); Stop-Process -Id $_.ProcessId -Force }"
echo [PAM] Done.
