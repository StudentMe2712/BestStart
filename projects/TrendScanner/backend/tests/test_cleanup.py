"""Unit tests for TrendScanner database cleanup utility.

Verifies:
1. Old unliked trends (> 30 days) are deleted.
2. Old liked trends (is_liked=1) are NEVER deleted.
3. Fresh unliked trends (< 30 days) are NOT deleted.
4. Custom day thresholds (e.g. days=10) behave accurately.
5. Orphaned / related records in `sources_trends` are properly cleaned.
6. CLI entrypoint parses args and executes cleanup correctly.
7. Validation on invalid parameters (e.g. negative days).
8. Both `app.db.cleanup` and compatibility module `db.cleanup` function identically.
"""

import sqlite3
import sys
from pathlib import Path
import pytest

from app.core.settings import settings
from app.db.database import get_db_connection, init_db
from app.db.dao import SourcesDAO
from app.db.cleanup import cleanup_old_unliked_trends, cleanup_unliked_cli
import db.cleanup as compat_cleanup


@pytest.fixture
def isolated_db(tmp_path, monkeypatch):
    """Fixture providing a clean, isolated SQLite database."""
    db_file = tmp_path / "test_cleanup.db"
    monkeypatch.setattr(settings, "DATABASE_PATH", str(db_file))
    init_db(seed_default_sources=False)
    yield db_file


def _insert_trend_with_custom_date(
    conn: sqlite3.Connection,
    source_id: int,
    text: str,
    date_modifier: str,
    is_liked: int = 0,
) -> int:
    """Helper to insert a trend record with a SQLite relative datetime."""
    cursor = conn.execute(
        f"""
        INSERT INTO trends (
            source_id, original_text, content_hash, parsed_date, is_liked
        )
        VALUES (?, ?, ?, datetime('now', ?), ?)
        """,
        (
            source_id,
            text,
            f"hash_{text.replace(' ', '_')}",
            date_modifier,
            is_liked,
        ),
    )
    return cursor.lastrowid


def test_cleanup_rules_unliked_vs_liked(isolated_db):
    """Verify cleanup deletes old unliked trends, preserves liked trends and fresh trends."""
    source_id = SourcesDAO.create(name="Test Source", url="https://example.com", source_type="rss")

    with get_db_connection(str(isolated_db)) as conn:
        # 1. Old unliked trend (45 days old, is_liked=0) -> SHOULD BE DELETED
        t1_id = _insert_trend_with_custom_date(conn, source_id, "Old Unliked 45d", "-45 days", is_liked=0)

        # 2. Old liked trend (45 days old, is_liked=1) -> MUST NOT BE DELETED
        t2_id = _insert_trend_with_custom_date(conn, source_id, "Old Liked 45d", "-45 days", is_liked=1)

        # 3. Fresh unliked trend (5 days old, is_liked=0) -> MUST NOT BE DELETED
        t3_id = _insert_trend_with_custom_date(conn, source_id, "Fresh Unliked 5d", "-5 days", is_liked=0)

        # 4. Fresh liked trend (5 days old, is_liked=1) -> MUST NOT BE DELETED
        t4_id = _insert_trend_with_custom_date(conn, source_id, "Fresh Liked 5d", "-5 days", is_liked=1)

        # 5. Old unliked trend (31 days old, is_liked=0) -> SHOULD BE DELETED
        t5_id = _insert_trend_with_custom_date(conn, source_id, "Old Unliked 31d", "-31 days", is_liked=0)

        # 6. Boundary unliked trend (29 days old, is_liked=0) -> MUST NOT BE DELETED
        t6_id = _insert_trend_with_custom_date(conn, source_id, "Recent Unliked 29d", "-29 days", is_liked=0)

    # Perform cleanup for 30 days threshold
    deleted = cleanup_old_unliked_trends(db_path=str(isolated_db), days=30)
    assert deleted == 2

    # Verify database contents
    with get_db_connection(str(isolated_db)) as conn:
        rows = conn.execute("SELECT id, original_text, is_liked FROM trends").fetchall()
        remaining_ids = {r["id"] for r in rows}

        # Deleted
        assert t1_id not in remaining_ids
        assert t5_id not in remaining_ids

        # Preserved
        assert t2_id in remaining_ids  # Old liked
        assert t3_id in remaining_ids  # Fresh unliked
        assert t4_id in remaining_ids  # Fresh liked
        assert t6_id in remaining_ids  # 29-day boundary unliked


