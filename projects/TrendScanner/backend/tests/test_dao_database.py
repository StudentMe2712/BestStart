"""Unit tests for SQLite database initialization, connection manager, and DAOs (Stage 1)."""

import sqlite3
from datetime import datetime, timezone
from pathlib import Path
import pytest

from app.core.settings import settings
from app.db.database import (
    DEFAULT_SOURCES,
    get_db_connection,
    get_db_path,
    init_db,
)
from app.db.dao import SourcesDAO, TrendsDAO, calculate_text_hash


@pytest.fixture
def isolated_db(tmp_path, monkeypatch):
    """Fixture to provide a clean, isolated SQLite database for each test."""
    db_file = tmp_path / "test_trendscanner.db"
    monkeypatch.setattr(settings, "DATABASE_PATH", str(db_file))
    init_db(seed_default_sources=False)
    yield db_file


@pytest.fixture
def seeded_db(tmp_path, monkeypatch):
    """Fixture providing a database initialized with default sources."""
    db_file = tmp_path / "test_trendscanner_seeded.db"
    monkeypatch.setattr(settings, "DATABASE_PATH", str(db_file))
    init_db(seed_default_sources=True)
    yield db_file


# ============================================================================
# Database Connection & Initialization Tests
# ============================================================================


def test_get_db_path(tmp_path, monkeypatch):
    """Verify get_db_path resolves the settings path and creates parent directory."""
    db_file = tmp_path / "sub_dir" / "test.db"
    monkeypatch.setattr(settings, "DATABASE_PATH", str(db_file))

    resolved_path = get_db_path()
    assert resolved_path == str(db_file)
    assert db_file.parent.exists()


def test_init_db_without_seed(isolated_db):
    """Verify init_db with seed_default_sources=False creates tables without seeding."""
    with get_db_connection() as conn:
        cursor = conn.execute("SELECT COUNT(*) as count FROM sources")
        assert cursor.fetchone()["count"] == 0

        cursor = conn.execute("SELECT COUNT(*) as count FROM trends")
        assert cursor.fetchone()["count"] == 0


def test_init_db_with_seed(seeded_db):
    """Verify init_db with seed_default_sources=True creates tables and seeds default sources."""
    with get_db_connection() as conn:
        cursor = conn.execute("SELECT * FROM sources ORDER BY id ASC")
        rows = cursor.fetchall()

        assert len(rows) == len(DEFAULT_SOURCES)
        assert rows[0]["name"] == DEFAULT_SOURCES[0][0]
        assert rows[0]["url"] == DEFAULT_SOURCES[0][1]
        assert rows[0]["source_type"] == DEFAULT_SOURCES[0][2]
        assert rows[0]["is_active"] == DEFAULT_SOURCES[0][3]


def test_init_db_idempotency(seeded_db):
    """Verify running init_db multiple times does not duplicate default sources."""
    # Run init_db again on an already seeded database
    init_db(seed_default_sources=True)

    with get_db_connection() as conn:
        cursor = conn.execute("SELECT COUNT(*) as count FROM sources")
        assert cursor.fetchone()["count"] == len(DEFAULT_SOURCES)


def test_get_db_connection_auto_commit(isolated_db):
    """Verify get_db_connection commits transaction on clean exit."""
    with get_db_connection() as conn:
        conn.execute(
            "INSERT INTO sources (name, url, source_type, is_active) VALUES (?, ?, ?, ?)",
            ("Test Source", "https://example.com", "rss", 1),
        )

    # Read in a new connection to confirm it was committed
    with get_db_connection() as conn:
        cursor = conn.execute("SELECT * FROM sources WHERE name = ?", ("Test Source",))
        row = cursor.fetchone()
        assert row is not None
        assert row["name"] == "Test Source"


def test_get_db_connection_rollback_on_exception(isolated_db):
    """Verify get_db_connection rolls back transaction when an unhandled error occurs."""
    with pytest.raises(ValueError, match="Simulated failure"):
        with get_db_connection() as conn:
            conn.execute(
                "INSERT INTO sources (name, url, source_type, is_active) VALUES (?, ?, ?, ?)",
                ("Should Rollback", "https://rollback.com", "reddit", 1),
            )
            raise ValueError("Simulated failure")

    # Read to verify row was not committed
    with get_db_connection() as conn:
        cursor = conn.execute("SELECT * FROM sources WHERE name = ?", ("Should Rollback",))
        assert cursor.fetchone() is None


