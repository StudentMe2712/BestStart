"""Telegram interface (aiogram 3.x).

Single-user companion: every handler is guarded so the bot only talks to its owner.
Commands let the owner steer cadence and roles; plain replies and reactions feed the
preference engine and may trigger a short in-role follow-up.
"""
from __future__ import annotations

import logging
import random

from aiogram import Bot, Dispatcher, F
from aiogram.filters import Command, CommandObject, CommandStart
from aiogram.types import Message, MessageReactionUpdated, ReactionTypeEmoji

from . import db, preferences
from .config import Settings
from .generator import generate_followup
from .roles import ROLES
from .scheduler import force_send, now_local, utc_iso

logger = logging.getLogger(__name__)

FOLLOWUP_PROBABILITY = 0.7
POSITIVE = {"👍", "❤", "❤️", "🔥", "🙏", "😍", "👏", "🤝", "💯", "🥰", "😎", "⚡"}
NEGATIVE = {"👎", "💩", "🤮", "😡", "🤬"}

MAX_PER_DAY_CAP = 6
MIN_GAP_FLOOR = 45
MIN_GAP_CAP = 240

GREETING = (
    "Привет. Я Echo — пишу первым, сам, в осмысленный момент: вопрос, мысль, "
    "брейншторм, иногда — напоминание встать и подышать.\n\n"
    "Я стараюсь не спамить и умею молчать. Со временем подстраиваюсь под то, "
    "на что ты отвечаешь.\n\n"
    "Команды: /help"
)

HELP = (
    "Что я понимаю:\n"
    "/now [роль] — напиши прямо сейчас\n"
    "/mode — какие роли активны и насколько\n"
    "/mode <роль> — больше таких сообщений\n"
    "/mode <роль> on|off — включить/выключить роль\n"
    "/more — чаще   /less — реже\n"
    "/quiet [часы] — помолчи (по умолчанию 4 ч)\n"
    "/pause — выключить   /resume — включить\n"
    "/stats — статистика\n\n"
    f"Роли: {', '.join(f'{r.key} ({r.name})' for r in ROLES.values())}"
)


def _role_lookup() -> dict[str, str]:
    table: dict[str, str] = {}
    for key, spec in ROLES.items():
        table[key] = key
        table[spec.name.lower()] = key
    return table


def _is_owner(user_id: int | None, settings: Settings) -> bool:
    return user_id is not None and user_id == settings.owner_id


def _format_roles() -> str:
    roles = db.get_roles()
    lines = []
    for key, state in sorted(roles.items(), key=lambda kv: kv[1]["weight"], reverse=True):
        flag = "✓" if state["enabled"] else "✗"
        lines.append(f"{flag} {ROLES[key].name} ({key}) · вес {state['weight']:.2f}")
    return "\n".join(lines)


async def cmd_start(message: Message, settings: Settings) -> None:
    if not _is_owner(message.from_user.id if message.from_user else None, settings):
        return
    await message.answer(GREETING)


async def cmd_help(message: Message, settings: Settings) -> None:
    if not _is_owner(message.from_user.id if message.from_user else None, settings):
        return
    await message.answer(HELP)


async def cmd_pause(message: Message, settings: Settings) -> None:
    if not _is_owner(message.from_user.id if message.from_user else None, settings):
        return
    db.update_user(paused=1)
    await message.answer("Окей, замолкаю. Верни меня командой /resume.")


async def cmd_resume(message: Message, settings: Settings) -> None:
    if not _is_owner(message.from_user.id if message.from_user else None, settings):
        return
    db.update_user(paused=0)
    db.update_runtime(silence_until=None)
    await message.answer("Снова на связи.")


async def cmd_quiet(message: Message, command: CommandObject, settings: Settings) -> None:
    if not _is_owner(message.from_user.id if message.from_user else None, settings):
        return
    hours = 4
    if command.args:
        try:
            hours = max(1, min(24, int(command.args.strip())))
        except ValueError:
            hours = 4
    from datetime import datetime, timedelta, timezone
    until = datetime.now(timezone.utc) + timedelta(hours=hours)
    db.update_runtime(silence_until=until.isoformat())
    await message.answer(f"Тихо ближайшие {hours} ч.")


async def cmd_more(message: Message, settings: Settings) -> None:
    if not _is_owner(message.from_user.id if message.from_user else None, settings):
        return
    user = db.get_user()
    new_max = min(MAX_PER_DAY_CAP, user["max_per_day"] + 1)
    new_gap = max(MIN_GAP_FLOOR, user["min_gap_minutes"] - 30)
    db.update_user(max_per_day=new_max, min_gap_minutes=new_gap)
    await message.answer(f"Буду чаще: до {new_max} в день, паузы от {new_gap} мин.")


