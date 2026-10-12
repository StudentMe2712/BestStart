"""In-memory Message Deduplication Cache.

Provides LRU + TTL ring buffer deduplication for Telegram messages
by unique message coordinates (chat_id:message_id) and content hash.
"""

from collections import OrderedDict
import hashlib
import logging
import re
import threading
import time
from typing import Any, Union

logger = logging.getLogger(__name__)


class MessageDedup:
    """In-memory LRU + TTL cache for message deduplication.

    Guarantees thread-safety and compatibility with async event loops.
    Deduplicates both by message ID coordinates (chat_id:message_id)
    and by normalized text hash (to detect identical cross-posted orders).
    """

    MIN_TEXT_HASH_LENGTH: int = 15

    def __init__(
        self,
        max_size: int = 5000,
        ttl_seconds: int = 86400,
    ) -> None:
        """Initializes deduplication cache.

        Args:
            max_size: Maximum entries in the LRU cache.
            ttl_seconds: Expiration time in seconds (default: 24h).
        """
        self.max_size = max_size
        self.ttl_seconds = ttl_seconds
        self._cache: OrderedDict[str, float] = OrderedDict()
        self._lock = threading.RLock()

        # Telemetry stats
        self._total_checked: int = 0
        self._duplicates_found: int = 0
        self._evictions: int = 0

    @staticmethod
    def normalize_text(text: str) -> str:
        """Normalizes text by lowercasing and collapsing whitespace.

        Args:
            text: Raw message text.

        Returns:
            Normalized single-spaced lowercase text.
        """
        if not text:
            return ""
        # Collapse multiple spaces, tabs, newlines into single spaces
        return re.sub(r"\s+", " ", text.strip().lower())

    @classmethod
    def compute_text_hash(cls, text: str) -> str | None:
        """Computes SHA-256 hash of normalized text if long enough.

        Args:
            text: Message text.

        Returns:
            Hex string hash or None if text is too short.
        """
        normalized = cls.normalize_text(text)
        if len(normalized) < cls.MIN_TEXT_HASH_LENGTH:
            return None
        return hashlib.sha256(normalized.encode("utf-8")).hexdigest()

    def _prune_expired(self, current_time: float) -> None:
        """Removes expired entries from the oldest end of the cache.

        Should be called within self._lock.
        """
        keys_to_remove = []
        for key, timestamp in self._cache.items():
            if current_time - timestamp > self.ttl_seconds:
                keys_to_remove.append(key)
            else:
                # Since OrderedDict retains insertion/access order,
                # once we hit a non-expired item, older ones have been checked
                break

        for key in keys_to_remove:
            self._cache.pop(key, None)
            self._evictions += 1

    def is_duplicate(
        self,
        chat_id: Union[int, str],
        message_id: int,
        text: str = "",
    ) -> bool:
        """Checks if a message has already been processed.

        If not seen, records the message in the cache and returns False.
        If already seen (by chat_id:message_id OR text content hash), returns True.

        Args:
            chat_id: Telegram chat or channel ID / username.
            message_id: Message ID inside that chat.
            text: Optional message text for cross-chat content deduplication.

        Returns:
            True if message is a duplicate, False if newly recorded.
        """
        now = time.monotonic()
        id_key = f"msg:{chat_id}:{message_id}"
        text_hash = self.compute_text_hash(text)
        text_key = f"hash:{text_hash}" if text_hash else None

        with self._lock:
            self._total_checked += 1
            self._prune_expired(now)

            # 1. Check ID key duplicate
            if id_key in self._cache:
                timestamp = self._cache[id_key]
                if now - timestamp <= self.ttl_seconds:
                    self._cache.move_to_end(id_key)
                    self._duplicates_found += 1
                    logger.debug(f"[Dedup] Duplicate found by message ID: {id_key}")
                    return True
                # Expired
                self._cache.pop(id_key, None)

            # 2. Check Text Hash duplicate (e.g. cross-posted identical text across channels)
            if text_key and text_key in self._cache:
                timestamp = self._cache[text_key]
                if now - timestamp <= self.ttl_seconds:
                    self._cache.move_to_end(text_key)
                    self._duplicates_found += 1
                    logger.debug(
                        f"[Dedup] Duplicate found by text hash: {text_key} (chat: {chat_id}, msg: {message_id})"
                    )
                    return True
                # Expired
                self._cache.pop(text_key, None)

            # 3. Not duplicate -> Save both keys into cache
            self._cache[id_key] = now
            self._cache.move_to_end(id_key)

            if text_key:
                self._cache[text_key] = now
                self._cache.move_to_end(text_key)

            # 4. Enforce max_size LRU eviction
            while len(self._cache) > self.max_size:
                evicted_key, _ = self._cache.popitem(last=False)
                self._evictions += 1
                logger.debug(f"[Dedup] LRU eviction of key: {evicted_key}")

            return False

    async def is_duplicate_async(
        self,
        chat_id: Union[int, str],
        message_id: int,
        text: str = "",
    ) -> bool:
        """Async-friendly wrapper for is_duplicate."""
        return self.is_duplicate(chat_id=chat_id, message_id=message_id, text=text)

    def clear(self) -> None:
        """Clears all entries from the deduplication cache."""
        with self._lock:
            self._cache.clear()
            logger.info("[Dedup] Cache cleared.")

    def get_stats(self) -> dict[str, Any]:
        """Returns statistics of cache operations."""
        with self._lock:
            return {
                "current_size": len(self._cache),
                "max_size": self.max_size,
                "ttl_seconds": self.ttl_seconds,
                "total_checked": self._total_checked,
                "duplicates_found": self._duplicates_found,
                "evictions": self._evictions,
            }

    def __len__(self) -> int:
        with self._lock:
            return len(self._cache)
