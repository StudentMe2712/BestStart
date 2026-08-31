"""Configuration settings for KzFlightSniper using Pydantic Settings."""

import os
from functools import lru_cache
from typing import Optional
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Application settings loaded from environment variables and .env file."""

    # Telegram Bot Token
    BOT_TOKEN: str = "placeholder_token"

    # SQLite Database Path
    DATABASE_PATH: str = "data/sniper.db"

    # FastAPI Application Server Port
    APP_PORT: int = 8000

    # Periodic Flight Check Interval in Seconds (default: 300 = 5 minutes)
    CHECK_INTERVAL_SECONDS: int = 300

    # Playwright Headless Mode
    HEADLESS: bool = True

    # Environment Name (development, testing, production)
    ENVIRONMENT: str = "production"

    # Logging Level
    LOG_LEVEL: str = "INFO"

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    @property
    def is_bot_token_configured(self) -> bool:
        """Check if a real Telegram Bot token has been provided."""
        token = self.BOT_TOKEN.strip()
        return bool(
            token
            and token != "placeholder_token"
            and not token.startswith("your_")
            and len(token) > 15
        )


@lru_cache()
def get_settings() -> Settings:
    """Return a cached singleton instance of Settings."""
    return Settings()