def test_foreign_key_constraint_enforcement(isolated_db):
    """Verify foreign key constraint blocks inserting a trend with invalid source_id."""
    with pytest.raises(sqlite3.IntegrityError):
        with get_db_connection() as conn:
            conn.execute(
                """
                INSERT INTO trends (source_id, original_text, content_hash)
                VALUES (?, ?, ?)
                """,
                (9999, "Some trend text", "dummy_hash"),
            )


def test_foreign_key_cascade_delete(isolated_db):
    """Verify deleting a source cascades and deletes associated trends."""
    source_id = SourcesDAO.create(name="Cascade Source", url="https://cascade.com", source_type="rss")
    trend_id = TrendsDAO.create(source_id=source_id, original_text="Cascade Trend Item")

    assert TrendsDAO.get_by_id(trend_id) is not None

    # Delete source
    deleted = SourcesDAO.delete(source_id)
    assert deleted is True

    # Trend should also be deleted
    assert TrendsDAO.get_by_id(trend_id) is None


def test_sqlite_pragmas(isolated_db):
    """Verify SQLite connection has foreign keys enabled and WAL pragma applied."""
    with get_db_connection() as conn:
        cursor = conn.execute("PRAGMA foreign_keys")
        assert cursor.fetchone()[0] == 1

        cursor = conn.execute("PRAGMA journal_mode")
        journal_mode = cursor.fetchone()[0].lower()
        assert journal_mode in ["wal", "memory"]


# ============================================================================
# SourcesDAO Tests
# ============================================================================


def test_sources_dao_create(isolated_db):
    """Verify SourcesDAO.create inserts a source and returns valid ID."""
    source_id = SourcesDAO.create(
        name="TechCrunch",
        url="https://techcrunch.com/feed",
        source_type="rss",
        is_active=True,
    )
    assert source_id > 0

    source = SourcesDAO.get_by_id(source_id)
    assert source is not None
    assert source["id"] == source_id
    assert source["name"] == "TechCrunch"
    assert source["url"] == "https://techcrunch.com/feed"
    assert source["source_type"] == "rss"
    assert source["is_active"] == 1
    assert source["last_scanned"] is None


def test_sources_dao_get_all(isolated_db):
    """Verify SourcesDAO.get_all returns all sources or active only."""
    id1 = SourcesDAO.create(name="Active RSS", url="https://active.com/rss", source_type="rss", is_active=True)
    id2 = SourcesDAO.create(name="Inactive Reddit", url="https://reddit.com/r/test", source_type="reddit", is_active=False)
    id3 = SourcesDAO.create(name="Active Reddit", url="https://reddit.com/r/test2", source_type="reddit", is_active=True)

    all_sources = SourcesDAO.get_all(active_only=False)
    assert len(all_sources) == 3
    assert [s["id"] for s in all_sources] == [id1, id2, id3]

    active_sources = SourcesDAO.get_all(active_only=True)
    assert len(active_sources) == 2
    assert [s["id"] for s in active_sources] == [id1, id3]


def test_sources_dao_get_by_id_not_found(isolated_db):
    """Verify SourcesDAO.get_by_id returns None for non-existent ID."""
    assert SourcesDAO.get_by_id(99999) is None


def test_sources_dao_update(isolated_db):
    """Verify SourcesDAO.update updates specific fields accurately."""
    source_id = SourcesDAO.create(
        name="Old Name",
        url="https://old.com",
        source_type="rss",
        is_active=True,
    )

    # Partial update
    updated = SourcesDAO.update(
        source_id=source_id,
        name="New Name",
        is_active=False,
    )
    assert updated is True

    source = SourcesDAO.get_by_id(source_id)
    assert source["name"] == "New Name"
    assert source["url"] == "https://old.com"
    assert source["source_type"] == "rss"
    assert source["is_active"] == 0

    # Full update
    updated = SourcesDAO.update(
        source_id=source_id,
        name="Final Name",
        url="https://final.com",
        source_type="reddit",
        is_active=True,
    )
    assert updated is True
    source = SourcesDAO.get_by_id(source_id)
    assert source["name"] == "Final Name"
    assert source["url"] == "https://final.com"
    assert source["source_type"] == "reddit"
    assert source["is_active"] == 1


def test_sources_dao_update_edge_cases(isolated_db):
    """Verify SourcesDAO.update handles empty fields and non-existent ID."""
    source_id = SourcesDAO.create(name="Edge", url="https://edge.com", source_type="rss")

    # Calling update with no fields provided returns False
    assert SourcesDAO.update(source_id=source_id) is False

    # Updating non-existent ID returns False
    assert SourcesDAO.update(source_id=99999, name="Non-existent") is False


