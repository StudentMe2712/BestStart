"""aiogram 3.x Telegram command routers, NLP text handlers, and callback handlers for KzFlightSniper."""

from datetime import datetime, timezone
import logging
import uuid
from typing import Any, Dict, List, Optional, Tuple

from aiogram import F, Router
from aiogram.filters import Command, CommandObject, CommandStart
from aiogram.types import CallbackQuery, InlineKeyboardButton, InlineKeyboardMarkup, Message

from backend.bot.nlp_parser import parse_flight_request
from backend.core.models import ParsedFlightIntent
from backend.db.dao import FlightSniperDAO

logger = logging.getLogger("kzflight_sniper.bot.handlers")
router = Router(name="flight_sniper_handlers")
dao = FlightSniperDAO()

# In-memory storage for pending NLP confirmation sessions
_pending_nlp_tasks: Dict[str, Dict[str, Any]] = {}

KAZAKHSTAN_AIRPORTS_INFO = """
<b>🇰🇿 Major Kazakhstan IATA Airport Codes:</b>
• <code>ALA</code> — Almaty
• <code>NQZ</code> — Astana
• <code>CIT</code> — Shymkent
• <code>SCO</code> — Aktau
• <code>GUW</code> — Atyrau
• <code>UKK</code> — Oskemen (Ust-Kamenogorsk)
• <code>AKX</code> — Aktobe
• <code>KSG</code> — Kostanay
• <code>PWQ</code> — Pavlodar
• <code>PLX</code> — Semey
• <code>DMB</code> — Taraz
• <code>KOV</code> — Kokshetau
• <code>BXH</code> — Balkhash
• <code>URA</code> — Uralsk
• <code>KGF</code> — Karaganda
• <code>PPK</code> — Petropavlovsk
• <code>KZO</code> — Kyzylorda
"""


def parse_snipe_arguments(args_text: str) -> Tuple[str, str, str, Optional[str], float]:
    """Parse and validate arguments for /snipe command.

    Supported formats:
    1) 4 arguments: <ORIGIN> <DEST> <YYYY-MM-DD> <TARGET_PRICE>
    2) 5 arguments: <ORIGIN> <DEST> <YYYY-MM-DD> <FLIGHT_NO> <TARGET_PRICE>

    Returns:
        Tuple of (origin, destination, date_str, flight_number, target_price)

    Raises:
        ValueError with descriptive user-facing error message.
    """
    parts = args_text.strip().split()
    if len(parts) == 4:
        origin_raw, dest_raw, date_raw, price_raw = parts
        flight_number = None
    elif len(parts) == 5:
        origin_raw, dest_raw, date_raw, flight_number_raw, price_raw = parts
        flight_number = flight_number_raw.strip().upper()
    else:
        raise ValueError(
            "<b>Invalid argument count.</b>\n\n"
            "<b>Usage format:</b>\n"
            "<code>/snipe &lt;ORIGIN&gt; &lt;DEST&gt; &lt;YYYY-MM-DD&gt; &lt;TARGET_PRICE&gt;</code>\n"
            "or with specific flight filter:\n"
            "<code>/snipe &lt;ORIGIN&gt; &lt;DEST&gt; &lt;YYYY-MM-DD&gt; &lt;FLIGHT_NO&gt; &lt;TARGET_PRICE&gt;</code>\n\n"
            "<b>Examples:</b>\n"
            "• <code>/snipe ALA NQZ 2026-10-15 25000</code>\n"
            "• <code>/snipe ALA NQZ 2026-10-15 KC-871 28000</code>"
        )

    # 1. Validate Origin & Destination IATA Codes
    origin = origin_raw.strip().upper()
    dest = dest_raw.strip().upper()

    if len(origin) != 3 or not origin.isalpha():
        raise ValueError(f"Origin code <code>{origin}</code> is invalid. Must be a 3-letter IATA code (e.g. <code>ALA</code>, <code>NQZ</code>).")

    if len(dest) != 3 or not dest.isalpha():
        raise ValueError(f"Destination code <code>{dest}</code> is invalid. Must be a 3-letter IATA code (e.g. <code>NQZ</code>, <code>ALA</code>).")

    if origin == dest:
        raise ValueError(f"Origin and destination cannot be identical (<code>{origin}</code>).")

    # 2. Validate Departure Date
    try:
        flight_date = datetime.strptime(date_raw.strip(), "%Y-%m-%d").date()
    except ValueError:
        raise ValueError(f"Invalid date format: <code>{date_raw}</code>. Please use standard <code>YYYY-MM-DD</code> format (e.g. <code>2026-10-15</code>).")

    today = datetime.now(timezone.utc).date()
    if flight_date < today:
        raise ValueError(f"Departure date <code>{date_raw}</code> is in the past. Please choose today or a future date.")

    # 3. Validate Target Price
    price_clean = price_raw.replace("₸", "").replace("KZT", "").replace(",", "").replace(" ", "").strip()
    try:
        target_price = float(price_clean)
        if target_price <= 0:
            raise ValueError()
    except ValueError:
        raise ValueError(f"Invalid price value: <code>{price_raw}</code>. Must be a positive number in Tenge (e.g. <code>25000</code>).")

    return origin, dest, flight_date.isoformat(), flight_number, target_price


