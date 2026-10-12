"""Configuration module for TgOrderSniper.

Provides Pydantic Settings management for MTProto listener,
filter engine, bot dispatcher, and deduplication cache.
"""

from functools import lru_cache
from typing import Any, Union
import logging

from pydantic import Field, field_validator
from pydantic_settings import BaseSettings, SettingsConfigDict

logger = logging.getLogger(__name__)


class Settings(BaseSettings):
    """TgOrderSniper application configuration.

    Loads configuration from environment variables and `.env` file.
    """

    # Telegram MTProto Client credentials (Userbot)
    TG_API_ID: int = Field(default=0, description="Telegram API ID from my.telegram.org")
    TG_API_HASH: str = Field(default="", description="Telegram API Hash from my.telegram.org")
    TG_SESSION_NAME: str = Field(default="order_sniper", description="Local SQLite session name")
    TG_STRING_SESSION: str | None = Field(
        default=None, description="Optional Telethon StringSession for stateless deployments"
    )

    # Telegram Bot API credentials (Dispatcher)
    TG_BOT_TOKEN: str = Field(
        default="", description="Bot token from @BotFather for sending notifications"
    )
    TARGET_CHAT_ID: Union[int, str] = Field(
        default=0,
        description="Target channel/group ID or username where filtered orders are dispatched",
    )

    # Channels to listen to
    CHANNELS_TO_WATCH: Union[list[Union[str, int]], str] = Field(
        default_factory=list,
        description="List of channels/chats to watch. Parsed from comma-separated string or list",
    )

    # Groq AI Classifier settings
    GROQ_API_KEY: str = Field(default="", description="Groq API key")
    GROQ_MODEL: str = Field(default="openai/gpt-oss-20b", description="Primary Groq model")
    GROQ_FALLBACK_MODEL: str = Field(
        default="llama-3.3-70b-versatile", description="Fallback Groq model"
    )

    # Global Telegram Search settings
    SEARCH_INTERVAL_MINUTES: int = Field(
        default=5, description="Search interval in minutes"
    )
    SEARCH_QUERIES: Union[list[str], str] = Field(
        default_factory=lambda: [
            "срочно заказ",
            "нужен разработчик",
            "требуется написать",
            "доработка 1с",
            "интеграция kaspi",
            "написать бота",
            "спарсить",
        ],
        description="Search queries for global Telegram search",
    )
    SEARCH_RATE_LIMIT_DELAY: float = Field(
        default=2.0, description="Pause between search queries"
    )

    # Deduplication cache settings
    DEDUP_CACHE_SIZE: int = Field(
        default=5000, description="Maximum number of items in deduplication LRU cache"
    )
    DEDUP_TTL_SECONDS: int = Field(
        default=86400, description="Time-to-live for deduplication entries in seconds (24 hours)"
    )

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    @field_validator("CHANNELS_TO_WATCH", mode="before")
    @classmethod
    def parse_channels_to_watch(cls, value: Any) -> list[Union[str, int]]:
        """Parses channels list from string or list input.

        Supports strings formatted as "@channel1, @channel2, -1001234567".
        Automatically converts integer strings into integers.
        """
        if value is None or value == "":
            return []

        if isinstance(value, str):
            raw_items = [item.strip() for item in value.split(",") if item.strip()]
            parsed_channels: list[Union[str, int]] = []
            for item in raw_items:
                try:
                    parsed_channels.append(int(item))
                except ValueError:
                    parsed_channels.append(item)
            return parsed_channels

        if isinstance(value, (list, tuple)):
            parsed_channels = []
            for item in value:
                if isinstance(item, int):
                    parsed_channels.append(item)
                elif isinstance(item, str):
                    clean_str = item.strip()
                    try:
                        parsed_channels.append(int(clean_str))
                    except ValueError:
                        parsed_channels.append(clean_str)
                else:
                    parsed_channels.append(str(item))
            return parsed_channels

        return []

    @field_validator("SEARCH_QUERIES", mode="before")
    @classmethod
    def parse_search_queries(cls, value: Any) -> list[str]:
        """Parses search queries list from comma-separated string or list input."""
        if value is None or value == "":
            return []
        if isinstance(value, str):
            return [item.strip() for item in value.split(",") if item.strip()]
        if isinstance(value, (list, tuple)):
            return [str(item).strip() for item in value if str(item).strip()]
        return []

    @field_validator("TARGET_CHAT_ID", mode="before")
    @classmethod
    def parse_target_chat_id(cls, value: Any) -> Union[int, str]:
        """Parses target chat ID, converting string representation of int if possible."""
        if isinstance(value, str):
            stripped = value.strip()
            try:
                return int(stripped)
            except ValueError:
                return stripped
        return value


@lru_cache
def get_settings() -> Settings:
    """Returns cached Settings instance."""
    return Settings()


# Default global instance
settings: Settings = get_settings()
