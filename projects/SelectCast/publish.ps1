#Requires -Version 5.1
<#
.SYNOPSIS
  Builds a self-contained, single-file SelectCast.exe (win-x64) into .\dist.
.DESCRIPTION
  Produces one .exe that runs on any 64-bit Windows without an installed .NET runtime.
  Trimming is intentionally OFF (WPF is not trim-safe).
.EXAMPLE
  ./publish.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out  = Join-Path $root 'dist'
$proj = Join-Path $root 'src/SelectCast.App/SelectCast.App.csproj'

Write-Host 'Publishing SelectCast (self-contained, single-file, win-x64)...' -ForegroundColor Cyan

dotnet publish $proj `
    -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $out

$exe = Join-Path $out 'SelectCast.exe'
if (Test-Path $exe) {
    $mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host "OK -> $exe ($mb MB)" -ForegroundColor Green
    Write-Host 'Run it once to put SelectCast in the tray (autostart is enabled by default).'
} else {
    Write-Error "Publish finished but $exe was not found."
}