@router.message(CommandStart())
async def handle_start(message: Message) -> None:
    """Handle /start command with greeting, NLP capabilities, and quick-start instructions."""
    welcome_text = (
        "🦅 <b>Welcome to KzFlightSniper!</b>\n\n"
        "Your automated flight tracker for Kazakhstan aviation (Air Astana, FlyArystan, SCAT, Qazaq Air).\n"
        "We continuously monitor ticket prices and alert you instantly the moment a price drops below your budget!\n\n"
        "<b>💬 Natural Language Flight Creation:</b>\n"
        "You can simply type a message in natural language, for example:\n"
        "<i>«Рейс Алматы - Бангкок, 15 октября, прямой, KC-871, ниже 300$. Проверять каждые 5 минут»</i>\n"
        "<i>«Астана - Шымкент на 1 ноября до 20000 тг»</i>\n\n"
        "<b>⚡ Standard Commands:</b>\n"
        "• <code>/snipe ALA NQZ 2026-10-15 25000</code> — Track flights under 25,000 ₸\n"
        "• <code>/snipe ALA NQZ 2026-10-15 KC-871 28000</code> — Track specific flight\n"
        "• <code>/list</code> — View your active monitored flights\n"
        "• <code>/delete &lt;id&gt;</code> — Cancel a monitoring task\n"
        "• <code>/help</code> — Full command reference & airport codes\n"
    )
    await message.answer(welcome_text)


@router.message(Command("help"))
async def handle_help(message: Message) -> None:
    """Handle /help command with complete syntax reference, NLP examples, and airport codes."""
    help_text = (
        "📖 <b>KzFlightSniper Command Guide & Syntax</b>\n\n"
        "<b>1. Natural Language Creation (AI-Powered):</b>\n"
        "Simply send any flight request in Russian or English:\n"
        "• <i>«Билет Алматы в Стамбул на 20 октября не дороже 100000 тг»</i>\n"
        "• <i>«Из Актау в Дубай 25 декабря, рейс DV-713, до 400$, каждые 10 мин»</i>\n\n"
        "<b>2. Manual Command Creation:</b>\n"
        "<code>/snipe &lt;ORIGIN&gt; &lt;DEST&gt; &lt;YYYY-MM-DD&gt; &lt;TARGET_PRICE&gt;</code>\n"
        "<code>/snipe &lt;ORIGIN&gt; &lt;DEST&gt; &lt;YYYY-MM-DD&gt; &lt;FLIGHT_NO&gt; &lt;TARGET_PRICE&gt;</code>\n"
        "<i>Examples:</i>\n"
        "• <code>/snipe ALA NQZ 2026-10-15 25000</code>\n"
        "• <code>/snipe CIT ALA 2026-11-20 18500</code>\n"
        "• <code>/snipe ALA NQZ 2026-10-15 KC-871 28000</code>\n\n"
        "<b>3. View Active Tasks:</b>\n"
        "<code>/list</code> — Shows all your registered snipe tasks.\n\n"
        "<b>4. Delete / Cancel a Task:</b>\n"
        "<code>/delete &lt;task_id&gt;</code> or <code>/cancel &lt;task_id&gt;</code>\n"
        "<i>Example:</i> <code>/delete 42</code>\n"
        f"{KAZAKHSTAN_AIRPORTS_INFO}"
    )
    await message.answer(help_text)