def test_sources_dao_update_last_scanned(isolated_db):
    """Verify SourcesDAO.update_last_scanned updates timestamp correctly."""
    source_id = SourcesDAO.create(name="Scanned Source", url="https://scanned.com", source_type="rss")

    # Default timestamp (current UTC)
    success = SourcesDAO.update_last_scanned(source_id)
    assert success is True

    source = SourcesDAO.get_by_id(source_id)
    assert source["last_scanned"] is not None

    # Custom timestamp
    custom_dt = datetime(2026, 8, 26, 15, 30, 0, tzinfo=timezone.utc)
    success = SourcesDAO.update_last_scanned(source_id, last_scanned=custom_dt)
    assert success is True

    source = SourcesDAO.get_by_id(source_id)
    assert source["last_scanned"] is not None
    assert isinstance(source["last_scanned"], (datetime, str))
    assert "2026-08-26" in str(source["last_scanned"])

    # Non-existent source
    assert SourcesDAO.update_last_scanned(99999) is False


def test_sources_dao_delete(isolated_db):
    """Verify SourcesDAO.delete deletes source and returns proper boolean."""
    source_id = SourcesDAO.create(name="To Delete", url="https://delete.com", source_type="rss")

    assert SourcesDAO.delete(source_id) is True
    assert SourcesDAO.get_by_id(source_id) is None

    # Delete non-existent ID
    assert SourcesDAO.delete(source_id) is False


# ============================================================================
# TrendsDAO Tests
# ============================================================================


def test_calculate_text_hash():
    """Verify calculate_text_hash normalizes casing and spacing."""
    text1 = "  Autonomous AI   Agents for  Market Research  "
    text2 = "autonomous ai agents for market research"
    text3 = "autonomous AI AGENTS   FOR market research\n"

    hash1 = calculate_text_hash(text1)
    hash2 = calculate_text_hash(text2)
    hash3 = calculate_text_hash(text3)

    assert hash1 == hash2 == hash3
    assert len(hash1) == 64  # SHA-256 hex digest length


def test_trends_dao_create(isolated_db):
    """Verify TrendsDAO.create inserts trend record and computes hash if missing."""
    source_id = SourcesDAO.create(name="Reddit AI", url="https://reddit.com/r/ai", source_type="reddit")

    trend_id = TrendsDAO.create(
        source_id=source_id,
        original_text="Deep dive into local LLM inference engines.",
        is_trend=True,
        trend_name="Local LLM Inference",
        ai_score=85,
        scam_probability=5,
        ai_summary="Growing interest in offline LLM runtimes.",
        source_url="https://reddit.com/r/ai/post1",
        is_reviewed=False,
    )
    assert trend_id > 0

    trend = TrendsDAO.get_by_id(trend_id)
    assert trend is not None
    assert trend["id"] == trend_id
    assert trend["source_id"] == source_id
    assert trend["source_name"] == "Reddit AI"
    assert trend["source_type"] == "reddit"
    assert trend["original_text"] == "Deep dive into local LLM inference engines."
    assert trend["content_hash"] == calculate_text_hash("Deep dive into local LLM inference engines.")
    assert trend["is_trend"] == 1
    assert trend["trend_name"] == "Local LLM Inference"
    assert trend["ai_score"] == 85
    assert trend["scam_probability"] == 5
    assert trend["ai_summary"] == "Growing interest in offline LLM runtimes."
    assert trend["source_url"] == "https://reddit.com/r/ai/post1"
    assert trend["is_reviewed"] == 0


def test_trends_dao_create_duplicate_hash_raises(isolated_db):
    """Verify inserting duplicate content_hash directly via create raises IntegrityError."""
    source_id = SourcesDAO.create(name="HN", url="https://news.ycombinator.com", source_type="rss")
    text = "Duplicate content test"
    content_hash = calculate_text_hash(text)

    TrendsDAO.create(source_id=source_id, original_text=text, content_hash=content_hash)

    with pytest.raises(sqlite3.IntegrityError):
        TrendsDAO.create(source_id=source_id, original_text=text, content_hash=content_hash)


