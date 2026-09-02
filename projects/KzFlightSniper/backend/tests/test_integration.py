"""Comprehensive End-to-End Integration Test Suite for KzFlightSniper.

Tests cover:
1. End-to-End Sniper Pipeline & Lifecycle:
   - Database schema initialization
   - Multiple monitoring task creation via DAO
   - Execution of SniperWorker.run_check_cycle with mocked flight provider
   - Verification of Telegram bot notification dispatch (HTML payload, savings, deep links)
   - Verification of alerts_history records creation
   - Verification of tasks table updates (last_checked_at, last_price)
   - Alert deduplication window suppression
2. FastAPI REST Endpoints against live database:
   - GET / (Service information)
   - GET /health (Healthcheck and active task counting)
   - GET /api/tasks (Active task listing and Pydantic serialization)
   - POST /api/check-now (Manual flight check cycle trigger)
3. Telegram Bot Handlers Pipeline:
   - /start and /help command handlers
   - /snipe command argument validation and task insertion
   - /list active task display
   - /delete task deletion and validation
4. FastAPI Application Lifespan Lifecycle:
   - Startup and graceful shutdown management
"""

import asyncio
from datetime import datetime, timezone
import os
import shutil
import tempfile
import unittest
from unittest.mock import AsyncMock, MagicMock, patch
from typing import Any, Dict, List, Optional
from fastapi.testclient import TestClient
from aiogram.filters import CommandObject
from aiogram.types import Message

import backend.main as main_module
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
    handle_delete,
    handle_flight_select_callback,
    handle_help,
    handle_interval_text_message,
    handle_list,
    handle_monitor_flight_callback,
    handle_nlp_message,
    handle_quick_interval_callback,
    handle_search_query_message,
    handle_snipe,
    handle_start,
    handle_start_new_snipe_fsm_callback,
    handle_step_back_callback,
    parse_snipe_arguments,
)
from backend.core.config import Settings, get_settings
from backend.core.models import FlightOffer, HealthResponse, TaskRead
from backend.db.dao import FlightSniperDAO
from backend.db.database import get_db, init_db
from backend.engine.scheduler import get_scheduler, stop_scheduler
from backend.engine.sniper_worker import SniperWorker, format_alert_message, run_sniper_check
from backend.providers.base import BaseFlightProvider


class MockFlightProvider(BaseFlightProvider):
    """Mock flight provider returning deterministic flight offers for testing."""

    def __init__(self, offers: Optional[List[FlightOffer]] = None) -> None:
        self._offers = offers or []

    @property
    def provider_name(self) -> str:
        return "mock_provider"

    def set_offers(self, offers: List[FlightOffer]) -> None:
        self._offers = offers

    async def search_flights(
        self,
        origin: str,
        destination: str,
        date: str,
        max_transfers: int = 0,
        direct_only: bool = False,
    ) -> List[FlightOffer]:
        filtered = []
        for o in self._offers:
            if o.origin.upper() == origin.upper() and o.destination.upper() == destination.upper():
                if (direct_only or max_transfers == 0) and o.transfers_count > 0:
                    continue
                if max_transfers > 0 and o.transfers_count > max_transfers:
                    continue
                filtered.append(o)
        return filtered


