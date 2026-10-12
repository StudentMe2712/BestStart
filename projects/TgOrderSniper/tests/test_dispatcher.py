"""Unit tests for TelegramBotDispatcher and OrderProcessingPipeline."""

from unittest.mock import AsyncMock, MagicMock, patch


import httpx
import pytest

from backend.bot.dispatcher import TelegramBotDispatcher
from backend.core.dedup import MessageDedup
from backend.filter.engine import FilterEngine, FilteredOrder
from backend.main import OrderProcessingPipeline


@pytest.fixture
def sample_order() -> FilteredOrder:
    return FilteredOrder(
        chat_id="-1001234567890",
        chat_title="Freelance Hub",
        message_id=777,
        raw_text="Срочно требуется разработчик! Нужно сделать Telegram-бота. Бюджет 45 000 руб.",
        cleaned_text="Срочно требуется разработчик! Нужно сделать Telegram-бота. Бюджет 45 000 руб.",
        author_id=987654,
        author_username="client_pro",
        author_name="Алексей Смирнов",
        message_url="https://t.me/c/1234567890/777",
        matched_triggers=["срочно", "требуется", "сделать"],
        detected_budget="45 000 руб",
        category_tags=["телеграм бот", "python"],
    )


def test_format_card_header_and_tags(sample_order: FilteredOrder) -> None:
    """Verifies that format_card generates correct header, title, and hashtags."""
    dispatcher = TelegramBotDispatcher(bot_token="fake_token", target_chat_id="-100999")
    card_text, reply_markup = dispatcher.format_card(sample_order)

    # 1. Header checks
    assert "🔥 <b>НОВЫЙ ЗАКАЗ</b>" in card_text
    assert "📍 <b>Источник:</b> Freelance Hub" in card_text
    assert "🏷️ <b>Категории:</b> #телеграм_бот #python" in card_text

    # 2. Budget checks
    assert "💰 Бюджет: <b>45 000 руб</b>" in card_text

    # 3. Author checks
    assert "👤 <b>Автор:</b>" in card_text
    assert '<a href="https://t.me/client_pro">@client_pro</a>' in card_text

    # 4. Task and trigger highlighting
    assert "📝 <b>Задача:</b>" in card_text
    assert "<b>Срочно</b>" in card_text or "<b>срочно</b>" in card_text
    assert "<b>требуется</b>" in card_text

    # 5. Inline buttons
    inline_keyboard = reply_markup["inline_keyboard"]
    assert len(inline_keyboard) == 1
    row = inline_keyboard[0]
    assert len(row) == 2

    assert row[0]["text"] == "🔍 Открыть пост"
    assert row[0]["url"] == "https://t.me/c/1234567890/777"

    assert row[1]["text"] == "💬 Написать заказчику"
    assert row[1]["url"] == "https://t.me/client_pro"


def test_format_card_without_budget() -> None:
    """Verifies format_card when budget is None."""
    order = FilteredOrder(
        chat_id="123",
        chat_title="Orders Chat",
        message_id=1,
        raw_text="Нужен скрипт для выгрузки данных",
        cleaned_text="Нужен скрипт для выгрузки данных",
        author_id=555,
        author_username=None,
        author_name="Иван",
        message_url="",
        matched_triggers=["нужен"],
        detected_budget=None,
        category_tags=["скрипт"],
    )
    dispatcher = TelegramBotDispatcher(bot_token="fake_token", target_chat_id="-100999")
    card_text, reply_markup = dispatcher.format_card(order)

    assert "💰 Бюджет:" not in card_text
    # Author ID tg:// link
    assert '<a href="tg://user?id=555">Иван</a>' in card_text

    # No message_url, but author tg:// link exists
    inline_keyboard = reply_markup["inline_keyboard"]
    assert len(inline_keyboard) == 1
    row = inline_keyboard[0]
    assert len(row) == 1
    assert row[0]["text"] == "💬 Написать заказчику"
    assert row[0]["url"] == "tg://user?id=555"


