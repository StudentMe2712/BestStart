<#
.SYNOPSIS
    Собирает солюшен Stepwise.sln и выводит сводку по предупреждениям и ошибкам.
.EXAMPLE
    ./skills/build-project.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$SolutionPath = Join-Path $ProjectRoot "Stepwise.sln"

Write-Host ">> [Build Runner] Сборка решения $SolutionPath ($Configuration) ..." -ForegroundColor Cyan

$output = & dotnet build $SolutionPath -c $Configuration 2>&1
$exitCode = $LASTEXITCODE

$warnings = $output | Where-Object { $_ -match "warning" -or $_ -match "предупреждени" }
$errors = $output | Where-Object { $_ -match "error" -or $_ -match "ошибк" }

if ($exitCode -eq 0) {
    Write-Host ">> [Build Runner] Сборка завершена УСПЕШНО! (Код выхода: 0)" -ForegroundColor Green
    if ($warnings) {
        Write-Host ">> Предупреждения компилятора ($($warnings.Count)):" -ForegroundColor Yellow
        $warnings | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    } else {
        Write-Host ">> Предупреждений: 0, Ошибок: 0" -ForegroundColor Gray
    }
} else {
    Write-Host ">> [Build Runner] Сборка ЗАВЕРШИЛАСЬ С ОШИБКОЙ! (Код выхода: $exitCode)" -ForegroundColor Red
    if ($errors) {
        Write-Host ">> Ошибки компиляции:" -ForegroundColor Red
        $errors | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    }
}

exit $exitCode