@router.message(Command("snipe"))
async def handle_snipe(message: Message, command: CommandObject) -> None:
    """Handle /snipe command to register a new flight monitoring task."""
    args = command.args
    if not args:
        guide_text = (
            "🎯 <b>How to Create a Flight Snipe:</b>\n\n"
            "<b>Syntax:</b>\n"
            "<code>/snipe &lt;ORIGIN&gt; &lt;DEST&gt; &lt;YYYY-MM-DD&gt; &lt;TARGET_PRICE&gt;</code>\n"
            "or with flight filter:\n"
            "<code>/snipe &lt;ORIGIN&gt; &lt;DEST&gt; &lt;YYYY-MM-DD&gt; &lt;FLIGHT_NO&gt; &lt;TARGET_PRICE&gt;</code>\n\n"
            "<b>Examples:</b>\n"
            "• <code>/snipe ALA NQZ 2026-10-15 25000</code>\n"
            "• <code>/snipe ALA NQZ 2026-10-15 KC-871 28000</code>\n"
            "• <code>/snipe NQZ SCO 2026-11-01 32000</code>\n\n"
            "💡 <i>Tip: You can also just type your request in natural language!</i>"
        )
        await message.answer(guide_text)
        return

    try:
        origin, dest, flight_date, flight_number, target_price = parse_snipe_arguments(args)
    except ValueError as e:
        await message.answer(f"⚠️ {str(e)}")
        return
    except Exception as e:
        logger.exception("Unexpected error parsing /snipe arguments: %s", args)
        await message.answer("❌ An error occurred while parsing your request. Please check <code>/help</code>.")
        return

    try:
        task_id = await dao.add_task(
            chat_id=message.chat.id,
            origin=origin,
            destination=dest,
            date=flight_date,
            target_price=target_price,
            flight_number=flight_number,
            max_transfers=0,
            interval_minutes=5,
        )

        flight_label = f"<code>{flight_number}</code>" if flight_number else "<i>Any Airline / Flight</i>"
        formatted_price = f"{target_price:,.0f} ₸".replace(",", " ")

        confirmation_card = (
            "🎯 <b>Sniper Task Activated!</b>\n\n"
            f"• <b>Task ID:</b> <code>#{task_id}</code>\n"
            f"• <b>Route:</b> <code>{origin}</code> ✈️ <code>{dest}</code>\n"
            f"• <b>Departure Date:</b> <code>{flight_date}</code>\n"
            f"• <b>Target Price:</b> ≤ <b>{formatted_price}</b>\n"
            f"• <b>Flight Filter:</b> {flight_label}\n"
            "• <b>Check Frequency:</b> ⏱ <i>Каждые 5 минут</i>\n"
            "• <b>Status:</b> 🟢 <i>Active Monitoring</i>\n\n"
            "🔔 <i>You will receive an instant Telegram alert when a matching ticket drops below your target!</i>\n\n"
            f"<i>To cancel anytime:</i> <code>/delete {task_id}</code>"
        )
        await message.answer(confirmation_card)
    except Exception as e:
        logger.exception("Failed to insert snipe task: %s", e)
        await message.answer("❌ Internal error creating your task. Please try again later.")


@router.message(Command("list"))
async def handle_list(message: Message) -> None:
    """Handle /list command displaying user's active snipe tasks."""
    try:
        tasks = await dao.get_user_tasks(chat_id=message.chat.id, active_only=True)
        if not tasks:
            await message.answer(
                "📭 <b>You have no active flight snipe tasks.</b>\n\n"
                "To start tracking flight prices, send a request like:\n"
                "<i>«Алматы - Бангкок на 15 октября до 300$»</i>\n"
                "or use command:\n"
                "<code>/snipe ALA NQZ 2026-10-15 25000</code>"
            )
            return

        lines = [f"📋 <b>Your Active Sniping Tasks ({len(tasks)})</b>\n"]
        for t in tasks:
            flight_info = f" (<code>{t['flight_number']}</code>)" if t.get("flight_number") else ""
            target_formatted = f"{t['target_price']:,.0f} ₸".replace(",", " ")
            last_price = f"{t['last_price']:,.0f} ₸".replace(",", " ") if t.get("last_price") else "<i>Not checked yet</i>"
            last_check = t.get("last_checked_at") or "<i>Pending</i>"
            interval_str = f"{t.get('interval_minutes', 5)} мин"

            task_entry = (
                f"🔹 <b>#{t['id']}</b> — <code>{t['origin']}</code> ✈️ <code>{t['destination']}</code>{flight_info}\n"
                f"  • <b>Date:</b> <code>{t['date']}</code>\n"
                f"  • <b>Target:</b> ≤ <b>{target_formatted}</b>\n"
                f"  • <b>Interval:</b> ⏱ {interval_str}\n"
                f"  • <b>Last Checked:</b> {last_check}\n"
                f"  • <b>Lowest Seen:</b> {last_price}\n"
                f"  • <i>Cancel:</i> <code>/delete {t['id']}</code>\n"
            )
            lines.append(task_entry)

        await message.answer("\n".join(lines))
    except Exception as e:
        logger.exception("Failed to retrieve user tasks for chat %s: %s", message.chat.id, e)
        await message.answer("❌ Error retrieving your tasks. Please try again later.")