def test_format_card_author_fallbacks() -> None:
    """Verifies author formatting when author is only name or unknown."""
    # Author with name only
    order_name_only = FilteredOrder(
        chat_id="123",
        chat_title="Orders Chat",
        message_id=2,
        raw_text="Требуется интеграция по API",
        cleaned_text="Требуется интеграция по API",
        author_id=None,
        author_username=None,
        author_name="Компания Ромашка",
        message_url="https://t.me/orders/2",
        matched_triggers=["требуется"],
        detected_budget=None,
        category_tags=[],
    )
    dispatcher = TelegramBotDispatcher(bot_token="fake_token", target_chat_id="-100999")
    text1, markup1 = dispatcher.format_card(order_name_only)
    assert "👤 <b>Автор:</b> Компания Ромашка" in text1
    assert len(markup1["inline_keyboard"][0]) == 1  # Only 'Открыть пост'

    # Author completely unknown
    order_none = FilteredOrder(
        chat_id="123",
        chat_title="Orders Chat",
        message_id=3,
        raw_text="Требуется доработка 1С",
        cleaned_text="Требуется доработка 1С",
        author_id=None,
        author_username=None,
        author_name=None,
        message_url="",
        matched_triggers=["требуется"],
        detected_budget=None,
        category_tags=["1с"],
    )
    text2, markup2 = dispatcher.format_card(order_none)
    assert "👤 <b>Автор:</b> Не указан" in text2
    assert markup2["inline_keyboard"] == []


def test_format_card_html_escaping_and_truncation() -> None:
    """Ensures HTML special characters are escaped and long messages are truncated."""
    long_malicious_text = (
        "Срочно нужен бот! <script>alert('pwned')</script> "
        + ("A" * 2000)
        + " & special characters < > \" '"
    )
    order = FilteredOrder(
        chat_id="123",
        chat_title="Security <Test>",
        message_id=999,
        raw_text=long_malicious_text,
        cleaned_text=long_malicious_text,
        author_id=1,
        author_username="hacker<script>",
        author_name="<Evil Admin>",
        message_url="",
        matched_triggers=["срочно", "нужен"],
        detected_budget=None,
        category_tags=[],
    )
    dispatcher = TelegramBotDispatcher(bot_token="fake_token", target_chat_id="-100999")
    card_text, _ = dispatcher.format_card(order)

    # Chat title and author must be escaped
    assert "&lt;Test&gt;" in card_text
    assert "<script>" not in card_text
    assert "&lt;script&gt;" in card_text
    assert "&lt;Evil Admin&gt;" in card_text or "hacker&lt;script&gt;" in card_text

    # Text must be truncated to <= 1500 chars + "..."
    # The whole card has header, but task section itself should have ellipsis
    assert "..." in card_text


@pytest.mark.asyncio
async def test_dispatch_success(sample_order: FilteredOrder) -> None:
    """Verifies successful message dispatching via mock httpx transport."""
    received_requests: list[httpx.Request] = []

    def mock_handler(request: httpx.Request) -> httpx.Response:
        received_requests.append(request)
        return httpx.Response(
            status_code=200,
            json={"ok": True, "result": {"message_id": 12345}},
        )

    transport = httpx.MockTransport(mock_handler)
    async with httpx.AsyncClient(transport=transport) as client:
        dispatcher = TelegramBotDispatcher(
            bot_token="123:test_token",
            target_chat_id="-100123456",
            client=client,
        )

        success = await dispatcher.dispatch(sample_order)
        assert success is True

    assert len(received_requests) == 1
    req = received_requests[0]
    assert req.method == "POST"
    assert req.url == "https://api.telegram.org/bot123:test_token/sendMessage"

    import json

    body = json.loads(req.content.decode("utf-8"))
    assert body["chat_id"] == "-100123456"
    assert body["parse_mode"] == "HTML"
    assert "🔥 <b>НОВЫЙ ЗАКАЗ</b>" in body["text"]
    assert "reply_markup" in body
    assert "inline_keyboard" in body["reply_markup"]


@pytest.mark.asyncio
async def test_dispatch_rate_limiting_retry(sample_order: FilteredOrder) -> None:
    """Verifies that 429 rate limiting triggers backoff and retry."""
    call_count = 0

    def mock_handler(request: httpx.Request) -> httpx.Response:
        nonlocal call_count
        call_count += 1
        if call_count == 1:
            return httpx.Response(
                status_code=429,
                json={
                    "ok": False,
                    "error_code": 429,
                    "description": "Too Many Requests: retry after 0.01",
                    "parameters": {"retry_after": 0.01},
                },
            )
        return httpx.Response(
            status_code=200,
            json={"ok": True, "result": {"message_id": 99}},
        )

    transport = httpx.MockTransport(mock_handler)
    async with httpx.AsyncClient(transport=transport) as client:
        dispatcher = TelegramBotDispatcher(
            bot_token="test_token",
            target_chat_id="-100123",
            client=client,
            max_retries=3,
        )

        success = await dispatcher.dispatch(sample_order)
        assert success is True
        assert call_count == 2


