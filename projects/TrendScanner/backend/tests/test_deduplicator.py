"""Unit tests for TrendScanner Level 2 Smart Deduplication Engine."""

import pytest
from app.core.settings import settings
from app.db.dao import SourcesDAO, TrendsDAO
from app.db.database import init_db
from app.services.deduplicator import (
    DeduplicationEngine,
    DeduplicationResult,
    deduplicator,
)


@pytest.fixture(autouse=True)
def setup_test_db(tmp_path, monkeypatch):
    """Set up an isolated temporary SQLite database for tests."""
    db_file = tmp_path / "test_dedup.db"
    monkeypatch.setattr(settings, "DATABASE_PATH", str(db_file))
    init_db(seed_default_sources=False)
    yield db_file


# -------------------------------------------------------------------------
# 1. URL Normalization Tests
# -------------------------------------------------------------------------

def test_normalize_url_strips_tracking_params_and_slashes():
    """Verify normalize_url strips utm parameters, ref params, and trailing slashes."""
    url1 = "https://www.Reddit.com/r/SaaS/comments/181xyz/great_tool/?utm_source=share&utm_medium=ios_app&ref=share/"
    url2 = "https://reddit.com/r/saas/comments/181xyz/great_tool"

    norm1 = deduplicator.normalize_url(url1)
    norm2 = deduplicator.normalize_url(url2)

    assert norm1 == "https://reddit.com/r/saas/comments/181xyz/great_tool"
    assert norm1 == norm2


def test_normalize_url_preserves_functional_params():
    """Verify non-tracking query parameters like item ID or video ID are preserved."""
    url = "https://news.ycombinator.com/item?id=38472910&utm_source=hackernewsletter"
    norm = deduplicator.normalize_url(url)
    assert norm == "https://news.ycombinator.com/item?id=38472910"


def test_normalize_url_edge_cases():
    """Verify handling of empty or None values."""
    assert deduplicator.normalize_url("") == ""
    assert deduplicator.normalize_url(None) == ""  # type: ignore
    assert deduplicator.normalize_url("   ") == ""


# -------------------------------------------------------------------------
# 2. Similarity Calculation Tests
# -------------------------------------------------------------------------

def test_calculate_similarity_exact_and_fuzzy():
    """Verify SequenceMatcher ratio calculations."""
    text1 = "AI Bookkeeping for Dental Clinics in 2026"
    text2 = "ai bookkeeping for dental clinics 2026"
    assert deduplicator.calculate_similarity(text1, text2) > 0.85

    text_unrelated1 = "AI Bookkeeping for Dental Clinics"
    text_unrelated2 = "Solana crypto trading bot with high leverage"
    assert deduplicator.calculate_similarity(text_unrelated1, text_unrelated2) < 0.4

    # Empty inputs
    assert deduplicator.calculate_similarity("", "some text") == 0.0
    assert deduplicator.calculate_similarity("some text", "") == 0.0


# -------------------------------------------------------------------------
# 3. Exact URL Match Detection
# -------------------------------------------------------------------------

def test_find_duplicate_exact_url_match():
    """Verify exact URL match detection even when URL formatting differs."""
    candidates = [
        {
            "id": 101,
            "trend_name": "Modern Dental Booking",
            "original_text": "A SaaS for managing dental appointments and patient SMS alerts.",
            "source_url": "https://www.producthunt.com/posts/dental-booking-ai?utm_source=daily",
        },
        {
            "id": 102,
            "trend_name": "Cold Outreach AI",
            "original_text": "Autonomous email personalization agent.",
            "source_url": "https://reddit.com/r/startups/comments/999/cold_ai",
        },
    ]

    new_item_url = "https://producthunt.com/posts/dental-booking-ai/?ref=producthunt"
    res = deduplicator.find_duplicate(
        title="Dental Booking AI App",
        text="App for dental clinics.",
        url=new_item_url,
        candidates=candidates,
    )

    assert res.is_duplicate is True
    assert res.matched_trend_id == 101
    assert res.match_type == "url_match"
    assert res.similarity_score == 1.0


def test_find_duplicate_generic_feed_url_does_not_false_positive():
    """Generic feed URLs (like reddit root or hn rss) should NOT cause false positive URL match."""
    candidates = [
        {
            "id": 201,
            "trend_name": "Post About Video Generation",
            "original_text": "Discussion on diffusion models for short form video.",
            "source_url": "https://news.ycombinator.com/rss",
        }
    ]

    # Incoming new item has the same source feed URL, but completely different content
    res = deduplicator.find_duplicate(
        title="Micro-SaaS for Barber Shop Scheduling",
        text="Unique software designed specifically for solo barbers.",
        url="https://news.ycombinator.com/rss",
        candidates=candidates,
    )

    assert res.is_duplicate is False
    assert res.matched_trend_id is None


# -------------------------------------------------------------------------
# 4. Fuzzy Title & Text Similarity Detection (> 85%)
# -------------------------------------------------------------------------

def test_find_duplicate_fuzzy_title_similarity():
    """Verify fuzzy title similarity detects duplicate when score >= 85%."""
    candidates = [
        {
            "id": 301,
            "trend_name": "Autonomous AI Customer Support Agents for Shopify",
            "original_text": "Detailed breakdown of Shopify stores saving 40 hours per week using LLM agents.",
            "source_url": "https://reddit.com/r/SaaS/comments/111/post1",
        }
    ]

    # Similar title with slight variation (> 85% ratio)
    new_title = "Autonomous AI Customer Support Agent for Shopify"
    new_text = "Completely different text snippet discussing similar automation."
    res = deduplicator.find_duplicate(
        title=new_title,
        text=new_text,
        url="https://medium.com/post/shopify-ai-support",
        candidates=candidates,
        threshold=0.85,
    )

    assert res.is_duplicate is True
    assert res.matched_trend_id == 301
    assert res.match_type in ("fuzzy_match", "exact_hash")
    assert res.similarity_score >= 0.85


