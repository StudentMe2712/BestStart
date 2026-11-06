<#
.SYNOPSIS
    Builds a standalone, single-file Release executable for QuickCalc on Windows x64.
.DESCRIPTION
    Compiles QuickCalc into a self-contained single .exe (including runtime & native dependencies)
    stored in the 'dist' directory.
#>

[CmdletBinding()]
param (
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrEmpty($scriptDir)) {
    $scriptDir = $PSScriptRoot
}
if ([string]::IsNullOrEmpty($scriptDir)) {
    $scriptDir = (Get-Location).Path
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $scriptDir "dist"
}

$projectPath = Join-Path $scriptDir "QuickCalc.csproj"
if (-not (Test-Path -Path $projectPath)) {
    Write-Error "Project file not found: $projectPath"
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  QuickCalc Standalone Executable Build" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Configuration : $Configuration"
Write-Host "Runtime       : $Runtime"
Write-Host "Project       : $projectPath"
Write-Host "Output        : $OutputDir"
Write-Host ""

# Close any running QuickCalc instances to prevent file lock during compilation
$runningProcesses = Get-Process -Name QuickCalc -ErrorAction SilentlyContinue
if ($runningProcesses) {
    Write-Host "Closing running QuickCalc instances to avoid file lock..." -ForegroundColor Yellow
    $runningProcesses | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

if (-not (Test-Path -Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Write-Host "Running dotnet publish..." -ForegroundColor Yellow

$cmd = "dotnet publish `"$projectPath`" -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o `"$OutputDir`""
Write-Host "> $cmd" -ForegroundColor Gray

& dotnet publish "$projectPath" `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o "$OutputDir"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

$exePath = Join-Path $OutputDir "QuickCalc.exe"
if (Test-Path -Path $exePath) {
    $fileInfo = Get-Item -Path $exePath
    $sizeMB = [Math]::Round($fileInfo.Length / 1MB, 2)
    $sizeKB = [Math]::Round($fileInfo.Length / 1KB, 0)
    Write-Host ""
    Write-Host "SUCCESS! Standalone executable built successfully." -ForegroundColor Green
    Write-Host "Location : $exePath" -ForegroundColor Green
    Write-Host "File size: $sizeMB MB ($sizeKB KB / $($fileInfo.Length) bytes)" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
} else {
    Write-Error "Build finished but QuickCalc.exe was not found in $OutputDir"
    exit 1
}
