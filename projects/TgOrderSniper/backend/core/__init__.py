"""TgOrderSniper Core Package.

Configuration management and deduplication cache.
"""

from backend.core.config import Settings, get_settings, settings
from backend.core.dedup import MessageDedup

__all__ = ["Settings", "get_settings", "settings", "MessageDedup"]
