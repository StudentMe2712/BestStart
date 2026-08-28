"""Core application configuration settings using Pydantic Settings."""

import os
from pathlib import Path
from typing import Any, List, Optional, Union
from pydantic import field_validator
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Application settings schema."""
    
    # Project Info
    PROJECT_NAME: str = "TrendScanner"
    API_V1_PREFIX: str = "/api"
    DEBUG: bool = True
    
    # Server
    APP_HOST: str = "0.0.0.0"
    APP_PORT: int = 8000
    
    # Database
    DATABASE_PATH: str = "data/trendscanner.db"
    
    # Groq LLM API Configuration
    GROQ_API_KEY: str = ""
    GROQ_MODEL: str = "openai/gpt-oss-20b"
    GROQ_MODEL_TRANSLATE: str = "llama-3.1-8b-instant"
    GROQ_MAX_RETRIES: int = 3
    GROQ_RETRY_DELAY_SECONDS: float = 2.0
    
    # Telegram Client Configuration (Telethon)
    TG_API_ID: Optional[int] = None
    TG_API_HASH: str = ""
    TG_PHONE: str = ""
    TG_SESSION_PATH: str = "data/trendscanner.session"
    
    # Telegram Push Notification Bot
    TG_BOT_TOKEN: str = ""
    TG_CHAT_ID: str = ""
    
    # Ingestion & Workers
    SCAN_INTERVAL_MINUTES: int = 60
    BATCH_SIZE: int = 5
    MIN_TEXT_LENGTH: int = 100
    
    # Obsidian Vault Directory
    VAULT_DIR: str = "/app/vault"

    # CORS
    CORS_ORIGINS: List[str] = ["*"]
    
    @field_validator("TG_API_ID", mode="before")
    @classmethod
    def parse_tg_api_id(cls, v: Any) -> Optional[int]:
        """Convert empty string to None or parse integer."""
        if v is None or v == "":
            return None
        try:
            return int(v)
        except (ValueError, TypeError):
            return None

    @field_validator("DATABASE_PATH", "TG_SESSION_PATH")
    @classmethod
    def resolve_file_path(cls, v: str) -> str:
        """Resolve file path and ensure directory existence."""
        path = Path(v)
        if not path.is_absolute():
            path = Path.cwd() / path
        path.parent.mkdir(parents=True, exist_ok=True)
        return str(path)

    @field_validator("VAULT_DIR", mode="before")
    @classmethod
    def resolve_vault_dir(cls, v: Any) -> str:
        """Resolve Obsidian Vault directory path with fallback for local dev."""
        if v is None or v == "":
            v = "/app/vault"
        val_str = str(v)
        if val_str == "/app/vault" and not os.path.exists("/app") and not os.environ.get("RUNNING_IN_DOCKER"):
            local_vault = Path(__file__).resolve().parents[3] / "TrendScanner_Vault"
            return str(local_vault)
        return val_str

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=False,
        extra="ignore"
    )


settings = Settings()
