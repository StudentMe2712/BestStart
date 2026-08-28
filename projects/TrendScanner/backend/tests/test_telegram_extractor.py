"""Unit tests for TelegramExtractor and Telegram authentication components (Level 3)."""

import asyncio
from datetime import datetime, timezone
from unittest.mock import AsyncMock, MagicMock, patch
import pytest
import httpx

from app.core.settings import settings
from app.services.extractors import (
    BaseExtractor,
    ExtractedItem,
    TelegramExtractor,
    get_extractor,
    EXTRACTOR_REGISTRY,
)


# --- Sample Telegram Web Preview HTML Data ---

SAMPLE_TG_WEB_PREVIEW_HTML = """
<!DOCTYPE html>
<html>
<head><title>Telegram: Contact @tech_trends</title></head>
<body>
<div class="tgme_page">
  <div class="tgme_channel_info">
    <div class="tgme_channel_info_header">
      <div class="tgme_channel_info_title"><span dir="auto">Tech & AI Trends Daily</span></div>
    </div>
  </div>

  <div class="tgme_widget_message_wrap js-widget_message_wrap">
    <!-- Post 1: Valid long message with header, formatting, and link -->
    <div class="tgme_widget_message js-widget_message" data-post="tech_trends/101">
      <div class="tgme_widget_message_user">
        <div class="tgme_widget_message_owner_name">Tech & AI Trends Daily</div>
      </div>
      <div class="tgme_widget_message_text js-message_text" dir="auto">
        <b>Autonomous AI Agents in Enterprise Software 2026</b><br><br>
        Autonomous agentic workflows are rapidly moving from experimental prototypes into high-scale production systems.
        Enterprise teams are deploying specialized multi-agent swarms for code review, data extraction, and customer support operations.
        Market demand for developer tooling around AI orchestration has increased by over 300% year over year.
      </div>
      <div class="tgme_widget_message_footer js-message_footer">
        <div class="tgme_widget_message_info">
          <span class="tgme_widget_message_meta">
            <a class="tgme_widget_message_date" href="https://t.me/tech_trends/101">
              <time datetime="2026-08-26T14:30:00+00:00" class="time">14:30</time>
            </a>
          </span>
        </div>
      </div>
    </div>
  </div>

  <div class="tgme_widget_message_wrap js-widget_message_wrap">
    <!-- Post 2: Short post (< 100 characters) that should be filtered out -->
    <div class="tgme_widget_message js-widget_message" data-post="tech_trends/102">
      <div class="tgme_widget_message_user">
        <div class="tgme_widget_message_owner_name">Tech & AI Trends Daily</div>
      </div>
      <div class="tgme_widget_message_text js-message_text" dir="auto">
        Just launched our new community chat! Join here: https://t.me/joinchat/xyz
      </div>
      <div class="tgme_widget_message_footer js-message_footer">
        <div class="tgme_widget_message_info">
          <span class="tgme_widget_message_meta">
            <a class="tgme_widget_message_date" href="https://t.me/tech_trends/102">
              <time datetime="2026-08-26T15:00:00+00:00" class="time">15:00</time>
            </a>
          </span>
        </div>
      </div>
    </div>
  </div>

  <div class="tgme_widget_message_wrap js-widget_message_wrap">
    <!-- Post 3: Valid second long message with forward / custom author -->
    <div class="tgme_widget_message js-widget_message" data-post="tech_trends/103">
      <div class="tgme_widget_message_user">
        <div class="tgme_widget_message_owner_name">Tech & AI Trends Daily</div>
      </div>
      <div class="tgme_widget_message_text js-message_text" dir="auto">
        🚀 #MicroSaaS Spotlight: Solo Founder Scaling to $15k MRR<br><br>
        A solo founder recently shared detailed insights on building a niche automated PDF parsing API for logistics brokers.
        By focusing purely on solving an acute B2B pain point with zero marketing budget, the project reached profitability in 45 days.
      </div>
      <div class="tgme_widget_message_footer js-message_footer">
        <div class="tgme_widget_message_info">
          <span class="tgme_widget_message_meta">
            <a class="tgme_widget_message_date" href="https://t.me/tech_trends/103">
              <time datetime="2026-08-26T16:15:00+00:00" class="time">16:15</time>
            </a>
          </span>
        </div>
      </div>
    </div>
  </div>
</div>
</body>
</html>
"""


# --- Unit Tests: Channel Normalization ---

