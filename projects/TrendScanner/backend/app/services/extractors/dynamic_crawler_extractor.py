"""Dynamic Deep Web Crawler Extractor leveraging Search Engines and Playwright (Level 11)."""

import asyncio
import logging
import re
import urllib.parse
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional, Set
from bs4 import BeautifulSoup
import httpx

from app.services.extractors.advanced_extractor import AdvancedExtractor
from app.services.extractors.base import BaseExtractor, ExtractedItem

logger = logging.getLogger(__name__)

DEFAULT_CRAWLER_QUERIES: List[str] = [
    "new AI SaaS launch 2026",
    "latest indie hacker revenue trends",
    "innovative B2B startups",
    "profitable micro-saas ideas 2026",
    "fast growing AI agents startups",
    "top product hunt trending tools",
    "B2B automation software revenue",
]

# Domains to skip from deep crawler page rendering (noise, social login, search engines)
IGNORED_CRAWLER_DOMAINS = {
    "duckduckgo.com",
    "google.com",
    "bing.com",
    "yahoo.com",
    "facebook.com",
    "twitter.com",
    "x.com",
    "instagram.com",
    "linkedin.com/login",
    "accounts.google.com",
    "support.apple.com",
}


def _clean_search_url(raw_url: str) -> Optional[str]:
    """Clean and validate destination URL from search redirect or direct href."""
    if not raw_url or not isinstance(raw_url, str):
        return None

    clean = raw_url.strip()
    if "uddg=" in clean:
        try:
            parsed = urllib.parse.urlparse(clean)
            qs = urllib.parse.parse_qs(parsed.query)
            if "uddg" in qs and qs["uddg"]:
                clean = urllib.parse.unquote(qs["uddg"][0])
        except Exception:
            pass

    if clean.startswith("//"):
        clean = f"https:{clean}"

    if not clean.startswith("http://") and not clean.startswith("https://"):
        return None

    try:
        parsed_dest = urllib.parse.urlparse(clean)
        netloc = (parsed_dest.netloc or "").lower()
        if any(ign in netloc for ign in IGNORED_CRAWLER_DOMAINS):
            return None
        # Skip static binary / image / media files
        if re.search(r"\.(pdf|png|jpg|jpeg|gif|svg|webp|ico|mp4|mp3|zip|gz|tar)$", parsed_dest.path, re.IGNORECASE):
            return None
        return clean
    except Exception:
        return None


