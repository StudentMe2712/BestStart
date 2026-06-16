"""Configuration loaded from the environment (.env). No secrets live in code."""
from __future__ import annotations

import os
from dataclasses import dataclass

from dotenv import load_dotenv

load_dotenv()

# Friend / Trainer / Mentor go out on a schedule. Challenger is enabled but reactive
# only (roles.py marks it scheduled=False) — it never auto-sends, only via /now or reaction.
DEFAULT_ROLES = ["friend", "coach", "mentor", "challenger"]


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
        return default
    return [item.strip() for item in raw.split(",") if item.strip()]


@dataclass(frozen=True)
class Settings:
    bot_token: str
    owner_id: int
    timezone: str
    db_path: str
    # generation backends (tried in order: groq -> openrouter -> templates)
    groq_api_key: str
    groq_model: str
    groq_base_url: str
    openrouter_api_key: str
    openrouter_model: str
    openrouter_base_url: str
    # cadence / anti-spam
    max_per_day: int
    min_gap_minutes: int
    quiet_start: int
    quiet_end: int
    tick_minutes: int
    enabled_roles: list[str]
    log_level: str


def load_settings() -> Settings:
    return Settings(
        bot_token=os.getenv("TELEGRAM_BOT_TOKEN", ""),
        owner_id=_int("OWNER_USER_ID", 0),
        timezone=os.getenv("TIMEZONE", "Asia/Almaty"),
        db_path=os.getenv("DB_PATH", "data/echo.db"),
        groq_api_key=os.getenv("GROQ_API_KEY", ""),
        groq_model=os.getenv("GROQ_MODEL", "llama-3.3-70b-versatile"),
        groq_base_url=os.getenv("GROQ_BASE_URL", "https://api.groq.com/openai/v1"),
        openrouter_api_key=os.getenv("OPENROUTER_API_KEY", ""),
        openrouter_model=os.getenv("OPENROUTER_MODEL", "meta-llama/llama-3.3-70b-instruct"),
        openrouter_base_url=os.getenv("OPENROUTER_BASE_URL", "https://openrouter.ai/api/v1"),
        max_per_day=_int("MAX_PER_DAY", 3),
        min_gap_minutes=_int("MIN_GAP_MINUTES", 90),
        quiet_start=_int("QUIET_START", 23),
        quiet_end=_int("QUIET_END", 8),
        tick_minutes=_int("TICK_MINUTES", 20),
        enabled_roles=_csv("ENABLED_ROLES", DEFAULT_ROLES),
        log_level=os.getenv("LOG_LEVEL", "INFO"),
    )
