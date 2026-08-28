"""Compatibility wrapper for TrendScanner DB cleanup utility."""

import sys
from pathlib import Path

# Ensure backend root is in sys.path
_backend_root = Path(__file__).resolve().parent.parent
if str(_backend_root) not in sys.path:
    sys.path.insert(0, str(_backend_root))

from app.db.cleanup import (  # noqa: F401
    cleanup_old_unliked_trends,
    cleanup_unliked_cli,
)

if __name__ == "__main__":
    sys.exit(0 if cleanup_unliked_cli() >= 0 else 1)
