"""Unit tests for Settings and configuration management."""

from backend.core.config import Settings


def test_default_settings():
    """Checks default settings values when no environment variables are provided."""
    cfg = Settings(_env_file=None)
    assert cfg.TG_API_ID == 0
    assert cfg.TG_API_HASH == ""
    assert cfg.TG_SESSION_NAME == "order_sniper"
    assert cfg.TG_STRING_SESSION is None
    assert cfg.DEDUP_CACHE_SIZE == 5000
    assert cfg.DEDUP_TTL_SECONDS == 86400
    assert cfg.CHANNELS_TO_WATCH == []
    assert cfg.GROQ_API_KEY == ""
    assert cfg.GROQ_MODEL == "openai/gpt-oss-20b"
    assert cfg.GROQ_FALLBACK_MODEL == "openai/gpt-oss-20b"
    assert cfg.GROQ_REQUEST_DELAY == 1.0
    assert cfg.SEARCH_INTERVAL_MINUTES == 5
    assert len(cfg.SEARCH_QUERIES) > 0
    assert cfg.SEARCH_RATE_LIMIT_DELAY == 2.0



def test_channels_string_parsing():
    """Checks parsing of comma-separated channel string with usernames and numeric IDs."""
    cfg = Settings(
        CHANNELS_TO_WATCH="@channel1, @channel2, -1001234567, 98765432"  # type: ignore[arg-type]
    )
    assert cfg.CHANNELS_TO_WATCH == ["@channel1", "@channel2", -1001234567, 98765432]


def test_channels_list_parsing():
    """Checks parsing when list of channels with mixed strings and ints is provided."""
    cfg = Settings(
        CHANNELS_TO_WATCH=["@channel1", "-1001234567", 55555]  # type: ignore[arg-type]
    )
    assert cfg.CHANNELS_TO_WATCH == ["@channel1", -1001234567, 55555]


def test_channels_empty_or_none():
    """Checks empty strings or None values."""
    cfg1 = Settings(CHANNELS_TO_WATCH="")  # type: ignore[arg-type]
    assert cfg1.CHANNELS_TO_WATCH == []

    cfg2 = Settings(CHANNELS_TO_WATCH=None)  # type: ignore[arg-type]
    assert cfg2.CHANNELS_TO_WATCH == []


def test_target_chat_id_parsing():
    """Checks parsing of target chat ID string as integer or string."""
    cfg_num = Settings(TARGET_CHAT_ID="-1001234567890")  # type: ignore[arg-type]
    assert cfg_num.TARGET_CHAT_ID == -1001234567890

    cfg_str = Settings(TARGET_CHAT_ID="@my_target_channel")  # type: ignore[arg-type]
    assert cfg_str.TARGET_CHAT_ID == "@my_target_channel"


def test_search_queries_parsing():
    """Checks parsing of search queries from string and list."""
    cfg1 = Settings(SEARCH_QUERIES="срочно заказ, нужен бот, 1с доработка")  # type: ignore[arg-type]
    assert cfg1.SEARCH_QUERIES == ["срочно заказ", "нужен бот", "1с доработка"]

    cfg2 = Settings(SEARCH_QUERIES=[" заказ 1 ", "заказ 2"])  # type: ignore[arg-type]
    assert cfg2.SEARCH_QUERIES == ["заказ 1", "заказ 2"]

    cfg3 = Settings(SEARCH_QUERIES="")  # type: ignore[arg-type]
    assert cfg3.SEARCH_QUERIES == []

