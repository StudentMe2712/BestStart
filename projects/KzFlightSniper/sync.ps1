param (
    [string]$CommitMessage = "feat(KzFlightSniper): update flight search engine and monitoring services"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  KzFlightSniper Git Synchronization" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

Write-Host "[1/4] Staging changes..." -ForegroundColor Yellow
git add -A

Write-Host "[2/4] Verifying staged changes..." -ForegroundColor Yellow
$stagedChanges = git status --porcelain
if ([string]::IsNullOrWhiteSpace($stagedChanges)) {
    Write-Host "No changes detected to commit. Workspace is clean." -ForegroundColor Green
} else {
    Write-Host "Committing with message: '$CommitMessage'..." -ForegroundColor Cyan
    git commit -m "$CommitMessage"
}

Write-Host "[3/4] Checking remote configuration..." -ForegroundColor Yellow
$hasRemote = $false
try {
    $remoteUrl = git remote get-url origin 2>$null
    if ($LASTEXITCODE -eq 0 -and $remoteUrl) {
        $hasRemote = $true
    }
} catch {
    $hasRemote = $false
}

if ($hasRemote) {
    Write-Host "[4/4] Pushing changes to remote origin HEAD..." -ForegroundColor Yellow
    try {
        git push origin HEAD
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Successfully synced and pushed to remote origin." -ForegroundColor Green
        } else {
            Write-Host "⚠️ Warning: Git push failed (network or authentication issue). Local commit preserved." -ForegroundColor DarkYellow
        }
    } catch {
        Write-Host "⚠️ Warning: Git push encountered an exception: $_" -ForegroundColor DarkYellow
    }
} else {
    Write-Host "[4/4] No remote 'origin' configured. Skipping push." -ForegroundColor Gray
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Synchronization Completed Successfully" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
