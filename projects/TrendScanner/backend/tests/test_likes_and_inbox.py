"""Unit and integration tests for Level 5: Likes, Inbox Zero, and Source Monitoring.

Tests cover:
1. TrendsDAO.toggle_like behavior (toggle, explicit boolean, and non-existent IDs).
2. TrendsDAO.get_trends Inbox Zero filtering (tab='inbox', tab='liked', tab='all', default tab).
3. TrendsDAO.get_stats aggregation for liked_count and inbox_count.
4. FastAPI endpoints:
   - PATCH & PUT /api/trends/{id}/like (toggling, explicit setting, 404 for invalid IDs).
   - GET /api/trends?tab=liked, tab=inbox, tab=all.
   - GET /api/sources (verifying Telegram source monitoring configuration).
   - GET /api/system/status (verifying stats reporting liked and inbox metrics).
"""

from typing import Generator
import pytest
from starlette.testclient import TestClient

from app.core.settings import settings
from app.db.dao import SourcesDAO, TrendsDAO
from app.db.database import DEFAULT_SOURCES, get_db_connection, init_db
from main import app


# ============================================================================
# Test Fixtures
# ============================================================================


@pytest.fixture
def isolated_db(tmp_path, monkeypatch) -> Generator:
    """Fixture providing a clean, isolated temporary SQLite database for DAO testing."""
    db_file = tmp_path / "test_likes_inbox.db"
    monkeypatch.setattr(settings, "DATABASE_PATH", str(db_file))
    init_db(seed_default_sources=False)
    yield db_file


@pytest.fixture
def seeded_db(tmp_path, monkeypatch) -> Generator:
    """Fixture providing a temporary SQLite database initialized with default sources."""
    db_file = tmp_path / "test_likes_inbox_seeded.db"
    monkeypatch.setattr(settings, "DATABASE_PATH", str(db_file))
    init_db(seed_default_sources=True)
    yield db_file


@pytest.fixture
def client(tmp_path, monkeypatch) -> Generator:
    """FastAPI TestClient fixture with isolated SQLite database and lifecycle management."""
    db_file = tmp_path / "test_likes_inbox_api.db"
    monkeypatch.setattr(settings, "DATABASE_PATH", str(db_file))
    init_db(seed_default_sources=True)
    with TestClient(app) as test_client:
        yield test_client


# ============================================================================
# 1. TrendsDAO.toggle_like Tests
# ============================================================================


def test_dao_toggle_like_cycle(isolated_db):
    """Verify TrendsDAO.toggle_like toggles between True and False when is_liked is omitted."""
    source_id = SourcesDAO.create(name="Test Source", url="https://example.com", source_type="rss")
    trend_id = TrendsDAO.create(
        source_id=source_id,
        original_text="AI tool for automated document summarization.",
        is_liked=False,
    )

    # Initially is_liked should be 0 (False)
    initial_trend = TrendsDAO.get_by_id(trend_id)
    assert initial_trend is not None
    assert initial_trend["is_liked"] == 0

    # 1. First toggle: False -> True
    new_state = TrendsDAO.toggle_like(trend_id)
    assert new_state is True
    updated_trend = TrendsDAO.get_by_id(trend_id)
    assert updated_trend["is_liked"] == 1

    # 2. Second toggle: True -> False
    new_state = TrendsDAO.toggle_like(trend_id)
    assert new_state is False
    updated_trend = TrendsDAO.get_by_id(trend_id)
    assert updated_trend["is_liked"] == 0

    # 3. Third toggle: False -> True
    new_state = TrendsDAO.toggle_like(trend_id)
    assert new_state is True
    updated_trend = TrendsDAO.get_by_id(trend_id)
    assert updated_trend["is_liked"] == 1


