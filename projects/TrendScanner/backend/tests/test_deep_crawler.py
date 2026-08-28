"""Tests for DynamicCrawlerExtractor and Deep Web Crawler Pipeline Integration."""

import asyncio
from datetime import datetime, timezone
from unittest.mock import AsyncMock, MagicMock, patch
import pytest

from app.services.extractors.base import ExtractedItem
from app.services.extractors.dynamic_crawler_extractor import (
    DEFAULT_CRAWLER_QUERIES,
    DynamicCrawlerExtractor,
)


@pytest.mark.asyncio
async def test_dynamic_crawler_extractor_init():
    """Test extractor initialization with default and custom search queries."""
    extractor = DynamicCrawlerExtractor()
    assert extractor.queries == DEFAULT_CRAWLER_QUERIES
    assert extractor.max_results_per_query == 10

    custom_queries = ["ai startups 2026", "micro-saas trends"]
    custom_extractor = DynamicCrawlerExtractor(
        queries=custom_queries,
        max_results_per_query=5,
        timeout=15.0,
    )
    assert custom_extractor.queries == custom_queries
    assert custom_extractor.max_results_per_query == 5


@pytest.mark.asyncio
async def test_search_urls_duckduckgo_mocked():
    """Test that search_urls returns list of clean destination URLs from search engine."""
    extractor = DynamicCrawlerExtractor(max_results_per_query=3)

    mock_search_results = [
        {"href": "https://techcrunch.com/2026/01/01/ai-agents-startup", "title": "AI Agents"},
        {"href": "https://news.ycombinator.com/item?id=12345", "title": "Show HN"},
        {"href": "https://www.producthunt.com/posts/cool-tool", "title": "Cool Tool"},
    ]

    with patch.object(extractor, "_search_engine_query", new_callable=AsyncMock) as mock_query:
        mock_query.return_value = ["https://techcrunch.com/2026/01/01/ai-agents-startup", "https://www.producthunt.com/posts/cool-tool"]
        urls = await extractor.search_urls(query="new AI SaaS launch 2026")

        assert len(urls) == 2
        assert "https://techcrunch.com/2026/01/01/ai-agents-startup" in urls
        assert "https://www.producthunt.com/posts/cool-tool" in urls


@pytest.mark.asyncio
async def test_crawl_and_extract_url_with_advanced_extractor():
    """Test crawling discovered URLs through AdvancedExtractor (Playwright)."""
    extractor = DynamicCrawlerExtractor(max_results_per_query=2)

    sample_items = [
        ExtractedItem(
            title="AutoGPT Pro - Autonomous SaaS for Enterprise",
            text="AutoGPT Pro is a new autonomous platform automating enterprise workflows with AI agents. ARR $50k in 2 months.",
            url="https://example.com/autogpt-pro",
            published_at=datetime.now(timezone.utc),
            source_type="deep_crawler",
        )
    ]

    mock_adv_instance = MagicMock()
    mock_adv_instance.extract = AsyncMock(return_value=sample_items)
    extractor = DynamicCrawlerExtractor(max_results_per_query=2, advanced_extractor=mock_adv_instance)

    extracted = await extractor.crawl_url("https://example.com/autogpt-pro")
    assert len(extracted) == 1
    assert extracted[0].title == "AutoGPT Pro - Autonomous SaaS for Enterprise"
    assert "AutoGPT Pro" in extracted[0].text
    assert extracted[0].source_type == "deep_crawler"


@pytest.mark.asyncio
async def test_extract_all_queries_deduplication():
    """Test that extract iterates over queries, deduplicates URLs, and handles errors gracefully."""
    extractor = DynamicCrawlerExtractor(
        queries=["query 1", "query 2"],
        max_results_per_query=2,
    )

    with patch.object(extractor, "search_urls", new_callable=AsyncMock) as mock_search:
        # Both queries return overlapping URLs
        mock_search.side_effect = [
            ["https://site1.com/p1", "https://site2.com/p2"],
            ["https://site2.com/p2", "https://site3.com/p3"],
        ]

        with patch.object(extractor, "crawl_url", new_callable=AsyncMock) as mock_crawl:
            mock_crawl.side_effect = lambda url: [
                ExtractedItem(
                    title=f"Title for {url}",
                    text=f"Content text for {url} with detailed information.",
                    url=url,
                    published_at=datetime.now(timezone.utc),
                    source_type="deep_crawler",
                )
            ]

            results = await extractor.extract("")
            # Should have crawled 3 unique URLs (site1, site2, site3)
            assert len(results) == 3
            crawled_urls = {item.url for item in results}
            assert crawled_urls == {
                "https://site1.com/p1",
                "https://site2.com/p2",
                "https://site3.com/p3",
            }


@pytest.mark.asyncio
async def test_extract_handles_failing_url():
    """Test that a failing URL extraction does not break the entire crawler execution."""
    extractor = DynamicCrawlerExtractor(queries=["query 1"], max_results_per_query=2)

    with patch.object(extractor, "search_urls", new_callable=AsyncMock) as mock_search:
        mock_search.return_value = ["https://fail.com", "https://ok.com"]

        async def fake_crawl(url):
            if "fail" in url:
                raise RuntimeError("Timeout or Cloudflare blocking")
            return [
                ExtractedItem(
                    title="OK Title",
                    text="OK Content text for testing deep crawler.",
                    url=url,
                    published_at=datetime.now(timezone.utc),
                    source_type="deep_crawler",
                )
            ]

        with patch.object(extractor, "crawl_url", side_effect=fake_crawl):
            results = await extractor.extract("")
            assert len(results) == 1
            assert results[0].url == "https://ok.com"
