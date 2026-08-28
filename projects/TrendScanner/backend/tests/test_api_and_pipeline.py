"""Integration tests for FastAPI endpoints, PipelineManager, and APScheduler (Stage 4)."""

import os
import tempfile
import pytest
from starlette.testclient import TestClient

from app.core.settings import settings
from app.db.database import init_db
from app.db.dao import SourcesDAO, TrendsDAO
from app.services.extractors.base import ExtractedItem
from app.services.pipeline import pipeline_manager
from app.workers.scheduler import get_scheduler_status, start_scheduler, shutdown_scheduler
from main import app


@pytest.fixture(autouse=True)
def isolated_db_environment(monkeypatch):
    """Ensure every test runs on a fresh, isolated temporary SQLite database."""
    with tempfile.TemporaryDirectory() as tmpdir:
        temp_db = os.path.join(tmpdir, "test_api.db")
        monkeypatch.setattr(settings, "DATABASE_PATH", temp_db)
        init_db(seed_default_sources=True)
        yield


@pytest.fixture
def client():
    """FastAPI TestClient fixture."""
    with TestClient(app) as test_client:
        yield test_client


def test_root_endpoint(client):
    """Verify root health check endpoint."""
    response = client.get("/")
    assert response.status_code == 200
    data = response.json()
    assert data["service"] == "TrendScanner"
    assert data["status"] == "online"


def test_system_status_endpoint(client):
    """Verify system status endpoint."""
    response = client.get("/api/system/status")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "operational"
    assert "scheduler" in data
    assert "stats" in data
    assert data["active_sources_count"] >= 3


def test_sources_crud_endpoints(client):
    """Verify CRUD endpoints for /api/sources."""
    # 1. List
    res = client.get("/api/sources")
    assert res.status_code == 200
    initial_sources = res.json()
    initial_count = len(initial_sources)

    # 2. Create
    new_src = {
        "name": "Test ProductHunt Feed",
        "url": "https://www.producthunt.com/feed",
        "source_type": "rss",
        "is_active": True,
    }
    create_res = client.post("/api/sources", json=new_src)
    assert create_res.status_code == 201
    created = create_res.json()
    assert created["id"] > 0
    assert created["name"] == new_src["name"]
    source_id = created["id"]

    # 3. Update
    update_data = {"name": "Updated Feed Name", "is_active": False}
    put_res = client.put(f"/api/sources/{source_id}", json=update_data)
    assert put_res.status_code == 200
    updated = put_res.json()
    assert updated["name"] == "Updated Feed Name"
    assert updated["is_active"] is False

    # 4. Delete
    del_res = client.delete(f"/api/sources/{source_id}")
    assert del_res.status_code == 200
    assert del_res.json()["deleted"] is True

    # 5. Verify Not Found
    get_again = client.put(f"/api/sources/{source_id}", json=update_data)
    assert get_again.status_code == 404


def test_trends_endpoints_and_review(client):
    """Verify trends listing, filtering, and review toggle endpoints."""
    # Seed sample trends
    source = SourcesDAO.get_all()[0]
    t1_id = TrendsDAO.create(
        source_id=source["id"],
        original_text="Micro-SaaS for automated PDF contract analysis with Stripe integration.",
        is_trend=True,
        trend_name="AI PDF Contracts",
        ai_score=9,
        scam_probability=5,
        ai_summary="High-demand B2B tool.",
        is_reviewed=False,
    )
    t2_id = TrendsDAO.create(
        source_id=source["id"],
        original_text="Low quality crypto project with suspicious claims.",
        is_trend=False,
        trend_name=None,
        ai_score=2,
        scam_probability=85,
        ai_summary="Potential scam.",
        is_reviewed=False,
    )

    # 1. Get all trends
    res = client.get("/api/trends")
    assert res.status_code == 200
    trends = res.json()
    assert len(trends) == 2

    # 2. Filter min_score >= 8
    res_filtered = client.get("/api/trends?min_score=8")
    assert res_filtered.status_code == 200
    items = res_filtered.json()
    assert len(items) == 1
    assert items[0]["trend_name"] == "AI PDF Contracts"

    # 3. Filter status=new
    res_new = client.get("/api/trends?status=new")
    assert len(res_new.json()) == 2

    # 4. Mark reviewed
    rev_res = client.put(f"/api/trends/{t1_id}/review", json={"is_reviewed": True})
    assert rev_res.status_code == 200
    assert rev_res.json()["is_reviewed"] is True

    # 5. Filter status=new after review
    res_new_after = client.get("/api/trends?status=new")
    assert len(res_new_after.json()) == 1

    # 6. Delete trend
    del_res = client.delete(f"/api/trends/{t2_id}")
    assert del_res.status_code == 200
    assert del_res.json()["deleted"] is True


@pytest.mark.asyncio
async def test_pipeline_manager_flow(monkeypatch):
    """Test full pipeline manager run across sources with mocked extractor and Groq."""
    from app.services.groq_client import AIClassificationResult

    # Mock Extractor
    sample_items = [
        ExtractedItem(
            title="AI Invoicing Assistant for Solo Freelancers",
            text="Solo freelancers waste 5 hours per week tracking invoices. An automated WhatsApp bot for invoice reminders solves this with high retention.",
            url="https://news.ycombinator.com/item?id=12345",
            source_type="rss",
        )
    ]

    class MockExtractor:
        async def extract(self, url: str):
            return sample_items

    monkeypatch.setattr("app.services.pipeline.get_extractor", lambda src_type: MockExtractor())

    # Mock Groq Client
    async def mock_classify(text: str):
        return AIClassificationResult(
            is_trend=True,
            trend_name="WhatsApp Invoice Bot",
            ai_score=8,
            scam_probability=5,
            ai_summary="Solves invoice tracking for freelancers via WhatsApp.",
        )

    monkeypatch.setattr("app.services.pipeline.groq_client.classify_text", mock_classify)

    # Execute pipeline
    summary = await pipeline_manager.run_all()
    assert summary["status"] == "completed"
    assert summary["new_trends_found"] > 0

    # Verify saved trend in DB
    trends = TrendsDAO.get_trends()
    assert len(trends) > 0
    assert trends[0]["trend_name"] == "WhatsApp Invoice Bot"
    assert trends[0]["ai_score"] == 8


@pytest.mark.asyncio
async def test_scheduler_lifecycle():
    """Verify scheduler start, status inspection, and shutdown."""
    start_scheduler()
    status_info = get_scheduler_status()
    assert "running" in status_info
    assert status_info["interval_minutes"] == settings.SCAN_INTERVAL_MINUTES
    shutdown_scheduler()
