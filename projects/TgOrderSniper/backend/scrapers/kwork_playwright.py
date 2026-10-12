"""Kwork web scraper using Playwright headless browser.

Scrapes freelance projects from https://kwork.ru/projects with JavaScript rendering
and graceful fallback if headless browser launch fails.
"""

from urllib.parse import urljoin
import logging
import re

from bs4 import BeautifulSoup

from backend.scrapers.base import BaseScraper, RawTask

logger = logging.getLogger(__name__)


class KworkPlaywrightScraper(BaseScraper):
    """Playwright-based scraper for Kwork exchange (https://kwork.ru/projects)."""

    BASE_URL: str = "https://kwork.ru"
    PROJECTS_URL: str = "https://kwork.ru/projects"
    SOURCE_NAME: str = "web:kwork"

    def __init__(self, headless: bool = True, timeout_ms: int = 25000) -> None:
        """Initializes KworkPlaywrightScraper.

        Args:
            headless: Whether to run Chromium in headless mode.
            timeout_ms: Page navigation and element wait timeout in milliseconds.
        """
        self.headless = headless
        self.timeout_ms = timeout_ms

    async def fetch_new_tasks(self) -> list[RawTask]:
        """Fetches latest freelance projects from Kwork using headless Chromium.

        Returns:
            list[RawTask]: Extracted tasks list, empty if browser launch fails or error occurs.
        """
        try:
            from playwright.async_api import async_playwright
        except ImportError:
            logger.warning("[KworkScraper] Playwright is not installed. Skipping Kwork scraper.")
            return []

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
                        "[KworkScraper] Failed to launch Chromium browser (run 'playwright install chromium'): %s",
                        launch_exc,
                    )
                    return []

                try:
                    context = await browser.new_context(
                        user_agent=(
                            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
                            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
                        ),
                        viewport={"width": 1280, "height": 800},
                    )
                    page = await context.new_page()

                    logger.debug("[KworkScraper] Navigating to %s...", self.PROJECTS_URL)
                    await page.goto(
                        self.PROJECTS_URL,
                        timeout=self.timeout_ms,
                        wait_until="domcontentloaded",
                    )

                    # Wait for projects container or cards
                    try:
                        await page.wait_for_selector(
                            ".project-card, .want-card, .wants-card, [class*='want-card'], [class*='project-card']",
                            timeout=min(self.timeout_ms, 10000),
                        )
                    except Exception:
                        logger.debug("[KworkScraper] Selector wait timed out, attempting HTML parsing of current state.")

                    html = await page.content()
                    return self.parse_html(html)
                finally:
                    await browser.close()

        except Exception as exc:
            logger.warning("[KworkScraper] Playwright scraping encountered error: %s", exc)
            return []

    def parse_html(self, html: str) -> list[RawTask]:
        """Parses HTML content containing Kwork project cards.

        Args:
            html: Raw HTML content from page.

        Returns:
            list[RawTask]: Extracted tasks.
        """
        soup = BeautifulSoup(html, "html.parser")
        tasks: list[RawTask] = []

        card_selectors = [
            ".want-card",
            ".wants-card",
            ".project-card",
            "div[class*='want-card']",
            "div[class*='project-card']",
            "div[class*='card--want']",
        ]

        card_elements = []
        for selector in card_selectors:
            found = soup.select(selector)
            if found:
                card_elements = found
                break

        # Fallback: locate elements enclosing project links
        if not card_elements:
            card_elements = [
                a.find_parent("div")
                for a in soup.select("a[href*='/projects/']")
                if a.find_parent("div")
            ]

        seen_ids: set[str] = set()

        for card in card_elements:
            try:
                title_elem = card.select_one("a[href*='/projects/']")
                if not title_elem:
                    continue

                href = title_elem.get("href", "")
                if not href or "/projects/" not in href:
                    continue

                full_url = urljoin(self.BASE_URL, href)
                id_match = re.search(r"/projects/(\d+)", href)
                external_id = id_match.group(1) if id_match else href.strip("/").split("/")[-1]

                if not external_id or external_id in seen_ids:
                    continue
                seen_ids.add(external_id)

                title = title_elem.get_text(strip=True)
                if not title:
                    continue

                # Budget extraction
                budget: str | None = None
                price_elem = card.select_one(
                    "[class*='price'], [class*='budget'], .want-card__price, .project-card__price"
                )
                if price_elem:
                    price_text = price_elem.get_text(strip=True)
                    if price_text and "договор" not in price_text.lower():
                        budget = price_text

                # Author / Client
                author: str | None = None
                author_elem = card.select_one(
                    "[class*='user'] a, [class*='author'] a, [class*='profile'] a"
                )
                if author_elem:
                    author = author_elem.get_text(strip=True)

                # Description
                desc_elem = card.select_one(
                    "[class*='description'], [class*='text'], [class*='break-word'], [class*='content']"
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
                logger.debug("[KworkScraper] Error parsing project card: %s", exc)
                continue

        logger.info("[KworkScraper] Scraped %d projects from Kwork", len(tasks))
        return tasks
