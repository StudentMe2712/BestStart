"""Scheduler for periodic execution of web scrapers.

Periodically queries registered web scrapers (Kwork, Freelancehunt, FL.ru, etc.) and
forwards retrieved RawTasks to the processing pipeline callback.
"""

from typing import Awaitable, Callable
import asyncio
import logging

from backend.core.config import Settings, settings as global_settings
from backend.scrapers.base import BaseScraper, RawTask
from backend.scrapers.fl_ru import FlRuScraper
from backend.scrapers.freelancehunt import FreelancehuntScraper
from backend.scrapers.kwork_playwright import KworkPlaywrightScraper

logger = logging.getLogger(__name__)


class WebScraperScheduler:
    """Manages periodic execution of web scrapers with interval timers."""

    def __init__(
        self,
        scrapers: list[BaseScraper] | None = None,
        on_task_callback: Callable[[RawTask], Awaitable[None]] | None = None,
        interval_minutes: int | None = None,
        settings: Settings | None = None,
    ) -> None:
        """Initializes WebScraperScheduler.

        Args:
            scrapers: List of BaseScraper instances. If None, instantiates default scrapers.
            on_task_callback: Async callback function invoked for each scraped task.
            interval_minutes: Execution interval in minutes. If None, loaded from settings.
            settings: Settings instance.
        """
        self.settings = settings or global_settings
        self.scrapers: list[BaseScraper] = scrapers if scrapers is not None else [
            KworkPlaywrightScraper(),
            FreelancehuntScraper(),
            FlRuScraper(),
        ]
        self.on_task_callback = on_task_callback
        self.interval_minutes: int = (
            interval_minutes
            if interval_minutes is not None
            else getattr(self.settings, "WEB_SCRAPE_INTERVAL_MINUTES", 5)
        )
        self._task: asyncio.Task[None] | None = None
        self._is_running: bool = False

    @property
    def is_running(self) -> bool:
        """Checks if scheduler background loop is running."""
        return self._is_running

    async def run_once(self) -> list[RawTask]:
        """Executes a single pass across all scrapers and triggers callback for each task.

        Returns:
            list[RawTask]: All tasks collected during this iteration.
        """
        all_tasks: list[RawTask] = []
        logger.info("[ScraperScheduler] Starting scraper iteration across %d scrapers...", len(self.scrapers))

        for scraper in self.scrapers:
            scraper_name = scraper.__class__.__name__
            try:
                logger.debug("[ScraperScheduler] Executing %s...", scraper_name)
                tasks = await scraper.fetch_new_tasks()
                logger.info("[ScraperScheduler] %s returned %d tasks", scraper_name, len(tasks))
                all_tasks.extend(tasks)

                if self.on_task_callback:
                    for task in tasks:
                        try:
                            await self.on_task_callback(task)
                        except Exception as cb_exc:
                            logger.error(
                                "[ScraperScheduler] Callback failed for task %s (%s): %s",
                                task.external_id,
                                task.source,
                                cb_exc,
                                exc_info=True,
                            )
            except Exception as exc:
                logger.error("[ScraperScheduler] Scraper %s encountered error: %s", scraper_name, exc, exc_info=True)

        return all_tasks

    async def _loop(self) -> None:
        """Internal background loop running scrapers at periodic intervals."""
        self._is_running = True
        logger.info(
            "[ScraperScheduler] Background scraper loop started (interval=%d min).",
            self.interval_minutes,
        )
        try:
            while self._is_running:
                await self.run_once()
                # Sleep interval
                sleep_seconds = max(1, self.interval_minutes * 60)
                logger.debug("[ScraperScheduler] Sleeping for %d seconds until next pass.", sleep_seconds)
                await asyncio.sleep(sleep_seconds)
        except asyncio.CancelledError:
            logger.info("[ScraperScheduler] Background loop received cancellation.")
        finally:
            self._is_running = False

    async def start(self) -> None:
        """Starts background scraper polling loop if not already running."""
        if self._is_running:
            return
        self._is_running = True
        self._task = asyncio.create_task(self._loop())

    async def stop(self) -> None:
        """Stops background scraper loop and cancels active tasks."""
        self._is_running = False
        if self._task and not self._task.done():
            self._task.cancel()
            try:
                await self._task
            except asyncio.CancelledError:
                pass
        self._task = None
        logger.info("[ScraperScheduler] Web scraper scheduler stopped.")
