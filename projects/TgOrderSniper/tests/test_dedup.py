"""Unit tests for MessageDedup cache."""

import time
import pytest
from backend.core.dedup import MessageDedup


def test_dedup_by_message_id():
    """Verifies deduplication by chat_id and message_id."""
    dedup = MessageDedup(max_size=100, ttl_seconds=3600)

    # First encounter
    assert dedup.is_duplicate(chat_id=1001, message_id=42) is False

    # Second encounter should be marked as duplicate
    assert dedup.is_duplicate(chat_id=1001, message_id=42) is True

    # Different message in same chat
    assert dedup.is_duplicate(chat_id=1001, message_id=43) is False

    # Same message ID in different chat
    assert dedup.is_duplicate(chat_id=1002, message_id=42) is False


def test_dedup_by_text_hash():
    """Verifies cross-channel deduplication by identical text content."""
    dedup = MessageDedup(max_size=100, ttl_seconds=3600)

    text = "Требуется опытный разработчик Python для создания Telegram-бота под ключ."

    # Post in Channel A
    assert dedup.is_duplicate(chat_id="@channel_a", message_id=1, text=text) is False

    # Identical post in Channel B should be caught by text hash
    assert dedup.is_duplicate(chat_id="@channel_b", message_id=5, text=text) is True

    # Same post with minor whitespace differences and casing
    variant_text = "   требуется   опытный разработчик Python для создания Telegram-бота под ключ. \n\n"
    assert dedup.is_duplicate(chat_id="@channel_c", message_id=10, text=variant_text) is True


def test_short_text_does_not_dedup_across_chats():
    """Ensures short generic phrases do not trigger cross-chat duplication."""
    dedup = MessageDedup(max_size=100, ttl_seconds=3600)

    short_msg = "Привет!"
    assert dedup.is_duplicate(chat_id="@chat1", message_id=1, text=short_msg) is False
    assert dedup.is_duplicate(chat_id="@chat2", message_id=2, text=short_msg) is False


def test_lru_eviction():
    """Verifies that oldest items are evicted when max_size is exceeded."""
    dedup = MessageDedup(max_size=3, ttl_seconds=3600)

    assert dedup.is_duplicate(chat_id=1, message_id=1) is False
    assert dedup.is_duplicate(chat_id=1, message_id=2) is False
    assert dedup.is_duplicate(chat_id=1, message_id=3) is False
    assert len(dedup) == 3

    # Adding a 4th entry should evict the 1st entry (chat_id=1, message_id=1)
    assert dedup.is_duplicate(chat_id=1, message_id=4) is False
    assert len(dedup) == 3

    # Now message 1 should be treated as fresh because it was evicted
    assert dedup.is_duplicate(chat_id=1, message_id=1) is False


def test_ttl_expiration():
    """Verifies that expired items are discarded."""
    dedup = MessageDedup(max_size=10, ttl_seconds=1)

    assert dedup.is_duplicate(chat_id=1, message_id=100) is False
    assert dedup.is_duplicate(chat_id=1, message_id=100) is True

    # Wait for TTL to expire
    time.sleep(1.1)

    # Should no longer be recognized as duplicate
    assert dedup.is_duplicate(chat_id=1, message_id=100) is False


@pytest.mark.asyncio
async def test_async_dedup():
    """Checks async wrapper compatibility."""
    dedup = MessageDedup()
    assert await dedup.is_duplicate_async(chat_id=10, message_id=20) is False
    assert await dedup.is_duplicate_async(chat_id=10, message_id=20) is True


def test_clear_and_stats():
    """Checks cache clear and stats telemetry."""
    dedup = MessageDedup()
    dedup.is_duplicate(chat_id=1, message_id=1)
    dedup.is_duplicate(chat_id=1, message_id=1)

    stats = dedup.get_stats()
    assert stats["total_checked"] == 2
    assert stats["duplicates_found"] == 1
    assert stats["current_size"] == 1

    dedup.clear()
    assert len(dedup) == 0
