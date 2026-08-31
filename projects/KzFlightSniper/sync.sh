#!/usr/bin/env bash
set -eo pipefail

echo "=========================================="
echo "  KzFlightSniper Git Synchronization"
echo "=========================================="

echo "[1/4] Staging changes..."
git add -A

echo "[2/4] Verifying staged changes..."
if git diff --staged --quiet; then
    echo "No changes detected to commit. Workspace is clean."
else
    COMMIT_MSG="${1:-feat(KzFlightSniper): Stage 1 - Architecture specification, scaffolding, and Aviata Playwright PoC}"
    echo "Committing with message: '$COMMIT_MSG'..."
    git commit -m "$COMMIT_MSG"
fi

echo "[3/4] Checking remote configuration..."
if git remote get-url origin >/dev/null 2>&1; then
    echo "[4/4] Pushing changes to remote origin HEAD..."
    if git push origin HEAD; then
        echo "✅ Successfully synced and pushed to remote origin."
    else
        echo "⚠️ Warning: Git push failed (network or authentication issue). Local commit preserved."
    fi
else
    echo "[4/4] No remote 'origin' configured. Skipping push."
fi

echo "=========================================="
echo "  Synchronization Completed Successfully"
echo "=========================================="
