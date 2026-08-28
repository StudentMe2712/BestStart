"""Unit and integration tests for Level 10 RLHF Feedback Loop in Database, DAO, Schemas, and Routers."""

from typing import Generator
import pytest
from starlette.testclient import TestClient

from app.core.settings import settings
from app.db.dao import SourcesDAO, TrendsDAO
from app.db.database import get_db_connection, init_db
from main import app


@pytest.fixture
def isolated_db(tmp_path, monkeypatch) -> Generator:
    """Fixture providing a clean, isolated temporary SQLite database for DAO testing."""
    db_file = tmp_path / "test_rlhf_feedback.db"
    monkeypatch.setattr(settings, "DATABASE_PATH", str(db_file))
    init_db(seed_default_sources=False)
    yield db_file


@pytest.fixture
def client(tmp_path, monkeypatch) -> Generator:
    """FastAPI TestClient fixture with isolated SQLite database."""
    db_file = tmp_path / "test_rlhf_feedback_api.db"
    monkeypatch.setattr(settings, "DATABASE_PATH", str(db_file))
    init_db(seed_default_sources=True)
    with TestClient(app) as test_client:
        yield test_client


# ============================================================================
# 1. Database Schema & Migration Tests
# ============================================================================


def test_database_init_db_creates_user_feedback_column_and_index(isolated_db):
    """Verify user_feedback column and idx_trends_user_feedback index are created."""
    with get_db_connection() as conn:
        cols = {row["name"] for row in conn.execute("PRAGMA table_info(trends)").fetchall()}
        assert "user_feedback" in cols
        assert "is_liked" in cols
        assert "is_new" in cols

        indices = [row["name"] for row in conn.execute("PRAGMA index_list(trends)").fetchall()]
        assert "idx_trends_user_feedback" in indices


def test_database_migration_from_is_liked_to_user_feedback(tmp_path, monkeypatch):
    """Verify init_db migrates existing is_liked=1 rows to user_feedback=1 on existing tables."""
    db_file = tmp_path / "test_migration.db"
    monkeypatch.setattr(settings, "DATABASE_PATH", str(db_file))

    # Create older schema without user_feedback
    with get_db_connection() as conn:
        conn.execute("""
            CREATE TABLE sources (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                url TEXT NOT NULL,
                source_type TEXT NOT NULL,
                is_active INTEGER NOT NULL DEFAULT 1,
                last_scanned TIMESTAMP NULL
            );
        """)
        conn.execute("""
            CREATE TABLE trends (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                source_id INTEGER NOT NULL,
                original_text TEXT NOT NULL,
                content_hash TEXT UNIQUE,
                parsed_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                is_trend INTEGER NOT NULL DEFAULT 0,
                trend_name TEXT NULL,
                ai_score INTEGER NULL,
                scam_probability INTEGER NULL,
                ai_summary TEXT NULL,
                source_url TEXT NULL,
                is_reviewed INTEGER NOT NULL DEFAULT 0,
                ai_status TEXT NOT NULL DEFAULT 'pending',
                mention_count INTEGER NOT NULL DEFAULT 1,
                detailed_report TEXT NULL,
                is_liked INTEGER NOT NULL DEFAULT 0,
                is_new INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (source_id) REFERENCES sources(id) ON DELETE CASCADE
            );
        """)
        conn.execute("INSERT INTO sources (name, url, source_type) VALUES ('Old Source', 'https://old.com', 'rss')")
        conn.execute("INSERT INTO trends (source_id, original_text, is_liked) VALUES (1, 'Liked item', 1)")
        conn.execute("INSERT INTO trends (source_id, original_text, is_liked) VALUES (1, 'Unliked item', 0)")

    # Run init_db which should apply schema upgrade & migration
    init_db(seed_default_sources=False)

    with get_db_connection() as conn:
        cols = {row["name"] for row in conn.execute("PRAGMA table_info(trends)").fetchall()}
        assert "user_feedback" in cols

        rows = conn.execute("SELECT id, is_liked, user_feedback FROM trends ORDER BY id ASC").fetchall()
        assert len(rows) == 2
        # Liked item migrated to user_feedback = 1
        assert rows[0]["is_liked"] == 1
        assert rows[0]["user_feedback"] == 1
        # Unliked item stays user_feedback = 0
        assert rows[1]["is_liked"] == 0
        assert rows[1]["user_feedback"] == 0


