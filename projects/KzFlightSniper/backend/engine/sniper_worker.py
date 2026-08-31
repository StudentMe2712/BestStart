"""Flight price sniper monitoring worker engine.

Executes periodic checks across active user tasks (or due tasks based on custom intervals),
searches flights using provider adapters, filters targets, verifies alert deduplication,
and dispatches Telegram push notifications.
"""

from collections import defaultdict
import logging
from typing import Any, Dict, List, Optional, Tuple
from aiogram import Bot
from aiogram.enums import ParseMode

from backend.core.models import FlightOffer
from backend.db.dao import FlightSniperDAO
from backend.providers.aviata_provider import AviataProvider
from backend.providers.base import BaseFlightProvider

logger = logging.getLogger("kzflight_sniper.engine.worker")


def format_alert_message(task: Dict[str, Any], offer: FlightOffer) -> str:
    """Format high-visibility HTML notification message for Telegram push alerts.

    Args:
        task: Active task dictionary from database.
        offer: Matching FlightOffer object triggering the alert.

    Returns:
        HTML formatted Telegram message string.
    """
    origin = str(task.get("origin", offer.origin)).upper()
    dest = str(task.get("destination", offer.destination)).upper()
    date_str = str(task.get("date", "N/A"))
    target_price = float(task.get("target_price", 0.0))
    current_price = float(offer.price_kzt)
    savings = max(0.0, target_price - current_price)

    flight_type = "Прямой рейс ⚡" if offer.transfers_count == 0 else f"{offer.transfers_count} пересадка(и)"
    times_line = (
        f"{offer.departure_time} ➡️ {offer.arrival_time}"
        if offer.departure_time and offer.departure_time != "Scheduled"
        else "По расписанию"
    )

    deep_link_html = (
        f'\n\n🔗 <a href="{offer.deep_link}"><b>Купить билет на Aviata</b></a>'
        if offer.deep_link
        else ""
    )

    message = (
        f"🎯 <b>KZ FLIGHT SNIPER — ЦЕЛЬ ОБНАРУЖЕНА!</b>\n\n"
        f"✈️ <b>Маршрут:</b> <code>{origin}</code> ➡️ <code>{dest}</code>\n"
        f"📅 <b>Дата:</b> {date_str}\n"
        f"🏢 <b>Авиакомпания:</b> {offer.airline}\n"
        f"🔢 <b>Рейс:</b> <code>{offer.flight_number}</code> ({flight_type})\n"
        f"⏰ <b>Время:</b> {times_line}\n\n"
        f"💰 <b>Найдена цена:</b> <b>{current_price:,.0f} ₸</b>\n"
        f"🎯 <b>Ваша цель:</b> {target_price:,.0f} ₸\n"
        f"💸 <b>Экономия:</b> <b>{savings:,.0f} ₸</b>"
        f"{deep_link_html}"
    )
    return message.replace(",", " ")


