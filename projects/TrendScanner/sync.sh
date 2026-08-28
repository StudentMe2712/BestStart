#!/usr/bin/env bash
set -e

echo "=== Starting sync to branch 'dev' ==="
git add -A

if git diff --staged --quiet; then
    echo "No staged changes to commit."
else
    git commit -m "feat(level12): deep code review, performance optimizations, and scanner pause/resume control"
fi

echo "Pushing commits to remote origin branch dev..."
git push origin HEAD:dev

echo "=== Sync to dev successfully finished! ==="