# ============================================================================
# 2. TrendsDAO Feedback and Creation Tests
# ============================================================================


def test_dao_create_user_feedback_and_is_liked_computation(isolated_db):
    """Verify TrendsDAO.create correctly handles user_feedback and computes is_liked."""
    source_id = SourcesDAO.create(name="Source 1", url="https://s1.com", source_type="rss")

    # 1. Default creation (user_feedback = 0, is_liked = False)
    t1_id = TrendsDAO.create(source_id=source_id, original_text="Default item")
    t1 = TrendsDAO.get_by_id(t1_id)
    assert t1["user_feedback"] == 0
    assert t1["is_liked"] == 0

    # 2. Positive feedback (user_feedback = 1 -> is_liked = 1)
    t2_id = TrendsDAO.create(source_id=source_id, original_text="Positive item", user_feedback=1)
    t2 = TrendsDAO.get_by_id(t2_id)
    assert t2["user_feedback"] == 1
    assert t2["is_liked"] == 1

    # 3. Negative feedback (user_feedback = -1 -> is_liked = 0)
    t3_id = TrendsDAO.create(source_id=source_id, original_text="Negative item", user_feedback=-1)
    t3 = TrendsDAO.get_by_id(t3_id)
    assert t3["user_feedback"] == -1
    assert t3["is_liked"] == 0

    # 4. Legacy is_liked=True parameter -> is_liked = 1
    t4_id = TrendsDAO.create(source_id=source_id, original_text="Legacy liked item", is_liked=True)
    t4 = TrendsDAO.get_by_id(t4_id)
    assert t4["is_liked"] == 1


def test_dao_create_batch_user_feedback(isolated_db):
    """Verify TrendsDAO.create_batch supports user_feedback and legacy is_liked."""
    source_id = SourcesDAO.create(name="Batch Source", url="https://batch.com", source_type="rss")

    items = [
        {"source_id": source_id, "original_text": "Batch Item 1", "user_feedback": 1},
        {"source_id": source_id, "original_text": "Batch Item 2", "user_feedback": -1},
        {"source_id": source_id, "original_text": "Batch Item 3", "is_liked": True},
        {"source_id": source_id, "original_text": "Batch Item 4"},  # Default neutral
    ]

    inserted = TrendsDAO.create_batch(items)
    assert inserted == 4

    trends = TrendsDAO.get_trends(tab="all")
    trends_map = {t["original_text"]: t for t in trends}

    assert trends_map["Batch Item 1"]["user_feedback"] == 1
    assert trends_map["Batch Item 1"]["is_liked"] == 1

    assert trends_map["Batch Item 2"]["user_feedback"] == -1
    assert trends_map["Batch Item 2"]["is_liked"] == 0

    assert trends_map["Batch Item 3"]["user_feedback"] == 1
    assert trends_map["Batch Item 3"]["is_liked"] == 1

    assert trends_map["Batch Item 4"]["user_feedback"] == 0
    assert trends_map["Batch Item 4"]["is_liked"] == 0


