"""Unit tests for Telegram Notifier push service (Level 4)."""

import html
from unittest.mock import AsyncMock, patch
import pytest
import httpx

from app.core.settings import settings
from app.services.notifier import TelegramNotifier, notifier


# ============================================================================
# TelegramNotifier Initialization & URL formatting
# ============================================================================


def test_notifier_init_defaults(monkeypatch):
    """Verify TelegramNotifier initializes with settings defaults."""
    monkeypatch.setattr(settings, "TG_BOT_TOKEN", "default_token_123")
    monkeypatch.setattr(settings, "TG_CHAT_ID", "default_chat_456")

    client = TelegramNotifier()
    assert client.bot_token == "default_token_123"
    assert client.chat_id == "default_chat_456"
    assert client.timeout == 10.0
    assert client._get_api_url() == "https://api.telegram.org/botdefault_token_123/sendMessage"


def test_notifier_init_custom():
    """Verify TelegramNotifier initializes with custom parameters."""
    client = TelegramNotifier(
        bot_token="custom_token_abc",
        chat_id="custom_chat_xyz",
        timeout=15.0,
    )
    assert client.bot_token == "custom_token_abc"
    assert client.chat_id == "custom_chat_xyz"
    assert client.timeout == 15.0
    assert client._get_api_url() == "https://api.telegram.org/botcustom_token_abc/sendMessage"


def test_global_notifier_instance():
    """Verify global notifier singleton instance is configured."""
    assert isinstance(notifier, TelegramNotifier)


# ============================================================================
# Alert Message HTML Formatting Tests
# ============================================================================


def test_format_alert_message_standard():
    """Test format_alert_message formatting with HTML tags, emojis, score, scam probability, title, summary, and link."""
    client = TelegramNotifier(bot_token="test_token", chat_id="test_chat")

    msg = client.format_alert_message(
        trend_name="Микро-SaaS для клиник",
        ai_score=9,
        scam_probability=5,
        ai_summary="Сервис автоматической записи пациентов через WhatsApp.",
        source_url="https://example.com/item/100",
        mention_count=1,
    )

    # Check header
    assert "💎 <b>ОБНАРУЖЕН ПЕРСПЕКТИВНЫЙ ТРЕНД</b> 💎" in msg

    # Check title
    assert "📌 <b>Название:</b> Микро-SaaS для клиник" in msg

    # Check score and scam probability
    assert "⭐ <b>AI Score:</b> 9/10" in msg
    assert "🛡 <b>Scam Risk:</b> 5%" in msg

    # Single mention should NOT have mention count badge
    assert "🔥 <b>Упоминаний на площадках:</b>" not in msg

    # Check summary
    assert "📝 <b>Аналитическое резюме:</b>\nСервис автоматической записи пациентов через WhatsApp." in msg

    # Check source URL link
    assert '🔗 <a href="https://example.com/item/100">Открыть первоисточник</a>' in msg


def test_format_alert_message_with_multiple_mentions():
    """Test format_alert_message includes viral mention count badge when mention_count > 1."""
    client = TelegramNotifier()

    msg = client.format_alert_message(
        trend_name="B2B Автоматизация",
        ai_score=8,
        scam_probability=10,
        ai_summary="Платформа интеграций CRM и 1С.",
        source_url="https://news.ycombinator.com/item?id=999",
        mention_count=4,
    )

    assert "🔥 <b>Упоминаний на площадках:</b> 4" in msg
    assert "📌 <b>Название:</b> B2B Автоматизация" in msg
    assert "⭐ <b>AI Score:</b> 8/10" in msg


