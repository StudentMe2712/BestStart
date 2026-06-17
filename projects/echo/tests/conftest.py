"""Shared test fixtures."""
from __future__ import annotations

import pytest

from app import db
from app.config import Settings


def make_settings(**overrides) -> Settings:
    base = dict(
        bot_token="test-token",
        owner_id=1,
        timezone="UTC",
        db_path="",
        groq_api_key="",
        groq_model="m",
        groq_base_url="http://example.invalid",
        openrouter_api_key="",
        openrouter_model="m",
        openrouter_base_url="http://example.invalid",
        max_per_day=3,
        min_gap_minutes=90,
        quiet_start=23,
        quiet_end=8,
        tick_minutes=20,
        enabled_roles=["friend", "presence"],
        log_level="INFO",
    )
    base.update(overrides)
    return Settings(**base)


@pytest.fixture
def settings() -> Settings:
    return make_settings()


@pytest.fixture
def fresh_db(tmp_path, settings):
    db.init(str(tmp_path / "test.db"))
    db.ensure_user(settings, "2026-06-15T00:00:00+00:00")
    yield
    db.close()
