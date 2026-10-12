"""Upwork RSS web scraper for freelance jobs.

Parses public Upwork search RSS feeds via HTTP client (httpx) without a headless browser,
extracting job title, description, budget (USD/hourly range), link, and external ID.
"""

from urllib.parse import urljoin
import logging
import re
import xml.etree.ElementTree as ET

from bs4 import BeautifulSoup
import httpx

from backend.scrapers.base import BaseScraper, RawTask

logger = logging.getLogger(__name__)


class UpworkRssScraper(BaseScraper):
    """Scraper for public Upwork job search RSS feeds."""

    DEFAULT_FEED_URLS: list[str] = [
        "https://www.upwork.com/ab/feed/jobs/rss?q=python&sort=recency",
        "https://www.upwork.com/ab/feed/jobs/rss?q=telegram+bot&sort=recency",
        "https://www.upwork.com/ab/feed/jobs/rss?q=automation&sort=recency",
        "https://www.upwork.com/ab/feed/jobs/rss?q=scraping&sort=recency",
    ]
    BASE_URL: str = "https://www.upwork.com"
    SOURCE_NAME: str = "web:upwork"

    DEFAULT_HEADERS: dict[str, str] = {
        "User-Agent": (
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
        ),
        "Accept": "application/rss+xml, application/xml, text/xml, */*",
        "Accept-Language": "en-US,en;q=0.9",
    }

    def __init__(
        self,
        feed_urls: list[str] | None = None,
        client: httpx.AsyncClient | None = None,
        timeout_sec: float = 20.0,
    ) -> None:
        """Initializes UpworkRssScraper.

        Args:
            feed_urls: Optional list of Upwork RSS feed URLs to scrape.
            client: Optional pre-configured httpx.AsyncClient for dependency injection.
            timeout_sec: Timeout for HTTP requests in seconds.
        """
        self.feed_urls = feed_urls if feed_urls is not None else list(self.DEFAULT_FEED_URLS)
        self.client = client
        self.timeout_sec = timeout_sec
        self._own_client = False

    async def fetch_new_tasks(self) -> list[RawTask]:
        """Fetches newly published tasks across all configured Upwork RSS feeds.

        Returns:
            list[RawTask]: Deduplicated list of extracted tasks.
        """
        if self.client is not None:
            return await self._fetch_all_feeds(self.client)

        try:
            async with httpx.AsyncClient(
                headers=self.DEFAULT_HEADERS,
                timeout=self.timeout_sec,
                follow_redirects=True,
            ) as client:
                return await self._fetch_all_feeds(client)
        except Exception as exc:
            logger.error("[UpworkRssScraper] Standalone HTTP fetch failed: %s", exc)
            return []

    async def _fetch_all_feeds(self, client: httpx.AsyncClient) -> list[RawTask]:
        """Queries each RSS feed URL using the provided HTTP client and aggregates tasks.

        Args:
            client: Active httpx.AsyncClient instance.

        Returns:
            list[RawTask]: Aggregated unique tasks.
        """
        all_tasks: list[RawTask] = []
        seen_ids: set[str] = set()

        for feed_url in self.feed_urls:
            try:
                response = await client.get(feed_url)
                if response.status_code != 200:
                    logger.warning(
                        "[UpworkRssScraper] Feed request %s returned status %d",
                        feed_url,
                        response.status_code,
                    )
                    continue

                feed_tasks = self.parse_rss(response.text)
                for task in feed_tasks:
                    if task.external_id not in seen_ids:
                        seen_ids.add(task.external_id)
                        all_tasks.append(task)
            except Exception as exc:
                logger.warning(
                    "[UpworkRssScraper] Error querying feed %s: %s",
                    feed_url,
                    exc,
                )
                continue

        logger.info(
            "[UpworkRssScraper] Scraped %d unique tasks across %d feeds",
            len(all_tasks),
            len(self.feed_urls),
        )
        return all_tasks

    def parse_rss(self, xml_content: str) -> list[RawTask]:
        """Parses RSS XML content into a list of normalized RawTask objects.

        Args:
            xml_content: Raw XML content string.

        Returns:
            list[RawTask]: Extracted tasks list.
        """
        if not xml_content or not xml_content.strip():
            return []

        # Attempt parsing with ElementTree first
        try:
            root = ET.fromstring(xml_content.strip())
            items = root.findall(".//item")
            tasks: list[RawTask] = []
            for item in items:
                task = self._parse_xml_item(item)
                if task:
                    tasks.append(task)
            return tasks
        except Exception as exc:
            logger.debug(
                "[UpworkRssScraper] ElementTree parse error (%s). Falling back to BeautifulSoup.",
                exc,
            )

        # Fallback to BeautifulSoup if XML is malformed
        try:
            soup = BeautifulSoup(xml_content, "html.parser")
            items = soup.find_all("item")
            tasks = []
            for item in items:
                task = self._parse_bs4_item(item)
                if task:
                    tasks.append(task)
            return tasks
        except Exception as exc:
            logger.error("[UpworkRssScraper] Failed to parse RSS content: %s", exc)
            return []

    def _parse_xml_item(self, item: ET.Element) -> RawTask | None:
        """Parses a single XML ElementTree item tag."""
        raw_title = item.findtext("title", default="").strip()
        link = item.findtext("link", default="").strip()
        guid = item.findtext("guid", default="").strip()
        raw_desc = item.findtext("description", default="").strip()

        return self._build_raw_task(raw_title=raw_title, link=link, guid=guid, raw_desc=raw_desc)

    def _parse_bs4_item(self, item: BeautifulSoup) -> RawTask | None:
        """Parses a single BeautifulSoup item element."""
        title_tag = item.find("title")
        link_tag = item.find("link")
        guid_tag = item.find("guid")
        desc_tag = item.find("description")

        raw_title = title_tag.get_text(strip=True) if title_tag else ""
        link = (link_tag.get_text(strip=True) or link_tag.next_sibling or "") if link_tag else ""
        if isinstance(link, str):
            link = link.strip()
        else:
            link = str(link).strip()

        guid = guid_tag.get_text(strip=True) if guid_tag else ""
        raw_desc = desc_tag.get_text(strip=True) if desc_tag else ""

        return self._build_raw_task(raw_title=raw_title, link=link, guid=guid, raw_desc=raw_desc)

    def _build_raw_task(
        self,
        raw_title: str,
        link: str,
        guid: str,
        raw_desc: str,
    ) -> RawTask | None:
        """Normalizes item fields and constructs a RawTask instance."""
        if not raw_title and not link:
            return None

        # Clean title (strip trailing ' - Upwork' if present)
        title = raw_title
        if title.endswith(" - Upwork"):
            title = title[:-9].strip()

        # Extract external ID from guid or link
        external_id = self._extract_external_id(link, guid)
        if not external_id:
            return None

        # Build direct URL
        if link and link.startswith("http"):
            url = link
        elif guid and guid.startswith("http"):
            url = guid
        elif external_id:
            url = f"{self.BASE_URL}/jobs/{external_id}"
        else:
            url = f"{self.BASE_URL}/jobs/"

        # Clean description HTML tags
        if raw_desc:
            clean_text = BeautifulSoup(raw_desc, "html.parser").get_text(
                separator=" ", strip=True
            )
        else:
            clean_text = title

        # Extract budget from description (fixed or hourly range)
        budget = self._extract_budget(raw_desc, clean_text)

        return RawTask(
            source=self.SOURCE_NAME,
            external_id=external_id,
            title=title or "Upwork Job",
            text=clean_text or title,
            url=url,
            budget=budget,
            author=None,
        )

    @staticmethod
    def _extract_external_id(link: str, guid: str) -> str:
        """Extracts unique Upwork job ID from guid or URL (e.g. ~01abc123)."""
        # Look for ciphertext ~01... in guid or link
        for candidate in (guid, link):
            if candidate:
                match = re.search(r"(~[a-zA-Z0-9]+)", candidate)
                if match:
                    return match.group(1)

        # Fallback to non-URL guid or last URL path segment
        if guid and not guid.startswith("http"):
            return guid.strip()

        if link:
            clean_link = link.rstrip("/")
            parts = clean_link.split("/")
            if parts and parts[-1]:
                return parts[-1]

        if guid:
            clean_guid = guid.rstrip("/")
            parts = clean_guid.split("/")
            if parts and parts[-1]:
                return parts[-1]

        return ""

    @staticmethod
    def _extract_budget(raw_desc: str, clean_desc: str) -> str | None:
        """Extracts USD budget or hourly rate range from job description.

        Recognizes patterns such as:
        - Budget: $500 or Budget: $1,200.00
        - Hourly Range: $25.00-$50.00 or Hourly Range: $25.00 - $50.00
        """
        for content in (clean_desc, raw_desc):
            if not content:
                continue

            # 1. Fixed Budget: $...
            budget_match = re.search(
                r"Budget(?:\s*:|\s+-)?\s*(\$[\d,]+(?:\.\d+)?)",
                content,
                re.IGNORECASE,
            )
            if budget_match:
                return budget_match.group(1).strip()

            # 2. Hourly Range: $... - $... or Hourly Range: $...
            hourly_match = re.search(
                r"Hourly\s*Range(?:\s*:|\s+-)?\s*(\$[\d,.]+\s*(?:-\s*\$[\d,.]+)?(?:\s*/hr)?)",
                content,
                re.IGNORECASE,
            )
            if hourly_match:
                return hourly_match.group(1).strip()

        return None

    async def close(self) -> None:
        """Closes internal HTTP client if owned."""
        if self._own_client and self.client and not self.client.is_closed:
            await self.client.aclose()

    async def __aenter__(self) -> "UpworkRssScraper":
        return self

    async def __aexit__(
        self,
        exc_type: type[BaseException] | None,
        exc_val: BaseException | None,
        exc_tb: object,
    ) -> None:
        await self.close()