def test_dao_toggle_like_explicit_values(isolated_db):
    """Verify TrendsDAO.toggle_like sets explicit boolean values when is_liked parameter is provided."""
    source_id = SourcesDAO.create(name="Test Source", url="https://example.com", source_type="rss")
    trend_id = TrendsDAO.create(
        source_id=source_id,
        original_text="B2B analytics platform for Shopify merchants.",
        is_liked=False,
    )

    # Set explicitly to True
    result = TrendsDAO.toggle_like(trend_id, is_liked=True)
    assert result is True
    trend = TrendsDAO.get_by_id(trend_id)
    assert trend["is_liked"] == 1

    # Set explicitly to True again (idempotent)
    result = TrendsDAO.toggle_like(trend_id, is_liked=True)
    assert result is True
    trend = TrendsDAO.get_by_id(trend_id)
    assert trend["is_liked"] == 1

    # Set explicitly to False
    result = TrendsDAO.toggle_like(trend_id, is_liked=False)
    assert result is False
    trend = TrendsDAO.get_by_id(trend_id)
    assert trend["is_liked"] == 0

    # Set explicitly to False again (idempotent)
    result = TrendsDAO.toggle_like(trend_id, is_liked=False)
    assert result is False
    trend = TrendsDAO.get_by_id(trend_id)
    assert trend["is_liked"] == 0


def test_dao_toggle_like_nonexistent_id(isolated_db):
    """Verify TrendsDAO.toggle_like returns None when trend_id does not exist."""
    assert TrendsDAO.toggle_like(999999) is None
    assert TrendsDAO.toggle_like(999999, is_liked=True) is None
    assert TrendsDAO.toggle_like(999999, is_liked=False) is None


# ============================================================================
# 2. TrendsDAO.get_trends Inbox Zero & Tab Filtering Tests
# ============================================================================


def test_dao_get_trends_inbox_zero_tabs(isolated_db):
    """Verify TrendsDAO.get_trends properly filters by inbox, liked, and all tabs."""
    source_id = SourcesDAO.create(name="Reddit SaaS", url="https://reddit.com/r/SaaS", source_type="reddit")

    # Create 2 unliked trends (Inbox) and 1 liked trend (Favorites)
    t1_id = TrendsDAO.create(
        source_id=source_id,
        original_text="Trend 1: Unliked idea for billing.",
        trend_name="Billing SaaS",
        is_liked=False,
    )
    t2_id = TrendsDAO.create(
        source_id=source_id,
        original_text="Trend 2: Unliked idea for CRM.",
        trend_name="Niche CRM",
        is_liked=False,
    )
    t3_id = TrendsDAO.create(
        source_id=source_id,
        original_text="Trend 3: Liked idea for AI transcription.",
        trend_name="AI Audio",
        is_liked=True,
    )

    # 1. Query with tab='inbox' -> returns only unliked trends (t1, t2)
    inbox_trends = TrendsDAO.get_trends(tab="inbox")
    inbox_ids = {t["id"] for t in inbox_trends}
    assert len(inbox_trends) == 2
    assert inbox_ids == {t1_id, t2_id}
    assert all(t["is_liked"] == 0 for t in inbox_trends)

    # 2. Query with default tab (None) -> defaults to Inbox Zero (unliked trends)
    default_trends = TrendsDAO.get_trends()
    default_ids = {t["id"] for t in default_trends}
    assert len(default_trends) == 2
    assert default_ids == {t1_id, t2_id}
    assert all(t["is_liked"] == 0 for t in default_trends)

    # 3. Query with tab='liked' -> returns only liked trend (t3)
    liked_trends = TrendsDAO.get_trends(tab="liked")
    liked_ids = {t["id"] for t in liked_trends}
    assert len(liked_trends) == 1
    assert liked_ids == {t3_id}
    assert liked_trends[0]["is_liked"] == 1
    assert liked_trends[0]["trend_name"] == "AI Audio"

    # 4. Query with tab='all' -> returns all 3 trends (t1, t2, t3)
    all_trends = TrendsDAO.get_trends(tab="all")
    all_ids = {t["id"] for t in all_trends}
    assert len(all_trends) == 3
    assert all_ids == {t1_id, t2_id, t3_id}


def test_dao_get_trends_is_liked_direct_filter(isolated_db):
    """Verify TrendsDAO.get_trends handles direct is_liked boolean filter."""
    source_id = SourcesDAO.create(name="HN Source", url="https://news.ycombinator.com/rss", source_type="rss")

    t1_id = TrendsDAO.create(source_id=source_id, original_text="Text 1", is_liked=False)
    t2_id = TrendsDAO.create(source_id=source_id, original_text="Text 2", is_liked=True)

    # Direct is_liked=True
    liked = TrendsDAO.get_trends(is_liked=True)
    assert len(liked) == 1
    assert liked[0]["id"] == t2_id

    # Direct is_liked=False
    unliked = TrendsDAO.get_trends(is_liked=False)
    assert len(unliked) == 1
    assert unliked[0]["id"] == t1_id


