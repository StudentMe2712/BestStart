<#
.SYNOPSIS
    Проверяет файл скриншота на диске (разрешение, байты, формат) и выводит структурированный JSON.
.EXAMPLE
    ./skills/inspect-screenshot.ps1 -Path "path/to/step_001.png"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$Path
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Path)) {
    $result = [PSCustomObject]@{
        Exists = $false
        Path = $Path
        Error = "File not found"
    }
    $result | ConvertTo-Json
    exit 1
}

$fileItem = Get-Item $Path
$bytes = $fileItem.Length

Add-Type -AssemblyName System.Drawing -ErrorAction SilentlyContinue

$width = 0
$height = 0
$format = "Unknown"
$isValidImage = $false

try {
    $img = [System.Drawing.Image]::FromFile($fileItem.FullName)
    $width = $img.Width
    $height = $img.Height
    $format = $img.RawFormat.ToString()
    $isValidImage = $true
    $img.Dispose()
} catch {
    $isValidImage = $false
}

$result = [PSCustomObject]@{
    Exists = $true
    Path = $fileItem.FullName
    SizeBytes = $bytes
    Width = $width
    Height = $height
    Format = $format
    IsValidImage = $isValidImage
    CreationTime = $fileItem.CreationTimeUtc.ToString("o")
}

$result | ConvertTo-Json
exit 0
