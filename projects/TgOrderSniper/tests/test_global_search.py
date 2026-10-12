"""Unit tests for GlobalSearchEngine."""

import asyncio
from datetime import datetime, timezone, timedelta
from typing import Any
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from telethon.errors import FloodWaitError
from telethon.tl.types import Channel, Chat, PeerChannel, PeerChat, PeerUser, User

from backend.core.config import Settings
from backend.listener.global_search import GlobalSearchEngine, is_strictly_forbidden_chat


class MockEntity:
    def __init__(self, id: int, title: str = "", username: str = "", first_name: str = "", last_name: str = ""):
        self.id = id
        self.title = title
        self.username = username
        self.first_name = first_name
        self.last_name = last_name


class MockMessage:
    def __init__(
        self,
        id: int,
        text: str,
        peer_id: Any = None,
        from_id: Any = None,
        date: datetime | None = None,
        chat_id: Any = None,
        out: bool = False,
        is_private: bool = False,
    ):
        self.id = id
        self.message = text
        self.text = text
        self.peer_id = peer_id
        self.from_id = from_id
        self.date = date or datetime.now(timezone.utc)
        self.chat_id = chat_id
        self.out = out
        self.is_private = is_private


def test_extract_message_data():
    """Verifies message data extraction with entity mapping."""
    engine = GlobalSearchEngine()

    channel_entity = MockEntity(id=123456, title="Freelance Telegram", username="freelance_tg")
    user_entity = MockEntity(id=777, first_name="Иван", last_name="Петров", username="ivan_p")

    msg = MockMessage(
        id=42,
        text="Срочно нужен бот!",
        peer_id=PeerChannel(123456),
        from_id=PeerUser(777),
    )

    chats_map = {123456: channel_entity}
    users_map = {777: user_entity}

    data = engine.extract_message_data(msg, chats=chats_map, users=users_map)

    assert data["message_id"] == 42
    assert data["text"] == "Срочно нужен бот!"
    assert data["chat_title"] == "Freelance Telegram"
    assert data["author_id"] == 777
    assert data["author_username"] == "ivan_p"
    assert data["author_name"] == "Иван Петров"
    assert "https://t.me/freelance_tg/42" in data["message_url"]


@pytest.mark.asyncio
async def test_search_iteration_dispatches_messages():
    """Verifies that search_iteration queries client and invokes on_message_callback."""
    client = MagicMock()
    client.is_connected = MagicMock(return_value=True)

    channel = MockEntity(id=1001, title="Orders Hub")
    user = MockEntity(id=2001, username="client_lead", first_name="Клиент")

    msg1 = MockMessage(id=1, text="Нужен парсер", peer_id=PeerChannel(1001), from_id=PeerUser(2001))
    msg2 = MockMessage(id=2, text="Разработка 1С", peer_id=PeerChannel(1001), from_id=PeerUser(2001))

    mock_result = MagicMock()
    mock_result.messages = [msg1, msg2]
    mock_result.chats = [channel]
    mock_result.users = [user]

    client.side_effect = AsyncMock(return_value=mock_result)

    cfg = Settings(
        SEARCH_QUERIES=["нужен парсер"],
        SEARCH_RATE_LIMIT_DELAY=0.01,
        _env_file=None,
    )
    engine = GlobalSearchEngine(client=client, settings=cfg)

    dispatched = []
    async def callback(data: dict):
        dispatched.append(data)

    processed_count = await engine.search_iteration(callback)
    assert processed_count == 2
    assert len(dispatched) == 2
    assert dispatched[0]["message_id"] == 1
    assert dispatched[1]["message_id"] == 2
    assert engine.last_run_timestamp is not None


@pytest.mark.asyncio
async def test_search_iteration_min_date_filtering():
    """Ensures that messages older than min_date are filtered out."""
    client = MagicMock()
    client.is_connected = MagicMock(return_value=True)

    now = datetime.now(timezone.utc)
    old_date = now - timedelta(hours=2)
    new_date = now - timedelta(minutes=5)

    msg_new = MockMessage(id=10, text="Новый заказ", date=new_date, peer_id=PeerChannel(1))
    msg_old = MockMessage(id=9, text="Старый заказ", date=old_date, peer_id=PeerChannel(1))

    mock_result = MagicMock()
    mock_result.messages = [msg_new, msg_old]
    mock_result.chats = [MockEntity(id=1, title="Channel")]
    mock_result.users = []

    client.side_effect = AsyncMock(return_value=mock_result)

    cfg = Settings(
        SEARCH_QUERIES=["заказ"],
        SEARCH_RATE_LIMIT_DELAY=0.01,
        _env_file=None,
    )
    engine = GlobalSearchEngine(client=client, settings=cfg)
    # Set previous run timestamp between old and new
    engine.last_run_timestamp = now - timedelta(hours=1)

    dispatched = []
    async def callback(data: dict):
        dispatched.append(data)

    processed_count = await engine.search_iteration(callback)
    assert processed_count == 1
    assert len(dispatched) == 1
    assert dispatched[0]["message_id"] == 10


