"""Unit and integration tests for NLP parsing, custom intervals, and handlers."""

import asyncio
from datetime import date, datetime, timedelta, timezone
import json
import os
from typing import Any, Dict, List, Optional, Tuple
import pytest
from unittest.mock import AsyncMock, MagicMock, patch

from backend.bot.handlers import (
    SniperStates,
    _pending_nlp_tasks,
    handle_cancel_snipe_callback,
    handle_confirm_snipe_callback,
    handle_nlp_message,
    handle_start,
    handle_start_new_snipe_fsm_callback,
    router,
)
from backend.bot.nlp_parser import (
    CITY_TO_IATA,
    RATES_TO_KZT,
    parse_flight_request,
    rule_based_flight_parser,
)
from backend.core.config import get_settings
from backend.core.models import FlightOffer, ParsedFlightIntent
from backend.db.dao import FlightSniperDAO
from backend.db.database import get_db, init_db
from backend.engine.sniper_worker import SniperWorker, run_sniper_check
from backend.providers.base import BaseFlightProvider


class TestNlpParser:
    """Test suite for NLP Intent Parser (Rule-based and LLM-assisted)."""

    def test_city_iata_mappings(self) -> None:
        """Verify city name mappings to 3-letter IATA codes."""
        assert CITY_TO_IATA["алматы"] == "ALA"
        assert CITY_TO_IATA["астана"] == "NQZ"
        assert CITY_TO_IATA["шымкент"] == "CIT"
        assert CITY_TO_IATA["актау"] == "SCO"
        assert CITY_TO_IATA["атырау"] == "GUW"
        assert CITY_TO_IATA["бангкок"] == "BKK"
        assert CITY_TO_IATA["дубай"] == "DXB"
        assert CITY_TO_IATA["стамбул"] == "IST"
        assert CITY_TO_IATA["пхукет"] == "HKT"
        assert CITY_TO_IATA["чэнду"] == "CTU"
        assert CITY_TO_IATA["ченду"] == "CTU"
        assert CITY_TO_IATA["chengdu"] == "CTU"
        assert CITY_TO_IATA["пекин"] == "PEK"
        assert CITY_TO_IATA["сеул"] == "ICN"
        assert CITY_TO_IATA["гуанчжоу"] == "CAN"
        assert CITY_TO_IATA["шанхай"] == "PVG"
        assert CITY_TO_IATA["ташкент"] == "TAS"

    def test_rule_based_parser_asian_city_without_price(self) -> None:
        """Test parsing Asian city route without target price."""
        text = "Алматы - Чэнду на 2026-11-21"
        base = date(2026, 9, 1)
        intent = rule_based_flight_parser(text, base_date=base)

        assert intent is not None
        assert intent.origin == "ALA"
        assert intent.destination == "CTU"
        assert intent.date == "2026-11-21"
        assert intent.target_price is None
        assert intent.flight_number is None
        assert intent.interval_minutes == 5

    def test_currency_conversion_rates(self) -> None:
        """Verify currency exchange rates to KZT."""
        assert RATES_TO_KZT["USD"] == 500.0
        assert RATES_TO_KZT["EUR"] == 540.0
        assert RATES_TO_KZT["RUB"] == 5.5
        assert RATES_TO_KZT["KZT"] == 1.0

    def test_rule_based_parser_usd_international(self) -> None:
        """Test parsing international flight request in USD with specific flight and interval."""
        text = "Рейс Алматы - Бангкок, 15 октября 2026, прямой, KC-871, ниже 300$. Проверять каждые 5 минут"
        base = date(2026, 9, 1)
        intent = rule_based_flight_parser(text, base_date=base)

        assert intent is not None
        assert intent.origin == "ALA"
        assert intent.destination == "BKK"
        assert intent.date == "2026-10-15"
        assert intent.flight_number == "KC-871"
        assert intent.direct_only is True
        assert intent.currency_detected == "USD"
        assert intent.original_price == 300.0
        assert intent.target_price == 150000.0  # 300 * 500
        assert intent.interval_minutes == 5

    def test_rule_based_parser_kzt_domestic(self) -> None:
        """Test parsing domestic route with ISO date and KZT price."""
        text = "Астана - Шымкент на 2026-11-01 до 20000 тг"
        base = date(2026, 9, 1)
        intent = rule_based_flight_parser(text, base_date=base)

        assert intent is not None
        assert intent.origin == "NQZ"
        assert intent.destination == "CIT"
        assert intent.date == "2026-11-01"
        assert intent.flight_number is None
        assert intent.direct_only is True
        assert intent.currency_detected == "KZT"
        assert intent.target_price == 20000.0
        assert intent.interval_minutes == 5

    def test_rule_based_parser_custom_interval_and_eur(self) -> None:
        """Test parsing request with EUR currency and 10-minute interval."""
        text = "Из Актау в Дубай 25 декабря 2026 не дороже 200€, проверка раз в 10 минут"
        base = date(2026, 9, 1)
        intent = rule_based_flight_parser(text, base_date=base)

        assert intent is not None
        assert intent.origin == "SCO"
        assert intent.destination == "DXB"
        assert intent.date == "2026-12-25"
        assert intent.currency_detected == "EUR"
        assert intent.original_price == 200.0
        assert intent.target_price == 108000.0  # 200 * 540
        assert intent.interval_minutes == 10

    def test_rule_based_parser_relative_date_tomorrow(self) -> None:
        """Test parsing relative date 'завтра'."""
        text = "Билет из Алматы в Астану завтра до 25000 тенге"
        base = date(2026, 10, 1)
        intent = rule_based_flight_parser(text, base_date=base)

        assert intent is not None
        assert intent.origin == "ALA"
        assert intent.destination == "NQZ"
        assert intent.date == "2026-10-02"
        assert intent.target_price == 25000.0

    def test_rule_based_parser_relative_date_after_week(self) -> None:
        """Test parsing relative date 'через неделю'."""
        text = "Шымкент - Актау через неделю до 30000"
        base = date(2026, 10, 1)
        intent = rule_based_flight_parser(text, base_date=base)

        assert intent is not None
        assert intent.origin == "CIT"
        assert intent.destination == "SCO"
        assert intent.date == "2026-10-08"

    def test_rule_based_parser_hourly_interval(self) -> None:
        """Test parsing 'каждый час' interval (60 minutes)."""
        text = "Астана - Алматы 2026-11-15 до 15000 тг, каждый час"
        intent = rule_based_flight_parser(text)
        assert intent is not None
        assert intent.interval_minutes == 60

    def test_rule_based_parser_invalid_or_incomplete_text(self) -> None:
        """Verify invalid or incomplete text returns None."""
        assert rule_based_flight_parser("") is None
        assert rule_based_flight_parser("Привет, как дела?") is None
        assert rule_based_flight_parser("Хочу полететь в Алматы") is None  # Missing destination, date, price

    @pytest.mark.asyncio
    async def test_parse_flight_request_with_mocked_groq(self) -> None:
        """Verify parse_flight_request uses Groq LLM when configured."""
        mock_payload = {
            "origin": "ALA",
            "destination": "BKK",
            "date": "2026-10-15",
            "flight_number": "KC-871",
            "direct_only": True,
            "target_price": 150000.0,
            "currency_detected": "USD",
            "original_price": 300.0,
            "interval_minutes": 5,
            "confidence": 0.98,
            "raw_explanation": "Extracted flight from Almaty to Bangkok",
        }

        mock_response = MagicMock()
        mock_choice = MagicMock()
        mock_choice.message.content = json.dumps(mock_payload)
        mock_response.choices = [mock_choice]

        mock_client = MagicMock()
        mock_client.chat.completions.create = AsyncMock(return_value=mock_response)

        with patch("groq.AsyncGroq", return_value=mock_client):
            intent = await parse_flight_request(
                text="Рейс Алматы Бангкок 15 октября 300$",
                api_key="gsk_test_mock_key_1234567890",
            )
            assert intent is not None
            assert intent.origin == "ALA"
            assert intent.destination == "BKK"
            assert intent.target_price == 150000.0
            assert intent.flight_number == "KC-871"


