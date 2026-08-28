"""Unit and integration tests for Level 4 Deep Report generation, DAO, and API endpoint."""

import os
import tempfile
from unittest.mock import AsyncMock, patch
import pytest
from starlette.testclient import TestClient

from app.core.settings import settings
from app.db.database import init_db
from app.db.dao import SourcesDAO, TrendsDAO
from app.services.groq_client import (
    GroqClient,
    DEEP_REPORT_SYSTEM_PROMPT,
    groq_client,
)
from main import app


# ============================================================================
# Test Database & TestClient Fixtures
# ============================================================================


@pytest.fixture
def isolated_db(monkeypatch):
    """Provide a clean isolated temporary SQLite database for DAO and API tests."""
    with tempfile.TemporaryDirectory() as tmpdir:
        temp_db = os.path.join(tmpdir, "test_deep_reports.db")
        monkeypatch.setattr(settings, "DATABASE_PATH", temp_db)
        init_db(seed_default_sources=True)
        yield temp_db


@pytest.fixture
def client(isolated_db):
    """FastAPI TestClient fixture with isolated SQLite database."""
    with TestClient(app) as test_client:
        yield test_client


# ============================================================================
# GroqClient.generate_deep_report() Unit Tests
# ============================================================================


@pytest.mark.asyncio
async def test_groq_client_generate_deep_report_success():
    """Test GroqClient.generate_deep_report() mocking _call_api_with_retry."""
    client = GroqClient(api_key="gsk_mock_test_key_123")

    mock_report_content = """
### 🎯 1. Суть и ценность продукта
- Автоматизированный генератор B2B инвойсов для фрилансеров.

### 👥 2. Целевая аудитория и сегменты
- Фрилансеры и дизайн-агентства, готовность платить $15-30/мес.

### ⚠️ 3. Риски и барьеры входа
- Конкуренция со стороны QuickBooks.

### 🚀 4. План запуска MVP за 2 недели
- Развертывание бота Telegram + Stripe Checkout.

### 💰 5. Модель монетизации и юнит-экономика
- Подписочная модель SaaS с 90% валовой маржой.
""".strip()

    client._call_api_with_retry = AsyncMock(return_value=mock_report_content)

    result = await client.generate_deep_report(
        text="Фрилансеры тратят 5 часов в неделю на выставление счетов.",
        trend_name="B2B Invoice Bot",
    )

    assert result == mock_report_content
    client._call_api_with_retry.assert_called_once()

    # Check arguments passed to _call_api_with_retry
    call_args, call_kwargs = client._call_api_with_retry.call_args
    messages = call_args[0]

    assert len(messages) == 2
    assert messages[0]["role"] == "system"
    assert messages[0]["content"] == DEEP_REPORT_SYSTEM_PROMPT
    assert messages[1]["role"] == "user"
    assert "B2B Invoice Bot" in messages[1]["content"]
    assert "выставление счетов" in messages[1]["content"]

    assert call_kwargs.get("temperature") == 0.3
    assert call_kwargs.get("json_mode") is False


@pytest.mark.asyncio
async def test_groq_client_generate_deep_report_without_trend_name():
    """Test generate_deep_report formats prompt correctly when trend_name is empty."""
    client = GroqClient(api_key="gsk_mock_test_key_123")
    client._call_api_with_retry = AsyncMock(return_value="### 🎯 1. Суть продукта")

    result = await client.generate_deep_report(
        text="Описание тренда для генерации отчета.",
        trend_name="",
    )

    assert result == "### 🎯 1. Суть продукта"
    call_args, _ = client._call_api_with_retry.call_args
    messages = call_args[0]
    # Should not include "(Тема: )"
    assert "(Тема:" not in messages[1]["content"]
    assert "Описание тренда для генерации отчета." in messages[1]["content"]



@pytest.mark.asyncio
async def test_groq_client_generate_deep_report_empty_text():
    """Test generate_deep_report returns None immediately when input text is empty or None."""
    client = GroqClient(api_key="gsk_mock_test_key_123")
    client._call_api_with_retry = AsyncMock()

    assert await client.generate_deep_report("") is None
    assert await client.generate_deep_report("   \n\t  ") is None
    assert await client.generate_deep_report(None) is None
    client._call_api_with_retry.assert_not_called()


@pytest.mark.asyncio
async def test_groq_client_generate_deep_report_exception_handling():
    """Test generate_deep_report catches exceptions and returns None gracefully."""
    client = GroqClient(api_key="gsk_mock_test_key_123")
    client._call_api_with_retry = AsyncMock(side_effect=RuntimeError("Groq API 500 error"))

    result = await client.generate_deep_report(
        text="Valid text that fails during API request.",
        trend_name="Failing Trend",
    )

    assert result is None
    client._call_api_with_retry.assert_called_once()


# ============================================================================
# TrendsDAO.save_detailed_report() Unit Tests
# ============================================================================