def test_cleanup_custom_days_parameter(isolated_db):
    """Verify cleanup functions with custom days parameter (e.g. days=10)."""
    source_id = SourcesDAO.create(name="Custom Days", url="https://custom.com", source_type="rss")

    with get_db_connection(str(isolated_db)) as conn:
        # 15 days old, unliked -> deleted when days=10
        t1 = _insert_trend_with_custom_date(conn, source_id, "15d unliked", "-15 days", is_liked=0)
        # 15 days old, liked -> preserved
        t2 = _insert_trend_with_custom_date(conn, source_id, "15d liked", "-15 days", is_liked=1)
        # 5 days old, unliked -> preserved
        t3 = _insert_trend_with_custom_date(conn, source_id, "5d unliked", "-5 days", is_liked=0)

    deleted = cleanup_old_unliked_trends(db_path=str(isolated_db), days=10)
    assert deleted == 1

    with get_db_connection(str(isolated_db)) as conn:
        rows = conn.execute("SELECT id FROM trends").fetchall()
        remaining = {r["id"] for r in rows}
        assert remaining == {t2, t3}


def test_cleanup_negative_days_raises_value_error(isolated_db):
    """Verify cleanup rejects negative day thresholds."""
    with pytest.raises(ValueError, match="must be non-negative"):
        cleanup_old_unliked_trends(db_path=str(isolated_db), days=-5)


def test_cleanup_with_sources_trends_orphans(isolated_db):
    """Verify cleanup purges associated and orphaned rows in sources_trends table."""
    source_id = SourcesDAO.create(name="Source ST", url="https://st.com", source_type="rss")

    with get_db_connection(str(isolated_db)) as conn:
        # Create sources_trends table to simulate multi-source relations
        conn.execute("""
            CREATE TABLE IF NOT EXISTS sources_trends (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                source_id INTEGER NOT NULL,
                trend_id INTEGER NOT NULL
            )
        """)

        # Insert trends
        old_unliked_id = _insert_trend_with_custom_date(conn, source_id, "Old ST Unliked", "-40 days", is_liked=0)
        old_liked_id = _insert_trend_with_custom_date(conn, source_id, "Old ST Liked", "-40 days", is_liked=1)

        # Insert relations into sources_trends
        conn.execute("INSERT INTO sources_trends (source_id, trend_id) VALUES (?, ?)", (source_id, old_unliked_id))
        conn.execute("INSERT INTO sources_trends (source_id, trend_id) VALUES (?, ?)", (source_id, old_liked_id))
        # Already orphaned entry
        conn.execute("INSERT INTO sources_trends (source_id, trend_id) VALUES (?, ?)", (source_id, 99999))

    # Run cleanup
    deleted = cleanup_old_unliked_trends(db_path=str(isolated_db), days=30)
    assert deleted == 1

    with get_db_connection(str(isolated_db)) as conn:
        st_rows = conn.execute("SELECT trend_id FROM sources_trends").fetchall()
        st_trend_ids = {r["trend_id"] for r in st_rows}
        # Only the old_liked_id should remain in sources_trends
        assert st_trend_ids == {old_liked_id}


def test_cleanup_empty_db_and_missing_table(tmp_path):
    """Verify cleanup handles empty database or databases without a trends table gracefully."""
    empty_db = tmp_path / "empty.db"

    # Database without tables
    result = cleanup_old_unliked_trends(db_path=str(empty_db), days=30)
    assert result == 0


def test_cleanup_unliked_cli(isolated_db, monkeypatch):
    """Verify cleanup CLI entrypoint correctly parses arguments and runs cleanup."""
    source_id = SourcesDAO.create(name="CLI Source", url="https://cli.com", source_type="rss")

    with get_db_connection(str(isolated_db)) as conn:
        _insert_trend_with_custom_date(conn, source_id, "CLI Old Unliked", "-50 days", is_liked=0)
        _insert_trend_with_custom_date(conn, source_id, "CLI Old Liked", "-50 days", is_liked=1)

    # Simulate CLI arguments
    test_args = [
        "cleanup.py",
        "--days", "30",
        "--db-path", str(isolated_db),
    ]
    monkeypatch.setattr(sys, "argv", test_args)

    deleted = cleanup_unliked_cli()
    assert deleted == 1

    with get_db_connection(str(isolated_db)) as conn:
        rows = conn.execute("SELECT is_liked FROM trends").fetchall()
        assert len(rows) == 1
        assert rows[0]["is_liked"] == 1


def test_compatibility_module_exposure():
    """Verify backend/db/cleanup.py exposes the exact same cleanup functions."""
    assert compat_cleanup.cleanup_old_unliked_trends is cleanup_old_unliked_trends
    assert compat_cleanup.cleanup_unliked_cli is cleanup_unliked_cli
