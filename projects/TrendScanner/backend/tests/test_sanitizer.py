"""Unit tests for TrendScanner text sanitizer (Stage 2)."""

import pytest
from app.services.sanitizer import TextSanitizer, SanitizedResult, sanitizer


def test_clean_text_html_stripping():
    """Verify HTML tags and entities are stripped properly."""
    raw_html = "<p>Hello <b>World</b> &amp; all builders!</p><script>alert(1)</script>"
    cleaned = sanitizer.clean_text(raw_html)
    assert cleaned == "Hello World & all builders!"


def test_clean_text_markdown_links():
    """Verify markdown links and images are converted to plain text."""
    raw_md = "Check out [My Tool](https://example.com/tool) and ![Logo](https://example.com/logo.png)"
    cleaned = sanitizer.clean_text(raw_md)
    assert cleaned == "Check out My Tool and Logo"


def test_spam_detection():
    """Verify spam patterns are detected."""
    assert sanitizer.is_spam("Join our crypto pump channel today!") is True
    assert sanitizer.is_spam("Guaranteed profit with 100x gains on telegram") is True
    assert sanitizer.is_spam("Free airdrop claim your tokens now") is True
    assert sanitizer.is_spam("Building a B2B SaaS for customer feedback collection") is False


def test_sanitize_rejection_rules():
    """Verify min_length and spam rejection logic."""
    # Too short
    short_res = sanitizer.sanitize("Too short", min_length=100)
    assert short_res.is_valid is False
    assert short_res.reject_reason == "too_short"

    # Spam
    spam_long_text = "Join our crypto pump and dump telegram channel for guaranteed profits every single day! " * 2
    spam_res = sanitizer.sanitize(spam_long_text, min_length=50)
    assert spam_res.is_valid is False
    assert spam_res.reject_reason == "spam_detected"

    # Valid long text
    valid_text = (
        "We discovered a growing trend in automated bookkeeping for dental clinics. "
        "Most clinics use outdated legacy software and pay excessive manual fees. "
        "A simple modern integration could easily charge $199/month."
    )
    valid_res = sanitizer.sanitize(valid_text, min_length=100)
    assert valid_res.is_valid is True
    assert valid_res.reject_reason is None


def test_extract_candidate_sources_targets():
    """Verify extract_candidate_sources detects and normalizes Telegram, Substack, Medium, HN, and websites."""
    from app.services.sanitizer import extract_candidate_sources

    content = """
    Check out the channel on https://t.me/tech_trends?utm_source=radar and @tech_trends.
    Also read articles on https://startupideas.substack.com/p/ai-micro-saas and https://medium.com/@founder_john/scaling.
    Publication feed: https://betterprogramming.medium.com/article-123 and tag https://medium.com/tag/technology.
    Discussion link: https://news.ycombinator.com/item?id=987654.
    Duplicate mention of https://t.me/tech_trends/100 and <a href="https://startupideas.substack.com">Substack Home</a>.
    """
    candidates = extract_candidate_sources(content)
    by_url = {c["url"]: c for c in candidates}

    # Telegram
    assert "https://t.me/tech_trends" in by_url
    assert by_url["https://t.me/tech_trends"]["source_type"] == "auto_discovered"
    assert "Telegram: @tech_trends (Найдено ИИ)" in by_url["https://t.me/tech_trends"]["name"]

    # Substack
    assert "https://startupideas.substack.com/feed" in by_url
    assert by_url["https://startupideas.substack.com/feed"]["name"] == "Substack: startupideas (Найдено ИИ)"

    # Medium author
    assert "https://medium.com/feed/@founder_john" in by_url
    assert by_url["https://medium.com/feed/@founder_john"]["name"] == "Medium: @founder_john (Найдено ИИ)"

    # Medium publication
    assert "https://betterprogramming.medium.com/feed" in by_url
    assert by_url["https://betterprogramming.medium.com/feed"]["name"] == "Medium: betterprogramming (Найдено ИИ)"

    # Medium tag
    assert "https://medium.com/feed/tag/technology" in by_url
    assert by_url["https://medium.com/feed/tag/technology"]["name"] == "Medium: tag/technology (Найдено ИИ)"

    # Hacker News
    assert "https://news.ycombinator.com/rss" in by_url
    assert by_url["https://news.ycombinator.com/rss"]["name"] == "Hacker News (Найдено ИИ)"

    # Check deduplication: startupideas.substack.com/feed and t.me/tech_trends appear only once
    urls = [c["url"] for c in candidates]
    assert len(urls) == len(set(urls))

