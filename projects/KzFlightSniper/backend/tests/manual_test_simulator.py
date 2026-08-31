"""Manual Test Simulator for KzFlightSniper NLP Evolution & Custom Intervals.

Simulates end-to-end natural language user inputs, AI/heuristic extraction,
task creation, custom interval evaluation, mock flight search, price alert
dispatch, deduplication, and database tracking.

Can be run directly:
    python backend/tests/manual_test_simulator.py
"""

import asyncio
from datetime import date, datetime, timedelta, timezone
import os
import sys
from typing import Any, Dict, List, Optional
from unittest.mock import AsyncMock, MagicMock

# Configure UTF-8 stdout encoding for Windows console compatibility
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

# Add project root to sys.path
PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../.."))
if PROJECT_ROOT not in sys.path:
    sys.path.insert(0, PROJECT_ROOT)

from backend.bot.nlp_parser import parse_flight_request, rule_based_flight_parser
from backend.core.models import FlightOffer, ParsedFlightIntent
from backend.db.dao import FlightSniperDAO
from backend.db.database import get_db, init_db
from backend.engine.sniper_worker import SniperWorker, run_sniper_check
from backend.providers.base import BaseFlightProvider


class MockFlightProvider(BaseFlightProvider):
    """Mock flight search provider providing deterministic flight offers for simulation."""

    def __init__(self, offers_by_route: Optional[Dict[str, List[FlightOffer]]] = None) -> None:
        self.offers_by_route = offers_by_route or {}
        self.search_call_count = 0

    @property
    def provider_name(self) -> str:
        return "mock_aviata"

    async def search_flights(
        self,
        origin: str,
        destination: str,
        date: str,
        max_transfers: int = 0,
    ) -> List[FlightOffer]:
        self.search_call_count += 1
        key = f"{origin.upper()}_{destination.upper()}_{date}"
        available = self.offers_by_route.get(key, [])
        if max_transfers == 0:
            return [o for o in available if o.transfers_count == 0]
        return [o for o in available if o.transfers_count <= max_transfers]


