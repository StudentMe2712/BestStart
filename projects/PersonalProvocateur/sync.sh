#!/usr/bin/env bash
set -e

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$DIR/../.." && pwd)"

COMMIT_MSG="${1:-feat(provocateur): Sprint 9 — 1180x780 Production Studio UI, YouTube Thumbnails, Smart Procrastination Detector & Save/Plus Button Fixes}"

if [ -f "$ROOT_DIR/sync.sh" ]; then
    bash "$ROOT_DIR/sync.sh" "$COMMIT_MSG"
else
    echo "=== Staging changes ==="
    git add -A
    echo "=== Committing changes ==="
    git commit -m "$COMMIT_MSG"
fi
