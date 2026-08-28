"""Unit tests for TrendScanner data extractors (Stage 2)."""

import asyncio
from datetime import datetime, timezone
import pytest
import httpx

from app.services.extractors import (
    BaseExtractor,
    ExtractedItem,
    RSSExtractor,
    RedditExtractor,
    get_extractor,
    EXTRACTOR_REGISTRY,
)


# --- Sample Test Data ---

SAMPLE_RSS_2_0 = """<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:content="http://purl.org/rss/1.0/modules/content/">
  <channel>
    <title>Tech News Feed</title>
    <link>https://example.com/feed</link>
    <description>Daily tech news</description>
    <item>
      <title>AI Agent Frameworks are Booming</title>
      <link>https://example.com/posts/ai-agent-frameworks</link>
      <description>&lt;p&gt;Overview of emerging autonomous AI frameworks in 2026.&lt;/p&gt;</description>
      <content:encoded><![CDATA[<p>Full in-depth analysis of AI agents, autonomous workflows, and monetization strategies.</p>]]></content:encoded>
      <pubDate>Wed, 26 Aug 2026 14:30:00 GMT</pubDate>
      <dc:creator>Jane Doe</dc:creator>
      <guid>https://example.com/posts/ai-agent-frameworks</guid>
    </item>
    <item>
      <title>Micro-SaaS Ideas for Solopreneurs</title>
      <link>https://example.com/posts/micro-saas-ideas</link>
      <description>Simple niche business models that require zero employees.</description>
      <pubDate>Tue, 25 Aug 2026 08:00:00 +0000</pubDate>
      <author>john@example.com (John Smith)</author>
    </item>
  </channel>
</rss>
"""

SAMPLE_ATOM_FEED = """<?xml version="1.0" encoding="utf-8"?>
<feed xmlns="http://www.w3.org/2005/Atom">
  <title>Startup Trends</title>
  <link href="https://startup-trends.io/atom.xml" rel="self"/>
  <updated>2026-08-26T12:00:00Z</updated>
  <entry>
    <title>Bootstrapped AI Tools Reaching $50k MRR</title>
    <link href="https://startup-trends.io/p/bootstrapped-ai" rel="alternate"/>
    <id>urn:uuid:12345-67890</id>
    <published>2026-08-26T10:15:00Z</published>
    <updated>2026-08-26T10:15:00Z</updated>
    <author>
      <name>Alex Rivera</name>
    </author>
    <summary>Case studies of solo founders building niche AI applications.</summary>
    <content type="html"><![CDATA[<div>Here is the breakdown of 5 profitable micro-SaaS tools launched this quarter.</div>]]></content>
  </entry>
</feed>
"""

SAMPLE_REDDIT_JSON = {
    "kind": "Listing",
    "data": {
        "after": "t3_xyz",
        "children": [
            {
                "kind": "t3",
                "data": {
                    "id": "post_1",
                    "title": "Monthly Megathread: Rules & Intro",
                    "selftext": "Please read the rules before posting.",
                    "permalink": "/r/Entrepreneur/comments/post_1/rules/",
                    "author": "AutoModerator",
                    "created_utc": 1787650000.0,
                    "stickied": True,
                    "is_self": True,
                    "score": 10,
                    "num_comments": 5,
                    "subreddit": "Entrepreneur",
                },
            },
            {
                "kind": "t3",
                "data": {
                    "id": "post_2",
                    "title": "I built a B2B scraper that generates $8k MRR",
                    "selftext": "Started this 6 months ago to solve data extraction for sales teams. Here is what worked.",
                    "permalink": "/r/Entrepreneur/comments/post_2/b2b_scraper/",
                    "author": "solobuilder",
                    "created_utc": 1787660000.0,
                    "stickied": False,
                    "is_self": True,
                    "score": 245,
                    "num_comments": 89,
                    "subreddit": "Entrepreneur",
                },
            },
            {
                "kind": "t3",
                "data": {
                    "id": "post_3",
                    "title": "New report on creator economy monetization",
                    "selftext": "[deleted]",
                    "url_overridden_by_dest": "https://industry-insights.com/report-2026",
                    "permalink": "/r/Entrepreneur/comments/post_3/creator_economy/",
                    "author": "market_analyst",
                    "created_utc": 1787670000.0,
                    "stickied": False,
                    "is_self": False,
                    "score": 92,
                    "num_comments": 14,
                    "subreddit": "Entrepreneur",
                },
            },
        ],
    },
}


