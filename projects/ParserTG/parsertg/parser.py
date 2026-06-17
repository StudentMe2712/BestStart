"""The Telethon side: read the next N messages from each configured channel.

A Telegram *bot* cannot pull arbitrary channel history, so parsing runs on a *user*
session (api_id / api_hash + a one-time phone login -> a .session file). For each channel
we keep a cursor (last_message_id) and fetch messages with a higher id, oldest first."""
from __future__ import annotations

import logging
import sqlite3
from dataclasses import dataclass

from telethon import TelegramClient
from telethon.errors import (
    ChannelPrivateError,
    FloodWaitError,
    UsernameInvalidError,
    UsernameNotOccupiedError,
)

from . import db
from .config import Settings

log = logging.getLogger(__name__)


@dataclass
class ChannelResult:
    channel: str           # human label (title if known, else the configured handle)
    saved: int             # how many NEW messages were stored
    error: str | None = None


def build_client(settings: Settings) -> TelegramClient:
    return TelegramClient(settings.session_path, settings.api_id, settings.api_hash)


def message_link(username: str | None, channel_id: int | None, message_id: int) -> str:
    """Public t.me link when the channel has a username, else the private c/ form."""
    if username:
        return f"https://t.me/{username.lstrip('@')}/{message_id}"
    if channel_id is not None:
        return f"https://t.me/c/{channel_id}/{message_id}"
    return ""


def _resolve_target(handle: str) -> str | int:
    """A numeric id must be passed to Telethon as int; @usernames/links stay strings."""
    cleaned = handle.strip()
    if cleaned.lstrip("-").isdigit():
        return int(cleaned)
    return cleaned


async def parse_channels(client: TelegramClient, settings: Settings, n: int) -> list[ChannelResult]:
    """Advance every configured channel by up to N messages. Returns a per-channel summary."""
    results: list[ChannelResult] = []
    for channel in db.get_channels(settings.db_path):
        results.append(await _parse_one(client, settings, channel, n))
    return results


async def _parse_one(
    client: TelegramClient, settings: Settings, channel: sqlite3.Row, n: int
) -> ChannelResult:
    handle = channel["username"]
    try:
        entity = await client.get_entity(_resolve_target(handle))
    except (UsernameNotOccupiedError, UsernameInvalidError, ValueError) as exc:
        log.warning("channel %s not found: %s", handle, exc)
        return ChannelResult(handle, 0, "не найден")
    except ChannelPrivateError:
        log.warning("no access to %s (private)", handle)
        return ChannelResult(handle, 0, "нет доступа (приватный)")
    except FloodWaitError as exc:
        log.warning("flood wait resolving %s: %ss", handle, exc.seconds)
        return ChannelResult(handle, 0, f"flood wait {exc.seconds}s")

    title = getattr(entity, "title", None)
    label = title or handle
    username = getattr(entity, "username", None)
    entity_id = getattr(entity, "id", None)

    rows: list[tuple] = []
    try:
        async for msg in client.iter_messages(
            entity, limit=n, min_id=channel["last_message_id"], reverse=True
        ):
            link = message_link(username, entity_id, msg.id)
            rows.append((msg.id, msg.date.isoformat(), msg.message or "", link))
    except FloodWaitError as exc:
        log.warning("flood wait reading %s: %ss", handle, exc.seconds)
        return ChannelResult(label, 0, f"flood wait {exc.seconds}s")

    saved, max_id = db.save_messages(settings.db_path, channel["id"], rows)
    new_cursor = max(channel["last_message_id"], max_id)
    db.update_channel(settings.db_path, channel["id"], title=title, last_message_id=new_cursor)
    log.info("%s: +%d new (cursor -> %d)", label, saved, new_cursor)
    return ChannelResult(label, saved)