def test_dao_set_feedback_clamping_and_synchronization(isolated_db):
    """Verify TrendsDAO.set_feedback clamps values and updates is_liked accordingly."""
    source_id = SourcesDAO.create(name="Feedback Source", url="https://fb.com", source_type="rss")
    t_id = TrendsDAO.create(source_id=source_id, original_text="Feedback target")

    # Set like (1)
    res = TrendsDAO.set_feedback(t_id, 1)
    assert res == 1
    row = TrendsDAO.get_by_id(t_id)
    assert row["user_feedback"] == 1
    assert row["is_liked"] == 1

    # Set dislike (-1)
    res = TrendsDAO.set_feedback(t_id, -1)
    assert res == -1
    row = TrendsDAO.get_by_id(t_id)
    assert row["user_feedback"] == -1
    assert row["is_liked"] == 0

    # Set neutral (0)
    res = TrendsDAO.set_feedback(t_id, 0)
    assert res == 0
    row = TrendsDAO.get_by_id(t_id)
    assert row["user_feedback"] == 0
    assert row["is_liked"] == 0

    # Test clamping: 10 -> 1
    res = TrendsDAO.set_feedback(t_id, 10)
    assert res == 1
    row = TrendsDAO.get_by_id(t_id)
    assert row["user_feedback"] == 1
    assert row["is_liked"] == 1

    # Test clamping: -100 -> -1
    res = TrendsDAO.set_feedback(t_id, -100)
    assert res == -1
    row = TrendsDAO.get_by_id(t_id)
    assert row["user_feedback"] == -1
    assert row["is_liked"] == 0

    # Non-existent trend ID
    assert TrendsDAO.set_feedback(999999, 1) is None


def test_dao_archive_previous_inbox_with_feedback(isolated_db):
    """Verify archive_previous_inbox only archives items where is_new = 1 AND user_feedback = 0."""
    source_id = SourcesDAO.create(name="Archive Src", url="https://asrc.com", source_type="rss")

    # 1. is_new=1, user_feedback=0 -> Should be archived (is_new -> 0)
    t1 = TrendsDAO.create(source_id=source_id, original_text="Inbox Neutral", is_new=True, user_feedback=0)
    # 2. is_new=1, user_feedback=1 -> Liked, NOT archived (is_new remains 1)
    t2 = TrendsDAO.create(source_id=source_id, original_text="Inbox Liked", is_new=True, user_feedback=1)
    # 3. is_new=1, user_feedback=-1 -> Disliked, NOT archived (is_new remains 1)
    t3 = TrendsDAO.create(source_id=source_id, original_text="Inbox Disliked", is_new=True, user_feedback=-1)
    # 4. is_new=0, user_feedback=0 -> Already in database
    t4 = TrendsDAO.create(source_id=source_id, original_text="Database Item", is_new=False, user_feedback=0)

    archived_count = TrendsDAO.archive_previous_inbox()
    assert archived_count == 1

    assert TrendsDAO.get_by_id(t1)["is_new"] == 0
    assert TrendsDAO.get_by_id(t2)["is_new"] == 1
    assert TrendsDAO.get_by_id(t3)["is_new"] == 1
    assert TrendsDAO.get_by_id(t4)["is_new"] == 0


# ============================================================================
# 3. TrendsDAO.get_trends & Tabs Filtering Tests
# ============================================================================


def test_dao_get_trends_feedback_tabs(isolated_db):
    """Verify get_trends filtering with tab='inbox', 'liked', 'disliked', 'database', 'all'."""
    source_id = SourcesDAO.create(name="Tabs Source", url="https://tabs.com", source_type="rss")

    t_inbox = TrendsDAO.create(source_id=source_id, original_text="Inbox Item", is_new=True, user_feedback=0)
    t_liked = TrendsDAO.create(source_id=source_id, original_text="Liked Item", is_new=True, user_feedback=1)
    t_disliked = TrendsDAO.create(source_id=source_id, original_text="Disliked Item", is_new=True, user_feedback=-1)
    t_db = TrendsDAO.create(source_id=source_id, original_text="DB Item", is_new=False, user_feedback=0)

    # 1. tab='inbox' (is_new=1, user_feedback=0)
    inbox = TrendsDAO.get_trends(tab="inbox")
    assert [t["id"] for t in inbox] == [t_inbox]

    # 2. tab='liked' (user_feedback=1)
    liked = TrendsDAO.get_trends(tab="liked")
    assert [t["id"] for t in liked] == [t_liked]

    # 3. tab='disliked' (user_feedback=-1)
    disliked = TrendsDAO.get_trends(tab="disliked")
    assert [t["id"] for t in disliked] == [t_disliked]

    # 4. tab='database' (is_new=0)
    db_items = TrendsDAO.get_trends(tab="database")
    assert [t["id"] for t in db_items] == [t_db]

    # 5. tab='all'
    all_items = TrendsDAO.get_trends(tab="all")
    assert len(all_items) == 4

    # 6. Direct user_feedback filter
    fb_pos = TrendsDAO.get_trends(user_feedback=1)
    assert [t["id"] for t in fb_pos] == [t_liked]

    fb_neg = TrendsDAO.get_trends(user_feedback=-1)
    assert [t["id"] for t in fb_neg] == [t_disliked]

    # 7. Direct is_liked filter
    liked_direct = TrendsDAO.get_trends(is_liked=True)
    assert [t["id"] for t in liked_direct] == [t_liked]


