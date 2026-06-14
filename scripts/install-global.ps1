<#
.SYNOPSIS
    Install the curated GLOBAL core from this BestStart skeleton into ~/.claude/, so any new
    folder/project on this PC starts with the orchestrator + the tool-selection gate — without
    cloning BestStart into that folder.

.DESCRIPTION
    Curated core (chosen scope): the generic orchestrator + the three core commands
    (start-task / add-tools / lesson) + a managed block in ~/.claude/CLAUDE.md + a
    skeleton-root marker. The 600+ catalog stays in library/ and is pulled per-project by the
    gate (scripts/add-tools.ps1). Run once per PC; re-run after `git pull` to refresh.

    Touches ~/.claude (your PERSONAL global Claude Code config), NOT the repo. The CLAUDE.md
    change is a clearly-delimited managed block (idempotent — re-run replaces it; delete the
    block by hand to undo). Copied command/rule files can simply be deleted to undo.

.EXAMPLE
    ./scripts/install-global.ps1
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'

$Root       = Split-Path -Parent $PSScriptRoot
$Library    = Join-Path $Root 'library'
$ClaudeHome = Join-Path $HOME '.claude'
$CmdDir     = Join-Path $ClaudeHome 'commands'
$RulesDir   = Join-Path $ClaudeHome 'rules'

if (-not (Test-Path $Library)) { throw "library/ not found next to scripts/: $Library" }
New-Item -ItemType Directory -Force -Path $ClaudeHome | Out-Null
New-Item -ItemType Directory -Force -Path $CmdDir     | Out-Null
New-Item -ItemType Directory -Force -Path $RulesDir   | Out-Null

function Write-Utf8NoBom([string]$path, [string]$text) {
    $enc = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($path, $text, $enc)
}

Write-Host "Installing curated global core into $ClaudeHome ..." -ForegroundColor Cyan

# 1) Core commands -> ~/.claude/commands/
foreach ($c in @('start-task','add-tools','lesson')) {
    $src = Join-Path $Library "commands/core/$c.md"
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $CmdDir "$c.md") -Force
        Write-Host "  + commands/$c.md"
    } else {
        Write-Warning "  missing: $src"
    }
}

# 2) Generic orchestrator -> ~/.claude/rules/orchestrator.md
$orchSrc = Join-Path $Library 'rules/core/orchestrator.md'
if (Test-Path $orchSrc) {
    Copy-Item $orchSrc (Join-Path $RulesDir 'orchestrator.md') -Force
    Write-Host "  + rules/orchestrator.md"
} else {
    Write-Warning "  missing: $orchSrc"
}

# 3) Skeleton-root marker (lets the gate find the catalog from any folder)
Write-Utf8NoBom (Join-Path $ClaudeHome 'skeleton-root') $Root
Write-Host "  + skeleton-root -> $Root"

# 4) Managed block in ~/.claude/CLAUDE.md (idempotent, between markers)
$claudeMd = Join-Path $ClaudeHome 'CLAUDE.md'
$begin = '<!-- BEGIN skeleton-orchestrator (managed by install-global.ps1) -->'
$end   = '<!-- END skeleton-orchestrator -->'

$blockTemplate = @'
<!-- BEGIN skeleton-orchestrator (managed by install-global.ps1) -->
## Оркестрация (глобально, для любого проекта)

В любом проекте действуй как **оркестратор**: трёхуровневая модель
(оркестратор -> исполнители -> верификатор), декомпозиция, контроль качества,
обязательная верификация перед завершением. Полный charter:
`~/.claude/rules/orchestrator.md`. Проектная специфика (приоритеты, стек) — в собственном
`CLAUDE.md` / `ORCHESTRATOR.md` проекта; он дополняет (не заменяет) этот каркас.

**Tool-selection gate.** Перед тем как строить что-либо в проекте — НЕ писать код сразу.
Сначала подобрать ~7 релевантных тулзов из каталога (команда `/start-task`) и установить.

**Каталог тулзов (BestStart).** Источник 600+ тулзов: `__ROOT__\library` (см. CATALOG.md).
Установить выбранное в текущую папку-проект:
`__ROOT__\scripts\add-tools.ps1 -Project "<абсолютный путь к текущей папке>" -Skills ... -Agents ... -Commands ...`
Если работаешь ВНЕ `__ROOT__` — используй этот путь явно (каталог `library/` в предках не найдётся).
<!-- END skeleton-orchestrator -->
'@
$block = $blockTemplate.Replace('__ROOT__', $Root)

if (Test-Path $claudeMd) {
    $content = Get-Content $claudeMd -Raw -Encoding UTF8
    if ($null -eq $content) { $content = '' }
} else {
    $content = ''
}

$bi = $content.IndexOf($begin)
$ei = $content.IndexOf($end)
if ($bi -ge 0 -and $ei -gt $bi) {
    $before = $content.Substring(0, $bi)
    $after  = $content.Substring($ei + $end.Length)
    $newContent = $before + $block + $after
    Write-Host "  ~ CLAUDE.md (обновлён управляемый блок оркестрации)"
} else {
    $trimmed = $content.TrimEnd()
    if ($trimmed.Length -gt 0) {
        $newContent = $trimmed + "`n`n" + $block + "`n"
    } else {
        $newContent = $block + "`n"
    }
    Write-Host "  + CLAUDE.md (добавлен управляемый блок оркестрации)"
}
Write-Utf8NoBom $claudeMd $newContent

Write-Host ""
Write-Host "Готово. Глобальное ядро установлено в $ClaudeHome." -ForegroundColor Green
Write-Host "Любая новая папка на этом ПК теперь стартует с оркестратором и gate." -ForegroundColor Green
Write-Host "Каталог 600+ тулзов остаётся в $Library и тянется per-project через /start-task." -ForegroundColor DarkGray
Write-Host "После 'git pull' в BestStart запусти скрипт снова, чтобы обновить ядро." -ForegroundColor DarkGray
