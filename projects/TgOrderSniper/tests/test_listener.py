"""Unit tests for TelegramListener client."""

from backend.core.config import Settings
from backend.listener.client import TelegramListener


def test_listener_session_initialization():
    """Checks session initialization for both file and StringSession modes."""
    # 1. Unconfigured state (defaults without API credentials)
    cfg_unconf = Settings(TG_SESSION_NAME="custom_session", TG_STRING_SESSION=None)
    listener_unconf = TelegramListener(cfg_unconf)
    assert listener_unconf.settings.TG_SESSION_NAME == "custom_session"
    assert listener_unconf.client is None
    assert listener_unconf.is_connected is False
    assert listener_unconf.is_running is False

    # 2. Configured file session
    cfg_file = Settings(
        TG_API_ID=123456,
        TG_API_HASH="0123456789abcdef0123456789abcdef",
        TG_SESSION_NAME="custom_session",
    )
    listener_file = TelegramListener(cfg_file)
    assert listener_file.client is not None
    assert listener_file.is_connected is False

    # 3. Configured StringSession (valid encoded format)
    import struct
    import ipaddress
    from telethon.sessions import StringSession
    from telethon.sessions.string import _STRUCT_PREFORMAT, CURRENT_VERSION

    ip_packed = ipaddress.ip_address("149.154.167.50").packed
    raw = struct.pack(_STRUCT_PREFORMAT.format(len(ip_packed)), 2, ip_packed, 443, b"\x00" * 256)
    valid_session_str = CURRENT_VERSION + StringSession.encode(raw)

    cfg_str = Settings(
        TG_API_ID=123456,
        TG_API_HASH="0123456789abcdef0123456789abcdef",
        TG_STRING_SESSION=valid_session_str,
    )
    listener_str = TelegramListener(cfg_str)
    assert listener_str.settings.TG_STRING_SESSION == valid_session_str
    assert listener_str.client is not None

    # 4. Corrupted StringSession should fallback gracefully without crashing
    cfg_bad_str = Settings(
        TG_API_ID=123456,
        TG_API_HASH="0123456789abcdef0123456789abcdef",
        TG_STRING_SESSION="corrupted_not_base64",
    )
    listener_bad = TelegramListener(cfg_bad_str)
    assert listener_bad.client is not None


def test_build_message_url():
    """Checks direct message URL formatting for various chat formats."""
    # Chat with public username
    class MockChat:
        username = "python_freelance"

    url_public = TelegramListener.build_message_url(MockChat(), -1001234567, 42)
    assert url_public == "https://t.me/python_freelance/42"

    # Private supergroup with -100 prefix
    url_private = TelegramListener.build_message_url(None, -1001987654321, 105)
    assert url_private == "https://t.me/c/1987654321/105"

    # String chat ID with @
    url_at = TelegramListener.build_message_url(None, "@python_orders", 50)
    assert url_at == "https://t.me/python_orders/50"

    # String chat ID with -100
    url_str_num = TelegramListener.build_message_url(None, "-1001555666777", 88)
    assert url_str_num == "https://t.me/c/1555666777/88"
