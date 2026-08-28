"""Advanced Playwright & SPA Extractor for JavaScript-rendered sites and anti-bot bypassing (Level 1)."""

import asyncio
import logging
import urllib.parse
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional

from bs4 import BeautifulSoup
import httpx

from app.services.extractors.base import BaseExtractor, ExtractedItem

logger = logging.getLogger(__name__)

# Stealth desktop browser User-Agent
DESKTOP_STEALTH_UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
)

# JavaScript snippet to hide automation flags from Cloudflare / anti-bot scripts
STEALTH_JS_INJECTION = """
// Overwrite the `navigator.webdriver` property
Object.defineProperty(navigator, 'webdriver', {
    get: () => undefined
});

// Overwrite languages
Object.defineProperty(navigator, 'languages', {
    get: () => ['en-US', 'en']
});

// Overwrite plugins
Object.defineProperty(navigator, 'plugins', {
    get: () => [1, 2, 3, 4, 5]
});
"""


class AdvancedExtractor(BaseExtractor):
    """Playwright-powered headless browser extractor for SPA, React/Next.js and Cloudflare protected portals."""

    def __init__(
        self,
        timeout: float = 25.0,
        user_agent: Optional[str] = None,
        wait_until: str = "domcontentloaded",
        scroll_down: bool = True,
    ) -> None:
        super().__init__(
            timeout=timeout,
            user_agent=user_agent or DESKTOP_STEALTH_UA,
        )
        self.wait_until = wait_until
        self.scroll_down = scroll_down

    def _parse_html_dom(self, html: str, base_url: str) -> List[ExtractedItem]:
        """Extract high-value items from rendered HTML DOM using BeautifulSoup."""
        soup = BeautifulSoup(html, "html.parser")

        # Remove irrelevant tags (scripts, styles, navigations, footers)
        for tag in soup(["script", "style", "nav", "footer", "noscript", "svg"]):
            tag.decompose()

        items: List[ExtractedItem] = []
        parsed_base = urllib.parse.urlparse(base_url)

        # 1. Look for structured semantic card elements (articles, cards, posts)
        card_selectors = [
            "article",
            "[data-test*='post-item']",
            "[data-test*='item']",
            ".post-item",
            ".product-item",
            ".feed-item",
            ".card",
        ]

        cards = []
        for selector in card_selectors:
            found = soup.select(selector)
            if len(found) >= 3:
                cards = found
                break

        if cards:
            for card in cards[:25]:
                try:
                    # Find title
                    title_elem = card.find(["h1", "h2", "h3", "h4", "a", "strong"])
                    if not title_elem:
                        continue
                    title = title_elem.get_text(strip=True)
                    if len(title) < 5:
                        continue

                    # Find link
                    link_elem = card.find("a", href=True) or (card if card.name == "a" and card.get("href") else None)
                    card_url = base_url
                    if link_elem and link_elem.get("href"):
                        raw_href = link_elem["href"]
                        if raw_href.startswith("/"):
                            card_url = f"{parsed_base.scheme}://{parsed_base.netloc}{raw_href}"
                        elif raw_href.startswith("http"):
                            card_url = raw_href

                    # Find description/body text
                    body_text = card.get_text(separator=" ", strip=True)
                    if len(body_text) < 30:
                        continue

                    items.append(
                        ExtractedItem(
                            title=title,
                            text=body_text,
                            url=card_url,
                            published_at=datetime.now(timezone.utc),
                            source_type="playwright_spa",
                        )
                    )
                except Exception as card_err:
                    logger.debug("Error parsing card: %s", card_err)
                    continue

        # 2. Fallback: if no cards detected, extract top paragraphs/sections
        if not items:
            page_title = soup.title.string.strip() if soup.title and soup.title.string else "SPA Web Signal"
            paragraphs = [p.get_text(strip=True) for p in soup.find_all("p") if len(p.get_text(strip=True)) >= 50]

            if paragraphs:
                full_text = "\n\n".join(paragraphs[:10])
                items.append(
                    ExtractedItem(
                        title=page_title,
                        text=full_text,
                        url=base_url,
                        published_at=datetime.now(timezone.utc),
                        source_type="playwright_spa",
                    )
                )

        return items

    async def _extract_with_playwright(self, url: str) -> List[ExtractedItem]:
        """Run headless Playwright browser to load and render dynamic JavaScript."""
        from playwright.async_api import async_playwright

        async with async_playwright() as p:
            browser = await p.chromium.launch(
                headless=True,
                args=[
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-dev-shm-usage",
                    "--disable-gpu",
                ],
            )
            context = await browser.new_context(
                user_agent=self.user_agent,
                viewport={"width": 1920, "height": 1080},
                java_script_enabled=True,
                locale="en-US",
            )

            # Inject stealth scripts before any page load
            await context.add_init_script(STEALTH_JS_INJECTION)
            page = await context.new_page()

            try:
                logger.info("Playwright navigating to SPA target: %s", url)
                await page.goto(url, wait_until=self.wait_until, timeout=int(self.timeout * 1000))

                # Optional slight scroll to trigger lazy loading
                if self.scroll_down:
                    await page.evaluate("window.scrollBy(0, 800)")
                    await asyncio.sleep(1.0)

                rendered_html = await page.content()
                items = self._parse_html_dom(rendered_html, url)
                logger.info("Playwright extracted %d items from '%s'", len(items), url)
                return items
            finally:
                await page.close()
                await context.close()
                await browser.close()

    async def _extract_fallback_httpx(self, url: str) -> List[ExtractedItem]:
        """Fallback to async HTTPX request if Playwright is not available."""
        logger.info("Using HTTPX fallback extractor for '%s'...", url)
        headers = {
            "User-Agent": self.user_agent,
            "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
            "Accept-Language": "en-US,en;q=0.9",
        }
        async with httpx.AsyncClient(timeout=self.timeout, follow_redirects=True, headers=headers) as client:
            response = await client.get(url)
            response.raise_for_status()
            return self._parse_html_dom(response.text, url)

    async def extract(self, url: str) -> List[ExtractedItem]:
        """Extract content from URL using Playwright with automatic HTTPX fallback."""
        if not url or not url.strip():
            logger.warning("Empty URL provided to AdvancedExtractor.")
            return []

        clean_url = url.strip()

        # Try Playwright first
        try:
            return await self._extract_with_playwright(clean_url)
        except ImportError:
            logger.warning("Playwright library not installed. Falling back to HTTPX...")
            try:
                return await self._extract_fallback_httpx(clean_url)
            except Exception as e:
                logger.error("HTTPX fallback failed for '%s': %s", clean_url, e)
                return []
        except Exception as playwright_err:
            logger.warning("Playwright failed for '%s' (%s). Trying HTTPX fallback...", clean_url, playwright_err)
            try:
                return await self._extract_fallback_httpx(clean_url)
            except Exception as fallback_err:
                logger.error("All extraction strategies failed for '%s': %s", clean_url, fallback_err)
                return []