async def run_simulation() -> bool:
    """Execute the full manual test simulation scenario."""
    print("=" * 80)
    print("🚀 KZ FLIGHT SNIPER — NLP EVOLUTION & CUSTOM INTERVALS SIMULATOR")
    print("=" * 80)

    # Use a temporary SQLite database in /tmp or data directory
    test_db_path = os.path.join(PROJECT_ROOT, "data", "test_simulator.db")
    if os.path.exists(test_db_path):
        os.remove(test_db_path)

    # 1. Initialize Database & Automated Migrations
    print("\n📦 STEP 1: Initializing Database & Automated Migrations...")
    await init_db(test_db_path)
    dao = FlightSniperDAO(db_path=test_db_path)
    print("  ✅ SQLite database initialized with tasks and alerts_history schema.")

    # 2. Simulate Natural Language User Queries & Live Preview
    print("\n🗣️ STEP 2: Simulating Natural Language User Inputs & Live Preview...")
    test_queries = [
        (
            "Рейс Алматы - Бангкок, 15 октября 2026, прямой, KC-871, ниже 300$. Проверять каждые 5 минут",
            "User A (International / USD / 5 min)",
        ),
        (
            "Астана - Шымкент на 2026-11-01 до 20000 тг",
            "User B (Domestic / KZT / Default 5 min)",
        ),
        (
            "Хочу улететь из Актау в Дубай 25 декабря 2026 не дороже 80000 тенге, проверка раз в 10 минут",
            "User C (International / KZT / 10 min)",
        ),
        (
            "Алматы - Чэнду на 2026-11-21",
            "User D (Asian Hub / Auto Target Price from Live Preview / 5 min)",
        ),
    ]

    mock_live_inventory = {
        "ALA_CTU_2026-11-21": [
            FlightOffer(
                provider="aviata",
                airline="Air China",
                flight_number="CA-484",
                origin="ALA",
                destination="CTU",
                departure_time="10:00",
                arrival_time="17:00",
                price_kzt=75000.0,
                transfers_count=0,
                deep_link="https://aviata.kz/search/ALA-CTU",
            )
        ]
    }

    simulated_tasks: List[Dict[str, Any]] = []

    for idx, (query, label) in enumerate(test_queries, 1):
        print(f"\n  📝 Query #{idx} [{label}]:")
        print(f'     "{query}"')

        # Parse via NLP Engine
        ref_date = date(2026, 9, 1)
        intent = await parse_flight_request(query, base_date=ref_date)
        assert intent is not None, f"Failed to parse query: {query}"

        # Resolve effective target price (Live preview simulation)
        route_key = f"{intent.origin}_{intent.destination}_{intent.date}"
        live_preview_offers = mock_live_inventory.get(route_key, [])
        if intent.target_price is not None:
            effective_price = intent.target_price
            price_source = f"User Specified ({intent.original_price} {intent.currency_detected or 'KZT'})"
        elif live_preview_offers:
            effective_price = min(o.price_kzt for o in live_preview_offers)
            price_source = f"Auto-selected min from Live Preview ({effective_price:,.0f} ₸)"
        else:
            effective_price = 50000.0
            price_source = "Fallback default (50,000 ₸)"

        print(f"     ✅ Parsed Intent & Live Preview:")
        print(f"        • Route: {intent.origin} ✈️ {intent.destination}")
        print(f"        • Date: {intent.date}")
        print(f"        • Target Price: {effective_price:,.0f} KZT [{price_source}]")
        print(f"        • Flight Filter: {intent.flight_number or 'Any'}")
        print(f"        • Direct Only: {intent.direct_only}")
        print(f"        • Interval: {intent.interval_minutes} minutes")

        # Persist task in DAO
        chat_id = 1000 + idx
        task_id = await dao.add_task(
            chat_id=chat_id,
            origin=intent.origin,
            destination=intent.destination,
            date=intent.date,
            target_price=effective_price,
            flight_number=intent.flight_number,
            max_transfers=0 if intent.direct_only else 1,
            interval_minutes=intent.interval_minutes,
        )
        print(f"     💾 Task Created: ID #{task_id} for chat {chat_id}")
        simulated_tasks.append({
            "task_id": task_id,
            "chat_id": chat_id,
            "intent": intent,
            "effective_price": effective_price,
        })

    # 3. Verify Active Tasks Count and Structure
    print("\n🔍 STEP 3: Verifying Database Task Records...")
    active_tasks = await dao.get_active_tasks()
    assert len(active_tasks) == 4, f"Expected 4 active tasks, got {len(active_tasks)}"
    print(f"  ✅ Retrieved {len(active_tasks)} active tasks from database.")
    for t in active_tasks:
        print(f"     • Task #{t['id']}: {t['origin']}->{t['destination']} on {t['date']} | Target: {t['target_price']} ₸ | Interval: {t['interval_minutes']}m")

    # 4. Interval Due Tasks Logic Check
    print("\n⏱️ STEP 4: Testing Custom Intervals & Due Tasks Query...")
    due_tasks = await dao.get_due_tasks()
    assert len(due_tasks) == 4, f"Expected all 4 tasks to be due initially, got {len(due_tasks)}"
    print(f"  ✅ All {len(due_tasks)} new tasks with NULL last_checked_at are immediately due.")

    # 5. Setup Mock Provider and Run First Sniper Check Cycle
    print("\n🎯 STEP 5: Running Sniper Check Cycle 1 (Due Tasks)...")
    mock_offers = {
        "ALA_BKK_2026-10-15": [
            FlightOffer(
                provider="aviata",
                airline="Air Astana",
                flight_number="KC-871",
                origin="ALA",
                destination="BKK",
                departure_time="01:20",
                arrival_time="08:50",
                price_kzt=142000.0,  # Below 150,000 target! (300$ = 150k KZT)
                transfers_count=0,
                deep_link="https://aviata.kz/search/ALA-BKK",
            ),
        ],
        "NQZ_CIT_2026-11-01": [
            FlightOffer(
                provider="aviata",
                airline="FlyArystan",
                flight_number="KC-7105",
                origin="NQZ",
                destination="CIT",
                departure_time="06:30",
                arrival_time="08:10",
                price_kzt=16500.0,  # Below 20,000 target!
                transfers_count=0,
                deep_link="https://aviata.kz/search/NQZ-CIT",
            ),
        ],
        "SCO_DXB_2026-12-25": [
            FlightOffer(
                provider="aviata",
                airline="FlyDubai",
                flight_number="FZ-1738",
                origin="SCO",
                destination="DXB",
                departure_time="14:00",
                arrival_time="17:30",
                price_kzt=95000.0,  # Above 80,000 target (No alert expected)
                transfers_count=0,
                deep_link="https://aviata.kz/search/SCO-DXB",
            ),
        ],
        "ALA_CTU_2026-11-21": [
            FlightOffer(
                provider="aviata",
                airline="Air China",
                flight_number="CA-484",
                origin="ALA",
                destination="CTU",
                departure_time="10:00",
                arrival_time="17:00",
                price_kzt=69000.0,  # Price dropped from 75k to 69k (Below 75,000 target!)
                transfers_count=0,
                deep_link="https://aviata.kz/search/ALA-CTU",
            ),
        ],
    }

    mock_provider = MockFlightProvider(offers_by_route=mock_offers)
    mock_bot = MagicMock()
    mock_bot.send_message = AsyncMock(return_value=True)

    worker = SniperWorker(bot=mock_bot, provider=mock_provider, dao=dao)
    stats1 = await worker.run_check(due_only=True)

    print(f"  📊 Cycle 1 Results:")
    print(f"     • Tasks checked: {stats1['tasks_checked']}")
    print(f"     • Alerts triggered: {stats1['alerts_triggered']} (Expected: 3)")
    print(f"     • Errors: {stats1['errors']}")
    assert stats1["alerts_triggered"] == 3, f"Expected 3 alerts, got {stats1['alerts_triggered']}"
    assert mock_bot.send_message.call_count == 3, f"Expected 3 bot dispatches, got {mock_bot.send_message.call_count}"

    # 6. Verify Due Tasks Query immediately after check (Should be 0)
    print("\n⏳ STEP 6: Checking Due Tasks Immediately After Run...")
    due_after_check = await dao.get_due_tasks()
    print(f"  • Due tasks immediately after check: {len(due_after_check)} (Expected: 0)")
    assert len(due_after_check) == 0, f"Expected 0 due tasks, got {len(due_after_check)}"
    print("  ✅ All tasks successfully marked as checked; 0 tasks due.")

    # 7. Simulate Time Advancing (6 minutes later)
    print("\n⏩ STEP 7: Simulating Time Advancement (6 minutes pass)...")
    # In SQLite, simulate by setting last_checked_at to 6 minutes ago for all tasks
    async with get_db(test_db_path) as conn:
        await conn.execute("UPDATE tasks SET last_checked_at = datetime('now', '-6 minutes')")
        await conn.commit()

    due_6min = await dao.get_due_tasks()
    print(f"  • Due tasks after 6 minutes: {len(due_6min)}")
    # Task 1 (5m) -> Due
    # Task 2 (5m) -> Due
    # Task 3 (10m) -> NOT Due (needs 10 minutes)
    # Task 4 (5m) -> Due
    assert len(due_6min) == 3, f"Expected exactly 3 tasks due (5-min interval tasks), got {len(due_6min)}"
    due_ids = [t["id"] for t in due_6min]
    print(f"  ✅ Correct tasks due at 6 min: Task IDs {due_ids} (5-min tasks due, 10-min task skipped)")

    # 8. Test Alert Deduplication in Cycle 2
    print("\n🛡️ STEP 8: Running Cycle 2 to Test Alert Deduplication...")
    mock_bot.send_message.reset_mock()
    stats2 = await worker.run_check(due_only=True)
    print(f"  📊 Cycle 2 Results:")
    print(f"     • Tasks checked: {stats2['tasks_checked']}")
    print(f"     • Alerts triggered: {stats2['alerts_triggered']} (Expected: 0 due to 60m deduplication window)")
    assert stats2["alerts_triggered"] == 0, "Duplicate alerts were triggered!"
    assert mock_bot.send_message.call_count == 0, "Bot sent duplicate notification!"
    print("  ✅ Alert deduplication successfully suppressed identical prices within 60 min window.")

    # 9. Clean up temporary test database
    if os.path.exists(test_db_path):
        os.remove(test_db_path)

    print("\n" + "=" * 80)
    print("🎉 ALL SIMULATION SCENARIOS PASSED WITH 100% SUCCESS!")
    print("=" * 80)
    return True


if __name__ == "__main__":
    success = asyncio.run(run_simulation())
    sys.exit(0 if success else 1)