def test_normalize_channel_name():
    """Verify normalization of various Telegram channel URL formats and usernames."""
    extractor = TelegramExtractor()

    # Username formats
    assert extractor._normalize_channel_name("tech_trends") == "tech_trends"
    assert extractor._normalize_channel_name("@tech_trends") == "tech_trends"
    assert extractor._normalize_channel_name("  @tech_trends  ") == "tech_trends"

    # HTTPS and HTTP URLs
    assert extractor._normalize_channel_name("https://t.me/tech_trends") == "tech_trends"
    assert extractor._normalize_channel_name("http://t.me/tech_trends") == "tech_trends"
    assert extractor._normalize_channel_name("https://t.me/tech_trends/") == "tech_trends"

    # Web preview /s/ URLs
    assert extractor._normalize_channel_name("https://t.me/s/tech_trends") == "tech_trends"
    assert extractor._normalize_channel_name("http://t.me/s/tech_trends/") == "tech_trends"
    assert extractor._normalize_channel_name("t.me/s/tech_trends") == "tech_trends"

    # URLs with query parameters & fragments
    assert extractor._normalize_channel_name("https://t.me/s/tech_trends?before=150") == "tech_trends"
    assert extractor._normalize_channel_name("https://t.me/tech_trends#post-123") == "tech_trends"

    # Alternative domain telegram.me
    assert extractor._normalize_channel_name("https://telegram.me/tech_trends") == "tech_trends"
    assert extractor._normalize_channel_name("https://telegram.me/s/tech_trends") == "tech_trends"

    # Empty / whitespace inputs
    assert extractor._normalize_channel_name("") == ""
    assert extractor._normalize_channel_name("   ") == ""
    assert extractor._normalize_channel_name(None) == ""


def test_generate_title():
    """Test title generation and sanitization from message bodies."""
    extractor = TelegramExtractor()

    text1 = "Autonomous AI Agents in Enterprise Software 2026\n\nFull details here..."
    assert extractor._generate_title(text1) == "Autonomous AI Agents in Enterprise Software 2026"

    # Hashtags and emojis stripped from beginning
    text2 = "🚀 #MicroSaaS Spotlight: Solo Founder Scaling to $15k MRR\n\nDetails..."
    assert "MicroSaaS Spotlight: Solo Founder Scaling to $15k MRR" in extractor._generate_title(text2)

    # Long single line truncated
    long_line = "A" * 150
    title = extractor._generate_title(long_line, max_length=50)
    assert len(title) <= 50
    assert title.endswith("...")

    # Empty text fallback
    assert extractor._generate_title("") == "Telegram Signal"
    assert extractor._generate_title("   ") == "Telegram Signal"


# --- Unit Tests: HTML Web Preview Parsing ---

def test_parse_web_preview_html():
    """Test HTML parsing of public Telegram channel web preview."""
    extractor = TelegramExtractor()
    items = extractor._parse_web_preview_html(SAMPLE_TG_WEB_PREVIEW_HTML, "tech_trends")

    # Post 2 was < 100 characters, so only Post 1 and Post 3 should be extracted
    assert len(items) == 2

    # Verify Post 1
    post1 = items[0]
    assert post1.title == "Autonomous AI Agents in Enterprise Software 2026"
    assert "Autonomous agentic workflows are rapidly moving" in post1.text
    assert post1.url == "https://t.me/tech_trends/101"
    assert post1.author == "Tech & AI Trends Daily"
    assert post1.source_type == "telegram"
    assert post1.published_at is not None
    assert post1.published_at.year == 2026
    assert post1.published_at.hour == 14
    assert post1.published_at.minute == 30
    assert post1.extra["channel"] == "tech_trends"
    assert post1.extra["extraction_mode"] == "web_preview"

    # Verify Post 2 (Post 3 in raw HTML)
    post2 = items[1]
    assert "MicroSaaS Spotlight" in post2.title
    assert "solo founder recently shared detailed insights" in post2.text
    assert post2.url == "https://t.me/tech_trends/103"
    assert post2.published_at.hour == 16


@pytest.mark.asyncio
async def test_extract_web_preview_network_mock(monkeypatch):
    """Test _extract_web_preview async fetch with mocked HTTPX response."""
    extractor = TelegramExtractor()

    class MockResponse:
        status_code = 200
        text = SAMPLE_TG_WEB_PREVIEW_HTML

        def raise_for_status(self):
            pass

    class MockAsyncClient:
        def __init__(self, *args, **kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, exc_type, exc_val, exc_tb):
            pass

        async def get(self, url, **kwargs):
            return MockResponse()

    monkeypatch.setattr(httpx, "AsyncClient", MockAsyncClient)

    items = await extractor._extract_web_preview("tech_trends")
    assert len(items) == 2
    assert "Autonomous AI Agents" in items[0].title


@pytest.mark.asyncio
async def test_extract_web_preview_error_handling(monkeypatch):
    """Verify _extract_web_preview returns empty list on network failure."""
    extractor = TelegramExtractor()

    class FailingAsyncClient:
        def __init__(self, *args, **kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, exc_type, exc_val, exc_tb):
            pass

        async def get(self, url, **kwargs):
            raise httpx.ConnectError("Network unreachable")

    monkeypatch.setattr(httpx, "AsyncClient", FailingAsyncClient)

    items = await extractor._extract_web_preview("nonexistent_channel")
    assert items == []


# --- Unit Tests: Telethon Extraction & Formatting ---