def test_format_alert_message_html_escaping():
    """Test format_alert_message properly escapes raw HTML in trend_name and summary to prevent injection."""
    client = TelegramNotifier()

    msg = client.format_alert_message(
        trend_name="<script>alert('xss')</script> & 'Quotes'",
        ai_score=7,
        scam_probability=15,
        ai_summary="Summary with <tags> & symbols.",
        source_url=None,
        mention_count=1,
    )

    # Raw script tags should not be present
    assert "<script>" not in msg
    assert "</script>" not in msg

    # HTML-escaped content should be present
    assert html.escape("<script>alert('xss')</script> & 'Quotes'") in msg
    assert html.escape("Summary with <tags> & symbols.") in msg
    assert '🔗 <a href="#">Открыть первоисточник</a>' in msg


def test_format_alert_message_fallback_defaults():
    """Test format_alert_message fallbacks for empty or None fields."""
    client = TelegramNotifier()

    msg = client.format_alert_message(
        trend_name="",
        ai_score=1,
        scam_probability=0,
        ai_summary="",
        source_url=None,
        mention_count=0,
    )

    assert "📌 <b>Название:</b> Новый тренд" in msg
    assert "📝 <b>Аналитическое резюме:</b>\nНет описания" in msg
    assert '🔗 <a href="#">Открыть первоисточник</a>' in msg
    assert "🔥 <b>Упоминаний на площадках:</b>" not in msg


# ============================================================================
# Send Trend Alert Async Tests (Success, Failures, Skipped)
# ============================================================================


@pytest.mark.asyncio
async def test_send_trend_alert_success(monkeypatch):
    """Test send_trend_alert success case mocking httpx.AsyncClient.post returning 200 OK."""
    client = TelegramNotifier(bot_token="valid_token_123", chat_id="123456789")

    mock_post = AsyncMock()
    mock_post.return_value.status_code = 200

    class MockAsyncClient:
        def __init__(self, *args, **kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, exc_type, exc_val, exc_tb):
            pass

        post = mock_post

    monkeypatch.setattr(httpx, "AsyncClient", MockAsyncClient)

    result = await client.send_trend_alert(
        trend_name="AI Legal Assistant",
        ai_score=9,
        scam_probability=2,
        ai_summary="Contract analysis tool for SMBs.",
        source_url="https://example.com/legal-ai",
        mention_count=2,
    )

    assert result is True
    mock_post.assert_called_once()

    # Check called URL and json payload
    call_args, call_kwargs = mock_post.call_args
    assert call_args[0] == "https://api.telegram.org/botvalid_token_123/sendMessage"

    payload = call_kwargs.get("json", {})
    assert payload["chat_id"] == "123456789"
    assert payload["parse_mode"] == "HTML"
    assert payload["disable_web_page_preview"] is False
    assert "AI Legal Assistant" in payload["text"]
    assert "🔥 <b>Упоминаний на площадках:</b> 2" in payload["text"]


@pytest.mark.asyncio
async def test_send_trend_alert_http_error(monkeypatch):
    """Test send_trend_alert returns False when Telegram API returns non-200 HTTP status."""
    client = TelegramNotifier(bot_token="valid_token_123", chat_id="123456789")

    mock_post = AsyncMock()
    mock_response = AsyncMock()
    mock_response.status_code = 400
    mock_response.text = '{"ok": false, "description": "Bad Request: chat not found"}'
    mock_post.return_value = mock_response

    class MockAsyncClient:
        def __init__(self, *args, **kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, exc_type, exc_val, exc_tb):
            pass

        post = mock_post

    monkeypatch.setattr(httpx, "AsyncClient", MockAsyncClient)

    result = await client.send_trend_alert(
        trend_name="Test Trend",
        ai_score=8,
        scam_probability=5,
        ai_summary="Valid summary.",
    )

    assert result is False
    mock_post.assert_called_once()


