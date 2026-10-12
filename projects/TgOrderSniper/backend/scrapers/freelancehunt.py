"""Freelancehunt web scraper for freelance projects.

Scrapes project orders from https://freelancehunt.com/projects using Playwright
with an HTTP client (httpx) fallback, extracting project title, description,
budget, link, and external ID.
"""

from urllib.parse import urljoin
import logging
import re

from bs4 import BeautifulSoup
import httpx

from backend.scrapers.base import BaseScraper, RawTask

logger = logging.getLogger(__name__)


class FreelancehuntScraper(BaseScraper):
    """Scraper for Freelancehunt platform (https://freelancehunt.com/projects)."""

    BASE_URL: str = "https://freelancehunt.com"
    PROJECTS_URL: str = "https://freelancehunt.com/projects"
    SOURCE_NAME: str = "web:freelancehunt"

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
        headless: bool = True,
        timeout_ms: int = 25000,
        client: httpx.AsyncClient | None = None,
    ) -> None:
        """Initializes FreelancehuntScraper.

        Args:
            headless: Whether to run Playwright Chromium in headless mode.
            timeout_ms: Page navigation and element wait timeout in milliseconds.
            client: Optional pre-configured httpx.AsyncClient for testing or direct fetching.
        """
        self.headless = headless
        self.timeout_ms = timeout_ms
        self.client = client
        self._own_client = False

    async def fetch_new_tasks(self) -> list[RawTask]:
        """Fetches newly published projects from Freelancehunt.

        Uses the injected HTTP client if present; otherwise launches headless
        Playwright Chromium, falling back to httpx if Playwright launch fails.

        Returns:
            list[RawTask]: Extracted project tasks, empty if request fails.
        """
        if self.client is not None:
            return await self._fetch_with_httpx(self.client)

        try:
            from playwright.async_api import async_playwright
        except ImportError:
            logger.warning("[FreelancehuntScraper] Playwright not installed. Falling back to httpx.")
            return await self._fetch_with_standalone_httpx()

        try:
            async with async_playwright() as p:
                try:
                    browser = await p.chromium.launch(
                        headless=self.headless,
                        args=[
                            "--no-sandbox",
                            "--disable-setuid-sandbox",
                            "--disable-dev-shm-usage",
                            "--disable-gpu",
                            "--disable-extensions",
                        ],
                    )
                except Exception as launch_exc:
                    logger.warning(
                        "[FreelancehuntScraper] Failed to launch Chromium browser: %s. Falling back to httpx.",
                        launch_exc,
                    )
                    return await self._fetch_with_standalone_httpx()

                try:
                    context = await browser.new_context(
                        user_agent=self.DEFAULT_HEADERS["User-Agent"],
                        viewport={"width": 1280, "height": 800},
                    )
                    page = await context.new_page()

                    logger.debug("[FreelancehuntScraper] Navigating to %s...", self.PROJECTS_URL)
                    await page.goto(
                        self.PROJECTS_URL,
                        timeout=self.timeout_ms,
                        wait_until="domcontentloaded",
                    )

                    try:
                        await page.wait_for_selector(
                            "article.project-card, .project-card, table.table tr, .project-list tr",
                            timeout=min(self.timeout_ms, 10000),
                        )
                    except Exception:
                        logger.debug(
                            "[FreelancehuntScraper] Selector wait timed out, parsing current DOM content."
                        )

                    html = await page.content()
                    return self.parse_html(html)
                finally:
                    await browser.close()

        except Exception as exc:
            logger.warning("[FreelancehuntScraper] Playwright scraping encountered error: %s", exc)
            return await self._fetch_with_standalone_httpx()

    async def _fetch_with_httpx(self, client: httpx.AsyncClient) -> list[RawTask]:
        """Fetches page HTML using the provided httpx client.

        Args:
            client: Active httpx AsyncClient.

        Returns:
            list[RawTask]: Parsed projects list.
        """
        try:
            response = await client.get(self.PROJECTS_URL)
            response.raise_for_status()
            return self.parse_html(response.text)
        except Exception as exc:
            logger.error("[FreelancehuntScraper] HTTP fetch failed: %s", exc)
            return []

    async def _fetch_with_standalone_httpx(self) -> list[RawTask]:
        """Fetches page HTML using a temporary standalone httpx client."""
        try:
            async with httpx.AsyncClient(
                timeout=self.timeout_ms / 1000.0,
                headers=self.DEFAULT_HEADERS,
                follow_redirects=True,
            ) as client:
                return await self._fetch_with_httpx(client)
        except Exception as exc:
            logger.error("[FreelancehuntScraper] Standalone HTTP request failed: %s", exc)
            return []

    def parse_html(self, html: str) -> list[RawTask]:
        """Parses HTML document containing Freelancehunt project cards.

        Args:
            html: Raw HTML string.

        Returns:
            list[RawTask]: Extracted tasks list.
        """
        soup = BeautifulSoup(html, "html.parser")
        tasks: list[RawTask] = []

        card_selectors = [
            "article.project-card",
            "article[itemtype*='ListItem']",
            ".project-card",
            "table.table tr",
            ".project-list tr",
            "tr[data-project-id]",
            "div[class*='project-card']",
        ]

        card_elements = []
        for selector in card_selectors:
            found = soup.select(selector)
            if found:
                card_elements = found
                break

        # Fallback: identify parent elements of project links
        if not card_elements:
            card_elements = [
                a.find_parent("article") or a.find_parent("tr") or a.find_parent("div")
                for a in soup.select("a[href*='/project/']")
                if a.find_parent("article") or a.find_parent("tr") or a.find_parent("div")
            ]

        seen_ids: set[str] = set()

        for card in card_elements:
            try:
                title_elem = card.select_one(
                    "a.project-link, h2 a, a[href*='/project/']"
                )
                if not title_elem:
                    continue

                href = title_elem.get("href", "")
                if not href or "/project/" not in href or "/projects" in href:
                    continue

                full_url = urljoin(self.BASE_URL, href)

                # Extract external ID (e.g. /project/.../1658408.html or /project/1658408)
                id_match = re.search(r"/project/(?:[^/]+/)?(\d+)(?:\.html)?", href) or re.search(r"(\d+)(?:\.html)?$", href)
                external_id = id_match.group(1) if id_match else href.strip("/").split("/")[-1]

                if not external_id or external_id in seen_ids:
                    continue
                seen_ids.add(external_id)

                title = title_elem.get_text(strip=True)
                if not title:
                    continue

                # Budget parsing
                budget: str | None = None
                price_elem = card.select_one(
                    ".budget, .project-stat.budget, [class*='budget'], [class*='price'], .price"
                )
                if price_elem:
                    price_text = price_elem.get_text(strip=True)
                    if price_text and "договор" not in price_text.lower():
                        budget = price_text

                # Description parsing
                desc_elem = card.select_one(
                    ".project-description, [itemprop='description'], .assignment-description, [class*='description'], p"
                )
                if desc_elem:
                    text = desc_elem.get_text(separator=" ", strip=True)
                else:
                    text = title

                # Author / Client parsing
                author: str | None = None
                author_elem = card.select_one(
                    "a[href*='/freelancer/'], a[href*='/employer/'], [class*='user'] a, [class*='author'] a"
                )
                if author_elem:
                    author = author_elem.get_text(strip=True)

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
                logger.debug("[FreelancehuntScraper] Error parsing project card: %s", exc)
                continue

        logger.info("[FreelancehuntScraper] Scraped %d projects from Freelancehunt", len(tasks))
        return tasks

    async def close(self) -> None:
        """Closes internal HTTP client if owned."""
        if self._own_client and self.client and not self.client.is_closed:
            await self.client.aclose()

    async def __aenter__(self) -> "FreelancehuntScraper":
        return self

    async def __aexit__(
        self,
        exc_type: type[BaseException] | None,
        exc_val: BaseException | None,
        exc_tb: object,
    ) -> None:
        await self.close()