@pytest.mark.asyncio
async def test_extract_telethon_mock_success(monkeypatch):
    """Test Telethon message extraction and conversion to ExtractedItem."""
    extractor = TelegramExtractor()

    # Mock Message objects
    class MockSender:
        username = "lead_researcher"
        first_name = "Alex"

    class MockMessage1:
        id = 201
        text = (
            "Open Source LLM Tooling Benchmark 2026\n\n"
            "Comprehensive benchmark evaluating the throughput, token latency, and memory footprint "
            "of emerging quantized open weights models across modern GPU architectures."
        )
        date = datetime(2026, 8, 26, 17, 0, tzinfo=timezone.utc)
        post_author = "AI Research Group"
        sender = MockSender()
        views = 1540
        forwards = 42

    class MockMessage2Short:
        id = 202
        text = "Short text under 100 characters."
        date = datetime(2026, 8, 26, 17, 10, tzinfo=timezone.utc)
        post_author = None
        sender = MockSender()
        views = 100
        forwards = 1

    class MockClient:
        def __init__(self, *args, **kwargs):
            self._connected = True

        async def connect(self):
            self._connected = True

        async def is_user_authorized(self):
            return True

        async def iter_messages(self, channel, limit=20):
            yield MockMessage1()
            yield MockMessage2Short()

        def is_connected(self):
            return self._connected

        async def disconnect(self):
            self._connected = False

    # Force settings with dummy credentials
    monkeypatch.setattr(settings, "TG_API_ID", 123456)
    monkeypatch.setattr(settings, "TG_API_HASH", "mock_hash_secret")

    with patch("telethon.TelegramClient", return_value=MockClient()):
        items = await extractor._extract_telethon("ai_startups_radar")

        # Only MockMessage1 should pass the length filter
        assert len(items) == 1
        item = items[0]
        assert item.title == "Open Source LLM Tooling Benchmark 2026"
        assert "Comprehensive benchmark evaluating" in item.text
        assert item.url == "https://t.me/ai_startups_radar/201"
        assert item.author == "AI Research Group"
        assert item.source_type == "telegram"
        assert item.published_at.hour == 17
        assert item.extra["message_id"] == 201
        assert item.extra["views"] == 1540
        assert item.extra["extraction_mode"] == "telethon"


@pytest.mark.asyncio
async def test_extract_telethon_unauthorized_fallback(monkeypatch):
    """Verify that unauthorized Telethon session safely falls back to web preview."""
    extractor = TelegramExtractor()

    class UnauthorizedClient:
        def __init__(self, *args, **kwargs):
            self._connected = True

        async def connect(self):
            pass

        async def is_user_authorized(self):
            return False

        def is_connected(self):
            return self._connected

        async def disconnect(self):
            self._connected = False

    monkeypatch.setattr(settings, "TG_API_ID", 123456)
    monkeypatch.setattr(settings, "TG_API_HASH", "mock_hash")

    # Mock web preview fallback to return 1 item
    async def mock_web_preview(channel_name):
        return [
            ExtractedItem(
                title="Web Preview Fallback Post",
                text="This is a fallback extracted message from Telegram web preview that meets min length requirement.",
                url=f"https://t.me/{channel_name}/50",
                source_type="telegram",
            )
        ]

    monkeypatch.setattr(extractor, "_extract_web_preview", mock_web_preview)

    with patch("telethon.TelegramClient", return_value=UnauthorizedClient()):
        items = await extractor.extract("https://t.me/tech_trends")
        assert len(items) == 1
        assert items[0].title == "Web Preview Fallback Post"


@pytest.mark.asyncio
async def test_extract_no_credentials_auto_fallback(monkeypatch):
    """Verify that when TG_API_ID is None, extract() directly falls back to web preview."""
    extractor = TelegramExtractor()

    monkeypatch.setattr(settings, "TG_API_ID", None)
    monkeypatch.setattr(settings, "TG_API_HASH", "")

    async def mock_web_preview(channel_name):
        return [
            ExtractedItem(
                title="Direct Web Preview Post",
                text="Message body scraped from public preview without needing Telethon authentication.",
                url=f"https://t.me/{channel_name}/99",
                source_type="telegram",
            )
        ]

    monkeypatch.setattr(extractor, "_extract_web_preview", mock_web_preview)

    items = await extractor.extract("@tech_trends")
    assert len(items) == 1
    assert items[0].title == "Direct Web Preview Post"


# --- Unit Tests: Registry & Factory Integration ---

def test_telegram_extractor_registry():
    """Verify that telegram, telegram_channel, and telegram_html resolve to TelegramExtractor."""
    assert "telegram" in EXTRACTOR_REGISTRY
    assert "telegram_channel" in EXTRACTOR_REGISTRY
    assert "telegram_html" in EXTRACTOR_REGISTRY

    ext1 = get_extractor("telegram", timeout=12.0)
    assert isinstance(ext1, TelegramExtractor)
    assert ext1.timeout == 12.0

    ext2 = get_extractor("TELEGRAM_CHANNEL")
    assert isinstance(ext2, TelegramExtractor)

    ext3 = get_extractor("telegram_html")
    assert isinstance(ext3, TelegramExtractor)
