"""FL.ru web scraper for freelance projects.

Scrapes freelance orders from https://www.fl.ru/projects/ using Playwright
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


class FlRuScraper(BaseScraper):
    """Scraper for FL.ru exchange (https://www.fl.ru/projects/)."""

    BASE_URL: str = "https://www.fl.ru"
    PROJECTS_URL: str = "https://www.fl.ru/projects/"
    SOURCE_NAME: str = "web:fl_ru"

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
        """Initializes FlRuScraper.

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
        """Fetches newly published projects from FL.ru.

        Uses the injected HTTP client if present; otherwise attempts Playwright
        headless Chromium, falling back to httpx if Playwright launch fails.

        Returns:
            list[RawTask]: Extracted project tasks, empty if request fails or on maintenance.
        """
        if self.client is not None:
            return await self._fetch_with_httpx(self.client)

        try:
            from playwright.async_api import async_playwright
        except ImportError:
            logger.warning("[FlRuScraper] Playwright not installed. Falling back to httpx.")
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
                        "[FlRuScraper] Failed to launch Chromium browser: %s. Falling back to httpx.",
                        launch_exc,
                    )
                    return await self._fetch_with_standalone_httpx()

                try:
                    context = await browser.new_context(
                        user_agent=self.DEFAULT_HEADERS["User-Agent"],
                        viewport={"width": 1280, "height": 800},
                    )
                    page = await context.new_page()

                    logger.debug("[FlRuScraper] Navigating to %s...", self.PROJECTS_URL)
                    await page.goto(
                        self.PROJECTS_URL,
                        timeout=self.timeout_ms,
                        wait_until="domcontentloaded",
                    )

                    try:
                        await page.wait_for_selector(
                            "div[id^='project-item'], .b-post, .project-item, a[href*='/projects/']",
                            timeout=min(self.timeout_ms, 10000),
                        )
                    except Exception:
                        logger.debug("[FlRuScraper] Selector wait timed out, parsing current DOM content.")

                    html = await page.content()
                    return self.parse_html(html)
                finally:
                    await browser.close()

        except Exception as exc:
            logger.warning("[FlRuScraper] Playwright scraping encountered error: %s", exc)
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
            logger.error("[FlRuScraper] HTTP fetch failed: %s", exc)
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
            logger.error("[FlRuScraper] Standalone HTTP request failed: %s", exc)
            return []

    def parse_html(self, html: str) -> list[RawTask]:
        """Parses HTML document containing FL.ru project cards.

        Args:
            html: Raw HTML string.

        Returns:
            list[RawTask]: Extracted tasks list.
        """
        # Guard against maintenance or error pages
        if "технические работы" in html.lower():
            logger.info("[FlRuScraper] FL.ru is currently undergoing maintenance. Skipping parse.")
            return []

        soup = BeautifulSoup(html, "html.parser")
        tasks: list[RawTask] = []

        card_selectors = [
            "div[id^='project-item']",
            ".b-post",
            "div[class*='b-post']",
            ".project-item",
            "div[class*='project-item']",
            "div[class*='b-post_padtop_15']",
            "div.b-post_padbot_15",
            "div.search-item",
        ]

        card_elements = []
        for selector in card_selectors:
            found = soup.select(selector)
            if found:
                card_elements = found
                break

        # Fallback: locate container enclosing project links
        if not card_elements:
            card_elements = [
                a.find_parent("div") or a.find_parent("article") or a.parent
                for a in soup.select("a[href*='/projects/']")
                if a.parent and re.search(r"/projects/\d+", a.get("href", ""))
            ]

        seen_ids: set[str] = set()

        for card in card_elements:
            try:
                title_elem = card.select_one(
                    "a.b-post__link, h2.b-post__title a, a[href*='/projects/']"
                )
                if not title_elem:
                    continue

                href = title_elem.get("href", "")
                if not href or "/projects/" not in href:
                    continue

                # Ignore general category or pagination links like /projects/?page=2
                id_match = re.search(r"/projects/(\d+)", href)
                if not id_match:
                    continue

                external_id = id_match.group(1)
                if external_id in seen_ids:
                    continue
                seen_ids.add(external_id)

                full_url = urljoin(self.BASE_URL, href)
                title = title_elem.get_text(strip=True)
                if not title:
                    continue

                # Budget parsing
                budget: str | None = None
                price_elem = card.select_one(
                    ".b-post__price, [class*='price'], span.b-post__bold, [class*='budget'], .text-success"
                )
                if price_elem:
                    price_text = price_elem.get_text(strip=True)
                    if price_text and "договор" not in price_text.lower():
                        budget = price_text

                # Description parsing
                desc_elem = card.select_one(
                    ".b-post__body, .b-post__txt, [class*='description'], [class*='text'], p"
                )
                if desc_elem:
                    text = desc_elem.get_text(separator=" ", strip=True)
                else:
                    text = title

                # Author / Client parsing
                author: str | None = None
                author_elem = card.select_one(
                    "a.b-user__link, a[href*='/users/'], [class*='author'] a, [class*='user'] a"
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
                logger.debug("[FlRuScraper] Error parsing project card: %s", exc)
                continue

        logger.info("[FlRuScraper] Scraped %d projects from FL.ru", len(tasks))
        return tasks

    async def close(self) -> None:
        """Closes internal HTTP client if owned."""
        if self._own_client and self.client and not self.client.is_closed:
            await self.client.aclose()

    async def __aenter__(self) -> "FlRuScraper":
        return self

    async def __aexit__(
        self,
        exc_type: type[BaseException] | None,
        exc_val: BaseException | None,
        exc_tb: object,
    ) -> None:
        await self.close()