# --- ExtractedItem & BaseExtractor Tests ---

def test_extracted_item_model():
    """Verify ExtractedItem validation, fields, and defaults."""
    item = ExtractedItem(
        title="Test Headline",
        text="Test body content.",
        url="https://example.com/test",
        published_at=datetime(2026, 8, 26, 12, 0, tzinfo=timezone.utc),
        author="Analyst",
        source_type="rss",
        extra={"custom_key": 123},
    )

    assert item.title == "Test Headline"
    assert item.text == "Test body content."
    assert item.url == "https://example.com/test"
    assert item.published_at.year == 2026
    assert item.author == "Analyst"
    assert item.source_type == "rss"
    assert item.extra["custom_key"] == 123


def test_registry_and_factory():
    """Verify EXTRACTOR_REGISTRY and get_extractor helper."""
    assert "rss" in EXTRACTOR_REGISTRY
    assert "reddit" in EXTRACTOR_REGISTRY

    rss_extractor = get_extractor("rss", timeout=10.0)
    assert isinstance(rss_extractor, RSSExtractor)
    assert rss_extractor.timeout == 10.0

    reddit_extractor = get_extractor("REDDIT", timeout=20.0)
    assert isinstance(reddit_extractor, RedditExtractor)
    assert reddit_extractor.timeout == 20.0

    unknown = get_extractor("unsupported_source_type_xyz")
    assert unknown is None


# --- RSSExtractor Tests ---

def test_rss_extractor_parse_rss_2_0():
    """Test RSSExtractor XML parsing on standard RSS 2.0."""
    extractor = RSSExtractor()
    items = extractor._parse_xml_items(SAMPLE_RSS_2_0, "https://example.com/feed")

    assert len(items) == 2

    # First item
    assert items[0].title == "AI Agent Frameworks are Booming"
    assert "Full in-depth analysis of AI agents" in items[0].text
    assert items[0].url == "https://example.com/posts/ai-agent-frameworks"
    assert items[0].author == "Jane Doe"
    assert items[0].source_type == "rss"
    assert items[0].published_at is not None
    assert items[0].published_at.hour == 14

    # Second item
    assert items[1].title == "Micro-SaaS Ideas for Solopreneurs"
    assert "Simple niche business models" in items[1].text
    assert items[1].url == "https://example.com/posts/micro-saas-ideas"
    assert items[1].author == "John Smith"
    assert items[1].source_type == "rss"


def test_rss_extractor_parse_atom():
    """Test RSSExtractor XML parsing on Atom feed."""
    extractor = RSSExtractor()
    items = extractor._parse_xml_items(SAMPLE_ATOM_FEED, "https://startup-trends.io/atom.xml")

    assert len(items) == 1
    assert items[0].title == "Bootstrapped AI Tools Reaching $50k MRR"
    assert "Here is the breakdown of 5 profitable micro-SaaS" in items[0].text
    assert items[0].url == "https://startup-trends.io/p/bootstrapped-ai"
    assert items[0].author == "Alex Rivera"
    assert items[0].published_at is not None
    assert items[0].published_at.isoformat() == "2026-08-26T10:15:00+00:00"


def test_rss_extractor_date_parsing():
    """Test datetime parser against various feed date formats."""
    extractor = RSSExtractor()

    # RFC 822 / 2822
    dt1 = extractor._parse_datetime("Wed, 26 Aug 2026 14:30:00 GMT")
    assert dt1 is not None
    assert dt1.year == 2026 and dt1.month == 8 and dt1.day == 26 and dt1.hour == 14

    # ISO 8601
    dt2 = extractor._parse_datetime("2026-08-26T18:45:00Z")
    assert dt2 is not None
    assert dt2.year == 2026 and dt2.hour == 18

    # ISO with offset
    dt3 = extractor._parse_datetime("2026-08-26T12:00:00+03:00")
    assert dt3 is not None
    assert dt3.tzinfo is not None

    # Invalid / empty
    assert extractor._parse_datetime("") is None
    assert extractor._parse_datetime(None) is None
    assert extractor._parse_datetime("invalid-date-string-123") is None