def test_dao_get_trends_tabs_combined_with_analytical_filters(isolated_db):
    """Verify Inbox Zero tabs work seamlessly in combination with score, scam, and source filters."""
    s1 = SourcesDAO.create(name="Source Alpha", url="https://alpha.com", source_type="rss")
    s2 = SourcesDAO.create(name="Source Beta", url="https://beta.com", source_type="reddit")

    # High score, liked, source s1
    t1_id = TrendsDAO.create(
        source_id=s1,
        original_text="Alpha Liked High Score",
        ai_score=9,
        scam_probability=5,
        is_liked=True,
        is_trend=True,
    )
    # High score, unliked, source s1
    t2_id = TrendsDAO.create(
        source_id=s1,
        original_text="Alpha Unliked High Score",
        ai_score=9,
        scam_probability=10,
        is_liked=False,
        is_trend=True,
    )
    # Low score, liked, source s2
    t3_id = TrendsDAO.create(
        source_id=s2,
        original_text="Beta Liked Low Score",
        ai_score=3,
        scam_probability=20,
        is_liked=True,
        is_trend=False,
    )

    # Filter tab='liked' AND min_score=8 -> only t1
    res = TrendsDAO.get_trends(tab="liked", min_score=8)
    assert len(res) == 1
    assert res[0]["id"] == t1_id

    # Filter tab='inbox' AND min_score=8 -> only t2
    res = TrendsDAO.get_trends(tab="inbox", min_score=8)
    assert len(res) == 1
    assert res[0]["id"] == t2_id

    # Filter tab='liked' AND source_id=s2 -> only t3
    res = TrendsDAO.get_trends(tab="liked", source_id=s2)
    assert len(res) == 1
    assert res[0]["id"] == t3_id

    # Filter tab='all' AND min_score=8 -> t1 and t2
    res = TrendsDAO.get_trends(tab="all", min_score=8)
    assert {r["id"] for r in res} == {t1_id, t2_id}


# ============================================================================
# 3. TrendsDAO.get_stats Tests
# ============================================================================


def test_dao_get_stats_includes_liked_and_inbox_counts_empty_db(isolated_db):
    """Verify TrendsDAO.get_stats returns 0 for liked_count and inbox_count when empty."""
    stats = TrendsDAO.get_stats()
    assert stats["total_count"] == 0
    assert stats["liked_count"] == 0
    assert stats["inbox_count"] == 0
    assert stats["reviewed_count"] == 0
    assert stats["new_count"] == 0
    assert stats["pending_ai_count"] == 0
    assert stats["avg_score"] == 0.0
    assert stats["avg_scam_probability"] == 0.0


def test_dao_get_stats_includes_liked_and_inbox_counts_populated(isolated_db):
    """Verify TrendsDAO.get_stats accurately aggregates liked_count, inbox_count, and total_count."""
    source_id = SourcesDAO.create(name="Stats Source", url="https://stats.com", source_type="rss")

    t1_id = TrendsDAO.create(
        source_id=source_id,
        original_text="Post 1",
        ai_score=8,
        scam_probability=10,
        is_liked=False,
        is_reviewed=False,
        is_trend=True,
    )
    t2_id = TrendsDAO.create(
        source_id=source_id,
        original_text="Post 2",
        ai_score=6,
        scam_probability=20,
        is_liked=False,
        is_reviewed=True,
        is_trend=True,
    )
    t3_id = TrendsDAO.create(
        source_id=source_id,
        original_text="Post 3",
        ai_score=10,
        scam_probability=0,
        is_liked=True,
        is_reviewed=False,
        is_trend=True,
    )

    stats = TrendsDAO.get_stats()
    assert stats["total_count"] == 3
    assert stats["liked_count"] == 1
    assert stats["inbox_count"] == 2
    assert stats["reviewed_count"] == 1
    assert stats["new_count"] == 3
    assert stats["confirmed_trends_count"] == 3
    # Average score: (8 + 6 + 10) / 3 = 8.0
    assert stats["avg_score"] == 8.0
    # Average scam: (10 + 20 + 0) / 3 = 10.0
    assert stats["avg_scam_probability"] == 10.0

    # Toggling like on an inbox trend shifts the stats
    TrendsDAO.toggle_like(t1_id, is_liked=True)
    updated_stats = TrendsDAO.get_stats()
    assert updated_stats["total_count"] == 3
    assert updated_stats["liked_count"] == 2
    assert updated_stats["inbox_count"] == 1