def test_trends_dao_create_batch(isolated_db):
    """Verify TrendsDAO.create_batch inserts items and safely skips duplicate hashes."""
    source_id = SourcesDAO.create(name="Batch Source", url="https://batch.com", source_type="rss")

    items = [
        {"source_id": source_id, "original_text": "Post One", "ai_score": 80},
        {"source_id": source_id, "original_text": "Post Two", "ai_score": 70},
        {"source_id": source_id, "original_text": "Post One"},  # Duplicate of item 0
        {"source_id": source_id, "original_text": "Post Three", "ai_score": 90},
    ]

    inserted_count = TrendsDAO.create_batch(items)
    # Exactly 3 distinct items should be inserted
    assert inserted_count == 3

    # Empty batch
    assert TrendsDAO.create_batch([]) == 0


def test_trends_dao_get_by_id_not_found(isolated_db):
    """Verify TrendsDAO.get_by_id returns None for non-existent ID."""
    assert TrendsDAO.get_by_id(99999) is None


def test_trends_dao_get_trends_pagination_and_sorting(isolated_db):
    """Verify TrendsDAO.get_trends pagination (skip, limit) and sorting."""
    source_id = SourcesDAO.create(name="Source 1", url="https://s1.com", source_type="rss")

    for i in range(10):
        TrendsDAO.create(
            source_id=source_id,
            original_text=f"Trend item number {i}",
            ai_score=50 + i,
        )

    # Page 1: limit 4, skip 0
    page1 = TrendsDAO.get_trends(skip=0, limit=4)
    assert len(page1) == 4

    # Page 2: limit 4, skip 4
    page2 = TrendsDAO.get_trends(skip=4, limit=4)
    assert len(page2) == 4

    # Page 3: limit 4, skip 8
    page3 = TrendsDAO.get_trends(skip=8, limit=4)
    assert len(page3) == 2

    # Check distinct IDs across pages
    ids_page1 = [t["id"] for t in page1]
    ids_page2 = [t["id"] for t in page2]
    assert not set(ids_page1).intersection(set(ids_page2))


def test_trends_dao_get_trends_filtering(isolated_db):
    """Verify filtering by min_score, max_scam, status, source_id, and only_trends."""
    s1 = SourcesDAO.create(name="Source Alpha", url="https://alpha.com", source_type="rss")
    s2 = SourcesDAO.create(name="Source Beta", url="https://beta.com", source_type="reddit")

    # Item 1: High score, Low scam, New, Trend, s1
    t1 = TrendsDAO.create(
        source_id=s1,
        original_text="Alpha AI Trend",
        is_trend=True,
        ai_score=90,
        scam_probability=10,
        is_reviewed=False,
    )

    # Item 2: Medium score, High scam, Reviewed, Trend, s1
    t2 = TrendsDAO.create(
        source_id=s1,
        original_text="Alpha Scam Trend",
        is_trend=True,
        ai_score=60,
        scam_probability=75,
        is_reviewed=True,
    )

    # Item 3: Low score, Low scam, New, Not a trend, s2
    t3 = TrendsDAO.create(
        source_id=s2,
        original_text="Beta Normal Discussion",
        is_trend=False,
        ai_score=30,
        scam_probability=5,
        is_reviewed=False,
    )

    # 1. Filter by min_score
    res = TrendsDAO.get_trends(min_score=70)
    assert [r["id"] for r in res] == [t1]

    # 2. Filter by max_scam
    res = TrendsDAO.get_trends(max_scam=20)
    assert {r["id"] for r in res} == {t1, t3}

    # 3. Filter by status='new' (is_reviewed=0)
    res = TrendsDAO.get_trends(status="new")
    assert {r["id"] for r in res} == {t1, t3}

    # 4. Filter by status='reviewed' (is_reviewed=1)
    res = TrendsDAO.get_trends(status="reviewed")
    assert [r["id"] for r in res] == [t2]

    # 5. Filter by source_id
    res = TrendsDAO.get_trends(source_id=s2)
    assert [r["id"] for r in res] == [t3]

    # 6. Filter by only_trends=True / False
    res_trends = TrendsDAO.get_trends(only_trends=True)
    assert {r["id"] for r in res_trends} == {t1, t2}

    res_non_trends = TrendsDAO.get_trends(only_trends=False)
    assert [r["id"] for r in res_non_trends] == [t3]

    # 7. Combined filter: min_score=50, max_scam=80, status="reviewed"
    res_comb = TrendsDAO.get_trends(min_score=50, max_scam=80, status="reviewed")
    assert [r["id"] for r in res_comb] == [t2]


