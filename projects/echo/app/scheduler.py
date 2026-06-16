"""Deciding *when* and *what* to send (spec §10).

Not a dumb cron: each tick computes a probability from the time-of-day window, the
remaining daily quota, the minimum gap and how often the user has been ignoring Echo
lately. Role choice is weighted by learned preferences and biased by the window.
"""
from __future__ import annotations

import logging
import random
from datetime import datetime, timezone
from zoneinfo import ZoneInfo

from aiogram import Bot
from aiogram.types import InlineKeyboardButton, InlineKeyboardMarkup

from . import db
from .config import Settings
from .generator import generate_message
from .roles import ROLES, WINDOW_BIAS, Window, window_for_hour

logger = logging.getLogger(__name__)


def feedback_keyboard() -> InlineKeyboardMarkup:
    """The 👍 / 👎 / 🤔 row under every Echo message — the main learning signal."""
    return InlineKeyboardMarkup(inline_keyboard=[[
        InlineKeyboardButton(text="👍 Понравилось", callback_data="rx:like"),
        InlineKeyboardButton(text="👎 Не зашло", callback_data="rx:dislike"),
        InlineKeyboardButton(text="🤔 Нормально", callback_data="rx:neutral"),
    ]])


def now_local(tz_name: str) -> datetime:
    return datetime.now(ZoneInfo(tz_name))


def utc_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def _parse(iso: str | None) -> datetime | None:
    if not iso:
        return None
    try:
        return datetime.fromisoformat(iso)
    except ValueError:
        return None


def within_quiet(hour: int, start: int, end: int) -> bool:
    if start == end:
        return False
    if start < end:
        return start <= hour < end
    return hour >= start or hour < end  # wraps past midnight, e.g. 23..8


def choose_role(window_key: str, last_role: str | None) -> str | None:
    roles = db.get_roles()
    bias = WINDOW_BIAS.get(window_key, {})
    weighted = {
        key: state["weight"] * bias.get(key, 1.0)
        for key, state in roles.items()
        if state["enabled"] and key in ROLES and ROLES[key].scheduled
    }
    if not weighted:
        return None
    if last_role in weighted and len(weighted) > 1:
        weighted[last_role] *= 0.25  # discourage immediate repeats
    population = list(weighted.keys())
    return random.choices(population, weights=[weighted[k] for k in population])[0]


async def _compose_and_send(
    bot: Bot, settings: Settings, role_key: str, window: Window, ignore_streak: int, sent_today: int, local_date: str
) -> bool:
    recent = db.recent_contents()
    text, source = await generate_message(settings, role_key, window, recent)
    try:
        sent = await bot.send_message(settings.owner_id, text, reply_markup=feedback_keyboard())
    except Exception as exc:  # noqa: BLE001 — Telegram send can fail many ways; log and skip
        logger.error("Failed to send message: %s", exc)
        return False
    db.log_message(utc_iso(), role_key, source, text, sent.message_id)
    db.update_runtime(
        last_sent_at=utc_iso(),
        sent_today=sent_today + 1,
        today_date=local_date,
        last_role=role_key,
        ignore_streak=ignore_streak,
    )
    logger.info("Sent [%s/%s] %r", role_key, source, text[:60])
    return True


async def maybe_send(bot: Bot, settings: Settings) -> None:
    """One scheduler tick: decide whether to write, and if so, in which role."""
    user = db.get_user()
    if user["paused"]:
        return

    now = now_local(settings.timezone)
    local_date = now.date().isoformat()
    runtime = db.get_runtime()

    silence_until = _parse(runtime["silence_until"])
    if silence_until and datetime.now(timezone.utc) < silence_until:
        return

    if within_quiet(now.hour, user["quiet_start"], user["quiet_end"]):
        return

    window = window_for_hour(now.hour)
    if window is None:
        return

    sent_today = runtime["sent_today"] if runtime["today_date"] == local_date else 0
    if sent_today >= user["max_per_day"]:
        return

    last_sent = _parse(runtime["last_sent_at"])
    if last_sent:
        gap_minutes = (datetime.now(timezone.utc) - last_sent).total_seconds() / 60
        if gap_minutes < user["min_gap_minutes"]:
            return

    ignore_streak = db.trailing_unanswered()
    ignore_penalty = 1.0 / (1.0 + 0.5 * ignore_streak)
    probability = window.base_prob * ignore_penalty
    if random.random() >= probability:
        return

    role_key = choose_role(window.key, runtime["last_role"])
    if role_key is None:
        logger.warning("No roles enabled — nothing to send")
        return

    await _compose_and_send(bot, settings, role_key, window, ignore_streak, sent_today, local_date)


async def force_send(bot: Bot, settings: Settings, role_key: str | None = None) -> bool:
    """Send right now, bypassing quiet hours / quota / probability (used by /now)."""
    now = now_local(settings.timezone)
    local_date = now.date().isoformat()
    runtime = db.get_runtime()
    window = window_for_hour(now.hour) or Window("day", "день", "по запросу", 0.0)
    if role_key is None:
        role_key = choose_role(window.key, runtime["last_role"])
    if role_key is None:
        return False
    sent_today = runtime["sent_today"] if runtime["today_date"] == local_date else 0
    return await _compose_and_send(bot, settings, role_key, window, db.trailing_unanswered(), sent_today, local_date)