@pytest.mark.asyncio
async def test_search_iteration_flood_wait():
    """Verifies that FloodWaitError triggers sleep and retry."""
    client = MagicMock()
    client.is_connected = MagicMock(return_value=True)

    call_count = 0
    msg = MockMessage(id=100, text="Заказ после flood", peer_id=PeerChannel(5))
    mock_result = MagicMock()
    mock_result.messages = [msg]
    mock_result.chats = [MockEntity(id=5, title="Channel")]
    mock_result.users = []

    async def mock_call(req):
        nonlocal call_count
        call_count += 1
        if call_count == 1:
            err = FloodWaitError(request=req)
            err.seconds = 1
            raise err
        return mock_result

    client.side_effect = mock_call

    cfg = Settings(
        SEARCH_QUERIES=["заказ"],
        SEARCH_RATE_LIMIT_DELAY=0.01,
        _env_file=None,
    )
    engine = GlobalSearchEngine(client=client, settings=cfg)

    dispatched = []
    async def callback(data: dict):
        dispatched.append(data)

    with patch("asyncio.sleep", new_callable=AsyncMock) as mock_sleep:
        processed_count = await engine.search_iteration(callback)
        assert processed_count == 1
        assert call_count == 2
        assert mock_sleep.await_count >= 1


@pytest.mark.asyncio
async def test_search_global_request_parameters_without_dialog_scan():
    """Verifies that search_iteration constructs SearchGlobalRequest correctly without dialog scanning."""
    from telethon.tl.functions.messages import SearchGlobalRequest
    from telethon.tl.types import InputMessagesFilterEmpty, InputPeerEmpty

    client = MagicMock()
    client.is_connected = MagicMock(return_value=True)
    client.iter_dialogs = MagicMock()

    mock_result = MagicMock()
    mock_result.messages = []
    mock_result.chats = []
    mock_result.users = []

    requests_sent = []
    async def mock_call(req):
        requests_sent.append(req)
        return mock_result

    client.side_effect = mock_call

    cfg = Settings(
        SEARCH_QUERIES=["нужен бот"],
        SEARCH_RATE_LIMIT_DELAY=0.01,
        _env_file=None,
    )
    engine = GlobalSearchEngine(client=client, settings=cfg)
    assert not hasattr(engine, "_known_dialogs")
    assert not hasattr(engine, "scan_dialogs")

    await engine.search_iteration(AsyncMock())

    assert len(requests_sent) == 1
    req = requests_sent[0]
    assert isinstance(req, SearchGlobalRequest)
    assert req.q == "нужен бот"
    assert isinstance(req.filter, InputMessagesFilterEmpty)
    assert isinstance(req.offset_peer, InputPeerEmpty)
    assert req.offset_rate == 0
    assert req.offset_id == 0
    assert req.limit == 50
    # Verify client.iter_dialogs was never accessed or called
    client.iter_dialogs.assert_not_called()


@pytest.mark.asyncio
async def test_search_iteration_multi_page_pagination():
    """Verifies pagination across multiple pages with offset_id."""
    client = MagicMock()
    client.is_connected = MagicMock(return_value=True)
    client.get_input_entity = AsyncMock(return_value="mock_input_peer")

    now = datetime.now(timezone.utc)
    # Page 1: 50 messages
    page1_msgs = [
        MockMessage(id=i, text=f"Заказ {i}", date=now, peer_id=PeerChannel(100))
        for i in range(100, 50, -1)
    ]
    # Page 2: 10 messages
    page2_msgs = [
        MockMessage(id=i, text=f"Заказ {i}", date=now, peer_id=PeerChannel(100))
        for i in range(50, 40, -1)
    ]

    res_page1 = MagicMock()
    res_page1.messages = page1_msgs
    res_page1.chats = [MockEntity(id=100, title="Big Orders")]
    res_page1.users = []

    res_page2 = MagicMock()
    res_page2.messages = page2_msgs
    res_page2.chats = [MockEntity(id=100, title="Big Orders")]
    res_page2.users = []

    requests_received = []

    async def mock_call(req):
        requests_received.append(req)
        if len(requests_received) == 1:
            return res_page1
        return res_page2

    client.side_effect = mock_call

    cfg = Settings(
        SEARCH_QUERIES=["парсинг"],
        SEARCH_RATE_LIMIT_DELAY=0.01,
        _env_file=None,
    )
    engine = GlobalSearchEngine(client=client, settings=cfg)

    received_ids = []
    async def callback(data: dict):
        received_ids.append(data["message_id"])

    total = await engine.search_iteration(callback)
    assert total == 60
    assert len(received_ids) == 60
    assert len(requests_received) == 2
    assert requests_received[0].offset_id == 0
    assert requests_received[1].offset_id == 51  # last message ID of page 1


