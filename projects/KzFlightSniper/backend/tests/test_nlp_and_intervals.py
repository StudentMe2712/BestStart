"""Unit and integration tests for NLP parsing, custom intervals, FSM flow, and handlers."""

import asyncio
from datetime import date, datetime, timedelta, timezone
import json
import os
from typing import Any, Dict, List, Optional, Tuple
import pytest
from unittest.mock import AsyncMock, MagicMock, patch

from backend.bot.handlers import (
    CancelSnipeCallback,
    ConfirmSnipeCallback,
    FlightSelectCallback,
    MonitorFlightCallback,
    QuickIntervalCallback,
    SniperStates,
    StepBackCallback,
    handle_cancel_snipe_callback,
    handle_confirm_snipe_callback,
    handle_flight_select_callback,
    handle_interval_text_message,
    handle_monitor_flight_callback,
    handle_nlp_message,
    handle_quick_interval_callback,
    handle_search_query_message,
    handle_start,
    handle_start_new_snipe_fsm_callback,
    handle_step_back_callback,
    router,
)
from backend.bot.nlp_parser import (
    CITY_TO_IATA,
    RATES_TO_KZT,
    parse_flight_request,
    parse_interval_nlp,
    parse_search_query,
    rule_based_flight_parser,
    rule_based_interval_parser,
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
        assert rule_based_flight_parser("Хочу полететь в Алматы") is None

    def test_all_asian_and_kazakhstan_cities_coverage(self) -> None:
        """Verify thorough coverage for all required Asian hubs and Kazakhstan cities."""
        assert CITY_TO_IATA["чэнду"] == "CTU"
        assert CITY_TO_IATA["ченду"] == "CTU"
        assert CITY_TO_IATA["chengdu"] == "CTU"
        assert CITY_TO_IATA["пекин"] == "PEK"
        assert CITY_TO_IATA["beijing"] == "PEK"
        assert CITY_TO_IATA["сеул"] == "ICN"
        assert CITY_TO_IATA["seoul"] == "ICN"
        assert CITY_TO_IATA["пхукет"] == "HKT"
        assert CITY_TO_IATA["phuket"] == "HKT"
        assert CITY_TO_IATA["гуанчжоу"] == "CAN"
        assert CITY_TO_IATA["guangzhou"] == "CAN"
        assert CITY_TO_IATA["шанхай"] == "PVG"
        assert CITY_TO_IATA["shanghai"] == "PVG"
        assert CITY_TO_IATA["бангкок"] == "BKK"
        assert CITY_TO_IATA["bangkok"] == "BKK"
        assert CITY_TO_IATA["дубай"] == "DXB"
        assert CITY_TO_IATA["dubai"] == "DXB"
        assert CITY_TO_IATA["стамбул"] == "IST"
        assert CITY_TO_IATA["istanbul"] == "IST"
        assert CITY_TO_IATA["ташкент"] == "TAS"
        assert CITY_TO_IATA["tashkent"] == "TAS"
        assert CITY_TO_IATA["бишкек"] == "FRU"
        assert CITY_TO_IATA["bishkek"] == "FRU"
        assert CITY_TO_IATA["тбилиси"] == "TBS"
        assert CITY_TO_IATA["tbilisi"] == "TBS"
        assert CITY_TO_IATA["анталья"] == "AYT"
        assert CITY_TO_IATA["antalya"] == "AYT"
        assert CITY_TO_IATA["доха"] == "DOH"
        assert CITY_TO_IATA["doha"] == "DOH"
        assert CITY_TO_IATA["абу-даби"] == "AUH"
        assert CITY_TO_IATA["абу даби"] == "AUH"
        assert CITY_TO_IATA["abu dhabi"] == "AUH"
        assert CITY_TO_IATA["санья"] == "SYX"
        assert CITY_TO_IATA["sanya"] == "SYX"

        assert CITY_TO_IATA["алматы"] == "ALA"
        assert CITY_TO_IATA["астана"] == "NQZ"
        assert CITY_TO_IATA["шымкент"] == "CIT"
        assert CITY_TO_IATA["актау"] == "SCO"
        assert CITY_TO_IATA["атырау"] == "GUW"
        assert CITY_TO_IATA["актобе"] == "AKX"
        assert CITY_TO_IATA["усть-каменогорск"] == "UKK"
        assert CITY_TO_IATA["оскемен"] == "UKK"
        assert CITY_TO_IATA["костанай"] == "KSG"
        assert CITY_TO_IATA["павлодар"] == "PWQ"
        assert CITY_TO_IATA["семей"] == "PLX"
        assert CITY_TO_IATA["тараз"] == "DMB"
        assert CITY_TO_IATA["кокшетау"] == "KOV"
        assert CITY_TO_IATA["балхаш"] == "BXH"
        assert CITY_TO_IATA["уральск"] == "URA"
        assert CITY_TO_IATA["караганда"] == "KGF"
        assert CITY_TO_IATA["петропавловск"] == "PPK"
        assert CITY_TO_IATA["кызылорда"] == "KZO"
        assert CITY_TO_IATA["туркестан"] == "HSA"
        assert CITY_TO_IATA["талдыкорган"] == "TDK"
        assert CITY_TO_IATA["жезказган"] == "DZN"

    @pytest.mark.asyncio
    async def test_parse_search_query_rule_based(self) -> None:
        """Test parse_search_query with various routes and dates."""
        base = date(2026, 9, 1)

        # 1. Asian hub query without price
        intent1 = await parse_search_query("Алматы - Пхукет на 25 декабря 2026", base_date=base)
        assert intent1 is not None
        assert intent1.origin == "ALA"
        assert intent1.destination == "HKT"
        assert intent1.date == "2026-12-25"
        assert intent1.direct_only is True

        # 2. Asian hub query with reverse order
        intent2 = await parse_search_query("В Сеул из Астаны на 2026-11-20", base_date=base)
        assert intent2 is not None
        assert intent2.origin == "NQZ"
        assert intent2.destination == "ICN"
        assert intent2.date == "2026-11-20"

        # 3. Multi-word city name: Абу-Даби
        intent3 = await parse_search_query("из Алматы в Абу-Даби 15 октября", base_date=base)
        assert intent3 is not None
        assert intent3.origin == "ALA"
        assert intent3.destination == "AUH"
        assert intent3.date == "2026-10-15"

        # 4. Incomplete query returns None
        assert await parse_search_query("Хочу полететь на море", base_date=base) is None

    @pytest.mark.asyncio
    async def test_parse_search_query_with_mocked_groq(self) -> None:
        """Verify parse_search_query uses Groq LLM when configured."""
        mock_payload = {
            "origin": "ALA",
            "destination": "CTU",
            "date": "2026-11-21",
            "flight_number": "CA-484",
            "direct_only": True,
            "target_price": None,
            "currency_detected": None,
            "original_price": None,
            "interval_minutes": 5,
            "confidence": 0.99,
            "raw_explanation": "Extracted flight from Almaty to Chengdu",
        }

        mock_response = MagicMock()
        mock_choice = MagicMock()
        mock_choice.message.content = json.dumps(mock_payload)
        mock_response.choices = [mock_choice]

        mock_client = MagicMock()
        mock_client.chat.completions.create = AsyncMock(return_value=mock_response)

        with patch("groq.AsyncGroq", return_value=mock_client):
            intent = await parse_search_query(
                text="Рейс Алматы в Чэнду на 21 ноября",
                api_key="gsk_test_mock_key_1234567890",
            )
            assert intent is not None
            assert intent.origin == "ALA"
            assert intent.destination == "CTU"
            assert intent.date == "2026-11-21"
            assert intent.flight_number == "CA-484"

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
    async def test_parse_interval_nlp_variations(self) -> None:
        """Verify parse_interval_nlp and rule_based_interval_parser for standard intervals."""
        assert await parse_interval_nlp("каждые 10 минут") == 10
        assert await parse_interval_nlp("раз в час") == 60
        assert await parse_interval_nlp("каждый час") == 60
        assert await parse_interval_nlp("раз в полчаса") == 30
        assert await parse_interval_nlp("полчаса") == 30
        assert await parse_interval_nlp("каждые 2 часа") == 120
        assert await parse_interval_nlp("2 часа") == 120
        assert await parse_interval_nlp("раз в сутки") == 1440
        assert await parse_interval_nlp("каждый день") == 1440
        assert await parse_interval_nlp("15 минут") == 15
        assert await parse_interval_nlp("30 мин") == 30
        assert await parse_interval_nlp("10") == 10
        assert await parse_interval_nlp("стандартно") == 5
        assert await parse_interval_nlp("") == 5

    @pytest.mark.asyncio
    async def test_parse_interval_nlp_with_mocked_groq(self) -> None:
        """Verify parse_interval_nlp uses Groq LLM when configured."""
        mock_payload = {"interval_minutes": 15}

        mock_response = MagicMock()
        mock_choice = MagicMock()
        mock_choice.message.content = json.dumps(mock_payload)
        mock_response.choices = [mock_choice]

        mock_client = MagicMock()
        mock_client.chat.completions.create = AsyncMock(return_value=mock_response)

        with patch("groq.AsyncGroq", return_value=mock_client):
            val = await parse_interval_nlp(
                text="проверять каждые 15 минут",
                api_key="gsk_test_mock_key_1234567890",
            )
            assert val == 15

    def test_models_helper_properties(self) -> None:
        """Verify helper properties on FlightOffer and ParsedFlightIntent."""
        offer = FlightOffer(
            airline="Air Astana",
            flight_number="KC-871",
            origin="ALA",
            destination="BKK",
            departure_time="01:20",
            arrival_time="08:50",
            price_kzt=142000.0,
            transfers_count=0,
        )
        assert offer.is_direct is True
        assert offer.route == "ALA -> BKK"
        assert "142 000 ₸" in offer.formatted_price

        intent = ParsedFlightIntent(
            origin="ALA",
            destination="CTU",
            date="2026-11-21",
            target_price=75000.0,
        )
        assert intent.route == "ALA -> CTU"
        assert "75 000 ₸" in intent.formatted_target_price

        intent_no_price = ParsedFlightIntent(
            origin="ALA",
            destination="CTU",
            date="2026-11-21",
        )
        assert intent_no_price.formatted_target_price == "Автоматически"


@pytest.mark.asyncio
class TestDatabaseCustomIntervalsAndMigration:
    """Test suite for Database migrations and Custom Intervals."""

    async def test_automated_migration_on_old_schema(self, tmp_path: Any) -> None:
        """Test init_db automatically runs ALTER TABLE if interval_minutes is missing."""
        db_file = str(tmp_path / "legacy.db")

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

        await init_db(db_file)

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

        t1_id = await dao.add_task(
            chat_id=101,
            origin="ALA",
            destination="NQZ",
            date="2026-10-15",
            target_price=25000.0,
            interval_minutes=5,
        )

        t2_id = await dao.add_task(
            chat_id=102,
            origin="SCO",
            destination="DXB",
            date="2026-12-25",
            target_price=80000.0,
            interval_minutes=10,
        )

        due_tasks = await dao.get_due_tasks()
        assert len(due_tasks) == 2
        assert {t["id"] for t in due_tasks} == {t1_id, t2_id}

        await dao.update_task_last_check(t1_id, last_price=24000.0)
        await dao.update_task_last_check(t2_id, last_price=75000.0)

        due_tasks_after = await dao.get_due_tasks()
        assert len(due_tasks_after) == 0

        async with get_db(db_file) as conn:
            await conn.execute("UPDATE tasks SET last_checked_at = datetime('now', '-6 minutes')")
            await conn.commit()

        due_after_6min = await dao.get_due_tasks()
        assert len(due_after_6min) == 1
        assert due_after_6min[0]["id"] == t1_id

        async with get_db(db_file) as conn:
            await conn.execute("UPDATE tasks SET last_checked_at = datetime('now', '-12 minutes')")
            await conn.commit()

        due_after_12min = await dao.get_due_tasks()
        assert len(due_after_12min) == 2


@pytest.mark.asyncio
class TestTelegramBotFSMAndLivePreviewFlow:
    """Test suite for 2-step FSM, Live Preview, Typed Callbacks, and State Transitions."""

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
        """Test start_new_snipe_fsm callback transitions state to waiting_for_search_query."""
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
        assert state.set_state.call_args[0][0] == SniperStates.waiting_for_search_query
        assert callback.answer.called
        assert callback_msg.answer.called
        assert "Куда и когда вы планируете лететь?" in callback_msg.answer.call_args[0][0]

    async def test_search_query_step1_with_offers(self) -> None:
        """Test Step 1: Live flight search in waiting_for_search_query renders flight list buttons."""
        status_msg = MagicMock()
        status_msg.edit_text = AsyncMock()

        message = MagicMock()
        message.text = "Алматы - Чэнду 21 ноября"
        message.chat.id = 777
        message.bot = MagicMock()
        message.bot.send_chat_action = AsyncMock()
        message.answer = AsyncMock(return_value=status_msg)

        state_data: Dict[str, Any] = {}

        async def mock_update_data(**kwargs: Any) -> None:
            state_data.update(kwargs)

        state = AsyncMock()
        state.update_data = AsyncMock(side_effect=mock_update_data)
        state.get_data = AsyncMock(return_value=state_data)

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
            await handle_search_query_message(message, state)

        assert message.answer.called
        assert message.answer.call_args[0][0] == "⏳ Выполняю Live-поиск рейсов..."
        assert state.update_data.called
        assert state_data["origin"] == "ALA"
        assert state_data["destination"] == "CTU"
        assert len(state_data["offers"]) == 2

        assert status_msg.edit_text.called
        edit_args = status_msg.edit_text.call_args
        card_text = edit_args[0][0]
        reply_markup = edit_args[1].get("reply_markup")

        assert "Найдено рейсов (2)" in card_text
        assert "ALA" in card_text
        assert "CTU" in card_text
        assert reply_markup is not None
        # 2 flight buttons + 1 cancel button
        assert len(reply_markup.inline_keyboard) == 3
        assert "fl_sel:0" in reply_markup.inline_keyboard[0][0].callback_data
        assert "fl_sel:1" in reply_markup.inline_keyboard[1][0].callback_data
        assert "fl_canc" in reply_markup.inline_keyboard[2][0].callback_data

    async def test_search_query_step1_no_offers(self) -> None:
        """Test Step 1: When no flights found, displays alert and cancel button."""
        status_msg = MagicMock()
        status_msg.edit_text = AsyncMock()

        message = MagicMock()
        message.text = "Астана - Шымкент 2026-11-01"
        message.chat.id = 777
        message.bot = MagicMock()
        message.bot.send_chat_action = AsyncMock()
        message.answer = AsyncMock(return_value=status_msg)

        state = AsyncMock()
        state.update_data = AsyncMock()

        with patch("backend.bot.handlers.AviataProvider.search", new=AsyncMock(return_value=[])):
            await handle_search_query_message(message, state)

        assert status_msg.edit_text.called
        edit_args = status_msg.edit_text.call_args
        card_text = edit_args[0][0]
        reply_markup = edit_args[1].get("reply_markup")

        assert "билетов в свободной продаже не обнаружено" in card_text
        assert reply_markup is not None
        assert "fl_canc" in reply_markup.inline_keyboard[0][0].callback_data

    async def test_search_query_step1_unrecognized(self) -> None:
        """Test Step 1: Unrecognized query prompts retry without crashing."""
        status_msg = MagicMock()
        status_msg.edit_text = AsyncMock()

        message = MagicMock()
        message.text = "Привет бот"
        message.chat.id = 777
        message.bot = MagicMock()
        message.bot.send_chat_action = AsyncMock()
        message.answer = AsyncMock(return_value=status_msg)

        state = AsyncMock()
        state.update_data = AsyncMock()

        await handle_search_query_message(message, state)

        assert status_msg.edit_text.called
        card_text = status_msg.edit_text.call_args[0][0]
        assert "Не удалось распознать маршрут и дату рейса" in card_text

    async def test_flight_select_step2(self) -> None:
        """Test Step 2: FlightSelectCallback displays detailed flight card with action buttons."""
        mock_offers = [
            {
                "airline": "Air China",
                "flight_number": "CA-484",
                "origin": "ALA",
                "destination": "CTU",
                "departure_time": "10:00",
                "arrival_time": "17:00",
                "price_kzt": 78500.0,
                "transfers_count": 0,
            }
        ]

        state_data: Dict[str, Any] = {"offers": mock_offers, "origin": "ALA", "destination": "CTU"}

        async def mock_update_data(**kwargs: Any) -> None:
            state_data.update(kwargs)

        state = AsyncMock()
        state.get_data = AsyncMock(return_value=state_data)
        state.update_data = AsyncMock(side_effect=mock_update_data)

        cb_msg = MagicMock()
        cb_msg.edit_text = AsyncMock()

        callback = MagicMock()
        callback.message = cb_msg
        callback.answer = AsyncMock()

        cb_data = FlightSelectCallback(flight_idx=0)
        await handle_flight_select_callback(callback, cb_data, state)

        assert state.update_data.called
        assert state_data["selected_flight_idx"] == 0
        assert state_data["selected_flight"] == mock_offers[0]

        assert callback.answer.called
        assert cb_msg.edit_text.called
        edit_args = cb_msg.edit_text.call_args
        card_text = edit_args[0][0]
        reply_markup = edit_args[1].get("reply_markup")

        assert "Детали выбранного рейса" in card_text
        assert "Air China" in card_text
        assert "CA-484" in card_text
        assert "78 500 ₸" in card_text
        assert "Прямой ⚡" in card_text

        assert reply_markup is not None
        assert "fl_mon:0" in reply_markup.inline_keyboard[0][0].callback_data
        assert "fl_back:flights" in reply_markup.inline_keyboard[1][0].callback_data

    async def test_monitor_flight_step3_transition(self) -> None:
        """Test Step 3: MonitorFlightCallback transitions state to waiting_for_interval."""
        state = AsyncMock()
        state.set_state = AsyncMock()

        cb_msg = MagicMock()
        cb_msg.edit_text = AsyncMock()

        callback = MagicMock()
        callback.message = cb_msg
        callback.answer = AsyncMock()

        cb_data = MonitorFlightCallback(flight_idx=0)
        await handle_monitor_flight_callback(callback, cb_data, state)

        assert state.set_state.called
        assert state.set_state.call_args[0][0] == SniperStates.waiting_for_interval
        assert callback.answer.called
        assert cb_msg.edit_text.called

        edit_args = cb_msg.edit_text.call_args
        prompt_text = edit_args[0][0]
        reply_markup = edit_args[1].get("reply_markup")

        assert "Настройка интервала проверки" in prompt_text
        assert reply_markup is not None
        # Quick interval buttons
        assert "fl_int:5" in reply_markup.inline_keyboard[0][0].callback_data
        assert "fl_int:10" in reply_markup.inline_keyboard[0][1].callback_data
        assert "fl_int:30" in reply_markup.inline_keyboard[0][2].callback_data
        assert "fl_int:60" in reply_markup.inline_keyboard[0][3].callback_data
        assert "fl_back:flights" in reply_markup.inline_keyboard[1][0].callback_data

    async def test_quick_interval_selection_step3(self) -> None:
        """Test Step 3: QuickIntervalCallback sets target price and shows confirmation card."""
        selected_flight = {
            "airline": "Air China",
            "flight_number": "CA-484",
            "origin": "ALA",
            "destination": "CTU",
            "price_kzt": 78500.0,
            "transfers_count": 0,
        }
        state_data: Dict[str, Any] = {
            "origin": "ALA",
            "destination": "CTU",
            "date": "2026-11-21",
            "selected_flight": selected_flight,
        }

        async def mock_update_data(**kwargs: Any) -> None:
            state_data.update(kwargs)

        state = AsyncMock()
        state.get_data = AsyncMock(return_value=state_data)
        state.update_data = AsyncMock(side_effect=mock_update_data)

        cb_msg = MagicMock()
        cb_msg.edit_text = AsyncMock()

        callback = MagicMock()
        callback.message = cb_msg
        callback.answer = AsyncMock()

        cb_data = QuickIntervalCallback(minutes=10)
        await handle_quick_interval_callback(callback, cb_data, state)

        assert state.update_data.called
        assert state_data["interval_minutes"] == 10
        assert state_data["target_price"] == 78500.0

        assert callback.answer.called
        assert cb_msg.edit_text.called

        edit_args = cb_msg.edit_text.call_args
        card_text = edit_args[0][0]
        reply_markup = edit_args[1].get("reply_markup")

        assert "Подтверждение параметров мониторинга" in card_text
        assert "ALA" in card_text
        assert "CTU" in card_text
        assert "78 500 ₸" in card_text
        assert "Каждые 10 мин" in card_text

        assert reply_markup is not None
        assert "fl_back:interval" in reply_markup.inline_keyboard[0][0].callback_data
        assert "fl_conf" in reply_markup.inline_keyboard[0][1].callback_data
        assert "fl_canc" in reply_markup.inline_keyboard[0][2].callback_data

    async def test_interval_text_message_step3(self) -> None:
        """Test Step 3: Natural language text interval in waiting_for_interval shows confirmation."""
        selected_flight = {
            "airline": "SCAT",
            "flight_number": "DV-713",
            "origin": "ALA",
            "destination": "NQZ",
            "price_kzt": 21000.0,
            "transfers_count": 0,
        }
        state_data: Dict[str, Any] = {
            "origin": "ALA",
            "destination": "NQZ",
            "date": "2026-10-15",
            "selected_flight": selected_flight,
        }

        async def mock_update_data(**kwargs: Any) -> None:
            state_data.update(kwargs)

        state = AsyncMock()
        state.get_data = AsyncMock(return_value=state_data)
        state.update_data = AsyncMock(side_effect=mock_update_data)

        message = MagicMock()
        message.text = "каждые 15 минут"
        message.answer = AsyncMock()

        await handle_interval_text_message(message, state)

        assert state.update_data.called
        assert state_data["interval_minutes"] == 15
        assert state_data["target_price"] == 21000.0

        assert message.answer.called
        call_args = message.answer.call_args
        card_text = call_args[0][0]
        reply_markup = call_args[1].get("reply_markup")

        assert "Подтверждение параметров мониторинга" in card_text
        assert "DV-713" in card_text
        assert "21 000 ₸" in card_text
        assert "Каждые 15 мин" in card_text

        assert reply_markup is not None
        assert "fl_conf" in reply_markup.inline_keyboard[0][1].callback_data

    async def test_confirm_snipe_step4(self, tmp_path: Any) -> None:
        """Test Step 4: ConfirmSnipeCallback inserts task into DB, clears FSM, and displays success card."""
        db_file = str(tmp_path / "bot_fsm.db")
        await init_db(db_file)

        selected_flight = {
            "airline": "Air Astana",
            "flight_number": "KC-871",
            "origin": "ALA",
            "destination": "BKK",
            "price_kzt": 142000.0,
            "transfers_count": 0,
        }
        state_data: Dict[str, Any] = {
            "origin": "ALA",
            "destination": "BKK",
            "date": "2026-10-15",
            "selected_flight": selected_flight,
            "target_price": 142000.0,
            "interval_minutes": 10,
        }

        state = AsyncMock()
        state.get_data = AsyncMock(return_value=state_data)
        state.clear = AsyncMock()

        cb_msg = MagicMock()
        cb_msg.chat = MagicMock()
        cb_msg.chat.id = 8888
        cb_msg.edit_text = AsyncMock()

        callback = MagicMock()
        callback.message = cb_msg
        callback.answer = AsyncMock()

        with patch("backend.bot.handlers.dao", FlightSniperDAO(db_path=db_file)):
            cb_data = ConfirmSnipeCallback()
            await handle_confirm_snipe_callback(callback, cb_data, state)

            assert state.clear.called
            assert callback.answer.called
            assert cb_msg.edit_text.called

            edit_args = cb_msg.edit_text.call_args
            card_text = edit_args[0][0]

            assert "Снайпер активирован!" in card_text
            assert "ALA" in card_text
            assert "BKK" in card_text
            assert "KC-871" in card_text
            assert "142 000 ₸" in card_text
            assert "Каждые 10 мин" in card_text

            # Verify in DB
            test_dao = FlightSniperDAO(db_path=db_file)
            tasks = await test_dao.get_user_tasks(chat_id=8888)
            assert len(tasks) == 1
            assert tasks[0]["origin"] == "ALA"
            assert tasks[0]["destination"] == "BKK"
            assert tasks[0]["flight_number"] == "KC-871"
            assert tasks[0]["target_price"] == 142000.0
            assert tasks[0]["interval_minutes"] == 10

    async def test_step_back_navigation(self) -> None:
        """Test StepBackCallback navigation to flights and interval steps."""
        mock_offers = [
            {
                "airline": "Air China",
                "flight_number": "CA-484",
                "origin": "ALA",
                "destination": "CTU",
                "price_kzt": 78500.0,
                "transfers_count": 0,
            }
        ]
        state_data: Dict[str, Any] = {
            "origin": "ALA",
            "destination": "CTU",
            "date": "2026-11-21",
            "offers": mock_offers,
        }

        state = AsyncMock()
        state.get_data = AsyncMock(return_value=state_data)
        state.set_state = AsyncMock()

        cb_msg = MagicMock()
        cb_msg.edit_text = AsyncMock()

        callback = MagicMock()
        callback.message = cb_msg
        callback.answer = AsyncMock()

        # 1. Back to flights
        back_flights = StepBackCallback(to_step="flights")
        await handle_step_back_callback(callback, back_flights, state)

        assert state.set_state.called
        assert state.set_state.call_args[0][0] == SniperStates.waiting_for_search_query
        assert cb_msg.edit_text.called
        assert "Найдено рейсов (1)" in cb_msg.edit_text.call_args[0][0]

        # 2. Back to interval
        state.set_state.reset_mock()
        cb_msg.edit_text.reset_mock()

        back_interval = StepBackCallback(to_step="interval")
        await handle_step_back_callback(callback, back_interval, state)

        assert state.set_state.called
        assert state.set_state.call_args[0][0] == SniperStates.waiting_for_interval
        assert "Настройка интервала проверки" in cb_msg.edit_text.call_args[0][0]

    async def test_cancel_snipe_callback(self) -> None:
        """Test CancelSnipeCallback clears FSM state and edits message."""
        state = AsyncMock()
        state.clear = AsyncMock()

        cb_msg = MagicMock()
        cb_msg.edit_text = AsyncMock()

        callback = MagicMock()
        callback.message = cb_msg
        callback.answer = AsyncMock()

        cb_data = CancelSnipeCallback()
        await handle_cancel_snipe_callback(callback, cb_data, state)

        assert state.clear.called
        assert callback.answer.called
        assert cb_msg.edit_text.called
        assert "Создание задачи отменено" in cb_msg.edit_text.call_args[0][0]
