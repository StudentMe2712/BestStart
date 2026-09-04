<#
.SYNOPSIS
    Запускает тестовый набор Stepwise.sln и выводит компактную структурированную сводку.
.EXAMPLE
    ./skills/run-tests.ps1
#>
[CmdletBinding()]
param(
    [string]$Filter = "",
    [string]$LogFile = "test-run.log"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$SolutionPath = Join-Path $ProjectRoot "Stepwise.sln"
$LogFullPath = Join-Path $ProjectRoot $LogFile

Write-Host ">> [Test Runner] Запуск тестов для $SolutionPath ..." -ForegroundColor Cyan

$testArgs = @("test", $SolutionPath, "--verbosity", "normal")
if ($Filter) {
    $testArgs += @("--filter", $Filter)
}

$proc = Start-Process -FilePath "dotnet" -ArgumentList $testArgs -NoNewWindow -PassThru -RedirectStandardOutput $LogFullPath -RedirectStandardError "$LogFullPath.err"
$proc.WaitForExit()

$exitCode = $proc.ExitCode
$logContent = Get-Content $LogFullPath -Raw -ErrorAction SilentlyContinue

$passCount = 0
$failCount = 0
$skipCount = 0

if ($logContent -match "Пройдено:\s+(\d+)") { $passCount = [int]$Matches[1] }
elseif ($logContent -match "пройдено\s+(\d+)") { $passCount = [int]$Matches[1] }
elseif ($logContent -match "Passed:\s+(\d+)") { $passCount = [int]$Matches[1] }

if ($logContent -match "С ошибкой:\s+(\d+)") { $failCount = [int]$Matches[1] }
elseif ($logContent -match "не пройдено\s+(\d+)") { $failCount = [int]$Matches[1] }
elseif ($logContent -match "Failed:\s+(\d+)") { $failCount = [int]$Matches[1] }

if ($logContent -match "Пропущено:\s+(\d+)") { $skipCount = [int]$Matches[1] }
elseif ($logContent -match "пропущено\s+(\d+)") { $skipCount = [int]$Matches[1] }
elseif ($logContent -match "Skipped:\s+(\d+)") { $skipCount = [int]$Matches[1] }

Write-Host "=================================================" -ForegroundColor DarkGray
Write-Host "  РЕЗУЛЬТАТ ТЕСТИРОВАНИЯ STEPWISE" -ForegroundColor White
Write-Host "=================================================" -ForegroundColor DarkGray
Write-Host "  PASS: $passCount" -ForegroundColor Green
if ($failCount -gt 0) {
    Write-Host "  FAIL: $failCount" -ForegroundColor Red
} else {
    Write-Host "  FAIL: 0" -ForegroundColor Gray
}
Write-Host "  SKIP: $skipCount" -ForegroundColor Yellow
Write-Host "  LOG:  $LogFullPath" -ForegroundColor DarkGray
Write-Host "=================================================" -ForegroundColor DarkGray

if ($failCount -gt 0) {
    Write-Host "`nУпавшие тесты:" -ForegroundColor Red
    Get-Content $LogFullPath | Where-Object { $_ -match "\[FAIL\]" -or $_ -match "Не пройден" } | ForEach-Object {
        Write-Host "  $_" -ForegroundColor Red
    }
}

exit $exitCode
