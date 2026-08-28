"""Reddit JSON API extractor with Anti-Ban User-Agent rotation and request pacing."""

import asyncio
import random
import urllib.parse
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional

import httpx

from app.services.extractors.base import BaseExtractor, ExtractedItem

# Pool of realistic desktop browser User-Agents for Reddit anti-blocking
DESKTOP_USER_AGENTS = [
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36",
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:123.0) Gecko/20100101 Firefox/123.0",
    "Mozilla/5.0 (Macintosh; Intel Mac OS X 14.3; rv:122.0) Gecko/20100101 Firefox/122.0",
    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Safari/605.1.15",
    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
]


class RedditExtractor(BaseExtractor):
    """Extractor for Reddit subreddits and feeds via Reddit's public JSON API with Anti-Ban safeguards."""

    def __init__(
        self,
        timeout: float = 15.0,
        user_agent: Optional[str] = None,
        include_stickied: bool = False,
        default_limit: int = 25,
        request_delay: float = 3.0,
    ) -> None:
        super().__init__(
            timeout=timeout,
            user_agent=user_agent or DESKTOP_USER_AGENTS[0],
        )
        self.include_stickied = include_stickied
        self.default_limit = default_limit
        self.request_delay = request_delay

    def _get_random_user_agent(self) -> str:
        """Rotate desktop browser user agents to prevent fingerprint blocking."""
        if self.user_agent and self.user_agent not in DESKTOP_USER_AGENTS:
            return self.user_agent
        return random.choice(DESKTOP_USER_AGENTS)

    def _normalize_reddit_url(self, raw_url: str) -> str:
        """Normalize various Reddit URL formats into a clean JSON API endpoint URL."""
        url = raw_url.strip()

        if url.startswith("r/"):
            url = f"https://www.reddit.com/{url}"
        elif not url.startswith("http://") and not url.startswith("https://"):
            url = f"https://{url}"

        parsed = urllib.parse.urlparse(url)
        path = parsed.path.rstrip("/")

        if not path.endswith(".json"):
            segments = path.split("/")
            if segments and segments[-1] in ("hot", "new", "top", "rising", "controversial"):
                path = f"{path}.json"
            else:
                path = f"{path}/hot.json"

        query_params = urllib.parse.parse_qs(parsed.query)
        if "limit" not in query_params:
            query_params["limit"] = [str(self.default_limit)]

        flat_query = {k: v[0] if isinstance(v, list) and len(v) == 1 else v for k, v in query_params.items()}
        new_query = urllib.parse.urlencode(flat_query, doseq=True)

        netloc = parsed.netloc or "www.reddit.com"

        return urllib.parse.urlunparse((
            parsed.scheme or "https",
            netloc,
            path,
            "",
            new_query,
            "",
        ))

    def _parse_reddit_json(self, data: Any, source_url: str) -> List[ExtractedItem]:
        """Parse Reddit JSON response into a list of ExtractedItem."""
        if not isinstance(data, dict):
            self.logger.warning("Unexpected non-dict JSON response from Reddit URL: %s", source_url)
            return []

        listing_data = data.get("data", {})
        children = listing_data.get("children", [])

        if not isinstance(children, list):
            self.logger.warning("No children listing found in Reddit JSON for: %s", source_url)
            return []

        extracted_items: List[ExtractedItem] = []

        for child in children:
            if not isinstance(child, dict):
                continue

            post = child.get("data", {})
            if not isinstance(post, dict):
                continue

            try:
                is_stickied = post.get("stickied", False)
                if is_stickied and not self.include_stickied:
                    continue

                title = post.get("title", "").strip()
                if not title:
                    continue

                selftext = post.get("selftext", "").strip()
                if selftext in ("[removed]", "[deleted]"):
                    selftext = ""

                permalink = post.get("permalink", "")
                item_url = f"https://www.reddit.com{permalink}" if permalink else post.get("url", source_url)

                author = post.get("author")
                if author in ("[deleted]", None):
                    author = None

                created_utc = post.get("created_utc")
                published_at = None
                if created_utc and isinstance(created_utc, (int, float)):
                    try:
                        published_at = datetime.fromtimestamp(created_utc, tz=timezone.utc)
                    except Exception:
                        published_at = None

                external_url = post.get("url_overridden_by_dest")
                if selftext:
                    text = f"{title}\n\n{selftext}"
                elif external_url and not post.get("is_self", False):
                    text = f"{title}\n\nLink: {external_url}"
                else:
                    text = title

                extra_meta: Dict[str, Any] = {
                    "score": post.get("score", 0),
                    "num_comments": post.get("num_comments", 0),
                    "subreddit": post.get("subreddit", ""),
                    "is_self": post.get("is_self", True),
                    "upvote_ratio": post.get("upvote_ratio"),
                }

                extracted_items.append(
                    ExtractedItem(
                        title=title,
                        text=text,
                        url=item_url,
                        published_at=published_at,
                        author=author,
                        source_type="reddit",
                        extra=extra_meta,
                    )
                )
            except Exception as item_err:
                self.logger.debug("Skipping malformed Reddit child item: %s", item_err)
                continue

        return extracted_items

    async def extract(self, url: str) -> List[ExtractedItem]:
        """Fetch and extract posts from a Reddit subreddit with Anti-Ban pacing."""
        if not url or not url.strip():
            self.logger.warning("Empty URL provided to RedditExtractor.")
            return []

        target_url = self._normalize_reddit_url(url)
        ua = self._get_random_user_agent()
        headers = {
            "User-Agent": ua,
            "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,application/json;q=0.8,*/*;q=0.7",
            "Accept-Language": "en-US,en;q=0.9",
        }

        # Pacing delay between calls to protect against IP block
        if self.request_delay > 0:
            await asyncio.sleep(self.request_delay)

        try:
            async with httpx.AsyncClient(
                timeout=self.timeout,
                follow_redirects=True,
                headers=headers,
            ) as client:
                response = await client.get(target_url)

                if response.status_code == 429:
                    self.logger.warning(
                        "Reddit rate limit (HTTP 429) hit for URL: %s",
                        target_url,
                    )
                    return []

                response.raise_for_status()
                json_data = response.json()

            return self._parse_reddit_json(json_data, target_url)

        except Exception as err:
            self.logger.warning("Error fetching Reddit feed '%s': %s", target_url, err)
            return []
