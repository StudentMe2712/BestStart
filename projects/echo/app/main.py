"""Echo entrypoint: run the Telegram bot and the scheduler in one asyncio loop."""
from __future__ import annotations

import asyncio
import logging

from aiogram import Bot
from apscheduler.schedulers.asyncio import AsyncIOScheduler

from . import db
from .bot import build_dispatcher
from .config import load_settings
from .scheduler import maybe_send, utc_iso


async def main() -> None:
    settings = load_settings()
    logging.basicConfig(
        level=settings.log_level,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )
    log = logging.getLogger("echo")

    if not settings.bot_token:
        raise SystemExit("TELEGRAM_BOT_TOKEN is not set (see .env)")
    if not settings.owner_id:
        raise SystemExit("OWNER_USER_ID is not set (see .env)")

    db.init(settings.db_path)
    db.ensure_user(settings, utc_iso())

    bot = Bot(settings.bot_token)
    dp = build_dispatcher(settings)

    scheduler = AsyncIOScheduler(timezone=settings.timezone)
    scheduler.add_job(maybe_send, "interval", minutes=settings.tick_minutes, args=[bot, settings])
    scheduler.start()
    log.info(
        "Echo started: tick=%dm tz=%s roles=%s",
        settings.tick_minutes, settings.timezone, settings.enabled_roles,
    )

    try:
        await dp.start_polling(bot, allowed_updates=dp.resolve_used_update_types())
    finally:
        scheduler.shutdown(wait=False)


if __name__ == "__main__":
    asyncio.run(main())