async def cmd_less(message: Message, settings: Settings) -> None:
    if not _is_owner(message.from_user.id if message.from_user else None, settings):
        return
    user = db.get_user()
    new_max = max(1, user["max_per_day"] - 1)
    new_gap = min(MIN_GAP_CAP, user["min_gap_minutes"] + 30)
    db.update_user(max_per_day=new_max, min_gap_minutes=new_gap)
    await message.answer(f"Буду реже: до {new_max} в день, паузы от {new_gap} мин.")


async def cmd_mode(message: Message, command: CommandObject, settings: Settings) -> None:
    if not _is_owner(message.from_user.id if message.from_user else None, settings):
        return
    args = (command.args or "").split()
    if not args:
        await message.answer("Роли сейчас:\n" + _format_roles())
        return

    lookup = _role_lookup()
    role_key = lookup.get(args[0].lower())
    if role_key is None:
        await message.answer("Не знаю такую роль. Список: /mode")
        return

    if len(args) >= 2 and args[1].lower() in {"on", "off"}:
        db.set_role_enabled(role_key, args[1].lower() == "on")
        await message.answer(f"{ROLES[role_key].name}: {'включил' if args[1].lower() == 'on' else 'выключил'}.")
        return

    db.set_role_enabled(role_key, True)
    db.adjust_weight(role_key, 1.4)
    await message.answer(f"Хорошо, больше таких сообщений ({ROLES[role_key].name}).")


async def cmd_now(message: Message, command: CommandObject, settings: Settings) -> None:
    if not _is_owner(message.from_user.id if message.from_user else None, settings):
        return
    role_key = None
    if command.args:
        role_key = _role_lookup().get(command.args.strip().lower())
        if role_key is None:
            await message.answer("Не знаю такую роль. Список: /mode")
            return
    if message.bot is None:
        return
    sent = await force_send(message.bot, settings, role_key)
    if not sent:
        await message.answer("Сейчас не получилось — нет активных ролей? Проверь /mode.")


async def cmd_stats(message: Message, settings: Settings) -> None:
    if not _is_owner(message.from_user.id if message.from_user else None, settings):
        return
    s = db.stats()
    runtime = db.get_runtime()
    local_date = now_local(settings.timezone).date().isoformat()
    today = runtime["sent_today"] if runtime["today_date"] == local_date else 0
    top = sorted(db.get_roles().items(), key=lambda kv: kv[1]["weight"], reverse=True)[:3]
    top_str = ", ".join(f"{ROLES[k].name}" for k, _ in top)
    await message.answer(
        f"Сегодня отправлено: {today}\n"
        f"Всего сообщений: {s['total']}\n"
        f"С ответом: {s['replied']} · с 👍: {s['liked']}\n"
        f"Любимые роли: {top_str}"
    )


async def on_text(message: Message, settings: Settings) -> None:
    """A plain reply from the owner: record engagement, maybe follow up in-role."""
    if not _is_owner(message.from_user.id if message.from_user else None, settings):
        return
    last = db.last_message()
    if last is None:
        return
    db.set_reply(last["id"], message.text or "", utc_iso())
    preferences.on_reply(last["role"])

    if random.random() < FOLLOWUP_PROBABILITY:
        followup = await generate_followup(settings, last["role"], last["content"], message.text or "")
        if followup:
            await message.answer(followup)


async def on_reaction(event: MessageReactionUpdated, settings: Settings) -> None:
    if not _is_owner(event.user.id if event.user else None, settings):
        return
    emojis = {r.emoji for r in event.new_reaction if isinstance(r, ReactionTypeEmoji)}
    if not emojis:
        return
    msg = db.message_by_tg(event.message_id)
    if msg is None:
        return
    if emojis & POSITIVE:
        db.set_reaction(msg["id"], "like")
        preferences.on_reaction(msg["role"], liked=True)
    elif emojis & NEGATIVE:
        db.set_reaction(msg["id"], "dislike")
        preferences.on_reaction(msg["role"], liked=False)


def build_dispatcher(settings: Settings) -> Dispatcher:
    dp = Dispatcher()
    dp["settings"] = settings

    dp.message.register(cmd_start, CommandStart())
    dp.message.register(cmd_help, Command("help"))
    dp.message.register(cmd_pause, Command("pause"))
    dp.message.register(cmd_resume, Command("resume"))
    dp.message.register(cmd_quiet, Command("quiet"))
    dp.message.register(cmd_more, Command("more"))
    dp.message.register(cmd_less, Command("less"))
    dp.message.register(cmd_mode, Command("mode"))
    dp.message.register(cmd_now, Command("now"))
    dp.message.register(cmd_stats, Command("stats"))
    dp.message.register(on_text, F.text & ~F.text.startswith("/"))
    dp.message_reaction.register(on_reaction)
    return dp