@router.message(Command("delete", "cancel"))
async def handle_delete(message: Message, command: CommandObject) -> None:
    """Handle /delete or /cancel command to remove a monitoring task."""
    args = command.args
    if not args:
        await message.answer(
            "⚠️ <b>Please specify the task ID to delete.</b>\n\n"
            "<b>Usage:</b> <code>/delete &lt;task_id&gt;</code>\n"
            "<b>Example:</b> <code>/delete 12</code>\n\n"
            "Use <code>/list</code> to see your active task IDs."
        )
        return

    clean_arg = args.strip().lstrip("#")
    try:
        task_id = int(clean_arg)
    except ValueError:
        await message.answer(f"❌ Invalid task ID: <code>{args}</code>. Must be a valid integer number.")
        return

    try:
        deleted = await dao.delete_task(task_id=task_id, chat_id=message.chat.id)
        if deleted:
            await message.answer(
                f"✅ <b>Sniper Task #{task_id} has been deleted.</b>\n"
                "Monitoring for this route has stopped."
            )
        else:
            await message.answer(
                f"❌ <b>Task #{task_id} not found</b> or it does not belong to your account.\n"
                "Use <code>/list</code> to see your active tasks."
            )
    except Exception as e:
        logger.exception("Failed to delete task %s for chat %s: %s", task_id, message.chat.id, e)
        await message.answer("❌ Error deleting task. Please try again later.")


@router.message(F.text & ~F.text.startswith("/"))
async def handle_nlp_message(message: Message) -> None:
    """Handle natural language flight monitoring requests via AI / Rule NLP engine."""
    user_text = (message.text or "").strip()
    if not user_text:
        return

    try:
        intent = await parse_flight_request(user_text)
    except Exception as e:
        logger.exception("Error running NLP parser on message '%s': %s", user_text, e)
        intent = None

    if not intent:
        await message.answer(
            "🤔 <b>Не удалось распознать параметры рейса.</b>\n\n"
            "Пожалуйста, укажите город вылета, назначения, дату и желаемую цену.\n\n"
            "<b>Примеры запросов:</b>\n"
            "• <i>«Рейс Алматы - Бангкок, 15 октября, прямой, KC-871, ниже 300$. Проверять каждые 5 минут»</i>\n"
            "• <i>«Астана - Шымкент на 2026-11-01 до 20000 тг»</i>\n"
            "• <i>«Из Актау в Дубай 25 декабря не дороже 80000 тенге»</i>\n\n"
            "Или воспользуйтесь командой: <code>/snipe ALA NQZ 2026-10-15 25000</code>"
        )
        return

    # Generate temporary session token for inline confirmation
    token = uuid.uuid4().hex[:12]
    _pending_nlp_tasks[token] = {
        "chat_id": message.chat.id,
        "intent": intent,
        "created_at": datetime.now(timezone.utc),
    }

    # Format user-facing confirmation card
    target_formatted = f"{intent.target_price:,.0f} ₸".replace(",", " ")
    orig_price_str = ""
    if intent.currency_detected and intent.currency_detected != "KZT" and intent.original_price:
        orig_price_str = f" ({intent.original_price:g} {intent.currency_detected} ≈ {target_formatted})"

    flight_label = f"<code>{intent.flight_number}</code>" if intent.flight_number else "<i>Любой рейс</i>"
    flight_type = "Прямой рейс ⚡" if intent.direct_only else "Любой (вкл. пересадки)"
    interval_label = f"Каждые {intent.interval_minutes} мин"

    summary_card = (
        "🔍 <b>Распознаны параметры снайпера:</b>\n\n"
        f"✈️ <b>Маршрут:</b> <code>{intent.origin}</code> ➡️ <code>{intent.destination}</code>\n"
        f"📅 <b>Дата:</b> <code>{intent.date}</code>\n"
        f"💰 <b>Целевая цена:</b> ≤ <b>{target_formatted}</b>{orig_price_str}\n"
        f"🔢 <b>Рейс:</b> {flight_label}\n"
        f"🔀 <b>Тип:</b> {flight_type}\n"
        f"⏱ <b>Интервал проверки:</b> {interval_label}\n\n"
        "Создать задачу отслеживания?"
    )

    keyboard = InlineKeyboardMarkup(
        inline_keyboard=[
            [
                InlineKeyboardButton(text="✅ Подтвердить", callback_data=f"confirm_snipe:{token}"),
                InlineKeyboardButton(text="❌ Отмена", callback_data=f"cancel_snipe:{token}"),
            ]
        ]
    )

    await message.answer(summary_card, reply_markup=keyboard)


