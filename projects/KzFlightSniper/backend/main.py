"""FastAPI entrypoint and application lifespan management for KzFlightSniper."""

import asyncio
from contextlib import asynccontextmanager
import logging
from typing import Any, AsyncGenerator, Dict, List
from fastapi import FastAPI, HTTPException, status
from fastapi.responses import JSONResponse

from backend.bot.bot import create_bot, create_dispatcher
from backend.core.config import get_settings
from backend.core.models import HealthResponse, TaskRead
from backend.db.dao import FlightSniperDAO
from backend.db.database import init_db
from backend.engine.scheduler import start_scheduler, stop_scheduler
from backend.engine.sniper_worker import run_sniper_check

# Configure logging
settings = get_settings()
logging.basicConfig(
    level=getattr(logging, settings.LOG_LEVEL.upper(), logging.INFO),
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)
logger = logging.getLogger("kzflight_sniper.main")

dao = FlightSniperDAO()
bot = None
dp = None
bot_task = None


@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncGenerator[None, None]:
    """Application lifespan context manager for startup and shutdown."""
    global bot, dp, bot_task

    # 1. Database Initialization
    logger.info("Initializing SQLite database at %s...", settings.DATABASE_PATH)
    await init_db()
    logger.info("Database schema initialized successfully.")

    # 2. Telegram Bot Polling (if configured)
    if settings.is_bot_token_configured:
        try:
            logger.info("Initializing Telegram Bot...")
            bot = create_bot()
            dp = create_dispatcher()
            logger.info("Starting Telegram Bot long-polling in background task...")
            bot_task = asyncio.create_task(dp.start_polling(bot))
            logger.info("Telegram Bot polling started.")
        except Exception as e:
            logger.warning("Could not initialize Telegram Bot polling: %s", e)
    else:
        logger.warning(
            "Telegram BOT_TOKEN is not configured or is placeholder (%s). "
            "Bot polling disabled. REST API is active.",
            settings.BOT_TOKEN[:8] + "..." if len(settings.BOT_TOKEN) > 8 else settings.BOT_TOKEN,
        )

    # 3. Start Periodic Flight Check Scheduler
    try:
        logger.info("Starting APScheduler flight check scheduler...")
        start_scheduler(bot=bot if settings.is_bot_token_configured else None)
        logger.info("Scheduler started successfully.")
    except Exception as e:
        logger.warning("Could not start scheduler: %s", e)

    yield

    # 4. Graceful Shutdown
    logger.info("Shutting down KzFlightSniper services...")
    stop_scheduler()

    if bot_task and not bot_task.done():
        bot_task.cancel()
        try:
            await bot_task
        except asyncio.CancelledError:
            pass
        logger.info("Telegram Bot background polling cancelled.")

    if bot and bot.session:
        await bot.session.close()
        logger.info("Telegram Bot session closed.")

    logger.info("KzFlightSniper shutdown complete.")


app = FastAPI(
    title="KzFlightSniper API",
    description="Asynchronous flight tracking, price monitoring, and automated alerting engine for Kazakhstan aviation.",
    version="1.0.0",
    lifespan=lifespan,
)


@app.get("/", summary="Service Information")
async def root() -> Dict[str, Any]:
    """Return basic service information and operational status."""
    return {
        "app": "KzFlightSniper",
        "description": "Kazakhstan Flight Price Sniper & Alerting Engine",
        "version": "1.0.0",
        "status": "running",
        "bot_configured": settings.is_bot_token_configured,
        "database_path": settings.DATABASE_PATH,
    }


@app.get("/health", response_model=HealthResponse, summary="Service Health Check")
async def health_check() -> HealthResponse:
    """Return healthcheck status, database connectivity, and count of active tasks."""
    try:
        active_count = await dao.get_active_tasks_count()
        return HealthResponse(
            status="ok",
            database="connected",
            active_tasks=active_count,
            version="1.0.0",
        )
    except Exception as e:
        logger.exception("Health check database query failed: %s", e)
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail=f"Database connectivity failure: {str(e)}",
        )


@app.get("/api/tasks", response_model=List[TaskRead], summary="List Active Tasks")
async def list_active_tasks() -> List[TaskRead]:
    """Retrieve all currently active flight monitoring tasks."""
    try:
        tasks = await dao.get_active_tasks()
        return [TaskRead(**task) for task in tasks]
    except Exception as e:
        logger.exception("Failed to retrieve active tasks: %s", e)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to retrieve tasks from database.",
        )


@app.post("/api/check-now", summary="Trigger Manual Flight Sniper Check")
async def trigger_check_now() -> Dict[str, Any]:
    """Trigger an immediate flight price check cycle across all active tasks."""
    try:
        stats = await run_sniper_check(
            bot=bot if settings.is_bot_token_configured else None,
            dao=dao,
        )
        return {
            "status": "success",
            "message": "Manual sniper check cycle completed.",
            "stats": stats,
        }
    except Exception as e:
        logger.exception("Manual flight check execution failed: %s", e)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Check cycle failed: {str(e)}",
        )


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(
        "backend.main:app",
        host="0.0.0.0",
        port=settings.APP_PORT,
        reload=False,
    )
