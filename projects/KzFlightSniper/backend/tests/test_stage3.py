"""Unit and integration tests for KzFlightSniper Stage 3 components.

Tests cover:
- AviataProvider JSON API response parsing and normalization
- AviataProvider search convenience method with filtering
- SniperWorker alert triggering, target price comparisons, and DB logging
- SniperWorker alert deduplication window suppression
- APScheduler lifecycle management (init, start, stop)
- FastAPI /api/check-now manual trigger endpoint
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

from backend.core.config import get_settings
from backend.core.models import FlightOffer
from backend.db.dao import FlightSniperDAO
from backend.db.database import get_db, init_db
from backend.engine.scheduler import get_scheduler, init_scheduler, start_scheduler, stop_scheduler
from backend.engine.sniper_worker import SniperWorker, format_alert_message, run_sniper_check
from backend.main import app
from backend.providers import AviasalesProvider, AviataProvider, BaseFlightProvider


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
    ) -> List[FlightOffer]:
        filtered = []
        for o in self._offers:
            if o.origin.upper() == origin.upper() and o.destination.upper() == destination.upper():
                if max_transfers == 0 and o.transfers_count > 0:
                    continue
                if max_transfers > 0 and o.transfers_count > max_transfers:
                    continue
                filtered.append(o)
        return filtered


class TestStage3Components(unittest.TestCase):
    """Test suite for Stage 3 Aviata provider, SniperWorker, Scheduler, and API."""

    def setUp(self) -> None:
        """Create a temporary directory and isolated SQLite database for testing."""
        self.test_dir = tempfile.mkdtemp()
        self.db_path = os.path.join(self.test_dir, "test_stage3.db")
        self.dao = FlightSniperDAO(db_path=self.db_path)

    def tearDown(self) -> None:
        """Clean up temporary files and ensure scheduler is stopped."""
        stop_scheduler()
        shutil.rmtree(self.test_dir, ignore_errors=True)

    def test_aviata_json_parser_standard(self) -> None:
        """Test AviataProvider JSON parser on standard results structure."""
        raw_payload = {
            "results": [
                {
                    "airline_name": "Air Astana",
                    "flight_number": "KC-853",
                    "departure_time": "2026-10-15T08:00:00",
                    "arrival_time": "2026-10-15T09:40:00",
                    "price": 24500.0,
                    "transfers_count": 0,
                    "duration_minutes": 100,
                    "deep_link": "https://aviata.kz/flights/search/ALANQZ20261015100E/",
                },
                {
                    "carrier_name": "FlyArystan",
                    "flight_no": "7133",
                    "departure": {"time": "2026-10-15T12:00:00"},
                    "arrival": {"time": "2026-10-15T13:45:00"},
                    "price": {"amount": 16900.0},
                    "stops": 0,
                    "duration": 105,
                },
                {
                    "company": "SCAT Airlines",
                    "code": "DV-713",
                    "departure_time": "2026-10-15T16:00:00",
                    "arrival_time": "2026-10-15T19:30:00",
                    "price": "31 500 ₸",
                    "transfers": 1,
                },
            ]
        }

        offers = AviataProvider.parse_aviata_json(raw_payload, "ALA", "NQZ", "https://aviata.kz")
        self.assertEqual(len(offers), 3)

        # Offer 1
        self.assertEqual(offers[0].airline, "Air Astana")
        self.assertEqual(offers[0].flight_number, "KC-853")
        self.assertEqual(offers[0].origin, "ALA")
        self.assertEqual(offers[0].destination, "NQZ")
        self.assertEqual(offers[0].price_kzt, 24500.0)
        self.assertEqual(offers[0].transfers_count, 0)
        self.assertEqual(offers[0].duration_minutes, 100)

        # Offer 2
        self.assertEqual(offers[1].airline, "FlyArystan")
        self.assertEqual(offers[1].flight_number, "7133")
        self.assertEqual(offers[1].price_kzt, 16900.0)
        self.assertEqual(offers[1].transfers_count, 0)

        # Offer 3
        self.assertEqual(offers[2].airline, "SCAT Airlines")
        self.assertEqual(offers[2].flight_number, "DV-713")
        self.assertEqual(offers[2].price_kzt, 31500.0)
        self.assertEqual(offers[2].transfers_count, 1)

    def test_aviata_json_parser_alternative_structures(self) -> None:
        """Test parsing variants, nested data, segments concatenation, and price formats."""
        # 1. Nested data structure with segments
        payload_nested = {
            "data": {
                "offers": [
                    {
                        "airline": {"name": "Qazaq Air"},
                        "price": {"value": 18500.0},
                        "departure_time": "2026-10-15T06:00:00",
                        "arrival_time": "2026-10-15T08:00:00",
                        "transfers_count": 1,
                        "segments": [
                            {"flight_number": "IQ-401", "carrier": "Qazaq Air"},
                            {"flight_number": "IQ-402", "carrier": "Qazaq Air"},
                        ],
                    }
                ]
            }
        }
        offers_nested = AviataProvider.parse_aviata_json(payload_nested, "CIT", "NQZ")
        self.assertEqual(len(offers_nested), 1)
        self.assertEqual(offers_nested[0].airline, "Qazaq Air")
        self.assertEqual(offers_nested[0].flight_number, "IQ-401 / IQ-402")
        self.assertEqual(offers_nested[0].price_kzt, 18500.0)
        self.assertEqual(offers_nested[0].transfers_count, 1)

        # 2. Direct list payload
        payload_list = [
            {
                "airline_name": "Air Astana",
                "flight_number": "KC-871",
                "price": 22000.0,
                "transfers_count": 0,
            }
        ]
        offers_list = AviataProvider.parse_aviata_json(payload_list, "ALA", "CIT")
        self.assertEqual(len(offers_list), 1)
        self.assertEqual(offers_list[0].flight_number, "KC-871")

        # 3. Invalid items (0 price, empty dict, etc.)
        invalid_payload = {
            "results": [
                {"airline_name": "Ghost Air", "price": 0},
                {"airline_name": "Free Air", "price": -500},
                {"invalid": True},
            ]
        }
        offers_invalid = AviataProvider.parse_aviata_json(invalid_payload, "ALA", "NQZ")
        self.assertEqual(len(offers_invalid), 0)

    def test_aviata_provider_search_convenience_filter(self) -> None:
        """Test AviataProvider convenience search method with flight_number and direct_only filters."""
        provider = AviataProvider(headless=True)

        sample_offers = [
            FlightOffer(
                provider="aviata",
                airline="Air Astana",
                flight_number="KC-853",
                origin="ALA",
                destination="NQZ",
                departure_time="08:00",
                arrival_time="09:40",
                price_kzt=25000.0,
                transfers_count=0,
            ),
            FlightOffer(
                provider="aviata",
                airline="Air Astana",
                flight_number="KC-855",
                origin="ALA",
                destination="NQZ",
                departure_time="14:00",
                arrival_time="15:40",
                price_kzt=29000.0,
                transfers_count=0,
            ),
            FlightOffer(
                provider="aviata",
                airline="SCAT",
                flight_number="DV-713",
                origin="ALA",
                destination="NQZ",
                departure_time="18:00",
                arrival_time="21:00",
                price_kzt=21000.0,
                transfers_count=1,
            ),
        ]

        async def _run() -> None:
            # Mock search_flights on provider instance
            with patch.object(provider, "search_flights", new=AsyncMock(return_value=sample_offers[:2])):
                # Filter specific flight number
                results = await provider.search(
                    origin="ALA",
                    destination="NQZ",
                    date="2026-10-15",
                    flight_number="KC-853",
                    direct_only=True,
                )
                self.assertEqual(len(results), 1)
                self.assertEqual(results[0].flight_number, "KC-853")

        asyncio.run(_run())

    def test_sniper_worker_alert_trigger(self) -> None:
        """Test SniperWorker triggering alert when found price <= target_price."""
        async def _run() -> None:
            await init_db(self.db_path)

            # Insert task: User 1001 wants ALA->NQZ on 2026-10-15 for <= 25000 KZT
            task_id = await self.dao.add_task(
                chat_id=1001,
                origin="ALA",
                destination="NQZ",
                date="2026-10-15",
                target_price=25000.0,
            )

            # Mock provider returns offer at 22000 KZT (qualifies!)
            mock_offers = [
                FlightOffer(
                    provider="aviata",
                    airline="Air Astana",
                    flight_number="KC-853",
                    origin="ALA",
                    destination="NQZ",
                    departure_time="08:00",
                    arrival_time="09:40",
                    price_kzt=22000.0,
                    transfers_count=0,
                    deep_link="https://aviata.kz/search/test",
                )
            ]
            provider = MockFlightProvider(mock_offers)

            mock_bot = MagicMock()
            mock_bot.send_message = AsyncMock()

            worker = SniperWorker(bot=mock_bot, provider=provider, dao=self.dao)
            stats = await worker.run_check()

            # Verify stats
            self.assertEqual(stats["tasks_checked"], 1)
            self.assertEqual(stats["alerts_triggered"], 1)
            self.assertEqual(stats["errors"], 0)

            # Verify bot message sent
            self.assertEqual(mock_bot.send_message.call_count, 1)
            call_args = mock_bot.send_message.call_args[1]
            self.assertEqual(call_args["chat_id"], 1001)
            self.assertIn("22 000 ₸", call_args["text"])
            self.assertIn("KC-853", call_args["text"])

            # Verify alert logged in database
            has_alert = await self.dao.check_recent_alert(
                task_id=task_id,
                flight_number="KC-853",
                price=22000.0,
                window_minutes=60,
            )
            self.assertTrue(has_alert)

            # Verify task last_price updated
            task = await self.dao.get_task_by_id(task_id)
            self.assertEqual(task["last_price"], 22000.0)
            self.assertIsNotNone(task["last_checked_at"])

        asyncio.run(_run())

    def test_sniper_worker_price_above_target_no_alert(self) -> None:
        """Test SniperWorker does NOT send alert when found price > target_price."""
        async def _run() -> None:
            await init_db(self.db_path)

            # Task: Target 20000 KZT
            task_id = await self.dao.add_task(
                chat_id=1001,
                origin="ALA",
                destination="NQZ",
                date="2026-10-15",
                target_price=20000.0,
            )

            # Mock provider returns lowest offer at 24500 KZT (too high)
            mock_offers = [
                FlightOffer(
                    provider="aviata",
                    airline="Air Astana",
                    flight_number="KC-853",
                    origin="ALA",
                    destination="NQZ",
                    departure_time="08:00",
                    arrival_time="09:40",
                    price_kzt=24500.0,
                    transfers_count=0,
                )
            ]
            provider = MockFlightProvider(mock_offers)

            mock_bot = MagicMock()
            mock_bot.send_message = AsyncMock()

            worker = SniperWorker(bot=mock_bot, provider=provider, dao=self.dao)
            stats = await worker.run_check()

            self.assertEqual(stats["tasks_checked"], 1)
            self.assertEqual(stats["alerts_triggered"], 0)
            self.assertEqual(mock_bot.send_message.call_count, 0)

            # But task last_price is still recorded as 24500.0
            task = await self.dao.get_task_by_id(task_id)
            self.assertEqual(task["last_price"], 24500.0)

        asyncio.run(_run())

    def test_sniper_worker_deduplication(self) -> None:
        """Test SniperWorker alert deduplication window."""
        async def _run() -> None:
            await init_db(self.db_path)

            task_id = await self.dao.add_task(
                chat_id=1001,
                origin="ALA",
                destination="NQZ",
                date="2026-10-15",
                target_price=25000.0,
            )

            provider = MockFlightProvider([
                FlightOffer(
                    provider="aviata",
                    airline="Air Astana",
                    flight_number="KC-853",
                    origin="ALA",
                    destination="NQZ",
                    departure_time="08:00",
                    arrival_time="09:40",
                    price_kzt=23000.0,
                    transfers_count=0,
                )
            ])

            mock_bot = MagicMock()
            mock_bot.send_message = AsyncMock()

            worker = SniperWorker(bot=mock_bot, provider=provider, dao=self.dao)

            # Cycle 1: Dispatches alert
            stats1 = await worker.run_check()
            self.assertEqual(stats1["alerts_triggered"], 1)
            self.assertEqual(mock_bot.send_message.call_count, 1)

            # Cycle 2: Same price 23000 KZT -> suppressed
            stats2 = await worker.run_check()
            self.assertEqual(stats2["alerts_triggered"], 0)
            self.assertEqual(mock_bot.send_message.call_count, 1)  # unchanged

            # Cycle 3: Price drops further to 19000 KZT -> new alert dispatched!
            provider.set_offers([
                FlightOffer(
                    provider="aviata",
                    airline="Air Astana",
                    flight_number="KC-853",
                    origin="ALA",
                    destination="NQZ",
                    departure_time="08:00",
                    arrival_time="09:40",
                    price_kzt=19000.0,
                    transfers_count=0,
                )
            ])
            stats3 = await worker.run_check()
            self.assertEqual(stats3["alerts_triggered"], 1)
            self.assertEqual(mock_bot.send_message.call_count, 2)

        asyncio.run(_run())

    def test_sniper_worker_flight_number_filter(self) -> None:
        """Test SniperWorker matches only the specified flight number when configured."""
        async def _run() -> None:
            await init_db(self.db_path)

            # Task specifies flight KC-853
            task_id = await self.dao.add_task(
                chat_id=1001,
                origin="ALA",
                destination="NQZ",
                date="2026-10-15",
                target_price=30000.0,
                flight_number="KC-853",
            )

            # Provider returns multiple flights under target price
            provider = MockFlightProvider([
                FlightOffer(
                    provider="aviata",
                    airline="FlyArystan",
                    flight_number="7133",
                    origin="ALA",
                    destination="NQZ",
                    departure_time="07:00",
                    arrival_time="08:40",
                    price_kzt=15000.0,
                    transfers_count=0,
                ),
                FlightOffer(
                    provider="aviata",
                    airline="Air Astana",
                    flight_number="KC-853",
                    origin="ALA",
                    destination="NQZ",
                    departure_time="08:00",
                    arrival_time="09:40",
                    price_kzt=26000.0,
                    transfers_count=0,
                ),
            ])

            mock_bot = MagicMock()
            mock_bot.send_message = AsyncMock()

            worker = SniperWorker(bot=mock_bot, provider=provider, dao=self.dao)
            stats = await worker.run_check()

            self.assertEqual(stats["alerts_triggered"], 1)
            # Verify alerted flight is KC-853, not 7133
            call_text = mock_bot.send_message.call_args[1]["text"]
            self.assertIn("KC-853", call_text)
            self.assertNotIn("7133", call_text)

        asyncio.run(_run())

    def test_scheduler_lifecycle(self) -> None:
        """Test APScheduler initialization, startup, and graceful shutdown."""
        async def _run() -> None:
            # 1. Initialize scheduler
            sched = init_scheduler(interval_seconds=60)
            self.assertIsNotNone(sched)
            self.assertFalse(sched.running)

            # 2. Start scheduler
            started_sched = start_scheduler(interval_seconds=60)
            self.assertTrue(started_sched.running)
            self.assertIsNotNone(started_sched.get_job("sniper_flight_check"))

            # 3. Stop scheduler
            stop_scheduler()
            self.assertIsNone(get_scheduler())

        asyncio.run(_run())

    def test_manual_check_endpoint(self) -> None:
        """Test POST /api/check-now endpoint triggering manual flight check."""
        with patch("backend.main.run_sniper_check", new=AsyncMock(return_value={
            "tasks_checked": 3,
            "alerts_triggered": 1,
            "errors": 0,
            "details": [],
        })):
            with TestClient(app) as client:
                res = client.post("/api/check-now")
                self.assertEqual(res.status_code, 200)
                data = res.json()
                self.assertEqual(data["status"], "success")
                self.assertEqual(data["stats"]["tasks_checked"], 3)
                self.assertEqual(data["stats"]["alerts_triggered"], 1)

    def test_format_alert_message(self) -> None:
        """Test alert message formatting function produces well-formed HTML."""
        task = {
            "origin": "ALA",
            "destination": "NQZ",
            "date": "2026-10-15",
            "target_price": 25000.0,
        }
        offer = FlightOffer(
            provider="aviata",
            airline="Air Astana",
            flight_number="KC-853",
            origin="ALA",
            destination="NQZ",
            departure_time="08:00",
            arrival_time="09:40",
            price_kzt=21500.0,
            transfers_count=0,
            deep_link="https://aviata.kz/search/ALANQZ20261015100E/",
        )
        msg = format_alert_message(task, offer)
        self.assertIn("🎯 <b>KZ FLIGHT SNIPER — ЦЕЛЬ ОБНАРУЖЕНА!</b>", msg)
        self.assertIn("ALA", msg)
        self.assertIn("NQZ", msg)
        self.assertIn("KC-853", msg)
        self.assertIn("21 500 ₸", msg)
        self.assertIn("25 000 ₸", msg)
        self.assertIn("3 500 ₸", msg)  # Savings = 25000 - 21500
        self.assertIn("https://aviata.kz/search/ALANQZ20261015100E/", msg)


if __name__ == "__main__":
    unittest.main()