@pytest.mark.asyncio
async def test_rss_extractor_network_mock(monkeypatch):
    """Test RSSExtractor async extract flow with mocked httpx response."""
    extractor = RSSExtractor()

    class MockResponse:
        status_code = 200
        text = SAMPLE_RSS_2_0

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

    items = await extractor.extract("https://example.com/rss")
    assert len(items) == 2
    assert items[0].title == "AI Agent Frameworks are Booming"


@pytest.mark.asyncio
async def test_rss_extractor_error_handling(monkeypatch):
    """Ensure RSSExtractor gracefully returns empty list on network errors."""
    extractor = RSSExtractor()

    class FailingAsyncClient:
        def __init__(self, *args, **kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, exc_type, exc_val, exc_tb):
            pass

        async def get(self, url, **kwargs):
            raise httpx.ConnectError("Connection refused")

    monkeypatch.setattr(httpx, "AsyncClient", FailingAsyncClient)

    # Must return empty list, not throw
    items = await extractor.extract("https://unreachable-feed.example.com/rss")
    assert items == []


# --- RedditExtractor Tests ---

def test_reddit_url_normalization():
    """Test RedditExtractor URL normalization logic."""
    extractor = RedditExtractor(default_limit=25)

    # Shorthand
    assert extractor._normalize_reddit_url("r/Entrepreneur") == "https://www.reddit.com/r/Entrepreneur/hot.json?limit=25"
    assert extractor._normalize_reddit_url("https://www.reddit.com/r/Entrepreneur/") == "https://www.reddit.com/r/Entrepreneur/hot.json?limit=25"
    assert extractor._normalize_reddit_url("https://reddit.com/r/startups/new") == "https://reddit.com/r/startups/new.json?limit=25"
    assert extractor._normalize_reddit_url("https://www.reddit.com/r/ai/top.json?t=month") == "https://www.reddit.com/r/ai/top.json?t=month&limit=25"


def test_reddit_parse_json():
    """Test RedditExtractor JSON parsing and filtering."""
    extractor = RedditExtractor(include_stickied=False)
    items = extractor._parse_reddit_json(SAMPLE_REDDIT_JSON, "https://www.reddit.com/r/Entrepreneur/hot.json")

    # Post 1 is stickied, so only post 2 and 3 should be extracted
    assert len(items) == 2

    # Post 2
    assert items[0].title == "I built a B2B scraper that generates $8k MRR"
    assert "Started this 6 months ago" in items[0].text
    assert items[0].url == "https://www.reddit.com/r/Entrepreneur/comments/post_2/b2b_scraper/"
    assert items[0].author == "solobuilder"
    assert items[0].source_type == "reddit"
    assert items[0].extra["score"] == 245
    assert items[0].extra["num_comments"] == 89

    # Post 3 (external link, deleted selftext)
    assert items[1].title == "New report on creator economy monetization"
    assert "Link: https://industry-insights.com/report-2026" in items[1].text
    assert items[1].author == "market_analyst"


@pytest.mark.asyncio
async def test_reddit_extractor_rate_limit(monkeypatch):
    """Test that HTTP 429 rate limit is handled gracefully by returning []."""
    extractor = RedditExtractor()

    class RateLimitedResponse:
        status_code = 429
        text = "Too Many Requests"

        def raise_for_status(self):
            raise httpx.HTTPStatusError("429", request=None, response=self)

    class MockAsyncClient:
        def __init__(self, *args, **kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, exc_type, exc_val, exc_tb):
            pass

        async def get(self, url, **kwargs):
            return RateLimitedResponse()

    monkeypatch.setattr(httpx, "AsyncClient", MockAsyncClient)

    items = await extractor.extract("r/Entrepreneur")
    assert items == []


@pytest.mark.asyncio
async def test_reddit_extractor_network_mock(monkeypatch):
    """Test RedditExtractor async extract flow with mocked JSON response."""
    extractor = RedditExtractor(include_stickied=True)

    class MockResponse:
        status_code = 200

        def raise_for_status(self):
            pass

        def json(self):
            return SAMPLE_REDDIT_JSON

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

    items = await extractor.extract("r/Entrepreneur")
    # All 3 posts included because include_stickied=True
    assert len(items) == 3


