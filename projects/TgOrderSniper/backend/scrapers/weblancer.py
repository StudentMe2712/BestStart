"""Weblancer web scraper for freelance projects and orders.

Scrapes freelance orders from https://www.weblancer.net/jobs/ using httpx and BeautifulSoup,
extracting job title, description, budget, direct URL, author, and external ID.
"""

from urllib.parse import urljoin
import logging
import re

from bs4 import BeautifulSoup
import httpx

from backend.scrapers.base import BaseScraper, RawTask

logger = logging.getLogger(__name__)


class WeblancerScraper(BaseScraper):
    """Scraper for Weblancer freelance exchange (https://www.weblancer.net/jobs/)."""

    BASE_URL: str = "https://www.weblancer.net"
    JOBS_URL: str = "https://www.weblancer.net/jobs/"
    SOURCE_NAME: str = "web:weblancer"

    DEFAULT_HEADERS: dict[str, str] = {
        "User-Agent": (
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
        ),
        "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
        "Accept-Language": "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7",
    }

    def __init__(
        self,
        client: httpx.AsyncClient | None = None,
        timeout_sec: float = 20.0,
        jobs_url: str | None = None,
    ) -> None:
        """Initializes WeblancerScraper.

        Args:
            client: Optional pre-configured httpx.AsyncClient for dependency injection.
            timeout_sec: Page request timeout in seconds.
            jobs_url: Custom jobs endpoint URL (defaults to JOBS_URL).
        """
        self.client = client
        self.timeout_sec = timeout_sec
        self.jobs_url = jobs_url or self.JOBS_URL
        self._own_client = False

    async def fetch_new_tasks(self) -> list[RawTask]:
        """Fetches newly published tasks from Weblancer.

        Returns:
            list[RawTask]: Extracted tasks list, empty if request fails.
        """
        if self.client is not None:
            return await self._fetch_with_client(self.client)

        try:
            async with httpx.AsyncClient(
                headers=self.DEFAULT_HEADERS,
                timeout=self.timeout_sec,
                follow_redirects=True,
            ) as client:
                return await self._fetch_with_client(client)
        except Exception as exc:
            logger.error("[WeblancerScraper] Standalone HTTP fetch failed: %s", exc)
            return []

    async def _fetch_with_client(self, client: httpx.AsyncClient) -> list[RawTask]:
        """Fetches and parses jobs page with the given HTTP client."""
        try:
            response = await client.get(self.jobs_url)
            if response.status_code != 200:
                logger.warning(
                    "[WeblancerScraper] Request to %s failed with status %d",
                    self.jobs_url,
                    response.status_code,
                )
                return []
            return self.parse_html(response.text)
        except Exception as exc:
            logger.error("[WeblancerScraper] HTTP request error: %s", exc)
            return []

    def parse_html(self, html_content: str) -> list[RawTask]:
        """Parses HTML content from Weblancer jobs page and extracts job tasks.

        Args:
            html_content: Raw HTML text of the page.

        Returns:
            list[RawTask]: Extracted tasks list.
        """
        if not html_content or not html_content.strip():
            return []

        soup = BeautifulSoup(html_content, "html.parser")
        tasks: list[RawTask] = []
        seen_ids: set[str] = set()

        card_selectors = [
            ".click_container",
            "div.cols_table div.row",
            ".cols_table .row",
            "article.job",
            ".job-item",
            "div.row",
        ]

        card_elements = []
        for selector in card_selectors:
            found = soup.select(selector)
            if found and any(elem.select_one("a[href*='/jobs/']") for elem in found):
                card_elements = found
                break

        # Fallback: locate container blocks enclosing job links
        if not card_elements:
            card_elements = [
                a.find_parent("div", class_="row")
                or a.find_parent("div", class_="click_container")
                or a.find_parent("article")
                or a.find_parent("div")
                for a in soup.select("a[href*='/jobs/']")
                if a.find_parent("div")
            ]

        for card in card_elements:
            if not card:
                continue
            try:
                # Find title anchor
                title_elem = card.select_one("h2 a, .title a, a[href*='/jobs/']")
                if not title_elem:
                    continue

                href = title_elem.get("href", "")
                if (
                    not href
                    or href.strip() in ("/jobs/", "/jobs", "#")
                    or href.startswith("/jobs/?")
                ):
                    continue

                full_url = urljoin(self.BASE_URL, href)

                # Extract external ID from URL (e.g. /jobs/.../telegram-bot-12345/ or /jobs/12345.html)
                id_match = (
                    re.search(r"-(\d+)(?:\.html)?/?$", href)
                    or re.search(r"/(\d+)(?:\.html)?/?$", href)
                    or re.search(r"(\d+)", href)
                )
                external_id = id_match.group(1) if id_match else href.strip("/").split("/")[-1]

                if not external_id or external_id in seen_ids:
                    continue
                seen_ids.add(external_id)

                title = title_elem.get_text(strip=True)
                if not title:
                    continue

                # Extract budget
                budget: str | None = None
                price_elem = card.select_one(
                    ".amount, .price, [class*='amount'], [class*='price'], .float-right"
                )
                if price_elem:
                    price_text = price_elem.get_text(strip=True)
                    if price_text and "договор" not in price_text.lower():
                        budget = price_text

                # Extract author
                author: str | None = None
                author_elem = card.select_one(
                    "a[href*='/users/'], a[href*='/employers/'], a[href*='/clients/'], "
                    "[class*='user'] a, [class*='author'] a"
                )
                if author_elem:
                    author = author_elem.get_text(strip=True)

                # Extract description
                desc_elem = card.select_one(
                    ".text_field, [class*='description'], .description, .collapse, [class*='text-muted']"
                )
                if desc_elem:
                    text = desc_elem.get_text(separator=" ", strip=True)
                else:
                    text = title

                tasks.append(
                    RawTask(
                        source=self.SOURCE_NAME,
                        external_id=external_id,
                        title=title,
                        text=text,
                        url=full_url,
                        budget=budget,
                        author=author,
                    )
                )
            except Exception as exc:
                logger.debug("[WeblancerScraper] Error parsing project card: %s", exc)
                continue

        logger.info("[WeblancerScraper] Scraped %d projects from Weblancer", len(tasks))
        return tasks

    async def close(self) -> None:
        """Closes internal HTTP client if owned."""
        if self._own_client and self.client and not self.client.is_closed:
            await self.client.aclose()

    async def __aenter__(self) -> "WeblancerScraper":
        return self

    async def __aexit__(
        self,
        exc_type: type[BaseException] | None,
        exc_val: BaseException | None,
        exc_tb: object,
    ) -> None:
        await self.close()