def test_find_duplicate_fuzzy_text_snippet_similarity():
    """Verify fuzzy text snippet similarity detects duplicate when titles differ."""
    shared_text = (
        "We built an open-source alternative to Datadog tailored for lightweight SQLite edge databases. "
        "It collects CPU, memory, query latency metrics and sends alerts via Telegram webhooks."
    )

    candidates = [
        {
            "id": 302,
            "trend_name": "Show HN: SQLite Monitor",
            "original_text": shared_text,
            "source_url": "https://news.ycombinator.com/item?id=123",
        }
    ]

    res = deduplicator.find_duplicate(
        title="Lightweight SQLite Edge Monitoring Tool",
        text=shared_text,
        url="https://indiehackers.com/post/sqlite-edge-monitoring",
        candidates=candidates,
        threshold=0.85,
    )

    assert res.is_duplicate is True
    assert res.matched_trend_id == 302
    assert res.similarity_score >= 0.85


# -------------------------------------------------------------------------
# 5. Unrelated Content Rejection (ratio < 85%)
# -------------------------------------------------------------------------

def test_find_duplicate_unrelated_content_rejected():
    """Verify completely different content returns is_duplicate = False."""
    candidates = [
        {
            "id": 401,
            "trend_name": "Notion Template Marketplace for Lawyers",
            "original_text": "Selling high-ticket Notion operating systems for boutique law firms.",
            "source_url": "https://reddit.com/r/SideProject/comments/aaa/notion_law",
        }
    ]

    res = deduplicator.find_duplicate(
        title="FastAPI Boilerplate with SvelteKit and Stripe",
        text="A production-ready SaaS starter kit built with Python, FastAPI, and SvelteKit frontend.",
        url="https://github.com/example/fastapi-svelte-starter",
        candidates=candidates,
        threshold=0.85,
    )

    assert res.is_duplicate is False
    assert res.matched_trend_id is None
    assert res.match_type is None
    assert res.similarity_score < 0.85


# -------------------------------------------------------------------------
# 6. Context Text Merging Format
# -------------------------------------------------------------------------

def test_format_merged_text():
    """Verify format_merged_text produces clean multi-source appended context."""
    existing_text = "Initial discovery of dental AI SaaS on Product Hunt."
    new_text = "Trending post on Reddit r/SaaS with 150 upvotes and active discussion on pricing."
    source_name = "Reddit /r/SaaS"

    merged = deduplicator.format_merged_text(
        existing_text=existing_text,
        new_text=new_text,
        new_source_name=source_name,
    )

    expected = (
        "Initial discovery of dental AI SaaS on Product Hunt.\n\n"
        "[Дополнительное упоминание (Reddit /r/SaaS)]:\n"
        "Trending post on Reddit r/SaaS with 150 upvotes and active discussion on pricing."
    )
    assert merged == expected


def test_format_merged_text_edge_cases():
    """Verify merging when existing or new text is empty."""
    assert (
        deduplicator.format_merged_text("", "New content", "Twitter")
        == "[Дополнительное упоминание (Twitter)]:\nNew content"
    )
    assert (
        deduplicator.format_merged_text("Existing content", "", "Twitter")
        == "Existing content"
    )


# -------------------------------------------------------------------------
# 7. Verification that Merged Trend Has Updated Mention Count
# -------------------------------------------------------------------------

def test_merged_trend_mention_count_increment():
    """Verify database integration: matching duplicate increments mention_count and updates text."""
    # 1. Create a source and initial trend
    source_id = SourcesDAO.create(
        name="Product Hunt Trending",
        url="https://www.producthunt.com",
        source_type="playwright_spa",
    )
    trend_id = TrendsDAO.create(
        source_id=source_id,
        original_text="Initial launch of AI Invoice Parser for Construction Contractors.",
        trend_name="AI Invoice Parser for Construction",
        source_url="https://www.producthunt.com/posts/contractor-invoice-ai",
        is_trend=True,
        ai_score=8,
    )

    # 2. Verify initial state
    initial_trend = TrendsDAO.get_by_id(trend_id)
    assert initial_trend is not None
    assert initial_trend["mention_count"] == 1

    # 3. Ingest duplicate item from a different source
    new_item_title = "AI Invoice Parser for Construction Contractors"
    new_item_text = "Discovered on Reddit: contractor accounting automation tool is gaining traction."
    new_item_url = "https://producthunt.com/posts/contractor-invoice-ai?ref=newsletter"
    new_source_name = "Reddit /r/startups"

    candidates = TrendsDAO.get_recent_candidates()
    dup_result = deduplicator.find_duplicate(
        title=new_item_title,
        text=new_item_text,
        url=new_item_url,
        candidates=candidates,
    )

    assert dup_result.is_duplicate is True
    assert dup_result.matched_trend_id == trend_id

    # 4. Merge text and increment mention count
    merged_text = deduplicator.format_merged_text(
        existing_text=initial_trend["original_text"],
        new_text=new_item_text,
        new_source_name=new_source_name,
    )
    update_success = TrendsDAO.increment_mention_count(trend_id, merged_text=merged_text)
    assert update_success is True

    # 5. Verify updated trend record
    updated_trend = TrendsDAO.get_by_id(trend_id)
    assert updated_trend is not None
    assert updated_trend["mention_count"] == 2
    assert "[Дополнительное упоминание (Reddit /r/startups)]:" in updated_trend["original_text"]
    assert "Discovered on Reddit: contractor accounting automation tool" in updated_trend["original_text"]
