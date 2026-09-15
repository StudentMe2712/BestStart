#!/usr/bin/env bash
set -e

echo "=== Staging CarteBlanche changes ==="
git add -A

echo "=== Checking for staged changes ==="
if git diff --staged --quiet; then
    echo "No staged changes to commit."
else
    echo "Committing staged changes..."
    git commit -m "feat(CarteBlanche): Level 1 - Carte Blanche Luxe Menu & Tasting Guide MVP"
fi

echo "=== Checking remote origin ==="
if git remote get-url origin >/dev/null 2>&1; then
    echo "Pushing changes to remote origin HEAD..."
    if git push origin HEAD; then
        echo "Successfully pushed to origin HEAD."
    else
        echo "Warning: Push to origin HEAD failed or encountered a network error."
    fi
else
    echo "No remote 'origin' configured. Skipping push."
fi

echo "=== Sync completed successfully! ==="