def test_extractor_sanitizer_integration():
    """Verify that items extracted from RSS/Reddit can be seamlessly sanitized."""
    from app.services.sanitizer import TextSanitizer

    sanitizer = TextSanitizer()

    # Valid RSS post
    rss_extractor = RSSExtractor()
    rss_items = rss_extractor._parse_xml_items(SAMPLE_RSS_2_0, "https://example.com/feed")
    res1 = sanitizer.sanitize(rss_items[0].text, min_length=50)
    assert res1.is_valid is True
    assert "Full in-depth analysis of AI agents" in res1.cleaned_text

    # Reddit post
    reddit_extractor = RedditExtractor(include_stickied=False)
    reddit_items = reddit_extractor._parse_reddit_json(SAMPLE_REDDIT_JSON, "https://reddit.com")
    res2 = sanitizer.sanitize(reddit_items[0].text, min_length=50)
    assert res2.is_valid is True
    assert "Started this 6 months ago" in res2.cleaned_text


@pytest.mark.asyncio
async def test_advanced_extractor_html_dom_parsing():
    """Test AdvancedExtractor HTML DOM card and paragraph extraction."""
    from app.services.extractors.advanced_extractor import AdvancedExtractor

    sample_html = """
    <html>
        <head><title>Top Micro SaaS Products</title></head>
        <body>
            <article class="product-item">
                <h2><a href="/products/ai-billing">AI Automated Billing Tool</a></h2>
                <p>An autonomous billing assistant for agencies that recovers overdue invoices via SMS.</p>
            </article>
            <article class="product-item">
                <h2><a href="https://example.com/item2">Code Review AI Bot</a></h2>
                <p>AI agent that reviews GitHub pull requests and spots security vulnerabilities in seconds.</p>
            </article>
            <article class="product-item">
                <h2><a href="/products/seo-radar">SEO Keyword Tracker</a></h2>
                <p>Continuous keyword tracking tool for indie developers and micro-SaaS founders.</p>
            </article>
        </body>
    </html>
    """
    extractor = AdvancedExtractor()
    items = extractor._parse_html_dom(sample_html, "https://www.producthunt.com")
    assert len(items) == 3
    assert items[0].title == "AI Automated Billing Tool"
    assert items[0].url == "https://www.producthunt.com/products/ai-billing"
    assert "autonomous billing assistant" in items[0].text
    assert items[1].url == "https://example.com/item2"


def test_advanced_extractor_registry():
    """Verify that playwright_spa and spa keys resolve to AdvancedExtractor."""
    from app.services.extractors.advanced_extractor import AdvancedExtractor

    ext = get_extractor("playwright_spa")
    assert isinstance(ext, AdvancedExtractor)

    ext2 = get_extractor("spa")
    assert isinstance(ext2, AdvancedExtractor)


def test_auto_discovered_extractor_and_get_extractor_for_url():
    """Verify get_extractor('auto_discovered') and get_extractor_for_url helper."""
    from app.services.extractors import (
        AdvancedWebExtractor,
        RSSExtractor,
        RedditExtractor,
        TelegramExtractor,
        get_extractor,
        get_extractor_for_url,
    )

    # 1. get_extractor with auto_discovered
    ext_auto = get_extractor("auto_discovered")
    assert isinstance(ext_auto, RSSExtractor)

    # 2. get_extractor_for_url
    # Telegram
    assert isinstance(get_extractor_for_url("https://t.me/tech_trends"), TelegramExtractor)
    assert isinstance(get_extractor_for_url("https://telegram.me/tech_channel"), TelegramExtractor)

    # Reddit
    assert isinstance(get_extractor_for_url("https://reddit.com/r/startups"), RedditExtractor)
    assert isinstance(get_extractor_for_url("r/micro_saas"), RedditExtractor)

    # RSS / feeds
    assert isinstance(get_extractor_for_url("https://startupideas.substack.com/feed"), RSSExtractor)
    assert isinstance(get_extractor_for_url("https://medium.com/feed/@author"), RSSExtractor)
    assert isinstance(get_extractor_for_url("https://news.ycombinator.com/rss"), RSSExtractor)
    assert isinstance(get_extractor_for_url("https://example.com/atom.xml"), RSSExtractor)

    # Web (AdvancedWebExtractor)
    assert isinstance(get_extractor_for_url("https://www.producthunt.com/topics/ai"), AdvancedWebExtractor)
    assert isinstance(get_extractor_for_url("https://news.google.com/topstories"), AdvancedWebExtractor)

    # Empty
    assert get_extractor_for_url("") is None
    assert get_extractor_for_url(None) is None