@pytest.mark.asyncio
class TestDatabaseCustomIntervalsAndMigration:
    """Test suite for Database migrations and Custom Intervals."""

    async def test_automated_migration_on_old_schema(self, tmp_path: Any) -> None:
        """Test init_db automatically runs ALTER TABLE if interval_minutes is missing."""
        db_file = str(tmp_path / "legacy.db")

        # Create an old schema table without interval_minutes or max_transfers
        async with get_db(db_file) as conn:
            await conn.execute("""
                CREATE TABLE tasks (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    chat_id INTEGER NOT NULL,
                    origin TEXT NOT NULL,
                    destination TEXT NOT NULL,
                    date TEXT NOT NULL,
                    flight_number TEXT NULL,
                    target_price REAL NOT NULL,
                    is_active INTEGER DEFAULT 1,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    last_checked_at TIMESTAMP NULL,
                    last_price REAL NULL
                );
            """)
            await conn.commit()

        # Run init_db which should perform migration
        await init_db(db_file)

        # Check table columns
        async with get_db(db_file) as conn:
            cursor = await conn.execute("PRAGMA table_info(tasks)")
            cols = [row["name"] for row in await cursor.fetchall()]
            assert "interval_minutes" in cols
            assert "max_transfers" in cols

    async def test_dao_custom_interval_crud_and_due_tasks(self, tmp_path: Any) -> None:
        """Test DAO tasks with custom intervals and due tasks query."""
        db_file = str(tmp_path / "intervals.db")
        await init_db(db_file)
        dao = FlightSniperDAO(db_path=db_file)

        # Task 1: 5-minute interval
        t1_id = await dao.add_task(
            chat_id=101,
            origin="ALA",
            destination="NQZ",
            date="2026-10-15",
            target_price=25000.0,
            interval_minutes=5,
        )

        # Task 2: 10-minute interval
        t2_id = await dao.add_task(
            chat_id=102,
            origin="SCO",
            destination="DXB",
            date="2026-12-25",
            target_price=80000.0,
            interval_minutes=10,
        )

        # Initially, both tasks have last_checked_at = NULL -> Both should be due
        due_tasks = await dao.get_due_tasks()
        assert len(due_tasks) == 2
        assert {t["id"] for t in due_tasks} == {t1_id, t2_id}

        # Update last_checked_at to NOW for both tasks
        await dao.update_task_last_check(t1_id, last_price=24000.0)
        await dao.update_task_last_check(t2_id, last_price=75000.0)

        # Now 0 tasks should be due
        due_tasks_after = await dao.get_due_tasks()
        assert len(due_tasks_after) == 0

        # Simulate 6 minutes passing:
        # Task 1 (5m interval) should be DUE
        # Task 2 (10m interval) should NOT be due
        async with get_db(db_file) as conn:
            await conn.execute("UPDATE tasks SET last_checked_at = datetime('now', '-6 minutes')")
            await conn.commit()

        due_after_6min = await dao.get_due_tasks()
        assert len(due_after_6min) == 1
        assert due_after_6min[0]["id"] == t1_id

        # Simulate 12 minutes passing:
        # Both tasks should now be DUE
        async with get_db(db_file) as conn:
            await conn.execute("UPDATE tasks SET last_checked_at = datetime('now', '-12 minutes')")
            await conn.commit()

        due_after_12min = await dao.get_due_tasks()
        assert len(due_after_12min) == 2


