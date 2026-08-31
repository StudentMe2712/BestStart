"""APScheduler async task scheduler integration for KzFlightSniper.

Manages recurring execution of flight price checking cycles at configured intervals.
"""

import logging
from typing import Any, Optional
from aiogram import Bot
from apscheduler.schedulers.asyncio import AsyncIOScheduler

from backend.core.config import get_settings
from backend.db.dao import FlightSniperDAO
from backend.engine.sniper_worker import run_sniper_check
from backend.providers.base import BaseFlightProvider

logger = logging.getLogger("kzflight_sniper.engine.scheduler")

_scheduler: Optional[AsyncIOScheduler] = None


def get_scheduler() -> Optional[AsyncIOScheduler]:
    """Return the active AsyncIOScheduler instance if initialized."""
    return _scheduler


def init_scheduler(
    bot: Optional[Bot] = None,
    provider: Optional[BaseFlightProvider] = None,
    dao: Optional[FlightSniperDAO] = None,
    interval_seconds: Optional[int] = None,
    event_loop: Optional[Any] = None,
) -> AsyncIOScheduler:
    """Initialize and configure the AsyncIOScheduler instance.

    Args:
        bot: Optional Bot instance for dispatching notifications.
        provider: Optional flight search provider.
        dao: Optional FlightSniperDAO instance.
        interval_seconds: Optional interval in seconds (defaults to CHECK_INTERVAL_SECONDS).
        event_loop: Optional asyncio event loop instance.

    Returns:
        Configured AsyncIOScheduler instance.
    """
    global _scheduler
    settings = get_settings()
    interval = interval_seconds or settings.CHECK_INTERVAL_SECONDS

    if _scheduler is not None and _scheduler.running:
        logger.info("Stopping existing scheduler instance prior to re-initialization...")
        _scheduler.shutdown(wait=False)

    kwargs: dict = {}
    if event_loop is not None:
        kwargs["event_loop"] = event_loop

    _scheduler = AsyncIOScheduler(**kwargs)
    _scheduler.add_job(
        run_sniper_check,
        trigger="interval",
        seconds=interval,
        kwargs={"bot": bot, "provider": provider, "dao": dao},
        id="sniper_flight_check",
        name="Periodic Flight Price Sniper Check",
        replace_existing=True,
    )
    logger.info("Scheduler initialized with %ds check interval.", interval)
    return _scheduler


def start_scheduler(
    bot: Optional[Bot] = None,
    provider: Optional[BaseFlightProvider] = None,
    dao: Optional[FlightSniperDAO] = None,
    interval_seconds: Optional[int] = None,
    event_loop: Optional[Any] = None,
) -> AsyncIOScheduler:
    """Start the periodic flight monitoring scheduler.

    Args:
        bot: Optional Bot instance.
        provider: Optional flight provider.
        dao: Optional FlightSniperDAO instance.
        interval_seconds: Optional check interval in seconds.
        event_loop: Optional asyncio event loop.

    Returns:
        Running AsyncIOScheduler instance.
    """
    global _scheduler
    if _scheduler is None:
        _scheduler = init_scheduler(
            bot=bot,
            provider=provider,
            dao=dao,
            interval_seconds=interval_seconds,
            event_loop=event_loop,
        )

    if not _scheduler.running:
        _scheduler.start()
        logger.info("AsyncIOScheduler started successfully.")

    return _scheduler


def stop_scheduler() -> None:
    """Gracefully stop and tear down the AsyncIOScheduler instance."""
    global _scheduler
    if _scheduler is not None:
        if _scheduler.running:
            _scheduler.shutdown(wait=False)
            logger.info("AsyncIOScheduler stopped.")
        _scheduler = None
