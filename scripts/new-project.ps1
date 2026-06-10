<#
.SYNOPSIS
    Scaffold a new project under projects/ and copy selected tools from the root library
    into the project's local .claude/ folder.

.DESCRIPTION
    This is the "каркас" bootstrapper. The root library/ is a catalog; each project gets
    ONLY the tools it needs, copied (not symlinked) into projects/<name>/.claude/.
    Tools are organised by source bucket so nothing collides:
        agents:   ecc, gsd, awesome
        skills:   ecc, alireza, superpowers, karpathy
        commands: ecc, gsd
        rules:    ecc, karpathy, best-practice
        hooks:    ecc, gsd, superpowers

.PARAMETER Name
    Project folder name (created under projects/).

.PARAMETER Template
    Optional starter template from projects/_templates (e.g. "vibe").

.PARAMETER Preset
    "lean"  (default) -> superpowers + karpathy skills, karpathy rules, gsd commands. Light.
    "full"  -> every bucket. Heavy; use only if you really want everything.
    "none"  -> empty .claude scaffold, pick buckets manually with the flags below.

.PARAMETER Agents     Comma list of agent buckets to copy: ecc,gsd,awesome  (or "all")
.PARAMETER Skills     Comma list of skill buckets:          ecc,alireza,superpowers,karpathy (or "all")
.PARAMETER Commands   Comma list of command buckets:        ecc,gsd (or "all")
.PARAMETER Rules      Comma list of rule buckets:           ecc,karpathy,best-practice (or "all")
.PARAMETER Hooks      Comma list of hook buckets:           ecc,gsd,superpowers (or "all")
.PARAMETER Memory     Switch: also copy the claude-mem memory plugin into .claude/plugins.
.PARAMETER List       Switch: just print the catalog and exit (no project created).

.EXAMPLE
    ./scripts/new-project.ps1 -List

.EXAMPLE
    ./scripts/new-project.ps1 -Name todo-api -Preset lean

.EXAMPLE
    ./scripts/new-project.ps1 -Name shop -Template vibe -Agents ecc,awesome -Skills ecc,superpowers -Commands ecc,gsd -Rules ecc,karpathy

.EXAMPLE
    ./scripts/new-project.ps1 -Name big -Preset full -Memory
#>
[CmdletBinding()]
param(
    [string]$Name,
    [string]$Template,
    [ValidateSet('lean','full','none')] [string]$Preset = 'lean',
    [string]$Agents,
    [string]$Skills,
    [string]$Commands,
    [string]$Rules,
    [string]$Hooks,
    [ValidateSet('starter','vibe','full')] [string]$Mcp,
    [switch]$Memory,
    [switch]$List
)

$ErrorActionPreference = 'Stop'
$Root    = Split-Path -Parent $PSScriptRoot          # repo root (parent of scripts/)
$Library = Join-Path $Root 'library'
$Projects= Join-Path $Root 'projects'

function Get-Buckets($dir) {
    if (-not (Test-Path $dir)) { return @() }
    Get-ChildItem $dir -Directory | Select-Object -ExpandProperty Name
}

function Show-Catalog {
    Write-Host "`n=== Root library catalog ===" -ForegroundColor Cyan
    foreach ($type in 'agents','skills','commands','rules','hooks') {
        $d = Join-Path $Library $type
        $buckets = Get-Buckets $d
        Write-Host ("  {0,-9}: {1}" -f $type, ($buckets -join ', '))
    }
    Write-Host ("  memory   : " + ((Get-Buckets (Join-Path $Library 'memory')) -join ', '))
    Write-Host "`n  templates: " -NoNewline
    Write-Host ((Get-Buckets $Projects | Where-Object { $_ -ne '_templates' }) -join ', ')
    Write-Host ("  _templates: " + ((Get-ChildItem (Join-Path $Projects '_templates') -Directory -ErrorAction SilentlyContinue).Name -join ', '))
    Write-Host ""
}

if ($List) { Show-Catalog; return }

if (-not $Name) { throw "Provide -Name <project> (or use -List to see the catalog)." }

# Resolve preset defaults (explicit flags override the preset)
switch ($Preset) {
    'lean' {
        if (-not $Skills)   { $Skills   = 'superpowers,karpathy' }
        if (-not $Rules)    { $Rules    = 'karpathy' }
        if (-not $Commands) { $Commands = 'gsd' }
    }
    'full' {
        if (-not $Agents)   { $Agents   = 'all' }
        if (-not $Skills)   { $Skills   = 'all' }
        if (-not $Commands) { $Commands = 'all' }
        if (-not $Rules)    { $Rules    = 'all' }
        if (-not $Hooks)    { $Hooks    = 'all' }
    }
    'none' { }
}

$projDir = Join-Path $Projects $Name
if (Test-Path $projDir) { throw "Project already exists: $projDir" }
New-Item -ItemType Directory -Path $projDir | Out-Null

