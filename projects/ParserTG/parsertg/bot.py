"""Control bot (python-telegram-bot). Long-polling. Every handler is owner-guarded:
the bot answers exactly one person (ADMIN_USER_ID) and ignores everyone else.

Commands: /parse N, /feed, /status (plus /start help)."""
from __future__ import annotations

import html
import logging
from datetime import datetime
from functools import wraps

from telegram import LinkPreviewOptions, Update
from telegram.constants import ParseMode
from telegram.ext import Application, CommandHandler, ContextTypes

from . import db, parser
from .config import Settings, load_settings

log = logging.getLogger(__name__)


# --- helpers (pure -> unit-tested) -------------------------------------------------

def _parse_n(args: list[str], default: int) -> int | None:
    """N from /parse args. Empty -> default. Non-positive / non-numeric -> None (usage error)."""
    if not args:
        return default
    try:
        n = int(args[0])
    except ValueError:
        return None
    return n if n > 0 else None


def _fmt_date(iso: str) -> str:
    try:
        return datetime.fromisoformat(iso).strftime("%Y-%m-%d %H:%M")
    except ValueError:
        return iso


def _shorten(text: str, limit: int = 200) -> str:
    collapsed = " ".join(text.split())
    if len(collapsed) <= limit:
        return collapsed
    return collapsed[: limit - 1] + "…"


def format_feed(rows, empty: str = "Пока нет сохранённых сообщений. Запусти /parse N.") -> str:
    if not rows:
        return empty
    blocks: list[str] = []
    for row in rows:
        channel = html.escape(str(row["channel"]))
        head = f"<b>{channel}</b> · {_fmt_date(row['date'])}"
        link = row["link"]
        if link:
            head = f'<a href="{html.escape(link)}">{head}</a>'
        body = html.escape(_shorten(row["text"])) or "[без текста]"
        blocks.append(f"{head}\n{body}")
    return "\n\n".join(blocks)


def format_status(stats: db.Stats) -> str:
    lines = [
        "📊 ParserTG",
        f"Каналов: {stats.channel_count}",
        f"Сообщений: {stats.message_count}",
    ]
    if stats.last:
        lines.append(
            f"Последнее: {stats.last['channel']} #{stats.last['message_id']} "
            f"({_fmt_date(stats.last['date'])})"
        )
    else:
        lines.append("Последнее: —")
    return "\n".join(lines)


def format_parse_summary(results: list[parser.ChannelResult]) -> str:
    if not results:
        return "Нет настроенных каналов. Добавь их в CHANNELS (.env) и перезапусти бота."
    total = sum(r.saved for r in results)
    lines = [f"Готово. Новых сообщений: {total}."]
    for r in results:
        lines.append(f"• {r.channel}: {r.error}" if r.error else f"• {r.channel}: +{r.saved}")
    return "\n".join(lines)


# --- access guard ------------------------------------------------------------------

def _owner_only(handler):
    @wraps(handler)
    async def wrapper(update: Update, context: ContextTypes.DEFAULT_TYPE):
        settings: Settings = context.bot_data["settings"]
        user = update.effective_user
        if user is None or user.id != settings.admin_id:
            log.info("ignored non-admin user %s", getattr(user, "id", "?"))
            return None
        return await handler(update, context)

    return wrapper


# --- command handlers --------------------------------------------------------------

@_owner_only
async def cmd_start(update: Update, context: ContextTypes.DEFAULT_TYPE):
    await update.message.reply_text(
        "ParserTG на связи.\n\n"
        "/parse N — забрать следующие N сообщений из каждого канала\n"
        "/feed — последние сохранённые записи\n"
        "/status — статистика"
    )


@_owner_only
async def cmd_parse(update: Update, context: ContextTypes.DEFAULT_TYPE):
    settings: Settings = context.bot_data["settings"]
    client = context.bot_data["client"]

    n = _parse_n(context.args, settings.parse_default_n)
    if n is None:
        await update.message.reply_text("Использование: /parse N (N — положительное число).")
        return
    if not await client.is_user_authorized():
        await update.message.reply_text(
            "Telethon не авторизован. Один раз выполни в терминале: python -m parsertg.login"
        )
        return

    await update.message.reply_text(f"Парсю следующие {n} сообщений из каждого канала…")
    try:
        results = await parser.parse_channels(client, settings, n)
    except Exception:  # noqa: BLE001 — log with stack and tell the admin, never swallow
        log.exception("parse failed")
        await update.message.reply_text("Ошибка парсинга — смотри логи.")
        return
    await update.message.reply_text(format_parse_summary(results))


@_owner_only
async def cmd_feed(update: Update, context: ContextTypes.DEFAULT_TYPE):
    settings: Settings = context.bot_data["settings"]
    rows = db.get_recent_messages(settings.db_path, settings.feed_limit)
    await update.message.reply_text(
        format_feed(rows),
        parse_mode=ParseMode.HTML,
        link_preview_options=LinkPreviewOptions(is_disabled=True),
    )


@_owner_only
async def cmd_status(update: Update, context: ContextTypes.DEFAULT_TYPE):
    settings: Settings = context.bot_data["settings"]
    await update.message.reply_text(format_status(db.get_stats(settings.db_path)))


# --- lifecycle ---------------------------------------------------------------------

async def _on_startup(app: Application) -> None:
    """Connect the Telethon client inside the bot's running loop, so handlers share it."""
    settings: Settings = app.bot_data["settings"]
    client = parser.build_client(settings)
    await client.connect()
    app.bot_data["client"] = client
    if await client.is_user_authorized():
        log.info("Telethon connected and authorized.")
    else:
        log.warning("Telethon session not authorized — run once: python -m parsertg.login")


async def _on_shutdown(app: Application) -> None:
    client = app.bot_data.get("client")
    if client is not None and client.is_connected():
        await client.disconnect()


def main() -> None:
    settings = load_settings()
    logging.basicConfig(
        level=settings.log_level,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )
    if not settings.bot_token:
        raise SystemExit("TELEGRAM_BOT_TOKEN не задан в .env")
    if not settings.admin_id:
        raise SystemExit("ADMIN_USER_ID не задан в .env")

    db.init_db(settings.db_path)
    db.sync_channels(settings.db_path, settings.channels)

    app = (
        Application.builder()
        .token(settings.bot_token)
        .post_init(_on_startup)
        .post_shutdown(_on_shutdown)
        .build()
    )
    app.bot_data["settings"] = settings
    app.add_handler(CommandHandler("start", cmd_start))
    app.add_handler(CommandHandler("parse", cmd_parse))
    app.add_handler(CommandHandler("feed", cmd_feed))
    app.add_handler(CommandHandler("status", cmd_status))

    log.info("ParserTG starting (admin=%s, channels=%s)…", settings.admin_id, settings.channels)
    app.run_polling()