@pytest.mark.asyncio
async def test_send_trend_alert_network_exception(monkeypatch):
    """Test send_trend_alert failure / network exception handling returning False."""
    client = TelegramNotifier(bot_token="valid_token_123", chat_id="123456789")

    mock_post = AsyncMock(side_effect=httpx.ConnectTimeout("Connection timed out to api.telegram.org"))

    class MockAsyncClient:
        def __init__(self, *args, **kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, exc_type, exc_val, exc_tb):
            pass

        post = mock_post

    monkeypatch.setattr(httpx, "AsyncClient", MockAsyncClient)

    result = await client.send_trend_alert(
        trend_name="Test Trend",
        ai_score=8,
        scam_probability=5,
        ai_summary="Valid summary.",
    )

    assert result is False
    mock_post.assert_called_once()


@pytest.mark.asyncio
async def test_send_trend_alert_generic_exception(monkeypatch):
    """Test send_trend_alert catches unexpected generic exceptions and returns False."""
    client = TelegramNotifier(bot_token="valid_token_123", chat_id="123456789")

    mock_post = AsyncMock(side_effect=RuntimeError("Unexpected client crash"))

    class MockAsyncClient:
        def __init__(self, *args, **kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, exc_type, exc_val, exc_tb):
            pass

        post = mock_post

    monkeypatch.setattr(httpx, "AsyncClient", MockAsyncClient)

    result = await client.send_trend_alert(
        trend_name="Test Trend",
        ai_score=8,
        scam_probability=5,
        ai_summary="Valid summary.",
    )

    assert result is False


@pytest.mark.asyncio
async def test_send_trend_alert_skips_when_bot_token_empty(monkeypatch):
    """Test send_trend_alert skipping when TG_BOT_TOKEN is empty."""
    monkeypatch.setattr(settings, "TG_BOT_TOKEN", "")
    monkeypatch.setattr(settings, "TG_CHAT_ID", "123456789")

    client = TelegramNotifier(bot_token="", chat_id="123456789")

    mock_post = AsyncMock()

    class MockAsyncClient:
        def __init__(self, *args, **kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, exc_type, exc_val, exc_tb):
            pass

        post = mock_post

    monkeypatch.setattr(httpx, "AsyncClient", MockAsyncClient)

    result = await client.send_trend_alert(
        trend_name="Skipped Trend",
        ai_score=9,
        scam_probability=0,
        ai_summary="Will not be sent.",
    )

    assert result is False
    mock_post.assert_not_called()


@pytest.mark.asyncio
async def test_send_trend_alert_skips_when_chat_id_empty(monkeypatch):
    """Test send_trend_alert skipping when TG_CHAT_ID is empty."""
    monkeypatch.setattr(settings, "TG_BOT_TOKEN", "valid_token_123")
    monkeypatch.setattr(settings, "TG_CHAT_ID", "")

    client = TelegramNotifier(bot_token="valid_token_123", chat_id="")

    mock_post = AsyncMock()

    class MockAsyncClient:
        def __init__(self, *args, **kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, exc_type, exc_val, exc_tb):
            pass

        post = mock_post

    monkeypatch.setattr(httpx, "AsyncClient", MockAsyncClient)

    result = await client.send_trend_alert(
        trend_name="Skipped Trend",
        ai_score=9,
        scam_probability=0,
        ai_summary="Will not be sent.",
    )

    assert result is False
    mock_post.assert_not_called()


@pytest.mark.asyncio
async def test_send_trend_alert_skips_when_both_credentials_none(monkeypatch):
    """Test send_trend_alert skipping when both token and chat_id are None/empty."""
    monkeypatch.setattr(settings, "TG_BOT_TOKEN", "")
    monkeypatch.setattr(settings, "TG_CHAT_ID", "")

    client = TelegramNotifier(bot_token=None, chat_id=None)

    mock_post = AsyncMock()

    class MockAsyncClient:
        def __init__(self, *args, **kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, exc_type, exc_val, exc_tb):
            pass

        post = mock_post

    monkeypatch.setattr(httpx, "AsyncClient", MockAsyncClient)

    result = await client.send_trend_alert(
        trend_name="Skipped Trend",
        ai_score=9,
        scam_probability=0,
        ai_summary="Will not be sent.",
    )

    assert result is False
    mock_post.assert_not_called()