# ============================================================================
# 4. FastAPI Endpoints Tests (with TestClient)
# ============================================================================


def test_api_patch_like_endpoint_toggle_cycle(client):
    """Verify PATCH /api/trends/{id}/like toggles like status and returns 200 OK."""
    source = SourcesDAO.get_all()[0]
    trend_id = TrendsDAO.create(
        source_id=source["id"],
        original_text="Micro-SaaS idea for automated podcast chapter generation.",
        trend_name="Podcast Chapters",
        is_liked=False,
    )

    # 1. Toggle to True
    res1 = client.patch(f"/api/trends/{trend_id}/like")
    assert res1.status_code == 200
    data1 = res1.json()
    assert data1["trend_id"] == trend_id
    assert data1["is_liked"] is True
    assert data1["updated"] is True

    # Confirm in DB
    assert TrendsDAO.get_by_id(trend_id)["is_liked"] == 1

    # 2. Toggle back to False
    res2 = client.patch(f"/api/trends/{trend_id}/like")
    assert res2.status_code == 200
    data2 = res2.json()
    assert data2["trend_id"] == trend_id
    assert data2["is_liked"] is False
    assert data2["updated"] is True

    # Confirm in DB
    assert TrendsDAO.get_by_id(trend_id)["is_liked"] == 0


def test_api_patch_like_endpoint_not_found(client):
    """Verify PATCH /api/trends/{999999}/like returns 404 Not Found."""
    response = client.patch("/api/trends/999999/like")
    assert response.status_code == 404
    assert "not found" in response.json()["detail"].lower()


def test_api_put_like_endpoint_explicit_state(client):
    """Verify PUT /api/trends/{id}/like supports explicit boolean payload and returns 200 OK."""
    source = SourcesDAO.get_all()[0]
    trend_id = TrendsDAO.create(
        source_id=source["id"],
        original_text="AI video highlights generator for streamers.",
        trend_name="Stream Highlights",
        is_liked=False,
    )

    # Explicitly set is_liked = True
    res_true = client.put(f"/api/trends/{trend_id}/like", json={"is_liked": True})
    assert res_true.status_code == 200
    assert res_true.json()["is_liked"] is True
    assert TrendsDAO.get_by_id(trend_id)["is_liked"] == 1

    # Explicitly set is_liked = False
    res_false = client.put(f"/api/trends/{trend_id}/like", json={"is_liked": False})
    assert res_false.status_code == 200
    assert res_false.json()["is_liked"] is False
    assert TrendsDAO.get_by_id(trend_id)["is_liked"] == 0

    # 404 on non-existent trend
    res_404 = client.put("/api/trends/999999/like", json={"is_liked": True})
    assert res_404.status_code == 404


def test_api_get_trends_tab_filtering_endpoints(client):
    """Verify GET /api/trends tab filtering (?tab=liked, ?tab=inbox, ?tab=all)."""
    source = SourcesDAO.get_all()[0]

    # Create 2 unliked trends and 1 liked trend
    t1_id = TrendsDAO.create(
        source_id=source["id"],
        original_text="Inbox Item 1: Open-source Airtable alternative for self-hosting.",
        trend_name="Self-hosted DB",
        is_liked=False,
    )
    t2_id = TrendsDAO.create(
        source_id=source["id"],
        original_text="Inbox Item 2: AI resume optimizer for engineering roles.",
        trend_name="Resume AI",
        is_liked=False,
    )
    t3_id = TrendsDAO.create(
        source_id=source["id"],
        original_text="Liked Item: Developer observability tool for LLM token budgets.",
        trend_name="Token Observability",
        is_liked=True,
    )

    # 1. GET /api/trends?tab=liked -> returns only t3
    res_liked = client.get("/api/trends?tab=liked")
    assert res_liked.status_code == 200
    items_liked = res_liked.json()
    assert len(items_liked) == 1
    assert items_liked[0]["id"] == t3_id
    assert items_liked[0]["is_liked"] is True
    assert items_liked[0]["trend_name"] == "Token Observability"

    # 2. GET /api/trends?tab=inbox -> returns only t1 and t2
    res_inbox = client.get("/api/trends?tab=inbox")
    assert res_inbox.status_code == 200
    items_inbox = res_inbox.json()
    assert len(items_inbox) == 2
    inbox_ids = {item["id"] for item in items_inbox}
    assert inbox_ids == {t1_id, t2_id}
    assert all(item["is_liked"] is False for item in items_inbox)

    # 3. GET /api/trends (default tab) -> Inbox Zero behavior (unliked trends only)
    res_default = client.get("/api/trends")
    assert res_default.status_code == 200
    items_default = res_default.json()
    assert len(items_default) == 2
    default_ids = {item["id"] for item in items_default}
    assert default_ids == {t1_id, t2_id}

    # 4. GET /api/trends?tab=all -> returns all 3 trends
    res_all = client.get("/api/trends?tab=all")
    assert res_all.status_code == 200
    items_all = res_all.json()
    assert len(items_all) == 3
    all_ids = {item["id"] for item in items_all}
    assert all_ids == {t1_id, t2_id, t3_id}