class DynamicCrawlerExtractor(BaseExtractor):
    """
    Global Deep Web Crawler:
    1. Executes search engine queries across dynamic tech and SaaS keywords.
    2. Gathers top candidate URLs.
    3. Crawls each page via Playwright (AdvancedExtractor) to bypass bot protection and render SPAs.
    """

    def __init__(
        self,
        queries: Optional[List[str]] = None,
        max_results_per_query: int = 10,
        timeout: float = 25.0,
        user_agent: Optional[str] = None,
        advanced_extractor: Optional[AdvancedExtractor] = None,
    ) -> None:
        super().__init__(timeout=timeout, user_agent=user_agent)
        self.queries: List[str] = list(queries) if queries else list(DEFAULT_CRAWLER_QUERIES)
        self.max_results_per_query: int = max_results_per_query
        self._advanced_extractor = advanced_extractor or AdvancedExtractor(timeout=timeout, user_agent=user_agent)

    async def _search_with_ddgs_library(self, query: str, max_results: int) -> List[str]:
        """Query DuckDuckGo using duckduckgo_search library if available."""
        try:
            from duckduckgo_search import DDGS
        except ImportError:
            return []

        def _sync_ddgs_call():
            results = []
            with DDGS() as ddgs:
                for r in ddgs.text(query, max_results=max_results):
                    href = r.get("href") or r.get("url") or r.get("link")
                    clean = _clean_search_url(href)
                    if clean and clean not in results:
                        results.append(clean)
            return results

        try:
            return await asyncio.to_thread(_sync_ddgs_call)
        except Exception as exc:
            logger.debug("DDGS library query failed for '%s': %s", query, exc)
            return []

    async def _search_with_ddg_http(self, query: str, max_results: int) -> List[str]:
        """Fallback direct HTTP request to DuckDuckGo HTML / Lite endpoints."""
        urls: List[str] = []
        headers = {
            "User-Agent": self.user_agent,
            "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
            "Accept-Language": "en-US,en;q=0.9",
        }

        try:
            async with httpx.AsyncClient(timeout=10.0, follow_redirects=True, headers=headers) as client:
                # 1. DDG HTML endpoint
                resp = await client.post("https://html.duckduckgo.com/html/", data={"q": query})
                if resp.status_code == 200 and resp.text:
                    soup = BeautifulSoup(resp.text, "html.parser")
                    for a_tag in soup.find_all("a", class_=re.compile(r"result__a|result__url")):
                        href = a_tag.get("href")
                        clean = _clean_search_url(href)
                        if clean and clean not in urls:
                            urls.append(clean)
                            if len(urls) >= max_results:
                                return urls

                # 2. DDG Lite endpoint if needed
                if not urls:
                    resp_lite = await client.post("https://lite.duckduckgo.com/lite/", data={"q": query})
                    if resp_lite.status_code == 200 and resp_lite.text:
                        soup_lite = BeautifulSoup(resp_lite.text, "html.parser")
                        for a_tag in soup_lite.find_all("a", class_="result-link"):
                            href = a_tag.get("href")
                            clean = _clean_search_url(href)
                            if clean and clean not in urls:
                                urls.append(clean)
                                if len(urls) >= max_results:
                                    return urls
        except Exception as http_err:
            logger.debug("DDG HTTP search fallback error: %s", http_err)

        return urls[:max_results]

    async def _search_engine_query(self, query: str, max_results: int) -> List[str]:
        """Orchestrate search via DDGS library with HTTP fallback."""
        # 1. Try DDGS library
        urls = await self._search_with_ddgs_library(query, max_results)
        if urls:
            return urls[:max_results]

        # 2. Try HTTP scraping fallback
        urls = await self._search_with_ddg_http(query, max_results)
        return urls[:max_results]

    async def search_urls(self, query: str) -> List[str]:
        """Search engine for target query and return clean candidate URLs."""
        if not query or not query.strip():
            return []
        return await self._search_engine_query(query.strip(), self.max_results_per_query)

    async def crawl_url(self, url: str) -> List[ExtractedItem]:
        """Crawl a single URL with Playwright / AdvancedExtractor and tag as deep_crawler."""
        try:
            items = await self._advanced_extractor.extract(url)
            # Tag source_type as deep_crawler
            tagged_items = []
            for it in items:
                tagged_items.append(
                    ExtractedItem(
                        title=it.title,
                        text=it.text,
                        url=it.url or url,
                        published_at=it.published_at or datetime.now(timezone.utc),
                        source_type="deep_crawler",
                    )
                )
            return tagged_items
        except Exception as err:
            logger.warning("Failed crawling page '%s': %s", url, err)
            return []

    async def extract(self, url_or_query: str = "") -> List[ExtractedItem]:
        """
        Execute full Deep Web Crawl:
        1. Queries search engine for all configured queries.
        2. Deduplicates target URLs.
        3. Crawls pages through Playwright AdvancedExtractor.
        """
        target_queries = [url_or_query.strip()] if url_or_query and url_or_query.strip() else self.queries
        logger.info("DynamicCrawlerExtractor starting crawl over %d queries...", len(target_queries))

        discovered_urls: Set[str] = set()
        ordered_urls: List[str] = []

        # 1. Search engine pass
        for q in target_queries:
            try:
                urls = await self.search_urls(q)
                logger.info("Crawler query '%s' discovered %d URLs.", q, len(urls))
                for u in urls:
                    if u not in discovered_urls:
                        discovered_urls.add(u)
                        ordered_urls.append(u)
            except Exception as q_err:
                logger.error("Error searching for query '%s': %s", q, q_err)

        logger.info("DynamicCrawlerExtractor found %d unique target URLs to scrape.", len(ordered_urls))

        # 2. Playwright crawl pass
        all_extracted: List[ExtractedItem] = []
        for target_url in ordered_urls:
            try:
                page_items = await self.crawl_url(target_url)
                if page_items:
                    all_extracted.extend(page_items)
            except Exception as crawl_err:
                logger.warning("Error crawling URL '%s': %s", target_url, crawl_err)
                continue

        logger.info("DynamicCrawlerExtractor successfully extracted %d total items.", len(all_extracted))
        return all_extracted
