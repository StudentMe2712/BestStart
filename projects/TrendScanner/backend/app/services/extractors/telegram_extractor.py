"""Telegram Channel Extractor for TrendScanner (Stage 2 / Level 3).

Supports:
1. Direct asynchronous Telethon client extraction using stored SQLite session.
2. Robust web preview scraping fallback (t.me/s/{channel}) via HTTPX and BeautifulSoup.
"""

import asyncio
import email.utils
import html
import logging
import re
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional

from bs4 import BeautifulSoup, Tag
import httpx

from app.core.settings import settings
from app.services.extractors.base import BaseExtractor, ExtractedItem

logger = logging.getLogger(__name__)

# Realistic browser User-Agent for web preview scraping
DEFAULT_TELEGRAM_USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
)


class TelegramExtractor(BaseExtractor):
    """Extractor for Telegram public and authenticated channels via Telethon with Web Preview fallback."""

    DEFAULT_USER_AGENT: str = DEFAULT_TELEGRAM_USER_AGENT
    DEFAULT_LIMIT: int = 20
    TELETHON_CONNECT_TIMEOUT: float = 5.0

    def __init__(
        self,
        timeout: float = 15.0,
        user_agent: Optional[str] = None,
        limit: int = DEFAULT_LIMIT,
    ) -> None:
        """Initialize TelegramExtractor.

        Args:
            timeout: Request timeout in seconds.
            user_agent: Custom User-Agent header for HTTP requests.
            limit: Number of latest messages to fetch per extraction.
        """
        super().__init__(
            timeout=timeout,
            user_agent=user_agent or self.DEFAULT_USER_AGENT,
        )
        self.limit = limit

    @staticmethod
    def _normalize_channel_name(url_or_username: str) -> str:
        """Extract clean channel username or handle from various URL and username representations.

        Supports:
            - @username
            - username
            - https://t.me/username
            - https://t.me/s/username
            - http://t.me/username/
            - t.me/username
            - t.me/s/username?before=123
            - https://telegram.me/username
            - https://telegram.me/s/username

        Args:
            url_or_username: Raw channel input string or URL.

        Returns:
            str: Normalized channel identifier or empty string.
        """
        if not url_or_username or not url_or_username.strip():
            return ""

        raw = url_or_username.strip()

        # Remove query parameters and fragment anchors
        raw = raw.split("?")[0].split("#")[0].rstrip("/")

        # Handle @ prefix
        if raw.startswith("@"):
            return raw.lstrip("@").strip()

        # Handle URLs with domains (t.me, telegram.me)
        if "t.me/" in raw or "telegram.me/" in raw:
            match = re.search(
                r"(?:https?://)?(?:www\.)?(?:t\.me|telegram\.me)/(?:s/)?([^/?#]+)",
                raw,
                re.IGNORECASE,
            )
            if match:
                channel = match.group(1).strip()
                return channel.lstrip("@")

        # Fallback: strip leading/trailing slashes and @ characters
        clean = raw.strip("/").lstrip("@").strip()
        return clean

    @staticmethod
    def _generate_title(text: str, max_length: int = 100) -> str:
        """Generate a clean, informative title from the message body text.

        Args:
            text: Raw message content.
            max_length: Maximum length of title in characters.

        Returns:
            str: Formatted headline.
        """
        if not text or not text.strip():
            return "Telegram Signal"

        lines = [line.strip() for line in text.splitlines() if line.strip()]
        if not lines:
            return "Telegram Signal"

        first_line = lines[0]
        # Clean leading hashtags, dashes, or bullet points
        first_line = re.sub(r"^[#\*\-•—\s]+", "", first_line).strip()
        if not first_line:
            first_line = text.strip()

        if len(first_line) > max_length:
            return first_line[: max_length - 3].strip() + "..."
        return first_line

    def _parse_datetime(self, date_str: Optional[str]) -> Optional[datetime]:
        """Parse ISO-8601 or RFC date strings into timezone-aware UTC datetime.

        Args:
            date_str: Date string from HTML markup or API.

        Returns:
            Optional[datetime]: Parsed datetime in UTC, or None if invalid.
        """
        if not date_str or not date_str.strip():
            return None

        clean_str = date_str.strip()

        # 1. ISO 8601 (used in Telegram HTML widget <time datetime="...">)
        try:
            iso_str = clean_str.replace("Z", "+00:00")
            parsed_dt = datetime.fromisoformat(iso_str)
            if parsed_dt.tzinfo is None:
                return parsed_dt.replace(tzinfo=timezone.utc)
            return parsed_dt.astimezone(timezone.utc)
        except Exception:
            pass

        # 2. RFC 2822
        try:
            parsed_dt = email.utils.parsedate_to_datetime(clean_str)
            if parsed_dt:
                if parsed_dt.tzinfo is None:
                    return parsed_dt.replace(tzinfo=timezone.utc)
                return parsed_dt.astimezone(timezone.utc)
        except Exception:
            pass

        return None

    def _clean_message_html(self, text_tag: Tag) -> str:
        """Extract clean formatted text from Telegram message widget HTML.

        Args:
            text_tag: BeautifulSoup Tag element containing message markup.

        Returns:
            str: Clean text representation.
        """
        # Replace <br> and paragraph breaks with newlines
        for br in text_tag.find_all(["br", "p"]):
            br.replace_with("\n" + br.text)

        raw_text = text_tag.get_text(separator=" ")
        # Unescape HTML entities
        raw_text = html.unescape(raw_text)

        # Normalize redundant spaces and blank lines
        raw_text = re.sub(r"[ \t]+", " ", raw_text)
        raw_text = re.sub(r"\n\s*\n\s*\n+", "\n\n", raw_text)
        return raw_text.strip()

    def _parse_web_preview_html(self, html_content: str, channel_name: str) -> List[ExtractedItem]:
        """Parse HTML from Telegram public web preview (t.me/s/{channel}).

        Args:
            html_content: Raw HTML body from t.me/s/{channel}.
            channel_name: Normalized channel handle.

        Returns:
            List[ExtractedItem]: Extracted items meeting quality criteria.
        """
        soup = BeautifulSoup(html_content, "html.parser")
        message_divs = soup.find_all(
            "div",
            class_=lambda c: c and ("tgme_widget_message" in c.split()),
        )

        extracted_items: List[ExtractedItem] = []

        for msg_div in message_divs:
            try:
                # 1. Extract message body text
                text_tag = msg_div.find(
                    "div",
                    class_=lambda c: c and ("tgme_widget_message_text" in c.split() or "js-message_text" in c.split()),
                )
                if not text_tag:
                    continue

                cleaned_text = self._clean_message_html(text_tag)

                # Skip messages shorter than MIN_TEXT_LENGTH
                min_len = getattr(settings, "MIN_TEXT_LENGTH", 100)
                if len(cleaned_text) < min_len:
                    continue

                # 2. Extract message URL
                msg_url = f"https://t.me/{channel_name}"
                date_anchor = msg_div.find("a", class_=lambda c: c and "tgme_widget_message_date" in c.split())
                if date_anchor and date_anchor.get("href"):
                    msg_url = date_anchor["href"].strip()
                else:
                    data_post = msg_div.get("data-post")
                    if data_post:
                        msg_url = f"https://t.me/{data_post.strip()}"

                # 3. Extract publication date
                published_at = None
                time_tag = msg_div.find("time")
                if time_tag and time_tag.get("datetime"):
                    published_at = self._parse_datetime(time_tag["datetime"])

                # 4. Extract author / channel title
                author = channel_name
                owner_tag = msg_div.find(
                    "div",
                    class_=lambda c: c and "tgme_widget_message_owner_name" in c.split(),
                )
                if owner_tag and owner_tag.get_text(strip=True):
                    author = owner_tag.get_text(strip=True)

                # 5. Build ExtractedItem
                title = self._generate_title(cleaned_text)
                extracted_items.append(
                    ExtractedItem(
                        title=title,
                        text=cleaned_text,
                        url=msg_url,
                        published_at=published_at,
                        author=author,
                        source_type="telegram",
                        extra={
                            "channel": channel_name,
                            "extraction_mode": "web_preview",
                        },
                    )
                )
            except Exception as item_err:
                self.logger.debug("Skipping malformed web preview post: %s", item_err)
                continue

        return extracted_items

    async def _extract_telethon(self, channel_name: str) -> List[ExtractedItem]:
        """Extract channel messages using Telethon client with persisted SQLite session.

        Args:
            channel_name: Normalized channel handle.

        Returns:
            List[ExtractedItem]: Extracted items or empty list if unconfigured/unauthorized.
        """
        api_id = getattr(settings, "TG_API_ID", None)
        api_hash = getattr(settings, "TG_API_HASH", "")
        session_path = getattr(settings, "TG_SESSION_PATH", "data/trendscanner.session")

        if not api_id or not api_hash:
            self.logger.debug("Telethon credentials (TG_API_ID / TG_API_HASH) not configured.")
            return []

        try:
            from telethon import TelegramClient
        except ImportError:
            self.logger.warning("Telethon library not installed; cannot use Telethon extractor.")
            return []

        client = None
        try:
            client = TelegramClient(
                session_path,
                api_id,
                api_hash,
                timeout=self.timeout,
            )
            self.logger.debug("Connecting Telethon client for channel '%s'...", channel_name)
            await asyncio.wait_for(client.connect(), timeout=self.TELETHON_CONNECT_TIMEOUT)

            if not await client.is_user_authorized():
                self.logger.warning(
                    "TelegramClient session '%s' is not authorized. Run login_telegram.py to authenticate.",
                    session_path,
                )
                return []

            min_len = getattr(settings, "MIN_TEXT_LENGTH", 100)
            items: List[ExtractedItem] = []

            async for msg in client.iter_messages(channel_name, limit=self.limit):
                if not msg:
                    continue

                # Extract text
                text = getattr(msg, "text", "") or getattr(msg, "message", "") or getattr(msg, "raw_text", "") or ""
                text = text.strip()
                if len(text) < min_len:
                    continue

                msg_id = getattr(msg, "id", None)
                msg_url = f"https://t.me/{channel_name}/{msg_id}" if msg_id else f"https://t.me/{channel_name}"

                # Date
                msg_date = getattr(msg, "date", None)
                if msg_date and msg_date.tzinfo is None:
                    published_at = msg_date.replace(tzinfo=timezone.utc)
                elif msg_date:
                    published_at = msg_date.astimezone(timezone.utc)
                else:
                    published_at = None

                # Author
                author = None
                post_author = getattr(msg, "post_author", None)
                if post_author:
                    author = post_author
                elif hasattr(msg, "sender") and msg.sender:
                    sender_user = getattr(msg.sender, "username", None)
                    sender_fn = getattr(msg.sender, "first_name", None)
                    author = sender_user or sender_fn or channel_name
                else:
                    author = channel_name

                # Title
                title = self._generate_title(text)

                # Extra metadata
                extra: Dict[str, Any] = {
                    "channel": channel_name,
                    "message_id": msg_id,
                    "views": getattr(msg, "views", None),
                    "forwards": getattr(msg, "forwards", None),
                    "extraction_mode": "telethon",
                }

                items.append(
                    ExtractedItem(
                        title=title,
                        text=text,
                        url=msg_url,
                        published_at=published_at,
                        author=author,
                        source_type="telegram",
                        extra=extra,
                    )
                )

            self.logger.info("Telethon extracted %d items from Telegram channel '%s'", len(items), channel_name)
            return items

        except Exception as telethon_err:
            self.logger.warning("Telethon extraction failed for channel '%s': %s", channel_name, telethon_err)
            return []
        finally:
            if client is not None:
                try:
                    if hasattr(client, "is_connected") and client.is_connected():
                        await client.disconnect()
                except Exception as disconnect_err:
                    self.logger.debug("Error disconnecting Telethon client: %s", disconnect_err)

    async def _extract_web_preview(self, channel_name: str) -> List[ExtractedItem]:
        """Scrape messages from public Telegram web preview (https://t.me/s/{channel}).

        Args:
            channel_name: Normalized channel handle.

        Returns:
            List[ExtractedItem]: Extracted items or empty list.
        """
        web_url = f"https://t.me/s/{channel_name}"
        headers = {
            "User-Agent": self.user_agent,
            "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
            "Accept-Language": "en-US,en;q=0.9,ru;q=0.8",
        }

        try:
            async with httpx.AsyncClient(
                timeout=self.timeout,
                follow_redirects=True,
                headers=headers,
            ) as http_client:
                response = await http_client.get(web_url)
                response.raise_for_status()
                html_content = response.text

            items = self._parse_web_preview_html(html_content, channel_name)
            self.logger.info(
                "Telegram web preview extracted %d items from '%s'",
                len(items),
                channel_name,
            )
            return items

        except httpx.HTTPStatusError as http_err:
            self.logger.warning(
                "HTTP %s error fetching Telegram web preview for '%s': %s",
                http_err.response.status_code,
                channel_name,
                http_err,
            )
            return []
        except httpx.TimeoutException as timeout_err:
            self.logger.warning(
                "Timeout (%ss) fetching Telegram web preview for '%s': %s",
                self.timeout,
                channel_name,
                timeout_err,
            )
            return []
        except httpx.RequestError as req_err:
            self.logger.warning(
                "Network error fetching Telegram web preview for '%s': %s",
                channel_name,
                req_err,
            )
            return []
        except Exception as err:
            self.logger.error(
                "Unexpected error in Telegram web preview for '%s': %s",
                channel_name,
                err,
                exc_info=True,
            )
            return []

    async def extract(self, url: str) -> List[ExtractedItem]:
        """Extract content from Telegram channel via Telethon with fallback to Web Preview.

        Args:
            url: Channel URL or @username or clean handle.

        Returns:
            List[ExtractedItem]: List of extracted items. Returns empty list on failure.
        """
        if not url or not url.strip():
            self.logger.warning("Empty URL/handle provided to TelegramExtractor.")
            return []

        channel_name = self._normalize_channel_name(url)
        if not channel_name:
            self.logger.warning("Could not normalize channel name from '%s'", url)
            return []

        # 1. Try Telethon first (if credentials and session are available)
        try:
            items = await self._extract_telethon(channel_name)
            if items:
                return items
        except Exception as telethon_err:
            self.logger.warning(
                "Telethon extraction error on channel '%s' (%s). Falling back to Web Preview.",
                channel_name,
                telethon_err,
            )

        # 2. Robust fallback to public web preview (t.me/s/{channel})
        try:
            self.logger.debug("Attempting Web Preview fallback for channel '%s'...", channel_name)
            return await self._extract_web_preview(channel_name)
        except Exception as web_err:
            self.logger.error(
                "All extraction strategies failed for Telegram channel '%s': %s",
                channel_name,
                web_err,
                exc_info=True,
            )
            return []
