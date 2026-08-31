"""Telegram Bot and Dispatcher setup for aiogram 3.x."""

from typing import Optional
from aiogram import Bot, Dispatcher
from aiogram.client.default import DefaultBotProperties
from aiogram.enums import ParseMode

from backend.bot.handlers import router
from backend.core.config import get_settings


def create_bot(token: Optional[str] = None) -> Bot:
    """Create and configure an aiogram Bot instance with HTML parse mode."""
    settings = get_settings()
    bot_token = token or settings.BOT_TOKEN
    return Bot(
        token=bot_token,
        default=DefaultBotProperties(parse_mode=ParseMode.HTML),
    )


def create_dispatcher() -> Dispatcher:
    """Create and configure an aiogram Dispatcher with registered routers."""
    dp = Dispatcher()
    dp.include_router(router)
    return dp