class TestKzFlightSniperIntegration(unittest.TestCase):
    """Full integration test suite for KzFlightSniper."""

    def setUp(self) -> None:
        """Create an isolated temporary SQLite database for testing."""
        self.test_dir = tempfile.mkdtemp()
        self.db_path = os.path.join(self.test_dir, "test_integration.db")
        self.dao = FlightSniperDAO(db_path=self.db_path)

        # Patch main module dao to use test database
        self.patcher_main_dao = patch.object(main_module, "dao", self.dao)
        self.patcher_main_dao.start()

        # Patch bot.handlers dao to use test database
        import backend.bot.handlers as handlers_module
        self.patcher_handlers_dao = patch.object(handlers_module, "dao", self.dao)
        self.patcher_handlers_dao.start()

    def tearDown(self) -> None:
        """Clean up patches, schedulers, and temporary test files."""
        self.patcher_main_dao.stop()
        self.patcher_handlers_dao.stop()
        stop_scheduler()
        shutil.rmtree(self.test_dir, ignore_errors=True)

    def test_e2e_sniper_check_cycle_and_alert_dispatch(self) -> None:
        """Test complete sniper check cycle: task evaluation, alert dispatch, and DB state."""
        async def _run() -> None:
            # 1. Initialize DB Schema
            await init_db(self.db_path)

            # 2. Create multiple sniper tasks
            # Task 1: User 101 wants ALA -> NQZ on 2026-10-15 under 25000 KZT (Direct)
            t1_id = await self.dao.add_task(
                chat_id=101,
                origin="ALA",
                destination="NQZ",
                date="2026-10-15",
                target_price=25000.0,
            )

            # Task 2: User 102 wants ALA -> CIT on 2026-10-20 under 18000 KZT for specific flight KC-871
            t2_id = await self.dao.add_task(
                chat_id=102,
                origin="ALA",
                destination="CIT",
                date="2026-10-20",
                target_price=18000.0,
                flight_number="KC-871",
            )

            # Task 3: User 103 wants NQZ -> SCO on 2026-11-05 under 30000 KZT (Price will be too high)
            t3_id = await self.dao.add_task(
                chat_id=103,
                origin="NQZ",
                destination="SCO",
                date="2026-11-05",
                target_price=30000.0,
            )

            # 3. Setup mock flight provider with test flight inventory
            mock_offers = [
                # Matching Task 1: Price 21500 <= 25000 -> Should alert!
                FlightOffer(
                    provider="aviata",
                    airline="Air Astana",
                    flight_number="KC-853",
                    origin="ALA",
                    destination="NQZ",
                    departure_time="08:00",
                    arrival_time="09:40",
                    price_kzt=21500.0,
                    transfers_count=0,
                    duration_minutes=100,
                    deep_link="https://aviata.kz/search/ALANQZ20261015100E/",
                ),
                # Matching Task 2 (Flight KC-871): Price 17500 <= 18000 -> Should alert!
                FlightOffer(
                    provider="aviata",
                    airline="Air Astana",
                    flight_number="KC-871",
                    origin="ALA",
                    destination="CIT",
                    departure_time="10:30",
                    arrival_time="11:50",
                    price_kzt=17500.0,
                    transfers_count=0,
                    duration_minutes=80,
                    deep_link="https://aviata.kz/search/ALACIT20261020100E/",
                ),
                # Non-matching flight for Task 2 (different flight number DV-701 with lower price)
                FlightOffer(
                    provider="aviata",
                    airline="SCAT",
                    flight_number="DV-701",
                    origin="ALA",
                    destination="CIT",
                    departure_time="06:00",
                    arrival_time="07:20",
                    price_kzt=16000.0,
                    transfers_count=0,
                    duration_minutes=80,
                ),
                # Matching Task 3: Price 35000 > 30000 target -> No alert
                FlightOffer(
                    provider="aviata",
                    airline="Qazaq Air",
                    flight_number="IQ-401",
                    origin="NQZ",
                    destination="SCO",
                    departure_time="14:00",
                    arrival_time="16:30",
                    price_kzt=35000.0,
                    transfers_count=0,
                    duration_minutes=150,
                ),
            ]
            provider = MockFlightProvider(mock_offers)

            # 4. Setup mock Telegram bot
            mock_bot = MagicMock()
            mock_bot.send_message = AsyncMock()

            # 5. Execute SniperWorker.run_check_cycle
            worker = SniperWorker(bot=mock_bot, provider=provider, dao=self.dao)
            stats = await worker.run_check_cycle()

            # Assert stats
            self.assertEqual(stats["tasks_checked"], 3)
            self.assertEqual(stats["alerts_triggered"], 2)
            self.assertEqual(stats["errors"], 0)

            # Assert Telegram bot notifications dispatched
            self.assertEqual(mock_bot.send_message.call_count, 2)

            # Check Alert 1 (User 101, ALA->NQZ)
            call_1 = mock_bot.send_message.call_args_list[0][1]
            self.assertEqual(call_1["chat_id"], 101)
            self.assertIn("🎯 <b>KZ FLIGHT SNIPER — ЦЕЛЬ ОБНАРУЖЕНА!</b>", call_1["text"])
            self.assertIn("ALA", call_1["text"])
            self.assertIn("NQZ", call_1["text"])
            self.assertIn("KC-853", call_1["text"])
            self.assertIn("21 500 ₸", call_1["text"])
            self.assertIn("25 000 ₸", call_1["text"])
            self.assertIn("3 500 ₸", call_1["text"])  # Savings: 25000 - 21500
            self.assertIn("https://aviata.kz/search/ALANQZ20261015100E/", call_1["text"])

            # Check Alert 2 (User 102, ALA->CIT KC-871)
            call_2 = mock_bot.send_message.call_args_list[1][1]
            self.assertEqual(call_2["chat_id"], 102)
            self.assertIn("KC-871", call_2["text"])
            self.assertIn("17 500 ₸", call_2["text"])
            self.assertIn("18 000 ₸", call_2["text"])
            self.assertIn("500 ₸", call_2["text"])  # Savings: 18000 - 17500
            self.assertIn("https://aviata.kz/search/ALACIT20261020100E/", call_2["text"])

            # 6. Assert alerts_history table records
            async with get_db(self.db_path) as conn:
                cursor = await conn.execute("SELECT * FROM alerts_history ORDER BY id ASC")
                alerts = [dict(r) for r in await cursor.fetchall()]
                self.assertEqual(len(alerts), 2)

                self.assertEqual(alerts[0]["task_id"], t1_id)
                self.assertEqual(alerts[0]["flight_number"], "KC-853")
                self.assertEqual(alerts[0]["price"], 21500.0)

                self.assertEqual(alerts[1]["task_id"], t2_id)
                self.assertEqual(alerts[1]["flight_number"], "KC-871")
                self.assertEqual(alerts[1]["price"], 17500.0)

            # 7. Assert tasks table state updates
            task1 = await self.dao.get_task_by_id(t1_id)
            self.assertEqual(task1["last_price"], 21500.0)
            self.assertIsNotNone(task1["last_checked_at"])

            task2 = await self.dao.get_task_by_id(t2_id)
            self.assertEqual(task2["last_price"], 17500.0)
            self.assertIsNotNone(task2["last_checked_at"])

            task3 = await self.dao.get_task_by_id(t3_id)
            self.assertEqual(task3["last_price"], 35000.0)
            self.assertIsNotNone(task3["last_checked_at"])

            # 8. Test Deduplication on subsequent cycle: no new alerts sent
            stats_cycle2 = await worker.run_check_cycle()
            self.assertEqual(stats_cycle2["tasks_checked"], 3)
            self.assertEqual(stats_cycle2["alerts_triggered"], 0)
            self.assertEqual(mock_bot.send_message.call_count, 2)  # Still 2, no extra call

        asyncio.run(_run())

    def test_fastapi_rest_endpoints_live_db(self) -> None:
        """Test FastAPI REST endpoints against the live SQLite test database."""
        async def _seed_data() -> None:
            await init_db(self.db_path)
            await self.dao.add_task(
                chat_id=888,
                origin="ALA",
                destination="NQZ",
                date="2026-10-15",
                target_price=25000.0,
            )
            await self.dao.add_task(
                chat_id=888,
                origin="CIT",
                destination="ALA",
                date="2026-11-01",
                target_price=18000.0,
            )

        asyncio.run(_seed_data())

        with TestClient(main_module.app) as client:
            # 1. Test GET / (Root Service Info)
            res_root = client.get("/")
            self.assertEqual(res_root.status_code, 200)
            data_root = res_root.json()
            self.assertEqual(data_root["app"], "KzFlightSniper")
            self.assertEqual(data_root["status"], "running")
            self.assertIn("version", data_root)

            # 2. Test GET /health (Live database connected, active task count = 2)
            res_health = client.get("/health")
            self.assertEqual(res_health.status_code, 200)
            data_health = res_health.json()
            self.assertEqual(data_health["status"], "ok")
            self.assertEqual(data_health["database"], "connected")
            self.assertEqual(data_health["active_tasks"], 2)

            # 3. Test GET /api/tasks (Retrieving list of active tasks)
            res_tasks = client.get("/api/tasks")
            self.assertEqual(res_tasks.status_code, 200)
            tasks_list = res_tasks.json()
            self.assertEqual(len(tasks_list), 2)
            self.assertEqual(tasks_list[0]["origin"], "ALA")
            self.assertEqual(tasks_list[0]["destination"], "NQZ")
            self.assertEqual(tasks_list[1]["origin"], "CIT")
            self.assertEqual(tasks_list[1]["destination"], "ALA")

            # 4. Test POST /api/check-now (Trigger manual flight check)
            with patch("backend.main.run_sniper_check", new=AsyncMock(return_value={
                "tasks_checked": 2,
                "alerts_triggered": 0,
                "errors": 0,
                "details": [],
            })):
                res_check = client.post("/api/check-now")
                self.assertEqual(res_check.status_code, 200)
                data_check = res_check.json()
                self.assertEqual(data_check["status"], "success")
                self.assertEqual(data_check["stats"]["tasks_checked"], 2)

    def test_bot_handlers_full_pipeline(self) -> None:
        """Test simulated Telegram bot interactions through the complete pipeline."""
        async def _run() -> None:
            await init_db(self.db_path)

            # Mock Telegram user chat
            mock_message = MagicMock(spec=Message)
            mock_message.chat = MagicMock()
            mock_message.chat.id = 55555
            mock_message.answer = AsyncMock()

            # 1. Test /start greeting
            await handle_start(mock_message)
            self.assertEqual(mock_message.answer.call_count, 1)
            start_reply = mock_message.answer.call_args[0][0]
            start_markup = mock_message.answer.call_args[1].get("reply_markup")
            self.assertIn("Добро пожаловать в KzFlightSniper", start_reply)
            self.assertIn("/list", start_reply)
            self.assertIsNotNone(start_markup)
            self.assertEqual(start_markup.inline_keyboard[0][0].callback_data, "start_new_snipe_fsm")

            mock_message.answer.reset_mock()

            # 2. Test /help command
            await handle_help(mock_message)
            self.assertEqual(mock_message.answer.call_count, 1)
            help_reply = mock_message.answer.call_args[0][0]
            self.assertIn("Справочник и команды KzFlightSniper", help_reply)
            self.assertIn("Основные коды аэропортов Казахстана", help_reply)

            mock_message.answer.reset_mock()

            # 3. Test /snipe without arguments shows guide
            empty_cmd = CommandObject(prefix="/", command="snipe", args=None)
            await handle_snipe(mock_message, empty_cmd)
            self.assertEqual(mock_message.answer.call_count, 1)
            guide_reply = mock_message.answer.call_args[0][0]
            self.assertIn("Создание мониторинга через команду", guide_reply)

            mock_message.answer.reset_mock()

            # 4. Test /snipe with invalid airport code
            invalid_cmd = CommandObject(prefix="/", command="snipe", args="ALMATY NQZ 2026-10-15 25000")
            await handle_snipe(mock_message, invalid_cmd)
            self.assertEqual(mock_message.answer.call_count, 1)
            err_reply = mock_message.answer.call_args[0][0]
            self.assertIn("Неверный код аэропорта вылета", err_reply)

            mock_message.answer.reset_mock()

            # 5. Test valid /snipe ALA NQZ 2026-10-15 25000
            valid_cmd = CommandObject(prefix="/", command="snipe", args="ALA NQZ 2026-10-15 25000")
            await handle_snipe(mock_message, valid_cmd)
            self.assertEqual(mock_message.answer.call_count, 1)
            snipe_reply = mock_message.answer.call_args[0][0]
            self.assertIn("Снайпер активирован!", snipe_reply)
            self.assertIn("ALA", snipe_reply)
            self.assertIn("NQZ", snipe_reply)
            self.assertIn("25 000 ₸", snipe_reply)

            mock_message.answer.reset_mock()

            # 6. Verify task created in database
            user_tasks = await self.dao.get_user_tasks(chat_id=55555)
            self.assertEqual(len(user_tasks), 1)
            created_task_id = user_tasks[0]["id"]
            self.assertEqual(user_tasks[0]["origin"], "ALA")
            self.assertEqual(user_tasks[0]["destination"], "NQZ")
            self.assertEqual(user_tasks[0]["target_price"], 25000.0)

            # 7. Test /list command
            await handle_list(mock_message)
            self.assertEqual(mock_message.answer.call_count, 1)
            list_reply = mock_message.answer.call_args[0][0]
            self.assertIn("Ваши активные мониторинги (1)", list_reply)
            self.assertIn(f"#{created_task_id}", list_reply)
            self.assertIn("<code>ALA</code> ✈️ <code>NQZ</code>", list_reply)

            mock_message.answer.reset_mock()

            # 8. Test /delete <task_id>
            del_cmd = CommandObject(prefix="/", command="delete", args=str(created_task_id))
            await handle_delete(mock_message, del_cmd)
            self.assertEqual(mock_message.answer.call_count, 1)
            del_reply = mock_message.answer.call_args[0][0]
            self.assertIn(f"Задача мониторинга #{created_task_id} удалена", del_reply)

            # 9. Verify task is removed in database
            tasks_after_del = await self.dao.get_user_tasks(chat_id=55555)
            self.assertEqual(len(tasks_after_del), 0)

            # 10. Test 2-step FSM & Live Preview flow
            # Step 1: User sends flight search text in waiting_for_search_query
            mock_status_msg = MagicMock()
            mock_status_msg.edit_text = AsyncMock()
            mock_nlp_msg = MagicMock(spec=Message)
            mock_nlp_msg.text = "Рейс Алматы - Бангкок 15 октября 2026"
            mock_nlp_msg.chat = MagicMock()
            mock_nlp_msg.chat.id = 55555
            mock_nlp_msg.bot = MagicMock()
            mock_nlp_msg.bot.send_chat_action = AsyncMock()
            mock_nlp_msg.answer = AsyncMock(return_value=mock_status_msg)

            fsm_data: Dict[str, Any] = {}

            async def _fsm_update(**kwargs: Any) -> None:
                fsm_data.update(kwargs)

            mock_state = AsyncMock()
            mock_state.get_data = AsyncMock(return_value=fsm_data)
            mock_state.update_data = AsyncMock(side_effect=_fsm_update)
            mock_state.set_state = AsyncMock()
            mock_state.clear = AsyncMock(side_effect=lambda: fsm_data.clear())

            mock_live_offers = [
                FlightOffer(
                    airline="Air Astana",
                    flight_number="KC-871",
                    origin="ALA",
                    destination="BKK",
                    departure_time="01:20",
                    arrival_time="08:50",
                    price_kzt=145000.0,
                    transfers_count=0,
                )
            ]

            with patch("backend.bot.handlers.AviasalesProvider.search", new=AsyncMock(return_value=mock_live_offers)):
                await handle_search_query_message(mock_nlp_msg, mock_state)

            self.assertTrue(mock_nlp_msg.bot.send_chat_action.called)
            self.assertEqual(mock_nlp_msg.answer.call_count, 1)
            self.assertEqual(mock_nlp_msg.answer.call_args[0][0], "⏳ Выполняю Live-поиск рейсов...")
            self.assertTrue(mock_status_msg.edit_text.called)
            list_card = mock_status_msg.edit_text.call_args[0][0]
            self.assertIn("ALA", list_card)
            self.assertIn("BKK", list_card)
            self.assertIn("Найдено рейсов (1)", list_card)
            self.assertEqual(fsm_data["origin"], "ALA")
            self.assertEqual(fsm_data["destination"], "BKK")

            # Step 2: User clicks FlightSelectCallback
            cb_msg = MagicMock()
            cb_msg.chat = MagicMock()
            cb_msg.chat.id = 55555
            cb_msg.edit_text = AsyncMock()

            cb_select = MagicMock()
            cb_select.message = cb_msg
            cb_select.answer = AsyncMock()

            await handle_flight_select_callback(cb_select, FlightSelectCallback(flight_idx=0), mock_state)
            self.assertTrue(cb_msg.edit_text.called)
            details_card = cb_msg.edit_text.call_args[0][0]
            self.assertIn("Детали выбранного рейса", details_card)
            self.assertIn("Air Astana", details_card)
            self.assertIn("KC-871", details_card)
            self.assertIn("145 000 ₸", details_card)

            # Step 3: User clicks MonitorFlightCallback -> transitions to waiting_for_interval
            cb_mon = MagicMock()
            cb_mon.message = cb_msg
            cb_mon.answer = AsyncMock()

            await handle_monitor_flight_callback(cb_mon, MonitorFlightCallback(flight_idx=0), mock_state)
            self.assertTrue(mock_state.set_state.called)
            self.assertEqual(mock_state.set_state.call_args[0][0], SniperStates.waiting_for_interval)

            # Step 3b: User selects QuickIntervalCallback(minutes=10)
            cb_int = MagicMock()
            cb_int.message = cb_msg
            cb_int.answer = AsyncMock()

            await handle_quick_interval_callback(cb_int, QuickIntervalCallback(minutes=10), mock_state)
            self.assertEqual(fsm_data["interval_minutes"], 10)
            self.assertEqual(fsm_data["target_price"], 145000.0)

            # Step 4: User clicks ConfirmSnipeCallback -> creates task in DB and clears state
            cb_conf = MagicMock()
            cb_conf.message = cb_msg
            cb_conf.answer = AsyncMock()

            await handle_confirm_snipe_callback(cb_conf, ConfirmSnipeCallback(), mock_state)
            self.assertTrue(mock_state.clear.called)
            self.assertTrue(cb_msg.edit_text.called)
            conf_reply = cb_msg.edit_text.call_args[0][0]
            self.assertIn("Снайпер активирован!", conf_reply)
            self.assertIn("KC-871", conf_reply)
            self.assertIn("145 000 ₸", conf_reply)

            # Verify task exists in database
            fsm_tasks = await self.dao.get_user_tasks(chat_id=55555)
            self.assertEqual(len(fsm_tasks), 1)
            self.assertEqual(fsm_tasks[0]["origin"], "ALA")
            self.assertEqual(fsm_tasks[0]["destination"], "BKK")
            self.assertEqual(fsm_tasks[0]["flight_number"], "KC-871")
            self.assertEqual(fsm_tasks[0]["target_price"], 145000.0)
            self.assertEqual(fsm_tasks[0]["interval_minutes"], 10)

        asyncio.run(_run())

    def test_app_lifespan_lifecycle(self) -> None:
        """Test FastAPI lifespan startup and shutdown context manager."""
        async def _run() -> None:
            # Test running lifespan context manager without bot token configured
            async with main_module.lifespan(main_module.app):
                # Verify scheduler started or initialized
                sched = get_scheduler()
                self.assertIsNotNone(sched)
                self.assertTrue(sched.running)

            # After exit, scheduler should be stopped
            self.assertIsNone(get_scheduler())

        asyncio.run(_run())


if __name__ == "__main__":
    unittest.main()
