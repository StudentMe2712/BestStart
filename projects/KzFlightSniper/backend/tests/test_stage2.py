"""Unit and integration tests for KzFlightSniper Stage 2 components.

Tests cover:
- Database initialization and schema creation
- DAO operations (add_task, get_active_tasks, get_user_tasks, delete_task, log_alert, check_recent_alert)
- /snipe command parsing and validation
- FastAPI endpoints (/health, /api/tasks, /)
"""

import asyncio
from datetime import datetime, date, timezone
import os
import shutil
import tempfile
import unittest
from fastapi.testclient import TestClient

from backend.bot.handlers import parse_snipe_arguments
from backend.core.config import Settings, get_settings
from backend.core.models import AlertRead, FlightOffer, TaskCreate, TaskRead
from backend.db.dao import FlightSniperDAO
from backend.db.database import get_db, init_db
from backend.main import app


class TestStage2Components(unittest.TestCase):
    """Test suite for database layer, DAO, models, parser, and FastAPI endpoints."""

    def setUp(self) -> None:
        """Create a temporary directory and isolated SQLite database for testing."""
        self.test_dir = tempfile.mkdtemp()
        self.db_path = os.path.join(self.test_dir, "test_sniper.db")
        self.dao = FlightSniperDAO(db_path=self.db_path)

    def tearDown(self) -> None:
        """Clean up temporary files after test run."""
        shutil.rmtree(self.test_dir, ignore_errors=True)

    def test_database_init(self) -> None:
        """Test database table and index creation."""
        async def _run() -> None:
            await init_db(self.db_path)
            async with get_db(self.db_path) as conn:
                # Check tables exist
                cursor = await conn.execute(
                    "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name"
                )
                tables = [row["name"] for row in await cursor.fetchall()]
                self.assertIn("tasks", tables)
                self.assertIn("alerts_history", tables)

                # Check indices exist
                cursor = await conn.execute(
                    "SELECT name FROM sqlite_master WHERE type='index' ORDER BY name"
                )
                indices = [row["name"] for row in await cursor.fetchall()]
                self.assertIn("idx_tasks_active", indices)
                self.assertIn("idx_tasks_chat_id", indices)
                self.assertIn("idx_alerts_task_id", indices)

        asyncio.run(_run())

    def test_dao_task_crud_operations(self) -> None:
        """Test task creation, retrieval, filtering, updating, and deletion."""
        async def _run() -> None:
            await init_db(self.db_path)

            # 1. Add tasks for User 1 (chat_id = 1001)
            t1_id = await self.dao.add_task(
                chat_id=1001,
                origin="ala",
                destination="nqz",
                date="2026-10-15",
                target_price=25000.0,
                flight_number=None,
            )
            self.assertIsInstance(t1_id, int)
            self.assertGreater(t1_id, 0)

            t2_id = await self.dao.add_task(
                chat_id=1001,
                origin="ALA",
                destination="CIT",
                date="2026-10-20",
                target_price=18000.0,
                flight_number="KC-871",
            )
            self.assertGreater(t2_id, t1_id)

            # 2. Add task for User 2 (chat_id = 2002)
            t3_id = await self.dao.add_task(
                chat_id=2002,
                origin="NQZ",
                destination="SCO",
                date="2026-11-05",
                target_price=32000.0,
                flight_number="IQ-401",
            )

            # 3. Test get_active_tasks
            active_tasks = await self.dao.get_active_tasks()
            self.assertEqual(len(active_tasks), 3)
            self.assertEqual(active_tasks[0]["origin"], "ALA")  # Uppercase normalization check
            self.assertEqual(active_tasks[0]["destination"], "NQZ")
            self.assertIsNone(active_tasks[0]["flight_number"])
            self.assertEqual(active_tasks[1]["flight_number"], "KC-871")

            # 4. Test get_user_tasks isolation
            user1_tasks = await self.dao.get_user_tasks(chat_id=1001)
            self.assertEqual(len(user1_tasks), 2)

            user2_tasks = await self.dao.get_user_tasks(chat_id=2002)
            self.assertEqual(len(user2_tasks), 1)
            self.assertEqual(user2_tasks[0]["id"], t3_id)

            empty_user_tasks = await self.dao.get_user_tasks(chat_id=9999)
            self.assertEqual(len(empty_user_tasks), 0)

            # 5. Test get_task_by_id
            task1 = await self.dao.get_task_by_id(t1_id)
            self.assertIsNotNone(task1)
            self.assertEqual(task1["id"], t1_id)
            self.assertEqual(task1["target_price"], 25000.0)

            # 6. Test update_task_last_check
            updated = await self.dao.update_task_last_check(t1_id, last_price=24500.0)
            self.assertTrue(updated)
            task1_updated = await self.dao.get_task_by_id(t1_id)
            self.assertEqual(task1_updated["last_price"], 24500.0)
            self.assertIsNotNone(task1_updated["last_checked_at"])

            # 7. Test active task count
            count = await self.dao.get_active_tasks_count()
            self.assertEqual(count, 3)

            # 8. Test delete_task
            # Attempt delete with wrong chat_id
            wrong_delete = await self.dao.delete_task(task_id=t1_id, chat_id=2002)
            self.assertFalse(wrong_delete)

            # Legitimate delete
            correct_delete = await self.dao.delete_task(task_id=t1_id, chat_id=1001)
            self.assertTrue(correct_delete)

            # Verify deletion
            task1_after = await self.dao.get_task_by_id(t1_id)
            self.assertIsNone(task1_after)

            user1_remaining = await self.dao.get_user_tasks(chat_id=1001)
            self.assertEqual(len(user1_remaining), 1)

            count_after = await self.dao.get_active_tasks_count()
            self.assertEqual(count_after, 2)

        asyncio.run(_run())

    def test_dao_alert_logging_and_deduplication(self) -> None:
        """Test alert logging and recent alert deduplication check."""
        async def _run() -> None:
            await init_db(self.db_path)

            task_id = await self.dao.add_task(
                chat_id=5005,
                origin="ALA",
                destination="NQZ",
                date="2026-10-15",
                target_price=25000.0,
            )

            # 1. Initially no recent alerts
            has_recent = await self.dao.check_recent_alert(
                task_id=task_id,
                flight_number="KC-853",
                price=24000.0,
                window_minutes=60,
            )
            self.assertFalse(has_recent)

            # 2. Log an alert
            alert_id = await self.dao.log_alert(
                task_id=task_id,
                flight_number="KC-853",
                price=24000.0,
            )
            self.assertIsInstance(alert_id, int)
            self.assertGreater(alert_id, 0)

            # 3. Check recent alert now returns True (same price or higher)
            has_recent_now = await self.dao.check_recent_alert(
                task_id=task_id,
                flight_number="KC-853",
                price=24000.0,
                window_minutes=60,
            )
            self.assertTrue(has_recent_now)

            # Higher price also suppressed (since we already alerted for <= 24000)
            has_recent_higher = await self.dao.check_recent_alert(
                task_id=task_id,
                flight_number="KC-853",
                price=25000.0,
                window_minutes=60,
            )
            self.assertTrue(has_recent_higher)

            # Lower price (e.g. 21000) should NOT be suppressed by the 24000 alert
            has_recent_lower = await self.dao.check_recent_alert(
                task_id=task_id,
                flight_number="KC-853",
                price=21000.0,
                window_minutes=60,
            )
            self.assertFalse(has_recent_lower)

            # Different flight number is not suppressed
            has_recent_diff_flight = await self.dao.check_recent_alert(
                task_id=task_id,
                flight_number="DV-713",
                price=24000.0,
                window_minutes=60,
            )
            self.assertFalse(has_recent_diff_flight)

            # 4. Test past alert outside window is not suppressed
            old_alert_id = await self.dao.log_alert(
                task_id=task_id,
                flight_number="IQ-401",
                price=19000.0,
                alert_time="2020-01-01 10:00:00",
            )
            has_recent_old = await self.dao.check_recent_alert(
                task_id=task_id,
                flight_number="IQ-401",
                price=19000.0,
                window_minutes=60,
            )
            self.assertFalse(has_recent_old)

        asyncio.run(_run())

    def test_snipe_argument_parsing(self) -> None:
        """Test valid and invalid /snipe command arguments."""
        # 1. Valid 4-argument syntax
        origin, dest, flight_date, flight_no, price = parse_snipe_arguments("ala nqz 2026-10-15 25000")
        self.assertEqual(origin, "ALA")
        self.assertEqual(dest, "NQZ")
        self.assertEqual(flight_date, "2026-10-15")
        self.assertIsNone(flight_no)
        self.assertEqual(price, 25000.0)

        # 2. Valid 5-argument syntax with flight filter
        origin, dest, flight_date, flight_no, price = parse_snipe_arguments("ALA NQZ 2026-10-15 kc-871 28500.50")
        self.assertEqual(origin, "ALA")
        self.assertEqual(dest, "NQZ")
        self.assertEqual(flight_date, "2026-10-15")
        self.assertEqual(flight_no, "KC-871")
        self.assertEqual(price, 28500.50)

        # 3. Formatted price with commas and currency symbols
        origin, dest, flight_date, flight_no, price = parse_snipe_arguments("ALA NQZ 2026-10-15 35,000₸")
        self.assertEqual(price, 35000.0)

        # 4. Invalid argument count
        with self.assertRaises(ValueError):
            parse_snipe_arguments("ALA NQZ 2026-10-15")

        with self.assertRaises(ValueError):
            parse_snipe_arguments("ALA NQZ 2026-10-15 KC-871 25000 EXTRA")

        # 5. Invalid IATA code length
        with self.assertRaises(ValueError):
            parse_snipe_arguments("ALAMATY NQZ 2026-10-15 25000")

        # 6. Identical origin and destination
        with self.assertRaises(ValueError):
            parse_snipe_arguments("ALA ALA 2026-10-15 25000")

        # 7. Past date
        with self.assertRaises(ValueError):
            parse_snipe_arguments("ALA NQZ 2020-01-01 25000")

        # 8. Invalid date format
        with self.assertRaises(ValueError):
            parse_snipe_arguments("ALA NQZ 15-10-2026 25000")

        # 9. Invalid / negative price
        with self.assertRaises(ValueError):
            parse_snipe_arguments("ALA NQZ 2026-10-15 -500")

        with self.assertRaises(ValueError):
            parse_snipe_arguments("ALA NQZ 2026-10-15 free")

    def test_pydantic_models(self) -> None:
        """Test validation and normalization in Pydantic models."""
        # TaskCreate normalization
        task_in = TaskCreate(
            chat_id=12345,
            origin="ala",
            destination="nqz",
            date="2026-10-15",
            target_price=25000.0,
            flight_number="kc-853",
        )
        self.assertEqual(task_in.origin, "ALA")
        self.assertEqual(task_in.destination, "NQZ")
        self.assertEqual(task_in.flight_number, "KC-853")

        # FlightOffer model
        offer = FlightOffer(
            provider="aviata",
            airline="Air Astana",
            flight_number="KC-853",
            origin="ALA",
            destination="NQZ",
            departure_time="2026-10-15T08:00:00",
            arrival_time="2026-10-15T09:40:00",
            price_kzt=24500.0,
            transfers_count=0,
            duration_minutes=100,
            deep_link="https://aviata.kz/search/...",
        )
        self.assertEqual(offer.airline, "Air Astana")
        self.assertEqual(offer.price_kzt, 24500.0)

    def test_fastapi_endpoints(self) -> None:
        """Test FastAPI REST endpoints."""
        with TestClient(app) as client:
            # 1. Root endpoint
            res_root = client.get("/")
            self.assertEqual(res_root.status_code, 200)
            data_root = res_root.json()
            self.assertEqual(data_root["app"], "KzFlightSniper")
            self.assertEqual(data_root["status"], "running")

            # 2. Health check endpoint
            res_health = client.get("/health")
            self.assertEqual(res_health.status_code, 200)
            data_health = res_health.json()
            self.assertEqual(data_health["status"], "ok")
            self.assertEqual(data_health["database"], "connected")
            self.assertIsInstance(data_health["active_tasks"], int)

            # 3. Tasks list endpoint
            res_tasks = client.get("/api/tasks")
            self.assertEqual(res_tasks.status_code, 200)
            self.assertIsInstance(res_tasks.json(), list)


if __name__ == "__main__":
    unittest.main()
