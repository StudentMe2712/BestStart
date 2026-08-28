"""APScheduler background jobs for Global Radar Ingestion and Throttled Groq Processing."""

import logging
from datetime import datetime, timezone
from typing import Any, Dict, Optional

from app.core.settings import settings
from app.services.pipeline import pipeline_manager

logger = logging.getLogger(__name__)

try:
    from apscheduler.schedulers.asyncio import AsyncIOScheduler
    from apscheduler.triggers.interval import IntervalTrigger

    APSCHEDULER_AVAILABLE = True
    scheduler = AsyncIOScheduler()
except ImportError:
    APSCHEDULER_AVAILABLE = False
    scheduler = None

JOB_RADAR_INGEST = "radar_periodic_ingest"
JOB_GROQ_WORKER = "groq_throttled_worker"
JOB_DEEP_CRAWLER = "deep_web_crawler"

_is_paused: bool = False


def pause_scheduler() -> Dict[str, Any]:
    """Pause APScheduler automated background scan jobs."""
    global _is_paused
    if APSCHEDULER_AVAILABLE and scheduler and scheduler.running:
        scheduler.pause()
    _is_paused = True
    logger.info("APScheduler paused.")
    return {
        "status": "paused",
        "is_paused": True,
        "message": "Автоматическое сканирование приостановлено",
    }


def resume_scheduler() -> Dict[str, Any]:
    """Resume APScheduler automated background scan jobs."""
    global _is_paused
    if APSCHEDULER_AVAILABLE and scheduler and scheduler.running:
        scheduler.resume()
    _is_paused = False
    logger.info("APScheduler resumed.")
    return {
        "status": "running",
        "is_paused": False,
        "message": "Автоматическое сканирование возобновлено",
    }


async def scheduled_radar_job() -> None:
    """Periodic job that scrapes and queues items from all active radar sources."""
    logger.info("APScheduler: Triggering Radar ingestion cycle...")
    try:
        summary = await pipeline_manager.ingest_all_sources()
        logger.info("Radar Ingestion completed: %s", summary)
    except Exception as exc:
        logger.error("Error in scheduled radar job: %s", exc, exc_info=True)


async def scheduled_crawler_job() -> None:
    """Periodic deep web search crawl job."""
    logger.info("APScheduler: Triggering Deep Web Crawler cycle...")
    try:
        summary = await pipeline_manager.run_crawler_cycle()
        logger.info("Deep Web Crawler completed: %s", summary)
    except Exception as exc:
        logger.error("Error in scheduled crawler job: %s", exc, exc_info=True)


async def scheduled_groq_worker_job() -> None:
    """Periodic throttling worker that takes 2-3 items from queue and classifies via Groq."""
    try:
        summary = await pipeline_manager.process_groq_queue(batch_size=3)
        if summary.get("processed", 0) > 0:
            logger.info("Groq worker processed: %s", summary)
    except Exception as exc:
        logger.error("Error in scheduled Groq worker job: %s", exc, exc_info=True)


def start_scheduler() -> None:
    """Configure and start background Radar Ingestion, Deep Crawler, and Groq Worker schedulers."""
    if not APSCHEDULER_AVAILABLE or scheduler is None:
        logger.warning("APScheduler is not installed. Background jobs will be inactive.")
        return

    if scheduler.running:
        logger.warning("Scheduler is already running.")
        return

    interval_minutes = max(1, settings.SCAN_INTERVAL_MINUTES)
    
    # 1. Radar Ingestion Job (e.g. every 60 mins)
    scheduler.add_job(
        scheduled_radar_job,
        trigger=IntervalTrigger(minutes=interval_minutes),
        id=JOB_RADAR_INGEST,
        name="Global Radar Ingestion",
        replace_existing=True,
    )

    # 2. Deep Web Crawler Job (e.g. every 90 mins or interval)
    scheduler.add_job(
        scheduled_crawler_job,
        trigger=IntervalTrigger(minutes=max(15, int(interval_minutes * 1.5))),
        id=JOB_DEEP_CRAWLER,
        name="Deep Web AI Search Crawler",
        replace_existing=True,
    )

    # 3. Throttled Groq AI Worker Job (every 3 minutes)
    scheduler.add_job(
        scheduled_groq_worker_job,
        trigger=IntervalTrigger(minutes=3),
        id=JOB_GROQ_WORKER,
        name="Groq Throttled AI Classification",
        replace_existing=True,
    )

    scheduler.start()
    logger.info(
        "APScheduler started: Radar Ingest (%dm), Deep Crawler (%dm) & Groq Worker (3m).",
        interval_minutes,
        max(15, int(interval_minutes * 1.5)),
    )


def shutdown_scheduler() -> None:
    """Gracefully stop the background scheduler."""
    if APSCHEDULER_AVAILABLE and scheduler and scheduler.running:
        scheduler.shutdown(wait=False)
        logger.info("APScheduler stopped.")


def get_scheduler_status() -> Dict[str, Any]:
    """Retrieve current scheduler state and next execution time."""
    if not APSCHEDULER_AVAILABLE or scheduler is None:
        return {
            "available": False,
            "running": False,
            "is_paused": _is_paused,
            "job_id": JOB_RADAR_INGEST,
            "interval_minutes": settings.SCAN_INTERVAL_MINUTES,
            "next_run_time": None,
            "pipeline_running": pipeline_manager.is_ingesting or pipeline_manager.is_classifying,
        }

    job = scheduler.get_job(JOB_RADAR_INGEST) if scheduler.running else None
    next_run = None
    if not _is_paused and job and job.next_run_time:
        next_run = job.next_run_time.isoformat()

    return {
        "available": True,
        "running": scheduler.running,
        "is_paused": _is_paused,
        "job_id": JOB_RADAR_INGEST,
        "interval_minutes": settings.SCAN_INTERVAL_MINUTES,
        "next_run_time": next_run,
        "pipeline_running": pipeline_manager.is_ingesting or pipeline_manager.is_classifying,
    }
