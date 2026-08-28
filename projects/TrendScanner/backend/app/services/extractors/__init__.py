"""TrendScanner data extractors package."""

from typing import Any, Dict, Optional, Type

from app.services.extractors.base import BaseExtractor, ExtractedItem
from app.services.extractors.reddit_extractor import RedditExtractor
from app.services.extractors.rss_extractor import RSSExtractor
from app.services.extractors.advanced_extractor import AdvancedExtractor
from app.services.extractors.telegram_extractor import TelegramExtractor

# AdvancedWebExtractor alias for web scraping / SPA
AdvancedWebExtractor = AdvancedExtractor

EXTRACTOR_REGISTRY: Dict[str, Type[BaseExtractor]] = {
    "rss": RSSExtractor,
    "reddit": RedditExtractor,
    "playwright_spa": AdvancedExtractor,
    "spa": AdvancedExtractor,
    "advanced": AdvancedExtractor,
    "advanced_web": AdvancedWebExtractor,
    "telegram": TelegramExtractor,
    "telegram_channel": TelegramExtractor,
    "telegram_html": TelegramExtractor,
    "auto_discovered": RSSExtractor,
}


def get_extractor(source_type: str, **kwargs: Any) -> Optional[BaseExtractor]:
    """Retrieve an initialized extractor instance by source_type.

    Args:
        source_type: The source type key (e.g. 'rss', 'reddit', 'playwright_spa', 'telegram', 'auto_discovered').
        **kwargs: Additional parameters passed to the extractor constructor.

    Returns:
        Optional[BaseExtractor]: Initialized extractor instance or None if unsupported.
    """
    if not source_type:
        return None
    source_type_clean = source_type.strip().lower()
    if source_type_clean == "auto_discovered":
        return RSSExtractor(**kwargs)
    extractor_cls = EXTRACTOR_REGISTRY.get(source_type_clean)
    if extractor_cls:
        return extractor_cls(**kwargs)
    return None


def get_extractor_for_url(url: str, **kwargs: Any) -> Optional[BaseExtractor]:
    """Inspect a URL and return the best suited extractor.

    - Telegram: t.me, telegram.me -> TelegramExtractor
    - Reddit: reddit.com, r/ -> RedditExtractor
    - RSS / Feeds: urls containing /feed, /rss, .xml, .atom, .rss -> RSSExtractor
    - Web / Others: AdvancedWebExtractor (AdvancedExtractor)
    """
    if not url:
        return None

    url_lower = url.strip().lower()

    if "t.me/" in url_lower or "telegram.me/" in url_lower:
        return TelegramExtractor(**kwargs)
    if "reddit.com" in url_lower or url_lower.startswith("r/"):
        return RedditExtractor(**kwargs)
    if any(pattern in url_lower for pattern in ("/rss", "/feed", ".xml", ".atom", ".rss", "feed/")):
        return RSSExtractor(**kwargs)

    return AdvancedWebExtractor(**kwargs)


__all__ = [
    "BaseExtractor",
    "ExtractedItem",
    "RSSExtractor",
    "RedditExtractor",
    "AdvancedExtractor",
    "AdvancedWebExtractor",
    "TelegramExtractor",
    "EXTRACTOR_REGISTRY",
    "get_extractor",
    "get_extractor_for_url",
]