def test_api_get_sources_telegram_monitoring(client):
    """Verify GET /api/sources returns list containing Telegram sources (source_type='telegram')."""
    res = client.get("/api/sources")
    assert res.status_code == 200
    sources = res.json()
    assert isinstance(sources, list)
    assert len(sources) >= len(DEFAULT_SOURCES)

    # Find Telegram sources
    telegram_sources = [s for s in sources if s["source_type"] == "telegram"]
    assert len(telegram_sources) >= 2

    # Verify expected default Telegram channels
    telegram_names = {s["name"] for s in telegram_sources}
    assert "Telegram: Tech Trends" in telegram_names
    assert "Telegram: AI & SaaS Radar" in telegram_names

    # Verify URLs and active status
    for s in telegram_sources:
        assert s["url"].startswith("https://t.me/")
        assert s["is_active"] is True


def test_api_system_status_reports_liked_and_inbox_metrics(client):
    """Verify GET /api/system/status returns correct liked_count and inbox_count in stats."""
    source = SourcesDAO.get_all()[0]

    # Create 1 liked trend and 2 unliked trends
    TrendsDAO.create(source_id=source["id"], original_text="Idea 1", is_liked=True)
    TrendsDAO.create(source_id=source["id"], original_text="Idea 2", is_liked=False)
    TrendsDAO.create(source_id=source["id"], original_text="Idea 3", is_liked=False)

    res = client.get("/api/system/status")
    assert res.status_code == 200
    data = res.json()

    assert data["status"] == "operational"
    assert "stats" in data
    stats = data["stats"]

    assert stats["total_count"] == 3
    assert stats["liked_count"] == 1
    assert stats["inbox_count"] == 2
    assert "database_count" in stats


# ============================================================================
# 5. Inbox Zero Archiving, Database Tab, Search & Candidate Sources Tests
# ============================================================================


def test_sources_dao_exists_and_get_by_url(isolated_db):
    """Verify SourcesDAO.exists_by_url and SourcesDAO.get_by_url."""
    url = "https://example.com/unique-feed"
    assert SourcesDAO.exists_by_url(url) is False
    assert SourcesDAO.get_by_url(url) is None

    src_id = SourcesDAO.create(name="Unique Source", url=url, source_type="rss")
    assert SourcesDAO.exists_by_url(url) is True
    
    src = SourcesDAO.get_by_url(url)
    assert src is not None
    assert src["id"] == src_id
    assert src["name"] == "Unique Source"
    assert src["url"] == url


def test_dao_archive_previous_inbox(isolated_db):
    """Verify TrendsDAO.archive_previous_inbox sets is_new = 0 only for unliked is_new = 1 trends."""
    src_id = SourcesDAO.create(name="Src", url="https://src.com", source_type="rss")

    # 1. is_new=1, is_liked=0 (Should be archived)
    t1 = TrendsDAO.create(source_id=src_id, original_text="Item 1", is_new=True, is_liked=False)
    # 2. is_new=1, is_liked=1 (Liked - should NOT be archived to is_new=0)
    t2 = TrendsDAO.create(source_id=src_id, original_text="Item 2", is_new=True, is_liked=True)
    # 3. is_new=0, is_liked=0 (Already archived)
    t3 = TrendsDAO.create(source_id=src_id, original_text="Item 3", is_new=False, is_liked=False)

    archived_count = TrendsDAO.archive_previous_inbox()
    assert archived_count == 1

    t1_row = TrendsDAO.get_by_id(t1)
    t2_row = TrendsDAO.get_by_id(t2)
    t3_row = TrendsDAO.get_by_id(t3)

    assert t1_row["is_new"] == 0
    assert t2_row["is_new"] == 1  # Liked stays is_new=1
    assert t3_row["is_new"] == 0


