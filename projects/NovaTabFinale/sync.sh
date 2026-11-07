#!/usr/bin/env bash
# NovaTabFinale — Automated Git Sync Script
set -e

echo "=========================================="
echo "  NovaTabFinale Sync: Staging, Commit & Push"
echo "=========================================="

# Check git repository
if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo "Error: Not a git repository."
  exit 1
fi

echo "[1/4] Checking current status..."
git status -s

echo "[2/4] Staging all modified and new files..."
git add .

COMMIT_MSG="${1:-feat: add upload/import data functionality in settings}"
echo "[3/4] Committing with message: '$COMMIT_MSG'..."
if git diff --staged --quiet; then
  echo "Nothing to commit, working directory clean."
else
  git commit -m "$COMMIT_MSG"
fi

echo "[4/4] Pushing changes to remote repository..."
CURRENT_BRANCH=$(git branch --show-current || echo "main")
git push origin "$CURRENT_BRANCH" || git push || echo "Push skipped or remote not configured."

echo "=========================================="
echo "  NovaTabFinale Sync Complete!"
echo "=========================================="
