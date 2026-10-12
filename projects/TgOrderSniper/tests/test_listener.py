"""Unit tests for TelegramListener client."""

import pytest

from backend.core.config import Settings
from backend.listener.client import TelegramListener


def test_listener_session_initialization():
    """Checks session initialization for both file and StringSession modes."""
    # 1. Unconfigured state (defaults without API credentials)
    cfg_unconf = Settings(
        TG_API_ID=0,
        TG_API_HASH="",
        TG_SESSION_NAME="custom_session",
        TG_STRING_SESSION=None,
    )
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


from typing import Any
from unittest.mock import AsyncMock, MagicMock
import pytest
from telethon.tl.types import Channel, Chat, PeerChannel, PeerChat, PeerUser, User

from backend.core.config import Settings
from backend.listener.client import TelegramListener, is_strictly_forbidden_chat


def test_is_strictly_forbidden_chat():
    """Unit tests verifying all Ironclad Privacy Barrier rules."""
    target_chat_id = -1004489991927
    my_id = 987654321

    # 1. Message from target chat (TARGET_CHAT_ID) is blocked
    event_target = MagicMock()
    event_target.chat_id = target_chat_id
    msg_target = MagicMock()
    msg_target.chat_id = target_chat_id
    msg_target.peer_id = PeerChannel(4489991927)
    msg_target.out = False
    assert is_strictly_forbidden_chat(event_target, msg_target, target_chat_id, my_id) is True
    # Test with string target_chat_id
    assert is_strictly_forbidden_chat(event_target, msg_target, str(target_chat_id), my_id) is True

    # 2. Personal dialogue with self / Saved messages ('SoloLevelUp' / Избранное) is blocked
    # 2a. chat_id == my_id
    event_self = MagicMock()
    event_self.chat_id = my_id
    msg_self = MagicMock()
    msg_self.chat_id = my_id
    msg_self.peer_id = PeerUser(my_id)
    msg_self.out = False
    assert is_strictly_forbidden_chat(event_self, msg_self, target_chat_id, my_id) is True
    # 2b. Outgoing message (out == True)
    msg_out = MagicMock()
    msg_out.chat_id = -100999
    msg_out.peer_id = PeerChannel(999)
    msg_out.out = True
    assert is_strictly_forbidden_chat(None, msg_out, target_chat_id, my_id) is True

    # 3. Personal chat 'Сухарев / ЙЙ' (User / PeerUser / is_private) is blocked
    event_user = MagicMock()
    event_user.chat_id = 112233
    event_user.is_private = True
    msg_user = MagicMock()
    msg_user.peer_id = PeerUser(112233)
    msg_user.out = False
    assert is_strictly_forbidden_chat(event_user, msg_user, target_chat_id, my_id) is True

    msg_user_entity = MagicMock()
    msg_user_entity.peer_id = MagicMock(spec=User)
    msg_user_entity.out = False
    assert is_strictly_forbidden_chat(None, msg_user_entity, target_chat_id, my_id) is True

    # 4. Ordinary group (Chat / PeerChat) is blocked
    msg_chat = MagicMock()
    msg_chat.chat_id = -555
    msg_chat.peer_id = PeerChat(555)
    msg_chat.out = False
    assert is_strictly_forbidden_chat(None, msg_chat, target_chat_id, my_id) is True

    msg_chat_entity = MagicMock()
    msg_chat_entity.chat_id = -555
    msg_chat_entity.peer_id = MagicMock(spec=Chat)
    msg_chat_entity.out = False
    assert is_strictly_forbidden_chat(None, msg_chat_entity, target_chat_id, my_id) is True

    # 5. Public channel / supergroup (Channel / PeerChannel) is allowed
    event_valid = MagicMock()
    event_valid.chat_id = -100123456789
    event_valid.is_private = False
    msg_valid = MagicMock()
    msg_valid.chat_id = -100123456789
    msg_valid.peer_id = PeerChannel(123456789)
    msg_valid.out = False
    msg_valid.is_private = False
    assert is_strictly_forbidden_chat(event_valid, msg_valid, target_chat_id, my_id) is False

    # Also with spec=Channel mock
    msg_valid_channel = MagicMock()
    msg_valid_channel.chat_id = -100123456789
    msg_valid_channel.peer_id = MagicMock(spec=Channel)
    msg_valid_channel.out = False
    msg_valid_channel.is_private = False
    assert is_strictly_forbidden_chat(None, msg_valid_channel, target_chat_id, my_id) is False