@pytest.mark.asyncio
class TestTelegramBotNlPHandlers:
    """Test suite for Telegram Bot NLP messages, FSM states, and inline callbacks."""

    async def test_handle_start_fsm_button(self) -> None:
        """Test /start command attaches create monitoring FSM inline button."""
        message = MagicMock()
        message.answer = AsyncMock()

        await handle_start(message)

        assert message.answer.called
        call_args = message.answer.call_args
        reply_text = call_args[0][0]
        reply_markup = call_args[1].get("reply_markup")

        assert "Добро пожаловать в KzFlightSniper" in reply_text
        assert reply_markup is not None
        assert len(reply_markup.inline_keyboard) >= 1
        assert reply_markup.inline_keyboard[0][0].callback_data == "start_new_snipe_fsm"

    async def test_start_new_snipe_fsm_callback(self) -> None:
        """Test start_new_snipe_fsm callback transitions state to waiting_for_flight_text."""
        state = AsyncMock()
        state.set_state = AsyncMock()

        callback_msg = MagicMock()
        callback_msg.answer = AsyncMock()

        callback = MagicMock()
        callback.data = "start_new_snipe_fsm"
        callback.message = callback_msg
        callback.answer = AsyncMock()

        await handle_start_new_snipe_fsm_callback(callback, state)

        assert state.set_state.called
        assert state.set_state.call_args[0][0] == SniperStates.waiting_for_flight_text
        assert callback.answer.called
        assert callback_msg.answer.called
        assert "Напишите параметры поиска обычным текстом" in callback_msg.answer.call_args[0][0]

    async def test_handle_nlp_message_recognized(self, tmp_path: Any) -> None:
        """Test handling natural language text with Live Preview and clearing FSM state."""
        status_msg = MagicMock()
        status_msg.edit_text = AsyncMock()

        message = MagicMock()
        message.text = "Рейс Алматы - Бангкок, 15 октября 2026, ниже 300$"
        message.chat.id = 777
        message.bot = MagicMock()
        message.bot.send_chat_action = AsyncMock()
        message.answer = AsyncMock(return_value=status_msg)

        state = AsyncMock()
        state.clear = AsyncMock()

        mock_offers = [
            FlightOffer(
                airline="Air Astana",
                flight_number="KC-871",
                origin="ALA",
                destination="BKK",
                departure_time="01:20",
                arrival_time="08:50",
                price_kzt=142000.0,
                transfers_count=0,
            )
        ]

        with patch("backend.bot.handlers.AviataProvider.search", new=AsyncMock(return_value=mock_offers)):
            await handle_nlp_message(message, state)

        assert message.bot.send_chat_action.called
        assert message.answer.called
        assert message.answer.call_args[0][0] == "⏳ Анализирую запрос через AI..."
        assert state.clear.called

        assert status_msg.edit_text.called
        call_args = status_msg.edit_text.call_args
        card_text = call_args[0][0]
        reply_markup = call_args[1].get("reply_markup")

        assert "ALA" in card_text
        assert "BKK" in card_text
        assert "150,000 ₸" in card_text
        assert "Air Astana" in card_text
        assert "142,000 ₸" in card_text
        assert reply_markup is not None
        assert len(reply_markup.inline_keyboard[0]) == 2
        assert "confirm_snipe:" in reply_markup.inline_keyboard[0][0].callback_data

    async def test_handle_nlp_message_without_price_auto_target(self) -> None:
        """Test natural language request without explicit price automatically uses min live offer."""
        status_msg = MagicMock()
        status_msg.edit_text = AsyncMock()

        message = MagicMock()
        message.text = "Алматы - Чэнду на 2026-11-21"
        message.chat.id = 888
        message.bot = MagicMock()
        message.bot.send_chat_action = AsyncMock()
        message.answer = AsyncMock(return_value=status_msg)

        state = AsyncMock()
        state.clear = AsyncMock()

        mock_offers = [
            FlightOffer(
                airline="Air China",
                flight_number="CA-484",
                origin="ALA",
                destination="CTU",
                departure_time="10:00",
                arrival_time="17:00",
                price_kzt=78500.0,
                transfers_count=0,
            ),
            FlightOffer(
                airline="China Southern",
                flight_number="CZ-6012",
                origin="ALA",
                destination="CTU",
                departure_time="14:00",
                arrival_time="21:00",
                price_kzt=92000.0,
                transfers_count=1,
            ),
        ]

        with patch("backend.bot.handlers.AviataProvider.search", new=AsyncMock(return_value=mock_offers)):
            await handle_nlp_message(message, state)

        assert state.clear.called
        assert status_msg.edit_text.called
        card_text = status_msg.edit_text.call_args[0][0]
        assert "ALA" in card_text
        assert "CTU" in card_text
        assert "78,500 ₸" in card_text
        assert "Цена установлена автоматически" in card_text

    async def test_handle_nlp_message_unrecognized(self) -> None:
        """Test unrecognized text message prompts retry without clearing FSM state."""
        status_msg = MagicMock()
        status_msg.edit_text = AsyncMock()

        message = MagicMock()
        message.text = "Привет бот"
        message.chat.id = 777
        message.bot = MagicMock()
        message.bot.send_chat_action = AsyncMock()
        message.answer = AsyncMock(return_value=status_msg)

        state = AsyncMock()
        state.clear = AsyncMock()

        await handle_nlp_message(message, state)

        assert message.bot.send_chat_action.called
        assert message.answer.called
        assert message.answer.call_args[0][0] == "⏳ Анализирую запрос через AI..."
        assert not state.clear.called

        assert status_msg.edit_text.called
        card_text = status_msg.edit_text.call_args[0][0]
        assert "Не удалось распознать маршрут и дату рейса" in card_text

    async def test_confirm_and_cancel_callbacks(self, tmp_path: Any) -> None:
        """Test confirming and cancelling pending NLP snipe tasks."""
        db_file = str(tmp_path / "bot_nlp.db")
        await init_db(db_file)

        # Inject DAO with test db path
        with patch("backend.bot.handlers.dao", FlightSniperDAO(db_path=db_file)):
            # Store pending task with effective_target_price
            token = "test_token_123"
            intent = ParsedFlightIntent(
                origin="ALA",
                destination="NQZ",
                date="2026-10-15",
                flight_number="KC-871",
                direct_only=True,
                target_price=25000.0,
                interval_minutes=5,
            )
            _pending_nlp_tasks[token] = {
                "chat_id": 999,
                "intent": intent,
                "effective_target_price": 25000.0,
                "created_at": datetime.now(timezone.utc),
            }

            # Simulate Confirm Callback
            cb_msg = MagicMock()
            cb_msg.edit_text = AsyncMock()
            callback = MagicMock()
            callback.data = f"confirm_snipe:{token}"
            callback.message = cb_msg
            callback.answer = AsyncMock()

            await handle_confirm_snipe_callback(callback)

            assert callback.answer.called
            assert cb_msg.edit_text.called
            edit_text = cb_msg.edit_text.call_args[0][0]
            assert "Снайпер активирован!" in edit_text
            assert token not in _pending_nlp_tasks

            # Verify saved in database
            test_dao = FlightSniperDAO(db_path=db_file)
            tasks = await test_dao.get_user_tasks(chat_id=999)
            assert len(tasks) == 1
            assert tasks[0]["origin"] == "ALA"
            assert tasks[0]["destination"] == "NQZ"
            assert tasks[0]["flight_number"] == "KC-871"
            assert tasks[0]["interval_minutes"] == 5
            assert tasks[0]["target_price"] == 25000.0

            # Test Cancel Callback
            token_cancel = "cancel_token_456"
            _pending_nlp_tasks[token_cancel] = {
                "chat_id": 999,
                "intent": intent,
            }
            cb_cancel = MagicMock()
            cb_cancel.data = f"cancel_snipe:{token_cancel}"
            cb_cancel.message = MagicMock()
            cb_cancel.message.edit_text = AsyncMock()
            cb_cancel.answer = AsyncMock()

            await handle_cancel_snipe_callback(cb_cancel)
            assert token_cancel not in _pending_nlp_tasks
            assert cb_cancel.message.edit_text.called
            cancel_text = cb_cancel.message.edit_text.call_args[0][0]
            assert "отменено" in cancel_text
