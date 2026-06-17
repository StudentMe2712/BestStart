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
from .roles import ROLES, WINDOW_BIAS, RoleSpec, Window, window_for_hour

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


# Echo's daily volume isn't a fixed cap: each day draws its own ceiling and spacing so the
# rhythm varies naturally. Stored max_per_day / min_gap_minutes are the *anchors* the owner
# tunes (/more, /less); the effective values float around them within these spans.
CAP_LOW_FRAC = 0.6   # today's cap is drawn from [CAP_LOW_FRAC * max_per_day, max_per_day]
GAP_HIGH_FRAC = 1.5  # today's gap is drawn from [min_gap_minutes, GAP_HIGH_FRAC * min_gap_minutes]


def _daily_rng(local_date: str, salt: int) -> random.Random:
    """Per-day deterministic RNG: stable within a day, varies day to day, survives restart.

    Seeded from the date itself (not process state), so every tick of the same day agrees on
    the day's cap and gap. ``salt`` decouples independent draws (cap vs gap).
    """
    return random.Random(int(local_date.replace("-", "")) * 100 + salt)


def effective_daily_cap(local_date: str, max_per_day: int) -> int:
    """Today's message ceiling: a random integer in [CAP_LOW_FRAC * max, max]."""
    low = max(1, round(max_per_day * CAP_LOW_FRAC))
    return _daily_rng(local_date, 0).randint(low, max(low, max_per_day))


def effective_min_gap(local_date: str, min_gap_minutes: int) -> int:
    """Today's minimum spacing: a random integer in [min_gap, GAP_HIGH_FRAC * min_gap] minutes."""
    high = round(min_gap_minutes * GAP_HIGH_FRAC)
    return _daily_rng(local_date, 1).randint(min_gap_minutes, max(min_gap_minutes, high))


def _role_available(spec: RoleSpec, hour: int, now_utc: datetime) -> bool:
    """V4 timing gates: Coach lives only in its work-hour window; rare roles honour a cooldown."""
    if spec.active_hours is not None:
        start, end = spec.active_hours
        if not (start <= hour < end):
            return False
    if spec.cooldown_minutes > 0:
        since = db.minutes_since_role(spec.key, now_utc)
        if since is not None and since < spec.cooldown_minutes:
            return False
    return True


def choose_role(
    window_key: str, hour: int, last_role: str | None, now_utc: datetime,
    respect_constraints: bool = True,
) -> str | None:
    roles = db.get_roles()
    bias = WINDOW_BIAS.get(window_key, {})
    weighted: dict[str, float] = {}
    for key, state in roles.items():
        if not (state["enabled"] and key in ROLES and ROLES[key].scheduled):
            continue
        if respect_constraints and not _role_available(ROLES[key], hour, now_utc):
            continue
        weighted[key] = state["weight"] * bias.get(key, 1.0)
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
    facts = db.fact_texts()
    text, source = await generate_message(settings, role_key, window, recent, facts=facts)
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
        logger.info("skip: paused")
        return

    now = now_local(settings.timezone)
    local_date = now.date().isoformat()
    runtime = db.get_runtime()

    silence_until = _parse(runtime["silence_until"])
    if silence_until and datetime.now(timezone.utc) < silence_until:
        logger.info("skip: silenced until %s", runtime["silence_until"])
        return

    if within_quiet(now.hour, user["quiet_start"], user["quiet_end"]):
        logger.info(
            "skip: quiet hours (hour=%d quiet=%d-%d)",
            now.hour, user["quiet_start"], user["quiet_end"],
        )
        return

    window = window_for_hour(now.hour)
    if window is None:
        logger.info("skip: outside active window (hour=%d)", now.hour)
        return

    daily_cap = effective_daily_cap(local_date, user["max_per_day"])
    sent_today = runtime["sent_today"] if runtime["today_date"] == local_date else 0
    if sent_today >= daily_cap:
        logger.info(
            "skip: daily limit reached (sent_today=%d daily_cap=%d max_per_day=%d)",
            sent_today, daily_cap, user["max_per_day"],
        )
        return

    last_sent = _parse(runtime["last_sent_at"])
    min_gap = effective_min_gap(local_date, user["min_gap_minutes"])
    minutes_since_last_send = (
        (datetime.now(timezone.utc) - last_sent).total_seconds() / 60 if last_sent else None
    )
    if minutes_since_last_send is not None and minutes_since_last_send < min_gap:
        logger.info(
            "skip: min gap not passed (minutes_since_last_send=%.1f min_gap=%d base_gap=%d)",
            minutes_since_last_send, min_gap, user["min_gap_minutes"],
        )
        return

    # Gentle back-off: at high cadence not every message earns a reaction, so the penalty
    # is mild and floored (~0.5 at worst) — Echo eases off if fully ignored, never goes mute.
    ignore_streak = db.trailing_unanswered()
    ignore_penalty = 1.0 / (1.0 + 0.15 * min(ignore_streak, 8))
    probability = window.base_prob * ignore_penalty
    random_roll = random.random()
    if random_roll >= probability:
        logger.info(
            "skip: probability roll failed (random_roll=%.3f probability=%.3f "
            "sent_today=%d daily_cap=%d minutes_since_last_send=%s)",
            random_roll, probability, sent_today, daily_cap,
            "n/a" if minutes_since_last_send is None else f"{minutes_since_last_send:.1f}",
        )
        return

    role_key = choose_role(window.key, now.hour, runtime["last_role"], datetime.now(timezone.utc))
    if role_key is None:
        logger.warning("skip: role unavailable (none eligible right now)")
        return

    await _compose_and_send(bot, settings, role_key, window, ignore_streak, sent_today, local_date)


async def force_send(bot: Bot, settings: Settings, role_key: str | None = None) -> bool:
    """Send right now, bypassing quiet hours / quota / probability (used by /now)."""
    now = now_local(settings.timezone)
    local_date = now.date().isoformat()
    runtime = db.get_runtime()
    window = window_for_hour(now.hour) or Window("day", "день", "по запросу", 0.0)
    if role_key is None:
        # /now is a manual override: ignore the work-hour / cooldown gates so any role works.
        role_key = choose_role(
            window.key, now.hour, runtime["last_role"],
            datetime.now(timezone.utc), respect_constraints=False,
        )
    if role_key is None:
        return False
    sent_today = runtime["sent_today"] if runtime["today_date"] == local_date else 0
    return await _compose_and_send(bot, settings, role_key, window, db.trailing_unanswered(), sent_today, local_date)