@pytest.mark.asyncio
async def test_privacy_gatekeeper_filters() -> None:
    """Verifies that Privacy Gatekeeper drops outgoing, private, oversized, and code messages."""
    cfg = Settings(
        TG_API_ID=123456,
        TG_API_HASH="0123456789abcdef0123456789abcdef",
        CHANNELS_TO_WATCH=["@freelance_chat"],
        TARGET_CHAT_ID=-1004489991927,
    )
    listener = TelegramListener(cfg)

    # Capture registered event handler
    captured_handler = None

    def mock_on(builder):
        def decorator(fn):
            nonlocal captured_handler
            captured_handler = fn
            return fn
        return decorator

    listener.client.on = mock_on
    listener.client.is_connected = MagicMock(return_value=True)
    listener.client.connect = AsyncMock()
    listener.client.is_user_authorized = AsyncMock(return_value=True)
    me_mock = MagicMock()
    me_mock.id = 777000
    me_mock.username = "test_user"
    listener.client.get_me = AsyncMock(return_value=me_mock)

    callback_mock = AsyncMock()
    await listener.start(message_handler_callback=callback_mock)

    assert captured_handler is not None
    assert listener.my_id == 777000

    def make_event(
        text: str,
        out: bool = False,
        is_private: bool = False,
        peer_id: Any = None,
        chat_id: int = -100111,
    ):
        event = MagicMock()
        event.chat_id = chat_id
        event.sender_id = 999
        event.is_private = is_private
        event.message = MagicMock()
        event.message.id = 123
        event.message.chat_id = chat_id
        event.message.out = out
        event.message.is_private = is_private
        event.message.text = text
        event.message.raw_text = text
        event.message.peer_id = peer_id if peer_id is not None else PeerChannel(111)
        event.get_chat = AsyncMock(return_value=None)
        event.get_sender = AsyncMock(return_value=None)
        return event

    # 1. Outgoing message -> dropped
    await captured_handler(make_event("Нужен разработчик бота", out=True))
    assert callback_mock.await_count == 0

    # 2. Private 1-on-1 dialogue -> dropped
    await captured_handler(make_event("Нужен разработчик бота", is_private=True))
    assert callback_mock.await_count == 0

    # 3. Message from target chat (TARGET_CHAT_ID) -> dropped
    await captured_handler(make_event("Заказ из целевого чата", chat_id=-1004489991927))
    assert callback_mock.await_count == 0

    # 4. Message from self / Saved Messages (chat_id == my_id) -> dropped
    await captured_handler(make_event("Мой личный чат", chat_id=777000))
    assert callback_mock.await_count == 0

    # 5. Direct User peer -> dropped
    await captured_handler(make_event("Личка от пользователя", peer_id=PeerUser(12345)))
    assert callback_mock.await_count == 0

    # 6. Basic group (PeerChat) -> dropped
    await captured_handler(make_event("Обычная группа", peer_id=PeerChat(555)))
    assert callback_mock.await_count == 0

    # 7. Oversized text (>1500 chars) -> dropped
    await captured_handler(make_event("А" * 1501))
    assert callback_mock.await_count == 0

    # 8. Code markers and system prompt leaks -> dropped
    for code_text in [
        "def run_script(): pass",
        "class OrderParser: pass",
        "```python\nprint(1)```",
        "Ты — автономный агент",
        "import telethon",
        "from fastapi import FastAPI",
    ]:
        await captured_handler(make_event(f"Текст заказа: {code_text}"))
        assert callback_mock.await_count == 0

    # 9. Legitimate commercial lead from Channel -> accepted and passed to callback
    await captured_handler(make_event("Срочно требуется разработчик Telegram-ботов!"))
    assert callback_mock.await_count == 1