@pytest.mark.asyncio
async def test_dispatch_network_error_retry(sample_order: FilteredOrder) -> None:
    """Verifies that network error triggers retry with exponential backoff."""
    call_count = 0

    def mock_handler(request: httpx.Request) -> httpx.Response:
        nonlocal call_count
        call_count += 1
        if call_count < 3:
            raise httpx.ConnectError("Connection refused by Telegram server")
        return httpx.Response(
            status_code=200,
            json={"ok": True, "result": {"message_id": 100}},
        )

    transport = httpx.MockTransport(mock_handler)
    async with httpx.AsyncClient(transport=transport) as client:
        dispatcher = TelegramBotDispatcher(
            bot_token="test_token",
            target_chat_id="-100123",
            client=client,
            max_retries=3,
        )

        with patch("asyncio.sleep", new_callable=AsyncMock) as mock_sleep:
            success = await dispatcher.dispatch(sample_order)
            assert success is True
            assert call_count == 3
            assert mock_sleep.await_count == 2


@pytest.mark.asyncio
async def test_dispatch_client_error_no_retry(sample_order: FilteredOrder) -> None:
    """Verifies that 400 Bad Request halts immediately without endless retries."""
    call_count = 0

    def mock_handler(request: httpx.Request) -> httpx.Response:
        nonlocal call_count
        call_count += 1
        return httpx.Response(
            status_code=400,
            json={"ok": False, "description": "Bad Request: chat not found"},
        )

    transport = httpx.MockTransport(mock_handler)
    async with httpx.AsyncClient(transport=transport) as client:
        dispatcher = TelegramBotDispatcher(
            bot_token="test_token",
            target_chat_id="-100123",
            client=client,
            max_retries=3,
        )

        success = await dispatcher.dispatch(sample_order)
        assert success is False
        assert call_count == 1  # No useless retries on 400


@pytest.mark.asyncio
async def test_dispatch_missing_credentials(sample_order: FilteredOrder) -> None:
    """Verifies that empty bot_token or target_chat_id aborts dispatch cleanly."""
    dispatcher = TelegramBotDispatcher(bot_token="", target_chat_id=0)
    success = await dispatcher.dispatch(sample_order)
    assert success is False


@pytest.mark.asyncio
async def test_dispatcher_close_own_client() -> None:
    """Verifies that closing dispatcher closes internally-created httpx client."""
    dispatcher = TelegramBotDispatcher(bot_token="tok", target_chat_id="123")
    assert dispatcher.client.is_closed is False
    await dispatcher.close()
    assert dispatcher.client.is_closed is True


@pytest.mark.asyncio
async def test_order_processing_pipeline_flow() -> None:
    """Verifies the complete pipeline flow: Dedup -> Filter -> Dispatch."""
    dedup = MessageDedup(max_size=100)
    filter_engine = FilterEngine()

    mock_dispatch = AsyncMock(return_value=True)
    dispatcher = TelegramBotDispatcher(bot_token="tok", target_chat_id="123")
    dispatcher.dispatch = mock_dispatch

    pipeline = OrderProcessingPipeline(
        dedup=dedup,
        filter_engine=filter_engine,
        dispatcher=dispatcher,
    )

    # 1. First arrival of a valid order -> Should evaluate and dispatch
    event1 = {
        "chat_id": "-1001",
        "chat_title": "Orders",
        "message_id": 501,
        "text": "Срочно требуется доработать парсер на Python! Бюджет: 20 000 руб.",
        "author_id": 11,
        "author_username": "client11",
        "author_name": "Сергей",
        "message_url": "https://t.me/c/1/501",
    }
    order1 = await pipeline.process_message(event1)
    assert order1 is not None
    assert mock_dispatch.await_count == 1

    # 2. Duplicate arrival of the same message ID -> Dedup drops it
    mock_dispatch.reset_mock()
    order2 = await pipeline.process_message(event1)
    assert order2 is None
    assert mock_dispatch.await_count == 0

    # 3. Resume message -> Dropped by FilterEngine
    event_resume = {
        "chat_id": "-1001",
        "chat_title": "Orders",
        "message_id": 502,
        "text": "Привет! Ищу работу разработчиком. Мое резюме прикреплено.",
        "author_id": 12,
        "author_username": "dev12",
        "author_name": "Иван",
        "message_url": "",
    }
    order3 = await pipeline.process_message(event_resume)
    assert order3 is None
    assert mock_dispatch.await_count == 0