@pytest.mark.asyncio
async def test_polling_loop_lifecycle():
    """Verifies start_polling lifecycle without dialog scanning."""
    client = MagicMock()
    client.is_connected = MagicMock(return_value=True)

    cfg = Settings(
        SEARCH_QUERIES=["бот"],
        SEARCH_INTERVAL_MINUTES=1,
        SEARCH_RATE_LIMIT_DELAY=0.01,
        _env_file=None,
    )
    engine = GlobalSearchEngine(client=client, settings=cfg)

    # Mock search_iteration
    engine.search_iteration = AsyncMock(return_value=1)

    async def run_and_stop():
        task = asyncio.create_task(engine.start_polling(AsyncMock()))
        await asyncio.sleep(0.05)
        engine.stop()
        await task

    await run_and_stop()
    assert engine.is_running is False
    assert engine.search_iteration.await_count >= 1


def test_is_strictly_forbidden_chat_global_search():
    """Verifies that is_strictly_forbidden_chat adheres to privacy rules."""
    target_chat_id = -1004489991927
    my_id = 12345

    # 1. Message from target chat (TARGET_CHAT_ID) is blocked
    msg_target = MockMessage(id=1, text="Из целевого чата", chat_id=target_chat_id, peer_id=PeerChannel(4489991927))
    assert is_strictly_forbidden_chat(None, msg_target, target_chat_id, my_id) is True

    # 2. Personal dialogue with self / Saved messages ('SoloLevelUp' / Избранное)
    msg_self = MockMessage(id=2, text="Избранное", chat_id=my_id, peer_id=PeerUser(my_id))
    assert is_strictly_forbidden_chat(None, msg_self, target_chat_id, my_id) is True

    msg_out = MockMessage(id=3, text="Исходящее", peer_id=PeerChannel(100), out=True)
    assert is_strictly_forbidden_chat(None, msg_out, target_chat_id, my_id) is True

    # 3. Personal chat 'Сухарев / ЙЙ' (User / PeerUser / is_private)
    msg_user = MockMessage(id=4, text="Личка", peer_id=PeerUser(777))
    assert is_strictly_forbidden_chat(None, msg_user, target_chat_id, my_id) is True

    msg_private = MockMessage(id=5, text="Личный диалог", peer_id=PeerChannel(100), is_private=True)
    assert is_strictly_forbidden_chat(None, msg_private, target_chat_id, my_id) is True

    # 4. Ordinary group (Chat / PeerChat) is blocked
    msg_chat = MockMessage(id=6, text="Обычная группа", peer_id=PeerChat(555))
    assert is_strictly_forbidden_chat(None, msg_chat, target_chat_id, my_id) is True

    # 5. Public channel (Channel / PeerChannel) is allowed
    msg_channel = MockMessage(id=7, text="Публичный канал", peer_id=PeerChannel(999), chat_id=-100999)
    assert is_strictly_forbidden_chat(None, msg_channel, target_chat_id, my_id) is False


@pytest.mark.asyncio
async def test_global_search_privacy_gatekeeper_blocks_forbidden_messages():
    """Ensures search_iteration filters out target chat, self, user, and basic group messages."""
    client = MagicMock()
    client.is_connected = MagicMock(return_value=True)
    client.is_user_authorized = AsyncMock(return_value=True)
    me_mock = MagicMock()
    me_mock.id = 99999
    client.get_me = AsyncMock(return_value=me_mock)

    target_id = -1004489991927
    channel_valid = MockEntity(id=1001, title="Orders Supergroup")

    msg_target = MockMessage(id=1, text="Заказ из целевой группы", chat_id=target_id, peer_id=PeerChannel(4489991927))
    msg_self = MockMessage(id=2, text="Заказ из Избранного", chat_id=99999, peer_id=PeerChannel(200))
    msg_out = MockMessage(id=3, text="Исходящий заказ", peer_id=PeerChannel(300), out=True)
    msg_user = MockMessage(id=4, text="Личный чат Сухарев", peer_id=PeerUser(888))
    msg_chat = MockMessage(id=5, text="Обычный чат", peer_id=PeerChat(555))
    msg_valid = MockMessage(id=6, text="Срочный заказ на парсер", chat_id=-1001001, peer_id=PeerChannel(1001))

    mock_result = MagicMock()
    mock_result.messages = [msg_target, msg_self, msg_out, msg_user, msg_chat, msg_valid]
    mock_result.chats = [channel_valid]
    mock_result.users = []

    client.side_effect = AsyncMock(return_value=mock_result)

    cfg = Settings(
        SEARCH_QUERIES=["заказ"],
        SEARCH_RATE_LIMIT_DELAY=0.01,
        TARGET_CHAT_ID=target_id,
        _env_file=None,
    )
    engine = GlobalSearchEngine(client=client, settings=cfg)

    dispatched = []
    async def callback(data: dict):
        dispatched.append(data)

    processed_count = await engine.search_iteration(callback)

    # Only msg_valid should have passed through the Ironclad Privacy Barrier!
    assert processed_count == 1
    assert len(dispatched) == 1
    assert dispatched[0]["message_id"] == 6
    assert engine.my_id == 99999