def test_trends_dao_mark_reviewed(isolated_db):
    """Verify TrendsDAO.mark_reviewed toggles is_reviewed column."""
    source_id = SourcesDAO.create(name="S1", url="https://s1.com", source_type="rss")
    trend_id = TrendsDAO.create(source_id=source_id, original_text="Review test", is_reviewed=False)

    assert TrendsDAO.get_by_id(trend_id)["is_reviewed"] == 0

    # Mark as reviewed
    assert TrendsDAO.mark_reviewed(trend_id, is_reviewed=True) is True
    assert TrendsDAO.get_by_id(trend_id)["is_reviewed"] == 1

    # Unmark
    assert TrendsDAO.mark_reviewed(trend_id, is_reviewed=False) is True
    assert TrendsDAO.get_by_id(trend_id)["is_reviewed"] == 0

    # Non-existent ID
    assert TrendsDAO.mark_reviewed(99999, is_reviewed=True) is False


def test_trends_dao_exists_by_hash(isolated_db):
    """Verify TrendsDAO.exists_by_hash accurately detects hash presence."""
    source_id = SourcesDAO.create(name="S1", url="https://s1.com", source_type="rss")
    h = calculate_text_hash("Unique Hash Test")

    assert TrendsDAO.exists_by_hash(h) is False

    TrendsDAO.create(source_id=source_id, original_text="Unique Hash Test", content_hash=h)

    assert TrendsDAO.exists_by_hash(h) is True


def test_trends_dao_get_existing_hashes(isolated_db):
    """Verify TrendsDAO.get_existing_hashes returns only the subset of present hashes."""
    source_id = SourcesDAO.create(name="S1", url="https://s1.com", source_type="rss")

    h1 = calculate_text_hash("Text 1")
    h2 = calculate_text_hash("Text 2")
    h3 = calculate_text_hash("Text 3")

    TrendsDAO.create(source_id=source_id, original_text="Text 1", content_hash=h1)
    TrendsDAO.create(source_id=source_id, original_text="Text 2", content_hash=h2)

    existing = TrendsDAO.get_existing_hashes([h1, h2, h3, "non_existent_hash"])
    assert existing == {h1, h2}

    # Empty list input
    assert TrendsDAO.get_existing_hashes([]) == set()


def test_trends_dao_get_stats_empty_db(isolated_db):
    """Verify TrendsDAO.get_stats returns safe defaults on empty database."""
    stats = TrendsDAO.get_stats()

    assert stats["total_count"] == 0
    assert stats["reviewed_count"] == 0
    assert stats["new_count"] == 0
    assert stats["confirmed_trends_count"] == 0
    assert stats["avg_score"] == 0.0
    assert stats["avg_scam_probability"] == 0.0


def test_trends_dao_get_stats_populated(isolated_db):
    """Verify TrendsDAO.get_stats aggregates counts and averages accurately."""
    source_id = SourcesDAO.create(name="S1", url="https://s1.com", source_type="rss")

    TrendsDAO.create(
        source_id=source_id,
        original_text="T1",
        is_trend=True,
        ai_score=80,
        scam_probability=10,
        is_reviewed=True,
    )
    TrendsDAO.create(
        source_id=source_id,
        original_text="T2",
        is_trend=True,
        ai_score=90,
        scam_probability=20,
        is_reviewed=False,
    )
    TrendsDAO.create(
        source_id=source_id,
        original_text="T3",
        is_trend=False,
        ai_score=50,
        scam_probability=30,
        is_reviewed=False,
    )

    stats = TrendsDAO.get_stats()

    assert stats["total_count"] == 3
    assert stats["reviewed_count"] == 1
    assert stats["new_count"] == 3
    assert stats["confirmed_trends_count"] == 2
    # avg_score = (80 + 90 + 50) / 3 = 73.333... -> rounded to 73.3
    assert stats["avg_score"] == 73.3
    # avg_scam = (10 + 20 + 30) / 3 = 20.0
    assert stats["avg_scam_probability"] == 20.0


def test_trends_dao_delete(isolated_db):
    """Verify TrendsDAO.delete deletes trend by ID and returns correct boolean."""
    source_id = SourcesDAO.create(name="S1", url="https://s1.com", source_type="rss")
    trend_id = TrendsDAO.create(source_id=source_id, original_text="To Delete")

    assert TrendsDAO.delete(trend_id) is True
    assert TrendsDAO.get_by_id(trend_id) is None

    # Delete non-existent ID
    assert TrendsDAO.delete(trend_id) is False
