"""Configuration loaded from the environment (.env). No secrets live in code."""
from __future__ import annotations

import os
from dataclasses import dataclass

from dotenv import load_dotenv

load_dotenv()


def _int(name: str, default: int) -> int:
    raw = os.getenv(name)
    if raw is None or raw.strip() == "":
        return default
    try:
        return int(raw)
    except ValueError:
        return default


def _csv(name: str, default: list[str]) -> list[str]:
    raw = os.getenv(name)
    if not raw:
        return list(default)
    return [item.strip() for item in raw.split(",") if item.strip()]


@dataclass(frozen=True)
class Settings:
    bot_token: str          # control bot (python-telegram-bot)
    admin_id: int           # the one person allowed to talk to the bot
    api_id: int             # Telethon user account — from my.telegram.org
    api_hash: str
    channels: list[str]     # channels to parse (@username / t.me link / numeric id)
    db_path: str
    session_path: str       # Telethon writes "<session_path>.session"
    parse_default_n: int    # N used when /parse is called without an argument
    feed_limit: int         # how many records /feed shows
    log_level: str


def load_settings() -> Settings:
    return Settings(
        bot_token=os.getenv("TELEGRAM_BOT_TOKEN", ""),
        admin_id=_int("ADMIN_USER_ID", 0),
        api_id=_int("TELETHON_API_ID", 0),
        api_hash=os.getenv("TELETHON_API_HASH", ""),
        channels=_csv("CHANNELS", []),
        db_path=os.getenv("DB_PATH", "data/parsertg.db"),
        session_path=os.getenv("SESSION_PATH", "data/parsertg"),
        parse_default_n=_int("PARSE_DEFAULT_N", 20),
        feed_limit=_int("FEED_LIMIT", 10),
        log_level=os.getenv("LOG_LEVEL", "INFO"),
    )