class SniperWorker:
    """Core flight price sniper engine running batch check cycles."""

    def __init__(
        self,
        bot: Optional[Bot] = None,
        provider: Optional[BaseFlightProvider] = None,
        dao: Optional[FlightSniperDAO] = None,
    ) -> None:
        """Initialize worker with optional dependency overrides.

        Args:
            bot: Optional aiogram Bot instance for dispatching alerts.
            provider: Optional flight search provider (defaults to AviataProvider).
            dao: Optional FlightSniperDAO instance (defaults to standard singleton).
        """
        self.bot = bot
        self.provider = provider or AviataProvider()
        self.dao = dao or FlightSniperDAO()

    async def run_check(self, due_only: bool = False) -> Dict[str, Any]:
        """Execute one complete monitoring cycle over active or due sniping tasks.

        Args:
            due_only: If True, evaluates only tasks whose custom interval has elapsed.
                      If False, evaluates all active tasks unconditionally.

        Returns:
            Dictionary containing cycle execution statistics:
            - tasks_checked: Total tasks evaluated in this cycle
            - alerts_triggered: Number of new alerts sent & logged
            - errors: Number of errors encountered
            - details: Per-task check outcomes
        """
        stats: Dict[str, Any] = {
            "tasks_checked": 0,
            "alerts_triggered": 0,
            "errors": 0,
            "details": [],
        }

        try:
            if due_only:
                tasks = await self.dao.get_due_tasks()
            else:
                tasks = await self.dao.get_active_tasks()
        except Exception as e:
            logger.exception("Failed to query tasks from database: %s", e)
            stats["errors"] += 1
            return stats

        if not tasks:
            logger.info("No due flight sniping tasks found. Cycle completed.")
            return stats

        logger.info("Starting sniper cycle for %d due task(s)...", len(tasks))

        # Group tasks by (origin, destination, date) to minimize browser queries
        grouped_tasks: Dict[Tuple[str, str, str], List[Dict[str, Any]]] = defaultdict(list)
        for t in tasks:
            key = (t["origin"].strip().upper(), t["destination"].strip().upper(), t["date"].strip())
            grouped_tasks[key].append(t)

        for (origin, destination, date_str), route_tasks in grouped_tasks.items():
            try:
                # Determine max transfers needed for this group
                max_transfer = max(t.get("max_transfers", 0) for t in route_tasks)
                offers = await self.provider.search_flights(
                    origin=origin,
                    destination=destination,
                    date=date_str,
                    max_transfers=max_transfer,
                )
            except Exception as search_err:
                logger.error(
                    "Flight search provider failed for route %s->%s on %s: %s",
                    origin,
                    destination,
                    date_str,
                    search_err,
                )
                stats["errors"] += len(route_tasks)
                for t in route_tasks:
                    stats["details"].append({
                        "task_id": t["id"],
                        "status": "error",
                        "error": str(search_err),
                    })
                continue

            for task in route_tasks:
                stats["tasks_checked"] += 1
                task_id = task["id"]
                chat_id = task["chat_id"]
                target_price = float(task["target_price"])
                flight_filter = task.get("flight_number")
                direct_only = task.get("max_transfers", 0) == 0

                # Filter offers for this specific task
                matching_offers: List[FlightOffer] = []
                for offer in offers:
                    if direct_only and offer.transfers_count > 0:
                        continue
                    if flight_filter and flight_filter.strip():
                        if flight_filter.strip().upper() not in offer.flight_number.upper():
                            continue
                    matching_offers.append(offer)

                if not matching_offers:
                    await self.dao.update_task_last_check(task_id, last_price=None)
                    stats["details"].append({
                        "task_id": task_id,
                        "status": "no_matching_flights",
                        "lowest_price": None,
                    })
                    continue

                # Find lowest observed price for this task
                lowest_offer = min(matching_offers, key=lambda o: o.price_kzt)
                await self.dao.update_task_last_check(task_id, last_price=lowest_offer.price_kzt)

                # Identify qualifying offers where price <= target_price
                qualifying_offers = [o for o in matching_offers if o.price_kzt <= target_price]

                if not qualifying_offers:
                    stats["details"].append({
                        "task_id": task_id,
                        "status": "price_above_target",
                        "lowest_price": lowest_offer.price_kzt,
                        "target_price": target_price,
                    })
                    continue

                # For qualifying offers, check deduplication before alerting
                for offer in qualifying_offers:
                    try:
                        is_duplicate = await self.dao.check_recent_alert(
                            task_id=task_id,
                            flight_number=offer.flight_number,
                            price=offer.price_kzt,
                            window_minutes=60,
                        )

                        if is_duplicate:
                            logger.debug(
                                "Suppressing duplicate alert for task %d (flight %s, price %.0f KZT)",
                                task_id,
                                offer.flight_number,
                                offer.price_kzt,
                            )
                            continue

                        # Format & dispatch alert
                        alert_text = format_alert_message(task, offer)

                        if self.bot is not None:
                            try:
                                await self.bot.send_message(
                                    chat_id=chat_id,
                                    text=alert_text,
                                    parse_mode=ParseMode.HTML,
                                )
                                logger.info(
                                    "Dispatched alert notification to user %d for task %d (price: %.0f KZT)",
                                    chat_id,
                                    task_id,
                                    offer.price_kzt,
                                )
                            except Exception as bot_err:
                                logger.error(
                                    "Failed to send Telegram message to chat %d for task %d: %s",
                                    chat_id,
                                    task_id,
                                    bot_err,
                                )

                        # Record alert history in database
                        await self.dao.log_alert(
                            task_id=task_id,
                            flight_number=offer.flight_number,
                            price=offer.price_kzt,
                        )
                        stats["alerts_triggered"] += 1

                        stats["details"].append({
                            "task_id": task_id,
                            "status": "alert_triggered",
                            "flight_number": offer.flight_number,
                            "price": offer.price_kzt,
                            "target_price": target_price,
                        })

                    except Exception as alert_err:
                        logger.exception("Error processing alert for task %d: %s", task_id, alert_err)
                        stats["errors"] += 1

        logger.info(
            "Sniper check completed: %d tasks checked, %d alert(s) triggered, %d error(s)",
            stats["tasks_checked"],
            stats["alerts_triggered"],
            stats["errors"],
        )
        return stats

    async def run_check_cycle(self, due_only: bool = False) -> Dict[str, Any]:
        """Execute one complete flight price monitoring check cycle.

        Args:
            due_only: If True, evaluates only due tasks. If False, evaluates all active tasks.
        """
        return await self.run_check(due_only=due_only)


async def run_sniper_check(
    bot: Optional[Bot] = None,
    provider: Optional[BaseFlightProvider] = None,
    dao: Optional[FlightSniperDAO] = None,
    due_only: bool = True,
) -> Dict[str, Any]:
    """Helper function to instantiate worker and run a single sniper check cycle.

    Args:
        bot: Optional Bot instance.
        provider: Optional provider instance.
        dao: Optional FlightSniperDAO instance.
        due_only: Whether to query only due tasks (defaults to True).

    Returns:
        Summary statistics dictionary.
    """
    worker = SniperWorker(bot=bot, provider=provider, dao=dao)
    return await worker.run_check(due_only=due_only)
