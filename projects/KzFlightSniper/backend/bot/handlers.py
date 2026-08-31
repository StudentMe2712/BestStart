"""aiogram 3.x Telegram command routers and message handlers for KzFlightSniper."""

from datetime import datetime, timezone
import logging
from typing import List, Optional, Tuple
from aiogram import Router
from aiogram.filters import Command, CommandObject, CommandStart
from aiogram.types import Message

from backend.db.dao import FlightSniperDAO


logger = logging.getLogger("kzflight_sniper.bot.handlers")
router = Router(name="flight_sniper_handlers")
dao = FlightSniperDAO()

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
    """Handle /start command with greeting and quick-start instructions."""
    welcome_text = (
        "🦅 <b>Welcome to KzFlightSniper!</b>\n\n"
        "Your automated flight tracker for Kazakhstan aviation (Air Astana, FlyArystan, SCAT, Qazaq Air).\n"
        "We continuously monitor ticket prices and alert you instantly the moment a price drops below your budget!\n\n"
        "<b>⚡ Quick Start Commands:</b>\n"
        "• <code>/snipe ALA NQZ 2026-10-15 25000</code> — Track flights under 25,000 ₸\n"
        "• <code>/snipe ALA NQZ 2026-10-15 KC-871 28000</code> — Track specific flight\n"
        "• <code>/list</code> — View your active monitored flights\n"
        "• <code>/delete &lt;id&gt;</code> — Cancel a monitoring task\n"
        "• <code>/help</code> — Full command reference & airport codes\n"
    )
    await message.answer(welcome_text)


@router.message(Command("help"))
async def handle_help(message: Message) -> None:
    """Handle /help command with complete syntax reference and airport codes."""
    help_text = (
        "📖 <b>KzFlightSniper Command Guide & Syntax</b>\n\n"
        "<b>1. Create a Flight Snipe Task:</b>\n"
        "<code>/snipe &lt;ORIGIN&gt; &lt;DEST&gt; &lt;YYYY-MM-DD&gt; &lt;TARGET_PRICE&gt;</code>\n"
        "<code>/snipe &lt;ORIGIN&gt; &lt;DEST&gt; &lt;YYYY-MM-DD&gt; &lt;FLIGHT_NO&gt; &lt;TARGET_PRICE&gt;</code>\n"
        "<i>Examples:</i>\n"
        "• <code>/snipe ALA NQZ 2026-10-15 25000</code>\n"
        "• <code>/snipe CIT ALA 2026-11-20 18500</code>\n"
        "• <code>/snipe ALA NQZ 2026-10-15 KC-871 28000</code>\n\n"
        "<b>2. View Active Tasks:</b>\n"
        "<code>/list</code> — Shows all your registered snipe tasks.\n\n"
        "<b>3. Delete / Cancel a Task:</b>\n"
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
            "• <code>/snipe NQZ SCO 2026-11-01 32000</code>"
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
                "To start tracking flight prices, use:\n"
                "<code>/snipe ALA NQZ 2026-10-15 25000</code>"
            )
            return

        lines = [f"📋 <b>Your Active Sniping Tasks ({len(tasks)})</b>\n"]
        for t in tasks:
            flight_info = f" (<code>{t['flight_number']}</code>)" if t.get("flight_number") else ""
            target_formatted = f"{t['target_price']:,.0f} ₸".replace(",", " ")
            last_price = f"{t['last_price']:,.0f} ₸".replace(",", " ") if t.get("last_price") else "<i>Not checked yet</i>"
            last_check = t.get("last_checked_at") or "<i>Pending</i>"

            task_entry = (
                f"🔹 <b>#{t['id']}</b> — <code>{t['origin']}</code> ✈️ <code>{t['destination']}</code>{flight_info}\n"
                f"  • <b>Date:</b> <code>{t['date']}</code>\n"
                f"  • <b>Target:</b> ≤ <b>{target_formatted}</b>\n"
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