def test_dao_get_trends_database_tabs_and_search(isolated_db):
    """Verify TrendsDAO.get_trends with tab='database', 'history', 'archive', and search_query."""
    src_id = SourcesDAO.create(name="Src", url="https://src.com", source_type="rss")

    # Database item (is_new=0)
    t1 = TrendsDAO.create(
        source_id=src_id,
        original_text="Deep research in Kubernetes optimization tools",
        trend_name="K8s Optimizer",
        is_new=False,
        is_liked=False,
    )
    # Inbox item (is_new=1, is_liked=0)
    t2 = TrendsDAO.create(
        source_id=src_id,
        original_text="Brand new AI code generator for mobile apps",
        trend_name="Mobile AI",
        is_new=True,
        is_liked=False,
    )
    # Liked item (is_liked=1)
    t3 = TrendsDAO.create(
        source_id=src_id,
        original_text="Favorite billing analytics tool",
        trend_name="Billing Insights",
        is_new=True,
        is_liked=True,
    )

    # 1. Database tab queries
    db_items = TrendsDAO.get_trends(tab="database")
    assert len(db_items) == 1
    assert db_items[0]["id"] == t1

    hist_items = TrendsDAO.get_trends(tab="history")
    assert len(hist_items) == 1
    assert hist_items[0]["id"] == t1

    arch_items = TrendsDAO.get_trends(tab="archive")
    assert len(arch_items) == 1
    assert arch_items[0]["id"] == t1

    # 2. Search query across all
    search_k8s = TrendsDAO.get_trends(tab="all", search_query="Kubernetes")
    assert len(search_k8s) == 1
    assert search_k8s[0]["id"] == t1

    search_mobile = TrendsDAO.get_trends(tab="all", search_query="Mobile AI")
    assert len(search_mobile) == 1
    assert search_mobile[0]["id"] == t2


def test_extract_candidate_sources_helper():
    """Verify extract_candidate_sources extracts feedable sources: telegram, subreddits, substack, medium."""
    from app.services.sanitizer import extract_candidate_sources

    text = """
    Check out new discussions on r/SideProject and https://medium.com/tag/saas!
    You can also follow @cool_tech_channel or visit https://t.me/another_channel.
    Ignore images like https://example.com/logo.png.
    """
    candidates = extract_candidate_sources(text)
    urls = {c["url"] for c in candidates}

    assert "https://www.reddit.com/r/SideProject" in urls
    assert "https://medium.com/feed/tag/saas" in urls
    assert "https://t.me/cool_tech_channel" in urls
    assert "https://t.me/another_channel" in urls
    assert not any(u.endswith(".png") for u in urls)


def test_api_database_tab_and_search_endpoint(client):
    """Verify /api/trends?tab=database and ?search=term endpoints."""
    src = SourcesDAO.get_all()[0]

    # Database item
    TrendsDAO.create(
        source_id=src["id"],
        original_text="PostgreSQL automated partition management system",
        trend_name="PG Partitioning",
        is_new=False,
        is_liked=False,
    )
    # Inbox item
    TrendsDAO.create(
        source_id=src["id"],
        original_text="Solana trading terminal with fast DEX routing",
        trend_name="Solana DEX",
        is_new=True,
        is_liked=False,
    )

    # 1. tab=database
    res_db = client.get("/api/trends?tab=database")
    assert res_db.status_code == 200
    db_items = res_db.json()
    assert any(item["trend_name"] == "PG Partitioning" for item in db_items)
    assert not any(item["trend_name"] == "Solana DEX" for item in db_items)

    # 2. search=PostgreSQL
    res_search = client.get("/api/trends?tab=all&search=PostgreSQL")
    assert res_search.status_code == 200
    search_items = res_search.json()
    assert len(search_items) == 1
    assert search_items[0]["trend_name"] == "PG Partitioning"