def test_format_card_with_ai_analysis(sample_order: FilteredOrder) -> None:
    """Verifies that format_card generates enriched AI card with pitch and quote."""
    from backend.filter.groq_analyzer import GroqAnalysisResult

    ai_result = GroqAnalysisResult(
        is_lead=True,
        summary="Разработка Telegram-бота для обработки заявок",
        budget="50 000 руб",
        tech_stack="Python, aiogram, PostgreSQL",
        ready_pitch="Здравствуйте! Реализую Telegram-бота под ключ с подключением БД. Опыт 3+ года.",
    )

    dispatcher = TelegramBotDispatcher(bot_token="fake_token", target_chat_id="-100999")
    card_text, reply_markup = dispatcher.format_card(sample_order, ai_analysis=ai_result)

    assert "🔥 <b>НОВЫЙ ЗАКАЗ</b>" in card_text
    assert "📍 <b>Источник:</b> Freelance Hub" in card_text
    assert "🎯 <b>Суть:</b> Разработка Telegram-бота для обработки заявок" in card_text
    assert "💰 <b>Бюджет:</b> 50 000 руб" in card_text
    assert "🛠 <b>Стек:</b> Python, aiogram, PostgreSQL" in card_text
    assert "👤 <b>Автор:</b>" in card_text
    assert "📝 <b>Готовый отклик (скопировать в 1 клик):</b>" in card_text
    assert "<code>Здравствуйте! Реализую Telegram-бота под ключ с подключением БД. Опыт 3+ года.</code>" in card_text
    assert "<blockquote expandable>" in card_text
    assert sample_order.cleaned_text in card_text
    assert "</blockquote>" in card_text

    # Buttons
    inline_keyboard = reply_markup["inline_keyboard"]
    assert len(inline_keyboard) == 1
    assert inline_keyboard[0][0]["text"] == "🔍 Открыть пост"
    assert inline_keyboard[0][1]["text"] == "💬 Написать заказчику"


@pytest.mark.asyncio
async def test_order_processing_pipeline_with_groq_analyzer() -> None:
    """Verifies pipeline flow with GroqAnalyzer scoring."""
    from backend.filter.groq_analyzer import GroqAnalysisResult, GroqAnalyzer

    dedup = MessageDedup(max_size=100)
    filter_engine = FilterEngine()
    mock_dispatch = AsyncMock(return_value=True)
    dispatcher = TelegramBotDispatcher(bot_token="tok", target_chat_id="123")
    dispatcher.dispatch = mock_dispatch

    mock_groq = MagicMock(spec=GroqAnalyzer)
    mock_groq.is_configured = True

    pipeline = OrderProcessingPipeline(
        dedup=dedup,
        filter_engine=filter_engine,
        dispatcher=dispatcher,
        groq_analyzer=mock_groq,
    )

    # 1. Lead accepted by AI
    mock_groq.analyze = AsyncMock(
        return_value=GroqAnalysisResult(
            is_lead=True,
            summary="Доработка скрипта",
            budget="15 000 руб",
            tech_stack="Python",
            ready_pitch="Готов доработать скрипт.",
        )
    )

    event_lead = {
        "chat_id": "-1001",
        "chat_title": "Orders",
        "message_id": 601,
        "text": "Нужен разработчик для доработки скрипта выгрузки. Бюджет 15 000 руб.",
        "author_id": 20,
        "author_username": "client20",
        "author_name": "Дмитрий",
        "message_url": "https://t.me/c/1/601",
    }
    order1 = await pipeline.process_message(event_lead)
    assert order1 is not None
    assert order1.ai_analysis is not None
    assert order1.ai_analysis.is_lead is True
    assert mock_dispatch.await_count == 1

    # 2. Message rejected by AI (not a lead)
    mock_dispatch.reset_mock()
    mock_groq.analyze = AsyncMock(
        return_value=GroqAnalysisResult(
            is_lead=False,
            summary="",
        )
    )

    event_not_lead = {
        "chat_id": "-1001",
        "chat_title": "Orders",
        "message_id": 602,
        "text": "Кто знает хороший хостинг для python скрипта?",
        "author_id": 21,
        "author_username": "user21",
        "author_name": "Олег",
        "message_url": "",
    }
    order2 = await pipeline.process_message(event_not_lead)
    assert order2 is None
    assert mock_dispatch.await_count == 0