# ============================================================================
# 4. TrendsDAO.get_stats & RLHF Examples Tests
# ============================================================================


def test_dao_get_stats_includes_feedback_metrics(isolated_db):
    """Verify TrendsDAO.get_stats includes liked_count, disliked_count, inbox_count, database_count."""
    source_id = SourcesDAO.create(name="Stats Source", url="https://stats.com", source_type="rss")

    TrendsDAO.create(source_id=source_id, original_text="I1", is_new=True, user_feedback=0)
    TrendsDAO.create(source_id=source_id, original_text="I2", is_new=True, user_feedback=0)
    TrendsDAO.create(source_id=source_id, original_text="L1", is_new=True, user_feedback=1)
    TrendsDAO.create(source_id=source_id, original_text="D1", is_new=True, user_feedback=-1)
    TrendsDAO.create(source_id=source_id, original_text="DB1", is_new=False, user_feedback=0)

    stats = TrendsDAO.get_stats()
    assert stats["total_count"] == 5
    assert stats["inbox_count"] == 2
    assert stats["liked_count"] == 1
    assert stats["disliked_count"] == 1
    assert stats["database_count"] == 1


def test_dao_get_rlhf_examples(isolated_db):
    """Verify TrendsDAO.get_rlhf_examples returns positive and negative few-shot records."""
    source_id = SourcesDAO.create(name="RLHF Source", url="https://rlhf.com", source_type="rss")

    TrendsDAO.create(
        source_id=source_id,
        original_text="Positive trend text 1",
        trend_name="Pos Trend 1",
        ai_summary="Summary Pos 1",
        ai_score=9,
        user_feedback=1,
    )
    TrendsDAO.create(
        source_id=source_id,
        original_text="Positive trend text 2",
        trend_name="Pos Trend 2",
        ai_summary="Summary Pos 2",
        ai_score=8,
        user_feedback=1,
    )
    TrendsDAO.create(
        source_id=source_id,
        original_text="Negative trend text 1",
        trend_name="Neg Trend 1",
        ai_summary="Summary Neg 1",
        ai_score=2,
        user_feedback=-1,
    )
    TrendsDAO.create(
        source_id=source_id,
        original_text="Neutral trend text",
        trend_name="Neutral Trend",
        ai_summary="Summary Neutral",
        ai_score=5,
        user_feedback=0,
    )

    rlhf = TrendsDAO.get_rlhf_examples(limit_positive=2, limit_negative=2)
    assert len(rlhf["positive"]) == 2
    assert len(rlhf["negative"]) == 1

    pos1 = rlhf["positive"][0]
    assert pos1["trend_name"] == "Pos Trend 2"
    assert pos1["ai_summary"] == "Summary Pos 2"
    assert pos1["ai_score"] == 8
    assert "Positive trend text 2" in pos1["original_text"]

    neg1 = rlhf["negative"][0]
    assert neg1["trend_name"] == "Neg Trend 1"
    assert neg1["ai_score"] == 2


# ============================================================================
# 5. FastAPI Routers Feedback Endpoints Tests
# ============================================================================


