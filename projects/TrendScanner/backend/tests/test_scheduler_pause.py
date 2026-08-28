"""Unit and integration tests for Scheduler Pause/Resume feature."""

import os
import tempfile
import pytest
from starlette.testclient import TestClient

from app.core.settings import settings
from app.db.database import init_db
from app.workers.scheduler import (
    get_scheduler_status,
    pause_scheduler,
    resume_scheduler,
    shutdown_scheduler,
    start_scheduler,
)
from main import app


@pytest.fixture(autouse=True)
def isolated_db_environment(monkeypatch):
    """Ensure every test runs on a fresh, isolated temporary SQLite database."""
    with tempfile.TemporaryDirectory() as tmpdir:
        temp_db = os.path.join(tmpdir, "test_pause.db")
        monkeypatch.setattr(settings, "DATABASE_PATH", temp_db)
        init_db(seed_default_sources=True)
        yield


@pytest.fixture
def client():
    """FastAPI TestClient fixture."""
    with TestClient(app) as test_client:
        yield test_client


def test_scheduler_pause_and_resume_direct():
    """Test pause_scheduler() and resume_scheduler() direct function state changes."""
    # Ensure resumed initially
    res_resume = resume_scheduler()
    assert res_resume["status"] == "running"
    assert res_resume["is_paused"] is False

    status = get_scheduler_status()
    assert status["is_paused"] is False

    # Pause
    res_pause = pause_scheduler()
    assert res_pause["status"] == "paused"
    assert res_pause["is_paused"] is True

    status_paused = get_scheduler_status()
    assert status_paused["is_paused"] is True
    assert status_paused["next_run_time"] is None

    # Resume again
    res_resume2 = resume_scheduler()
    assert res_resume2["status"] == "running"
    assert res_resume2["is_paused"] is False

    status_resumed = get_scheduler_status()
    assert status_resumed["is_paused"] is False


def test_scheduler_pause_with_running_scheduler(monkeypatch):
    """Test pause and resume with scheduler object."""
    import app.workers.scheduler as sched_mod
    from datetime import datetime, timezone

    class MockJob:
        def __init__(self):
            self.next_run_time = datetime(2026, 8, 28, 12, 0, 0, tzinfo=timezone.utc)

    class MockScheduler:
        def __init__(self):
            self.running = True
            self.paused = False

        def pause(self):
            self.paused = True

        def resume(self):
            self.paused = False

        def get_job(self, job_id):
            return MockJob()

    mock_sched = MockScheduler()
    monkeypatch.setattr(sched_mod, "APSCHEDULER_AVAILABLE", True)
    monkeypatch.setattr(sched_mod, "scheduler", mock_sched)

    try:
        resume_scheduler()
        status_initial = get_scheduler_status()
        assert status_initial["running"] is True
        assert status_initial["is_paused"] is False
        assert status_initial["next_run_time"] is not None

        # Pause
        pause_res = pause_scheduler()
        assert pause_res["is_paused"] is True
        assert mock_sched.paused is True

        status_paused = get_scheduler_status()
        assert status_paused["is_paused"] is True
        assert status_paused["next_run_time"] is None

        # Resume
        resume_res = resume_scheduler()
        assert resume_res["is_paused"] is False
        assert mock_sched.paused is False

        status_resumed = get_scheduler_status()
        assert status_resumed["is_paused"] is False
        assert status_resumed["next_run_time"] is not None
    finally:
        resume_scheduler()


def test_api_pause_and_resume_endpoints(client):
    """Test POST /api/system/pause and POST /api/system/resume endpoints."""
    # 1. Resume first
    r1 = client.post("/api/system/resume")
    assert r1.status_code == 200
    data1 = r1.json()
    assert data1["status"] == "running"
    assert data1["is_paused"] is False
    assert "возобновлено" in data1["message"]

    # 2. Check /api/system/status reflects is_paused: false
    status1 = client.get("/api/system/status").json()
    assert status1["is_paused"] is False
    assert status1["scheduler"]["is_paused"] is False

    # 3. Call /api/system/pause
    r2 = client.post("/api/system/pause")
    assert r2.status_code == 200
    data2 = r2.json()
    assert data2["status"] == "paused"
    assert data2["is_paused"] is True
    assert "приостановлено" in data2["message"]

    # 4. Check /api/system/status reflects is_paused: true and next_scan_time: null
    status2 = client.get("/api/system/status").json()
    assert status2["is_paused"] is True
    assert status2["scheduler"]["is_paused"] is True
    assert status2["next_scan_time"] is None

    # 5. Call /api/system/resume
    r3 = client.post("/api/system/resume")
    assert r3.status_code == 200
    data3 = r3.json()
    assert data3["status"] == "running"
    assert data3["is_paused"] is False

    # 6. Status reflects is_paused: false
    status3 = client.get("/api/system/status").json()
    assert status3["is_paused"] is False
    assert status3["scheduler"]["is_paused"] is False
