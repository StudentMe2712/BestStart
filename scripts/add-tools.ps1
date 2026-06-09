<#
.SYNOPSIS
    Add tools from the root library into an EXISTING project's .claude/ folder.
    This is the install step of the /start-task tool-selection gate.

.PARAMETER Project
    Name of the project under projects/ (or a path to a project folder).

.PARAMETER Agents / Skills / Commands / Rules / Hooks
    Comma-separated bucket OR "bucket/name" items, or "all" for a whole type. Examples:
      -Agents ecc,awesome              (whole buckets)
      -Skills "superpowers,ecc/api-design,alireza/engineering/code-tour"  (mixed)
    A plain bucket copies the whole bucket; "bucket/sub/path" copies just that item.

.PARAMETER Mcp
    "starter" or "full" — copy that MCP preset to the project's .mcp.json (merges/overwrites).

.EXAMPLE
    ./scripts/add-tools.ps1 -Project myapp -Skills superpowers/test-driven-development -Agents ecc/architect -Mcp starter
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$Project,
    [string]$Agents, [string]$Skills, [string]$Commands, [string]$Rules, [string]$Hooks,
    [ValidateSet('starter','full')] [string]$Mcp
)
$ErrorActionPreference = 'Stop'
$Root    = Split-Path -Parent $PSScriptRoot
$Library = Join-Path $Root 'library'
$projDir = if (Test-Path $Project) { (Resolve-Path $Project).Path } else { Join-Path $Root "projects/$Project" }
if (-not (Test-Path $projDir)) { throw "Project not found: $projDir" }
$claude = Join-Path $projDir '.claude'
New-Item -ItemType Directory -Path $claude -Force | Out-Null

function Add-Items($type, $spec) {
    if (-not $spec) { return }
    $srcType = Join-Path $Library $type
    foreach ($item in ($spec -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })) {
        if ($item -eq 'all') {
            $destAll = Join-Path $claude $type
            New-Item -ItemType Directory -Path $destAll -Force | Out-Null
            Get-ChildItem $srcType | ForEach-Object { Copy-Item $_.FullName $destAll -Recurse -Force }
            Write-Host "  + $type/* (all)"; continue
        }
        $src = Join-Path $srcType $item
        $rel = $item
        if (-not (Test-Path $src)) {
            # leaf items (agents/commands) are .md files; allow specifying them without the extension
            if (Test-Path "$src.md") { $src = "$src.md"; $rel = "$item.md" }
            else { Write-Warning "  not found: $type/$item"; continue }
        }
        $dest = Join-Path $claude (Join-Path $type $rel)
        New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null
        Copy-Item $src $dest -Recurse -Force
        Write-Host "  + $type/$rel"
    }
}

Write-Host "Adding tools to $projDir/.claude ..." -ForegroundColor Cyan
Add-Items 'agents'   $Agents
Add-Items 'skills'   $Skills
Add-Items 'commands' $Commands
Add-Items 'rules'    $Rules
Add-Items 'hooks'    $Hooks

if ($Mcp) {
    Copy-Item (Join-Path $Library "mcp/$Mcp.mcp.json") (Join-Path $projDir '.mcp.json') -Force
    Write-Host "  + .mcp.json ($Mcp pack) — set any required API keys as env vars" -ForegroundColor DarkGray
}
Write-Host "Done." -ForegroundColor Green
