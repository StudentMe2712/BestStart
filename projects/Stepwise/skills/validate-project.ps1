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
Write-Host "`n[1/3] Checking solution build..." -ForegroundColor Cyan
& (Join-Path $ScriptDir "build-project.ps1")
if ($LASTEXITCODE -ne 0) {
    Write-Host ">> Build failed. Validation stopped." -ForegroundColor Red
    exit 1
}

# 2. Tests
Write-Host "`n[2/3] Running xUnit test suite..." -ForegroundColor Cyan
& (Join-Path $ScriptDir "run-tests.ps1")
if ($LASTEXITCODE -ne 0) {
    Write-Host ">> Tests failed. Validation stopped." -ForegroundColor Red
    exit 1
}

# 3. Structure
Write-Host "`n[3/3] Checking core directories and artifacts..." -ForegroundColor Cyan
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
    Write-Host ">> Missing required components: $($missing -join ', ')" -ForegroundColor Red
    exit 1
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  ALL CHECKS PASSED (HEALTH STATUS: 100% OK)" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
exit 0
