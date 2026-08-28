"""Unit tests for TrendScanner application settings and configuration (Stage 1)."""

import os
from pathlib import Path
import pytest
from app.core.settings import Settings, settings


def test_settings_default_values(monkeypatch):
    """Verify that Settings initializes with expected default configuration values."""
    # Ensure clean environment without accidental overrides
    monkeypatch.delenv("PROJECT_NAME", raising=False)
    monkeypatch.delenv("API_V1_PREFIX", raising=False)
    monkeypatch.delenv("DEBUG", raising=False)
    monkeypatch.delenv("APP_HOST", raising=False)
    monkeypatch.delenv("APP_PORT", raising=False)
    monkeypatch.delenv("DATABASE_PATH", raising=False)
    monkeypatch.delenv("GROQ_API_KEY", raising=False)
    monkeypatch.delenv("GROQ_MODEL", raising=False)
    monkeypatch.delenv("GROQ_MAX_RETRIES", raising=False)
    monkeypatch.delenv("GROQ_RETRY_DELAY_SECONDS", raising=False)
    monkeypatch.delenv("SCAN_INTERVAL_MINUTES", raising=False)
    monkeypatch.delenv("BATCH_SIZE", raising=False)
    monkeypatch.delenv("MIN_TEXT_LENGTH", raising=False)
    monkeypatch.delenv("CORS_ORIGINS", raising=False)

    app_settings = Settings(_env_file=None)

    assert app_settings.PROJECT_NAME == "TrendScanner"
    assert app_settings.API_V1_PREFIX == "/api"
    assert app_settings.DEBUG is True
    assert app_settings.APP_HOST == "0.0.0.0"
    assert app_settings.APP_PORT == 8000
    assert app_settings.GROQ_API_KEY == ""
    assert app_settings.GROQ_MODEL in ["openai/gpt-oss-20b", "llama-3.1-8b-instant"]
    assert app_settings.GROQ_MAX_RETRIES == 3
    assert app_settings.GROQ_RETRY_DELAY_SECONDS == 2.0
    assert app_settings.SCAN_INTERVAL_MINUTES == 60
    assert app_settings.BATCH_SIZE == 5
    assert app_settings.MIN_TEXT_LENGTH == 100
    assert app_settings.CORS_ORIGINS == ["*"]


def test_database_path_relative_resolution(tmp_path, monkeypatch):
    """Verify that relative DATABASE_PATH is resolved to absolute and directory is created."""
    monkeypatch.chdir(tmp_path)
    relative_path = "nested_dir/test_data/app.db"

    app_settings = Settings(DATABASE_PATH=relative_path, _env_file=None)
    resolved_path = Path(app_settings.DATABASE_PATH)

    assert resolved_path.is_absolute()
    assert resolved_path.name == "app.db"
    assert resolved_path.parent.exists()
    assert resolved_path.parent.is_dir()


def test_database_path_absolute_resolution(tmp_path):
    """Verify that absolute DATABASE_PATH is properly preserved and parent directory is created."""
    custom_dir = tmp_path / "custom_sqlite_dir"
    custom_db_file = custom_dir / "custom_test.db"

    assert not custom_dir.exists()

    app_settings = Settings(DATABASE_PATH=str(custom_db_file), _env_file=None)
    resolved_path = Path(app_settings.DATABASE_PATH)

    assert resolved_path.is_absolute()
    assert str(resolved_path) == str(custom_db_file)
    assert custom_dir.exists()
    assert custom_dir.is_dir()


def test_settings_environment_variable_overrides(tmp_path, monkeypatch):
    """Verify that Settings respects environment variable overrides for all major fields."""
    custom_db_path = str(tmp_path / "env_override.db")

    monkeypatch.setenv("PROJECT_NAME", "CustomTrendScanner")
    monkeypatch.setenv("API_V1_PREFIX", "/api/v2")
    monkeypatch.setenv("DEBUG", "false")
    monkeypatch.setenv("APP_HOST", "127.0.0.1")
    monkeypatch.setenv("APP_PORT", "9050")
    monkeypatch.setenv("DATABASE_PATH", custom_db_path)
    monkeypatch.setenv("GROQ_API_KEY", "gsk_test_api_key_12345")
    monkeypatch.setenv("GROQ_MODEL", "llama-3.3-70b-versatile")
    monkeypatch.setenv("GROQ_MAX_RETRIES", "5")
    monkeypatch.setenv("GROQ_RETRY_DELAY_SECONDS", "3.5")
    monkeypatch.setenv("SCAN_INTERVAL_MINUTES", "15")
    monkeypatch.setenv("BATCH_SIZE", "12")
    monkeypatch.setenv("MIN_TEXT_LENGTH", "150")
    monkeypatch.setenv("CORS_ORIGINS", '["http://localhost:3000","http://localhost:5173"]')

    app_settings = Settings(_env_file=None)

    assert app_settings.PROJECT_NAME == "CustomTrendScanner"
    assert app_settings.API_V1_PREFIX == "/api/v2"
    assert app_settings.DEBUG is False
    assert app_settings.APP_HOST == "127.0.0.1"
    assert app_settings.APP_PORT == 9050
    assert app_settings.DATABASE_PATH == custom_db_path
    assert app_settings.GROQ_API_KEY == "gsk_test_api_key_12345"
    assert app_settings.GROQ_MODEL == "llama-3.3-70b-versatile"
    assert app_settings.GROQ_MAX_RETRIES == 5
    assert app_settings.GROQ_RETRY_DELAY_SECONDS == 3.5
    assert app_settings.SCAN_INTERVAL_MINUTES == 15
    assert app_settings.BATCH_SIZE == 12
    assert app_settings.MIN_TEXT_LENGTH == 150
    assert app_settings.CORS_ORIGINS == [
        "http://localhost:3000",
        "http://localhost:5173",
    ]


def test_singleton_settings_instance():
    """Verify that the module-level settings instance is instantiated properly."""
    assert isinstance(settings, Settings)
    assert settings.PROJECT_NAME == "TrendScanner"
    assert Path(settings.DATABASE_PATH).is_absolute()
