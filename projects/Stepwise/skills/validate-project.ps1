<#
.SYNOPSIS
    Комплексная валидация проекта Stepwise (Git -> Build -> Tests -> Storage).
.EXAMPLE
    ./skills/validate-project.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir

Write-Host "==========================================================" -ForegroundColor Magenta
Write-Host "  STEPWISE PROJECT HEALTH & INTEGRITY VALIDATOR" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Magenta

# 1. Build
Write-Host "`n[1/3] Проверка сборки солюшена..." -ForegroundColor Cyan
& (Join-Path $ScriptDir "build-project.ps1")
if ($LASTEXITCODE -ne 0) {
    Write-Host ">> Сборка провалена. Валидация остановлена." -ForegroundColor Red
    exit 1
}

# 2. Tests
Write-Host "`n[2/3] Запуск набора тестов xUnit..." -ForegroundColor Cyan
& (Join-Path $ScriptDir "run-tests.ps1")
if ($LASTEXITCODE -ne 0) {
    Write-Host ">> Тесты провалены. Валидация остановлена." -ForegroundColor Red
    exit 1
}

# 3. Structure
Write-Host "`n[3/3] Проверка ключевых каталогов и артефактов..." -ForegroundColor Cyan
$requiredPaths = @(
    "Stepwise.sln",
    "specs/spec.md",
    "src/Stepwise.Core",
    "src/Stepwise.WindowsIntegration",
    "src/Stepwise.Storage",
    "src/Stepwise.App",
    "tests/Stepwise.Tests"
)

$missing = @()
foreach ($rel in $requiredPaths) {
    $full = Join-Path $ProjectRoot $rel
    if (-not (Test-Path $full)) {
        $missing += $rel
    }
}

if ($missing.Count -gt 0) {
    Write-Host ">> Отсутствуют обязательные компоненты: $($missing -join ', ')" -ForegroundColor Red
    exit 1
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  ВСЕ ПРОВЕРКИ УСПЕШНО ПРОЙДЕНЫ (HEALTH STATUS: 100% OK)" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
exit 0