def test_trends_dao_save_detailed_report(isolated_db):
    """Test TrendsDAO.save_detailed_report() saves markdown report and handles invalid IDs."""
    source = SourcesDAO.get_all()[0]
    trend_id = TrendsDAO.create(
        source_id=source["id"],
        original_text="Original text for deep report test.",
        is_trend=True,
        trend_name="AI Medical Notes",
        ai_score=9,
        scam_probability=0,
    )

    # Initial state should have no detailed_report
    trend_before = TrendsDAO.get_by_id(trend_id)
    assert trend_before is not None
    assert trend_before.get("detailed_report") is None

    # Save detailed report
    report_text = "### 🎯 1. Суть и ценность продукта\n- ИИ-ассистент врача для заполнения карт."
    saved = TrendsDAO.save_detailed_report(trend_id, report_text)
    assert saved is True

    # Verify report is persisted in database
    trend_after = TrendsDAO.get_by_id(trend_id)
    assert trend_after is not None
    assert trend_after["detailed_report"] == report_text

    # Overwrite detailed report with updated content
    updated_report = "### 🎯 1. Суть (Обновлено)"
    saved_again = TrendsDAO.save_detailed_report(trend_id, updated_report)
    assert saved_again is True

    trend_updated = TrendsDAO.get_by_id(trend_id)
    assert trend_updated["detailed_report"] == updated_report

    # Attempt saving to non-existent trend ID
    saved_non_existent = TrendsDAO.save_detailed_report(99999, "Report for non-existent trend")
    assert saved_non_existent is False


# ============================================================================
# POST /api/trends/{id}/report Endpoint Integration Tests
# ============================================================================


def test_post_trend_report_not_found(client):
    """Test POST /api/trends/{id}/report when trend does not exist -> 404 Not Found."""
    response = client.post("/api/trends/99999/report")
    assert response.status_code == 404
    data = response.json()
    assert "Trend with ID #99999 not found" in data["detail"]


def test_post_trend_report_generates_and_caches_new_report(client, monkeypatch):
    """Test POST /api/trends/{id}/report when report not cached -> generates via Groq, saves to SQLite DB, returns 200."""
    source = SourcesDAO.get_all()[0]
    trend_id = TrendsDAO.create(
        source_id=source["id"],
        original_text="Micro-SaaS for cold email personalization using LinkedIn scraping.",
        is_trend=True,
        trend_name="Cold Email AI",
        ai_score=8,
        scam_probability=5,
    )

    generated_report_markdown = """
### 🎯 1. Суть и ценность продукта
- Автоматизация написания холодных писем.

### 👥 2. Целевая аудитория и сегменты
- B2B отделы продаж, агентства лидогенерации.

### 🚀 4. План запуска MVP за 2 недели
- Chrome extension + OpenAI API.
""".strip()

    mock_generate = AsyncMock(return_value=generated_report_markdown)
    monkeypatch.setattr(groq_client, "generate_deep_report", mock_generate)

    # 1. First call: generate report
    response = client.post(f"/api/trends/{trend_id}/report")
    assert response.status_code == 200

    data = response.json()
    assert data["trend_id"] == trend_id
    assert data["trend_name"] == "Cold Email AI"
    assert data["detailed_report"] == generated_report_markdown

    mock_generate.assert_called_once_with(
        text="Micro-SaaS for cold email personalization using LinkedIn scraping.",
        trend_name="Cold Email AI",
    )

    # Verify report is now saved in SQLite DB
    trend_in_db = TrendsDAO.get_by_id(trend_id)
    assert trend_in_db["detailed_report"] == generated_report_markdown


def test_post_trend_report_returns_cached_immediately(client, monkeypatch):
    """Test POST /api/trends/{id}/report when report already cached -> returns immediately without calling Groq."""
    source = SourcesDAO.get_all()[0]
    cached_report_text = "### 🎯 Cached Report Content Already in DB"

    trend_id = TrendsDAO.create(
        source_id=source["id"],
        original_text="Some trend text that already has a cached report.",
        is_trend=True,
        trend_name="Cached Trend Item",
        ai_score=9,
    )
    TrendsDAO.save_detailed_report(trend_id, cached_report_text)

    # Mock GroqClient to verify it is NOT called
    mock_generate = AsyncMock(side_effect=AssertionError("Groq client should not be called when cached!"))
    monkeypatch.setattr(groq_client, "generate_deep_report", mock_generate)

    # Call endpoint
    response = client.post(f"/api/trends/{trend_id}/report")
    assert response.status_code == 200

    data = response.json()
    assert data["trend_id"] == trend_id
    assert data["trend_name"] == "Cached Trend Item"
    assert data["detailed_report"] == cached_report_text

    # Verify generate was never invoked
    mock_generate.assert_not_called()


def test_post_trend_report_groq_failure_returns_502(client, monkeypatch):
    """Test POST /api/trends/{id}/report returns 502 Bad Gateway when Groq generation fails."""
    source = SourcesDAO.get_all()[0]
    trend_id = TrendsDAO.create(
        source_id=source["id"],
        original_text="Failing generation trend item.",
        is_trend=True,
        trend_name="Failing Item",
    )

    mock_generate = AsyncMock(return_value=None)
    monkeypatch.setattr(groq_client, "generate_deep_report", mock_generate)

    response = client.post(f"/api/trends/{trend_id}/report")
    assert response.status_code == 502
    data = response.json()
    assert "Failed to generate deep report via Groq AI" in data["detail"]

    # Verify nothing was saved in DB
    trend_in_db = TrendsDAO.get_by_id(trend_id)
    assert trend_in_db["detailed_report"] is None