# Optional starter template
if ($Template) {
    $tpl = Join-Path $Projects "_templates/$Template"
    if (-not (Test-Path $tpl)) { throw "Template not found: $tpl" }
    Copy-Item "$tpl/*" $projDir -Recurse -Force
    Write-Host "Applied template '$Template'." -ForegroundColor Green
}

$claude = Join-Path $projDir '.claude'
New-Item -ItemType Directory -Path $claude -Force | Out-Null

# ALWAYS install the core gate commands (start-task / add-tools / lesson)
$coreDest = Join-Path $claude 'commands/core'
New-Item -ItemType Directory -Path $coreDest -Force | Out-Null
Copy-Item (Join-Path $Library 'commands/core/*') $coreDest -Recurse -Force
Write-Host "  + commands/core (start-task, add-tools, lesson)" -ForegroundColor DarkGray

function Copy-Buckets($type, $spec) {
    if (-not $spec) { return }
    $srcType = Join-Path $Library $type
    $available = Get-Buckets $srcType
    $want = if ($spec -eq 'all') { $available } else { $spec -split ',' | ForEach-Object { $_.Trim() } }
    $destType = Join-Path $claude $type
    foreach ($b in $want) {
        if ($available -notcontains $b) { Write-Warning "  $type bucket '$b' not found, skipping"; continue }
        $dest = Join-Path $destType $b
        New-Item -ItemType Directory -Path $dest -Force | Out-Null
        Copy-Item (Join-Path $srcType "$b/*") $dest -Recurse -Force
        Write-Host ("  + {0}/{1}" -f $type, $b) -ForegroundColor DarkGray
    }
}

Write-Host "Copying tools into $Name/.claude ..." -ForegroundColor Cyan
Copy-Buckets 'agents'   $Agents
Copy-Buckets 'skills'   $Skills
Copy-Buckets 'commands' $Commands
Copy-Buckets 'rules'    $Rules
Copy-Buckets 'hooks'    $Hooks

if ($Memory) {
    $memDest = Join-Path $claude 'plugins/claude-mem'
    New-Item -ItemType Directory -Path $memDest -Force | Out-Null
    Copy-Item (Join-Path $Library 'memory/claude-mem/*') $memDest -Recurse -Force
    Write-Host "  + memory/claude-mem (run its own install per upstream README)" -ForegroundColor DarkGray
}

# Optional MCP pack -> project-root .mcp.json
if ($Mcp) {
    Copy-Item (Join-Path $Library "mcp/$Mcp.mcp.json") (Join-Path $projDir '.mcp.json') -Force
    Write-Host "  + .mcp.json ($Mcp pack) — set any required API keys as env vars" -ForegroundColor DarkGray
}

# Project LESSONS.md (from template) — paired with the /lesson command
$projLessons = Join-Path $projDir 'LESSONS.md'
if (-not (Test-Path $projLessons)) {
    # Read template as UTF-8 explicitly (PowerShell 5.1's Get-Content defaults to ANSI and
    # would mangle the em-dashes in the template).
    $tpl = [System.IO.File]::ReadAllText((Join-Path $Library 'templates/LESSONS.template.md'), [System.Text.Encoding]::UTF8)
    $tpl.Replace('{{PROJECT}}', $Name) | Out-File -FilePath $projLessons -Encoding utf8
    Write-Host "  + LESSONS.md" -ForegroundColor DarkGray
}

# Starter project CLAUDE.md (only if the template didn't provide one)
$projClaudeMd = Join-Path $projDir 'CLAUDE.md'
if (-not (Test-Path $projClaudeMd)) {
@"
# $Name

Project-specific instructions for Claude Code. This file overrides / extends the
baseline philosophy in the root CLAUDE.md.

## Stack
- (describe languages, frameworks, run commands here)

## Tools enabled (copied from root library into .claude/)
- agents:   $Agents
- skills:   $Skills
- commands: $Commands
- rules:    $Rules
- hooks:    $Hooks
- mcp:      $Mcp

## ⛔ Tool-selection gate (before building anything)
When I give you a new task or paste a prompt, **do not start coding immediately.** Run
``/start-task`` first (it reads the root ``library/CATALOG.md`` + ``library/mcp/README.md``,
proposes the best-matching tools as a grouped list of max ~7, and installs my picks via
``scripts/add-tools.ps1``). Only after I choose do you start development.
Skip only if I say "skip tools" or the needed tools are already in ``.claude/``.

## 📓 Lessons
Read ``LESSONS.md`` (this project) and the root ``LESSONS.md`` at the start of work. After a
non-obvious bug or wrong approach, append an entry with ``/lesson``.

## Conventions
- (project-specific rules go here)
"@ | Out-File -FilePath $projClaudeMd -Encoding utf8
    Write-Host "Wrote starter CLAUDE.md" -ForegroundColor Green
}

Write-Host "`nDone -> $projDir" -ForegroundColor Green
Write-Host "Next: cd into it, edit CLAUDE.md, prune any tools you don't need under .claude/." -ForegroundColor Yellow