def test_api_feedback_patch_and_put(client):
    """Verify PATCH and PUT /api/trends/{id}/feedback endpoints."""
    source = SourcesDAO.get_all()[0]
    trend_id = TrendsDAO.create(
        source_id=source["id"],
        original_text="SaaS platform for automated accounting reconciliation.",
        trend_name="Accounting SaaS",
        user_feedback=0,
    )

    # 1. PATCH like (score=1)
    res_like = client.patch(f"/api/trends/{trend_id}/feedback", json={"score": 1})
    assert res_like.status_code == 200
    data_like = res_like.json()
    assert data_like["trend_id"] == trend_id
    assert data_like["user_feedback"] == 1
    assert data_like["is_liked"] is True
    assert data_like["updated"] is True

    # Confirm in DB
    db_row = TrendsDAO.get_by_id(trend_id)
    assert db_row["user_feedback"] == 1
    assert db_row["is_liked"] == 1

    # 2. PUT dislike (score=-1)
    res_dislike = client.put(f"/api/trends/{trend_id}/feedback", json={"score": -1})
    assert res_dislike.status_code == 200
    data_dislike = res_dislike.json()
    assert data_dislike["trend_id"] == trend_id
    assert data_dislike["user_feedback"] == -1
    assert data_dislike["is_liked"] is False
    assert data_dislike["updated"] is True

    # Confirm in DB
    db_row = TrendsDAO.get_by_id(trend_id)
    assert db_row["user_feedback"] == -1
    assert db_row["is_liked"] == 0

    # 3. PATCH reset to neutral (score=0)
    res_neutral = client.patch(f"/api/trends/{trend_id}/feedback", json={"score": 0})
    assert res_neutral.status_code == 200
    assert res_neutral.json()["user_feedback"] == 0
    assert res_neutral.json()["is_liked"] is False

    # 4. 404 on non-existent trend ID
    res_404 = client.patch("/api/trends/999999/feedback", json={"score": 1})
    assert res_404.status_code == 404


def test_api_disliked_tab_and_user_feedback_query(client):
    """Verify /api/trends?tab=disliked and ?user_feedback=-1 / ?user_feedback=1."""
    source = SourcesDAO.get_all()[0]

    t_inbox = TrendsDAO.create(source_id=source["id"], original_text="Inbox 1", user_feedback=0)
    t_liked = TrendsDAO.create(source_id=source["id"], original_text="Liked 1", user_feedback=1)
    t_disliked = TrendsDAO.create(source_id=source["id"], original_text="Disliked 1", user_feedback=-1)

    # 1. tab=disliked
    res_disliked = client.get("/api/trends?tab=disliked")
    assert res_disliked.status_code == 200
    items = res_disliked.json()
    assert len(items) == 1
    assert items[0]["id"] == t_disliked
    assert items[0]["user_feedback"] == -1

    # 2. query param user_feedback=1
    res_fb_1 = client.get("/api/trends?user_feedback=1")
    assert res_fb_1.status_code == 200
    assert len(res_fb_1.json()) == 1
    assert res_fb_1.json()[0]["id"] == t_liked

    # 3. query param user_feedback=-1
    res_fb_neg = client.get("/api/trends?user_feedback=-1")
    assert res_fb_neg.status_code == 200
    assert len(res_fb_neg.json()) == 1
    assert res_fb_neg.json()[0]["id"] == t_disliked


def test_api_like_backward_compatibility(client):
    """Verify legacy PATCH and PUT /api/trends/{id}/like still work and sync user_feedback."""
    source = SourcesDAO.get_all()[0]
    t_id = TrendsDAO.create(source_id=source["id"], original_text="Legacy target", user_feedback=0)

    # 1. PUT like (is_liked=True)
    res = client.put(f"/api/trends/{t_id}/like", json={"is_liked": True})
    assert res.status_code == 200
    assert res.json()["is_liked"] is True
    row = TrendsDAO.get_by_id(t_id)
    assert row["user_feedback"] == 1
    assert row["is_liked"] == 1

    # 2. Toggle like off via PATCH (no body)
    res_toggle = client.patch(f"/api/trends/{t_id}/like")
    assert res_toggle.status_code == 200
    assert res_toggle.json()["is_liked"] is False
    row = TrendsDAO.get_by_id(t_id)
    assert row["user_feedback"] == 0
    assert row["is_liked"] == 0
