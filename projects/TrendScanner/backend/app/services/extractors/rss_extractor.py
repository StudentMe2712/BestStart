"""RSS and Atom feed extractor for TrendScanner (Stage 2)."""

import email.utils
import html
import re
from datetime import datetime, timezone
from typing import List, Optional
from urllib.parse import urljoin

import httpx
from bs4 import BeautifulSoup, Tag

from app.services.extractors.base import BaseExtractor, ExtractedItem


class RSSExtractor(BaseExtractor):
    """Extractor for standard RSS 2.0, RSS 1.0 (RDF), and Atom feeds."""

    DEFAULT_USER_AGENT: str = "TrendScanner-RSS/1.0 (+https://github.com/BestStart/TrendScanner)"

    def __init__(
        self,
        timeout: float = 15.0,
        user_agent: Optional[str] = None,
    ) -> None:
        """Initialize RSSExtractor."""
        super().__init__(
            timeout=timeout,
            user_agent=user_agent or self.DEFAULT_USER_AGENT,
        )

    def _parse_datetime(self, date_str: Optional[str]) -> Optional[datetime]:
        """Parse various RFC-822 / RFC-2822 / ISO-8601 date string representations into UTC datetime."""
        if not date_str or not date_str.strip():
            return None

        clean_str = date_str.strip()

        # 1. Try RFC 2822 / RFC 822 parsing (standard for RSS pubDate)
        try:
            parsed_dt = email.utils.parsedate_to_datetime(clean_str)
            if parsed_dt:
                if parsed_dt.tzinfo is None:
                    return parsed_dt.replace(tzinfo=timezone.utc)
                return parsed_dt.astimezone(timezone.utc)
        except Exception:
            pass

        # 2. Try ISO 8601 (standard for Atom updated/published)
        try:
            # Replace trailing Z with UTC offset
            iso_str = clean_str.replace("Z", "+00:00")
            parsed_dt = datetime.fromisoformat(iso_str)
            if parsed_dt.tzinfo is None:
                return parsed_dt.replace(tzinfo=timezone.utc)
            return parsed_dt.astimezone(timezone.utc)
        except Exception:
            pass

        # 3. Fallback: try dateutil parser if available
        try:
            from dateutil import parser as dateutil_parser
            parsed_dt = dateutil_parser.parse(clean_str)
            if parsed_dt.tzinfo is None:
                return parsed_dt.replace(tzinfo=timezone.utc)
            return parsed_dt.astimezone(timezone.utc)
        except Exception:
            pass

        return None

    def _clean_html_content(self, raw_html: str) -> str:
        """Strip HTML tags and unescape text while preserving clean structure."""
        if not raw_html or not raw_html.strip():
            return ""

        text = html.unescape(raw_html)
        if "<" in text and ">" in text:
            try:
                soup = BeautifulSoup(text, "html.parser")
                for tag in soup(["script", "style", "head", "title", "meta", "noscript"]):
                    tag.decompose()
                text = soup.get_text(separator="\n")
            except Exception:
                text = re.sub(r"<[^>]+>", " ", text)

        # Normalize redundant spaces and newlines
        text = re.sub(r"[^\S\r\n]+", " ", text)
        text = re.sub(r"\n{3,}", "\n\n", text)
        return text.strip()

    def _extract_link(self, item_tag: Tag, base_url: str) -> str:
        """Extract item link from RSS or Atom XML tags."""
        # Check Atom <link> tags
        link_tags = item_tag.find_all("link")
        if link_tags:
            # Check for rel="alternate" or first with href
            for lt in link_tags:
                href = lt.get("href")
                if href:
                    rel = lt.get("rel")
                    if rel is None or rel == "alternate" or isinstance(rel, list) and "alternate" in rel:
                        return urljoin(base_url, href.strip())

            # If no rel="alternate" matched, check first href
            for lt in link_tags:
                href = lt.get("href")
                if href:
                    return urljoin(base_url, href.strip())

            # Check tag text if <link>http...</link>
            for lt in link_tags:
                text = lt.get_text(strip=True)
                if text.startswith("http://") or text.startswith("https://"):
                    return text

        # Fallback for html.parser where <link>...</link> text is not captured as child
        raw_match = re.search(r"<link(?:\s+[^>]*)?>\s*(https?://[^\s<]+)\s*</link>", str(item_tag), re.IGNORECASE)
        if raw_match:
            return raw_match.group(1).strip()

        # Check RSS <guid> or Atom <id>
        guid_tag = item_tag.find(["guid", "id"])
        if guid_tag:
            guid_text = guid_tag.get_text(strip=True)
            if guid_text.startswith("http://") or guid_text.startswith("https://"):
                return guid_text

        return base_url

    def _extract_author(self, item_tag: Tag) -> Optional[str]:
        """Extract author name from RSS or Atom item."""
        # Check dc:creator or creator
        creator_tag = item_tag.find(["dc:creator", "creator"])
        if creator_tag:
            name = creator_tag.get_text(strip=True)
            if name:
                return name

        # Check author tag
        author_tag = item_tag.find("author")
        if author_tag:
            # Atom format: <author><name>Author</name></author>
            name_tag = author_tag.find("name")
            if name_tag:
                name = name_tag.get_text(strip=True)
                if name:
                    return name
            # RSS format: <author>email (Author Name)</author> or <author>Author Name</author>
            author_text = author_tag.get_text(strip=True)
            if author_text:
                match = re.search(r"\(([^)]+)\)", author_text)
                if match:
                    return match.group(1).strip()
                return author_text

        return None

    def _extract_content(self, item_tag: Tag) -> str:
        """Extract body text or summary from RSS/Atom tags in priority order."""
        # 1. Look for full content: <content:encoded>, <content>
        content_tag = item_tag.find(["content:encoded", "content"])
        if content_tag:
            content_text = self._clean_html_content(content_tag.get_text())
            if content_text:
                return content_text

        # 2. Look for description: <description>
        desc_tag = item_tag.find("description")
        if desc_tag:
            desc_text = self._clean_html_content(desc_tag.get_text())
            if desc_text:
                return desc_text

        # 3. Look for summary: <summary>
        summary_tag = item_tag.find("summary")
        if summary_tag:
            summary_text = self._clean_html_content(summary_tag.get_text())
            if summary_text:
                return summary_text

        return ""

    def _parse_xml_items(self, xml_content: str, base_url: str) -> List[ExtractedItem]:
        """Parse XML string (RSS or Atom) into list of ExtractedItem."""
        # Try parsing with BeautifulSoup xml parser, fallback to html.parser
        try:
            soup = BeautifulSoup(xml_content, "xml")
        except Exception:
            soup = BeautifulSoup(xml_content, "html.parser")

        # Find items (RSS: <item>, Atom: <entry>)
        raw_items = soup.find_all("item")
        if not raw_items:
            raw_items = soup.find_all("entry")

        if not raw_items:
            self.logger.warning("No <item> or <entry> elements found in feed: %s", base_url)
            return []

        extracted_items: List[ExtractedItem] = []

        for item_tag in raw_items:
            try:
                # Title
                title_tag = item_tag.find("title")
                title = title_tag.get_text(strip=True) if title_tag else ""
                title = html.unescape(title)

                # Link
                url = self._extract_link(item_tag, base_url)

                # Author
                author = self._extract_author(item_tag)

                # Published Date
                pub_date_tag = item_tag.find(["pubdate", "pubDate", "published", "updated", "dc:date", "date"])
                pub_date_str = pub_date_tag.get_text(strip=True) if pub_date_tag else None
                published_at = self._parse_datetime(pub_date_str)

                # Text Content
                body_content = self._extract_content(item_tag)

                if body_content and title:
                    text = f"{title}\n\n{body_content}"
                elif body_content:
                    text = body_content
                elif title:
                    text = title
                else:
                    # Skip items without title and text
                    continue

                extracted_items.append(
                    ExtractedItem(
                        title=title or "Untitled",
                        text=text,
                        url=url,
                        published_at=published_at,
                        author=author,
                        source_type="rss",
                    )
                )
            except Exception as item_err:
                self.logger.debug("Skipping malformed feed item: %s", item_err)
                continue

        return extracted_items

    async def extract(self, url: str) -> List[ExtractedItem]:
        """Fetch and parse RSS/Atom feed from the given URL.

        Args:
            url: RSS or Atom feed URL.

        Returns:
            List[ExtractedItem]: List of parsed items, or empty list on error.
        """
        if not url or not url.strip():
            self.logger.warning("Empty URL provided to RSSExtractor.")
            return []

        clean_url = url.strip()
        headers = {
            "User-Agent": self.user_agent,
            "Accept": "application/rss+xml, application/atom+xml, application/xml, text/xml;q=0.9, */*;q=0.8",
            "Accept-Language": "en-US,en;q=0.9",
        }

        try:
            async with httpx.AsyncClient(
                timeout=self.timeout,
                follow_redirects=True,
                headers=headers,
            ) as client:
                response = await client.get(clean_url)
                response.raise_for_status()
                xml_content = response.text

            return self._parse_xml_items(xml_content, clean_url)

        except httpx.HTTPStatusError as http_err:
            self.logger.warning(
                "HTTP %s error fetching RSS feed '%s': %s",
                http_err.response.status_code,
                clean_url,
                http_err,
            )
            return []
        except httpx.TimeoutException as timeout_err:
            self.logger.warning(
                "Timeout (%ss) fetching RSS feed '%s': %s",
                self.timeout,
                clean_url,
                timeout_err,
            )
            return []
        except httpx.RequestError as req_err:
            self.logger.warning(
                "Network request error fetching RSS feed '%s': %s",
                clean_url,
                req_err,
            )
            return []
        except Exception as err:
            self.logger.error(
                "Unexpected error in RSSExtractor for URL '%s': %s",
                clean_url,
                err,
                exc_info=True,
            )
            return []