@router.callback_query(F.data.startswith("confirm_snipe:"))
async def handle_confirm_snipe_callback(callback: CallbackQuery) -> None:
    """Handle inline button confirmation to commit parsed NLP snipe task into database."""
    token = (callback.data or "").split(":", 1)[1]
    pending = _pending_nlp_tasks.pop(token, None)

    if not pending:
        await callback.answer("⚠️ Время сессии истекло. Пожалуйста, отправьте запрос снова.", show_alert=True)
        return

    intent: ParsedFlightIntent = pending["intent"]
    chat_id = pending["chat_id"]

    try:
        task_id = await dao.add_task(
            chat_id=chat_id,
            origin=intent.origin,
            destination=intent.destination,
            date=intent.date,
            target_price=intent.target_price,
            flight_number=intent.flight_number,
            max_transfers=0 if intent.direct_only else 99,
            interval_minutes=intent.interval_minutes,
        )

        flight_label = f"<code>{intent.flight_number}</code>" if intent.flight_number else "<i>Any Airline / Flight</i>"
        formatted_price = f"{intent.target_price:,.0f} ₸".replace(",", " ")

        confirmation_card = (
            "🎯 <b>Sniper Task Activated!</b>\n\n"
            f"• <b>Task ID:</b> <code>#{task_id}</code>\n"
            f"• <b>Route:</b> <code>{intent.origin}</code> ✈️ <code>{intent.destination}</code>\n"
            f"• <b>Departure Date:</b> <code>{intent.date}</code>\n"
            f"• <b>Target Price:</b> ≤ <b>{formatted_price}</b>\n"
            f"• <b>Flight Filter:</b> {flight_label}\n"
            f"• <b>Check Frequency:</b> ⏱ <i>Каждые {intent.interval_minutes} мин</i>\n"
            "• <b>Status:</b> 🟢 <i>Active Monitoring</i>\n\n"
            "🔔 <i>You will receive an instant Telegram alert when a matching ticket drops below your target!</i>\n\n"
            f"<i>To cancel anytime:</i> <code>/delete {task_id}</code>"
        )

        await callback.answer("🎯 Задача успешно создана!")
        if callback.message:
            await callback.message.edit_text(confirmation_card, reply_markup=None)

    except Exception as e:
        logger.exception("Failed to insert task from NLP callback: %s", e)
        await callback.answer("❌ Ошибка создания задачи.", show_alert=True)


@router.callback_query(F.data.startswith("cancel_snipe"))
async def handle_cancel_snipe_callback(callback: CallbackQuery) -> None:
    """Handle inline button cancellation for pending NLP snipe task."""
    if callback.data and ":" in callback.data:
        token = callback.data.split(":", 1)[1]
        _pending_nlp_tasks.pop(token, None)

    await callback.answer("Отменено")
    if callback.message:
        await callback.message.edit_text("❌ <b>Создание задачи отменено.</b>", reply_markup=None)
