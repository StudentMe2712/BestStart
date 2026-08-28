"""Database cleanup utility for TrendScanner.

Removes old unliked trends and orphaned references while strictly preserving
all liked/favorited records (is_liked=1).
"""

import argparse
import logging
import sys
from pathlib import Path
from typing import Optional

# Ensure project root is in sys.path when run directly
_current_dir = Path(__file__).resolve().parent
_backend_root = _current_dir.parent.parent
if str(_backend_root) not in sys.path:
    sys.path.insert(0, str(_backend_root))

from app.db.database import get_db_connection

logger = logging.getLogger("trendscanner.cleanup")


def cleanup_old_unliked_trends(
    db_path: Optional[str] = None,
    days: int = 30,
) -> int:
    """Find and delete unliked trends older than `days` days.

    Strict rules:
    1. Records with `is_liked == 1` are NEVER deleted regardless of age.
    2. Only unliked records (`is_liked == 0` or `is_liked IS NULL`) older than
       `days` days are deleted.
    3. Associated orphaned records in `sources_trends` (if table exists) are removed.
    4. Database transaction is committed and the count of deleted trends is returned.

    Args:
        db_path: Optional path to SQLite database. If None, uses default DATABASE_PATH.
        days: Number of days threshold (default: 30).

    Returns:
        int: Number of deleted trend records.
    """
    if days < 0:
        raise ValueError(f"Days parameter must be non-negative, got {days}")

    deleted_count = 0
    days_modifier = f"-{int(days)} days"

    with get_db_connection(db_path=db_path) as conn:
        # Check if trends table exists
        cursor = conn.execute(
            "SELECT name FROM sqlite_master WHERE type='table' AND name='trends'"
        )
        if cursor.fetchone() is None:
            logger.warning("Table 'trends' does not exist in database: %s", db_path or "default")
            return 0

        # Inspect columns to dynamically find the date column
        cursor = conn.execute("PRAGMA table_info(trends)")
        cols = {row["name"] for row in cursor.fetchall()}

        date_col = "parsed_date"
        for candidate in ("parsed_date", "created_at", "published_at", "first_seen_at"):
            if candidate in cols:
                date_col = candidate
                break

        # Check if sources_trends table exists
        cursor = conn.execute(
            "SELECT name FROM sqlite_master WHERE type='table' AND name='sources_trends'"
        )
        has_sources_trends = cursor.fetchone() is not None

        if has_sources_trends:
            # Delete related sources_trends records referencing trends to be deleted
            conn.execute(
                f"""
                DELETE FROM sources_trends
                WHERE trend_id IN (
                    SELECT id FROM trends
                    WHERE (is_liked = 0 OR is_liked IS NULL)
                      AND is_liked != 1
                      AND datetime({date_col}) < datetime('now', ?)
                )
                """,
                (days_modifier,),
            )

        # Delete unliked trends older than threshold
        delete_cursor = conn.execute(
            f"""
            DELETE FROM trends
            WHERE (is_liked = 0 OR is_liked IS NULL)
              AND is_liked != 1
              AND datetime({date_col}) < datetime('now', ?)
            """,
            (days_modifier,),
        )
        deleted_count = delete_cursor.rowcount if delete_cursor.rowcount >= 0 else 0

        # Clean up any leftover orphaned records in sources_trends
        if has_sources_trends:
            conn.execute(
                "DELETE FROM sources_trends WHERE trend_id NOT IN (SELECT id FROM trends)"
            )

    logger.info(
        "Successfully removed %d unliked trends older than %d days from %s (date_col: %s)",
        deleted_count,
        days,
        db_path or "default",
        date_col,
    )
    return deleted_count


def cleanup_unliked_cli() -> int:
    """CLI entrypoint for cleaning up old unliked trends.

    Parses command line arguments `--days` (default 30) and `--db-path`,
    configures logging, runs cleanup, and reports results.
    """
    parser = argparse.ArgumentParser(
        description="TrendScanner Database Cleanup: safely remove old unliked trends."
    )
    parser.add_argument(
        "--days",
        type=int,
        default=30,
        help="Age threshold in days for unliked trends (default: 30)",
    )
    parser.add_argument(
        "--db-path",
        type=str,
        default=None,
        help="Optional custom SQLite database file path",
    )
    args = parser.parse_args()

    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    )

    logger.info(
        "Initiating database cleanup CLI: days=%d, db_path=%s",
        args.days,
        args.db_path,
    )
    deleted = cleanup_old_unliked_trends(db_path=args.db_path, days=args.days)
    logger.info("Cleanup completed. Total trends deleted: %d", deleted)
    return deleted


if __name__ == "__main__":
    sys.exit(0 if cleanup_unliked_cli() >= 0 else 1)
