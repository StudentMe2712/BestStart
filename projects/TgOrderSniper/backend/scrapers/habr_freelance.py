"""Habr Freelance web scraper for OmniLeadSniper.

Parses public tasks from https://freelance.habr.com/tasks using httpx and BeautifulSoup.
"""

from urllib.parse import urljoin
import logging
import re

from bs4 import BeautifulSoup
import httpx

from backend.scrapers.base import BaseScraper, RawTask

logger = logging.getLogger(__name__)


class HabrFreelanceScraper(BaseScraper):
    """Scrapes latest tasks from Habr Freelance (https://freelance.habr.com/tasks)."""

    BASE_URL: str = "https://freelance.habr.com"
    TASKS_URL: str = "https://freelance.habr.com/tasks"
    SOURCE_NAME: str = "web:habr_freelance"

    DEFAULT_HEADERS: dict[str, str] = {
        "User-Agent": (
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
        ),
        "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
        "Accept-Language": "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7",
    }

    def __init__(
        self,
        client: httpx.AsyncClient | None = None,
        timeout: float = 15.0,
    ) -> None:
        """Initializes HabrFreelanceScraper.

        Args:
            client: Optional pre-configured httpx.AsyncClient.
            timeout: Request timeout in seconds.
        """
        self.timeout = timeout
        if client is not None:
            self.client = client
            self._own_client = False
        else:
            self.client = httpx.AsyncClient(
                timeout=timeout,
                headers=self.DEFAULT_HEADERS,
                follow_redirects=True,
            )
            self._own_client = True

    async def fetch_new_tasks(self) -> list[RawTask]:
        """Fetches and parses the first page of recent tasks from Habr Freelance.

        Returns:
            list[RawTask]: Extracted tasks list, empty if network or parsing error.
        """
        try:
            response = await self.client.get(self.TASKS_URL)
            response.raise_for_status()
            html = response.text
            return self.parse_html(html)
        except Exception as exc:
            logger.error("[HabrScraper] Failed to fetch tasks from %s: %s", self.TASKS_URL, exc)
            return []

    def parse_html(self, html: str) -> list[RawTask]:
        """Parses HTML document containing Habr Freelance task cards.

        Args:
            html: Raw HTML content.

        Returns:
            list[RawTask]: Parsed task models.
        """
        soup = BeautifulSoup(html, "html.parser")
        tasks: list[RawTask] = []

        # Habr Freelance lists tasks in list items or articles
        card_selectors = [
            "li.content-list__item",
            "article.task",
            ".task_list .task",
            "div.task__column",
        ]

        card_elements = []
        for selector in card_selectors:
            found = soup.select(selector)
            if found:
                card_elements = found
                break

        # Fallback: find any containers with task titles
        if not card_elements:
            card_elements = [
                a.find_parent("li") or a.find_parent("article") or a.parent
                for a in soup.select("div.task__title a, a[href*='/tasks/']")
                if a.parent
            ]

        seen_ids: set[str] = set()

        for card in card_elements:
            try:
                title_elem = card.select_one(".task__title a, a[href*='/tasks/']")
                if not title_elem:
                    continue

                href = title_elem.get("href", "")
                if not href or "/tasks/" not in href:
                    continue

                full_url = urljoin(self.BASE_URL, href)
                id_match = re.search(r"/tasks/(\d+)", href)
                external_id = id_match.group(1) if id_match else href.strip("/").split("/")[-1]

                if not external_id or external_id in seen_ids:
                    continue
                seen_ids.add(external_id)

                title = title_elem.get_text(strip=True)
                if not title:
                    continue

                # Budget parsing
                budget: str | None = None
                price_elem = card.select_one(".task__price, .price, .count")
                if price_elem:
                    price_text = price_elem.get_text(strip=True)
                    # Exclude negotiated price or empty indicators
                    if price_text and "договор" not in price_text.lower():
                        budget = price_text

                # Author / Client parsing
                author: str | None = None
                author_elem = card.select_one(".task__client a, .user-params__item a, .task__user a")
                if author_elem:
                    author = author_elem.get_text(strip=True)

                # Description / snippet text parsing
                desc_elem = card.select_one(".task__description, .task__text, .task__params")
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
                logger.debug("[HabrScraper] Error parsing single card: %s", exc)
                continue

        logger.info("[HabrScraper] Scraped %d tasks from Habr Freelance", len(tasks))
        return tasks

    async def close(self) -> None:
        """Closes internal HTTP client if owned."""
        if self._own_client and self.client and not self.client.is_closed:
            await self.client.aclose()

    async def __aenter__(self) -> "HabrFreelanceScraper":
        return self

    async def __aexit__(self, exc_type: type[BaseException] | None, exc_val: BaseException | None, exc_tb: object) -> None:
        await self.close()
