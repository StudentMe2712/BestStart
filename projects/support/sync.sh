#!/usr/bin/env bash
set -e

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$DIR/../.." && pwd)"

COMMIT_MSG="${1:-feat(support): Sprint 7 — Mammoth DOCX/DOC support, 1:1 image preview, shared attachments, Unified Confirm Modal & Smart Pin}"

if [ -f "$ROOT_DIR/sync.sh" ]; then
    bash "$ROOT_DIR/sync.sh" "$COMMIT_MSG"
else
    echo "=== Staging changes ==="
    git add -A
    echo "=== Committing changes ==="
    git commit -m "$COMMIT_MSG"
fi
