from datetime import datetime, timezone
import logging
import uuid
from typing import Any, Dict, List, Optional, Tuple

from aiogram import F, Router
from aiogram.enums import ChatAction
from aiogram.filters import Command, CommandObject, CommandStart
from aiogram.fsm.context import FSMContext
from aiogram.fsm.state import State, StatesGroup
from aiogram.types import CallbackQuery, InlineKeyboardButton, InlineKeyboardMarkup, Message

from backend.bot.nlp_parser import parse_flight_request
from backend.core.models import FlightOffer, ParsedFlightIntent
from backend.db.dao import FlightSniperDAO
from backend.providers.aviata_provider import AviataProvider

logger = logging.getLogger("kzflight_sniper.bot.handlers")
router = Router(name="flight_sniper_handlers")
dao = FlightSniperDAO()


class SniperStates(StatesGroup):
    """FSM States for Flight Sniper conversation flow."""

    waiting_for_flight_text = State()


# In-memory storage for pending NLP confirmation sessions
_pending_nlp_tasks: Dict[str, Dict[str, Any]] = {}

KAZAKHSTAN_AIRPORTS_INFO = """
<b>🇰🇿 Основные коды аэропортов Казахстана (IATA):</b>
• <code>ALA</code> — Алматы
• <code>NQZ</code> — Астана
• <code>CIT</code> — Шымкент
• <code>SCO</code> — Актау
• <code>GUW</code> — Атырау
• <code>UKK</code> — Усть-Каменогорск (Оскемен)
• <code>AKX</code> — Актобе
• <code>KSG</code> — Костанай
• <code>PWQ</code> — Павлодар
• <code>PLX</code> — Семей
• <code>DMB</code> — Тараз
• <code>KOV</code> — Кокшетау
• <code>BXH</code> — Балхаш
• <code>URA</code> — Уральск (Орал)
• <code>KGF</code> — Караганда
• <code>PPK</code> — Петропавловск
• <code>KZO</code> — Кызылорда
"""


def parse_snipe_arguments(args_text: str) -> Tuple[str, str, str, Optional[str], float]:
    """Parse and validate arguments for /snipe command.

    Supported formats:
    1) 4 arguments: <ORIGIN> <DEST> <YYYY-MM-DD> <TARGET_PRICE>
    2) 5 arguments: <ORIGIN> <DEST> <YYYY-MM-DD> <FLIGHT_NO> <TARGET_PRICE>

    Returns:
        Tuple of (origin, destination, date_str, flight_number, target_price)

    Raises:
        ValueError with descriptive user-facing error message in Russian.
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
            "<b>Неверное количество аргументов.</b>\n\n"
            "<b>Формат:</b>\n"
            "<code>/snipe &lt;ОТКУДА&gt; &lt;КУДА&gt; &lt;ГГГГ-ММ-ДД&gt; &lt;ЦЕЛЕВАЯ_ЦЕНА&gt;</code>\n"
            "или с фильтром по рейсу:\n"
            "<code>/snipe &lt;ОТКУДА&gt; &lt;КУДА&gt; &lt;ГГГГ-ММ-ДД&gt; &lt;НОМЕР_РЕЙСА&gt; &lt;ЦЕЛЕВАЯ_ЦЕНА&gt;</code>\n\n"
            "<b>Примеры:</b>\n"
            "• <code>/snipe ALA NQZ 2026-10-15 25000</code>\n"
            "• <code>/snipe ALA NQZ 2026-10-15 KC-871 28000</code>"
        )

    # 1. Validate Origin & Destination IATA Codes
    origin = origin_raw.strip().upper()
    dest = dest_raw.strip().upper()

    if len(origin) != 3 or not origin.isalpha():
        raise ValueError(f"Неверный код аэропорта вылета: <code>{origin}</code>. Требуется 3-буквенный IATA-код (например, <code>ALA</code>, <code>NQZ</code>).")

    if len(dest) != 3 or not dest.isalpha():
        raise ValueError(f"Неверный код аэропорта назначения: <code>{dest}</code>. Требуется 3-буквенный IATA-код (например, <code>NQZ</code>, <code>ALA</code>).")

    if origin == dest:
        raise ValueError(f"Город вылета и назначения не могут совпадать (<code>{origin}</code>).")

    # 2. Validate Departure Date
    try:
        flight_date = datetime.strptime(date_raw.strip(), "%Y-%m-%d").date()
    except ValueError:
        raise ValueError(f"Неверный формат даты: <code>{date_raw}</code>. Используйте формат <code>ГГГГ-ММ-ДД</code> (например, <code>2026-10-15</code>).")

    today = datetime.now(timezone.utc).date()
    if flight_date < today:
        raise ValueError(f"Дата вылета <code>{date_raw}</code> уже прошла. Выберите сегодняшнюю или будущую дату.")

    # 3. Validate Target Price
    price_clean = price_raw.replace("₸", "").replace("KZT", "").replace("тг", "").replace(",", "").replace(" ", "").strip()
    try:
        target_price = float(price_clean)
        if target_price <= 0:
            raise ValueError()
    except ValueError:
        raise ValueError(f"Неверная сумма: <code>{price_raw}</code>. Укажите положительное число в тенге (например, <code>25000</code>).")

    return origin, dest, flight_date.isoformat(), flight_number, target_price


@router.message(CommandStart())
async def handle_start(message: Message) -> None:
    """Handle /start command with greeting, NLP capabilities, and quick-start instructions."""
    welcome_text = (
        "🦅 <b>Добро пожаловать в KzFlightSniper!</b>\n"
        "Я — твой умный помощник для перехвата дешевых авиабилетов (Air Astana, FlyArystan, SCAT и др.).\n\n"
        "💬 <b>Просто напиши мне, что ты ищешь, обычным текстом. Например:</b>\n"
        "• <i>«Алматы - Чэнду на 21 ноября»</i>\n"
        "• <i>«Астана - Шымкент на 1 ноября до 20000 тг»</i>\n"
        "• <i>«Алматы - Бангкок, 15 октября, прямой, KC-871, ниже 300$»</i>\n\n"
        "<b>Мои команды:</b>\n"
        "• <code>/list</code> — Посмотреть активные мониторинги\n"
        "• <code>/help</code> — Справочник кодов аэропортов"
    )
    keyboard = InlineKeyboardMarkup(
        inline_keyboard=[
            [InlineKeyboardButton(text="🎯 Создать мониторинг", callback_data="start_new_snipe_fsm")]
        ]
    )
    await message.answer(welcome_text, reply_markup=keyboard)


@router.callback_query(F.data == "start_new_snipe_fsm")
async def handle_start_new_snipe_fsm_callback(callback: CallbackQuery, state: FSMContext) -> None:
    """Activate FSM state for user to submit natural language flight search."""
    await state.set_state(SniperStates.waiting_for_flight_text)
    guide_text = (
        "💬 <b>Напишите параметры поиска обычным текстом:</b>\n\n"
        "Укажите город вылета, назначения, дату и (если знаете) желаемую цену или номер рейса.\n\n"
        "<b>Например:</b>\n"
        "• <i>«Алматы - Чэнду на 21 ноября»</i>\n"
        "• <i>«Астана - Шымкент на 1 ноября до 20000 тг»</i>\n"
        "• <i>«Алматы - Бангкок 15 октября, прямой, KC-871, ниже 300$»</i>"
    )
    await callback.answer()
    if callback.message:
        await callback.message.answer(guide_text, parse_mode="HTML")


@router.message(Command("help"))
async def handle_help(message: Message) -> None:
    """Handle /help command with complete syntax reference, NLP examples, and airport codes."""
    help_text = (
        "📖 <b>Справочник и команды KzFlightSniper</b>\n\n"
        "<b>💬 Поиск на обычном языке (AI):</b>\n"
        "Просто отправь запрос обычным сообщением:\n"
        "• <i>«Билет Алматы в Стамбул на 20 октября не дороже 100000 тг»</i>\n"
        "• <i>«Из Актау в Дубай 25 декабря, рейс DV-713, до 400$, каждые 10 мин»</i>\n\n"
        "<b>⚡ Основные команды:</b>\n"
        "• <code>/list</code> — Показать все активные задачи отслеживания\n"
        "• <code>/delete &lt;id&gt;</code> или <code>/cancel &lt;id&gt;</code> — Отменить задачу (например: <code>/delete 42</code>)\n"
        "• <code>/help</code> — Показать эту справку\n"
        f"{KAZAKHSTAN_AIRPORTS_INFO}"
    )
    await message.answer(help_text)


@router.message(Command("snipe"))
async def handle_snipe(message: Message, command: CommandObject) -> None:
    """Handle /snipe command to register a new flight monitoring task."""
    args = command.args
    if not args:
        guide_text = (
            "🎯 <b>Создание мониторинга через команду:</b>\n\n"
            "<b>Формат:</b>\n"
            "<code>/snipe &lt;ОТКУДА&gt; &lt;КУДА&gt; &lt;ГГГГ-ММ-ДД&gt; &lt;ЦЕЛЕВАЯ_ЦЕНА&gt;</code>\n"
            "или с номером рейса:\n"
            "<code>/snipe &lt;ОТКУДА&gt; &lt;КУДА&gt; &lt;ГГГГ-ММ-ДД&gt; &lt;НОМЕР_РЕЙСА&gt; &lt;ЦЕЛЕВАЯ_ЦЕНА&gt;</code>\n\n"
            "<b>Примеры:</b>\n"
            "• <code>/snipe ALA NQZ 2026-10-15 25000</code>\n"
            "• <code>/snipe ALA NQZ 2026-10-15 KC-871 28000</code>\n"
            "• <code>/snipe NQZ SCO 2026-11-01 32000</code>\n\n"
            "💡 <i>Совет: Вы также можете просто написать запрос обычным текстом!</i>"
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
        await message.answer("❌ Произошла ошибка при обработке запроса. См. <code>/help</code>.")
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

        flight_label = f"<code>{flight_number}</code>" if flight_number else "<i>Любая авиакомпания / рейс</i>"
        formatted_price = f"{target_price:,.0f} ₸".replace(",", " ")

        confirmation_card = (
            "🎯 <b>Снайпер активирован!</b>\n\n"
            f"• <b>ID задачи:</b> <code>#{task_id}</code>\n"
            f"• <b>Маршрут:</b> <code>{origin}</code> ✈️ <code>{dest}</code>\n"
            f"• <b>Дата вылета:</b> <code>{flight_date}</code>\n"
            f"• <b>Целевая цена:</b> ≤ <b>{formatted_price}</b>\n"
            f"• <b>Фильтр рейса:</b> {flight_label}\n"
            "• <b>Частота проверки:</b> ⏱ <i>Каждые 5 минут</i>\n"
            "• <b>Статус:</b> 🟢 <i>Активный мониторинг</i>\n\n"
            "🔔 <i>Вы получите мгновенное уведомление в Telegram, как только цена билета упадет ниже целевой!</i>\n\n"
            f"<i>Для отмены в любой момент:</i> <code>/delete {task_id}</code>"
        )
        await message.answer(confirmation_card)
    except Exception as e:
        logger.exception("Failed to insert snipe task: %s", e)
        await message.answer("❌ Ошибка создания задачи. Пожалуйста, попробуйте позже.")


@router.message(Command("list"))
async def handle_list(message: Message) -> None:
    """Handle /list command displaying user's active snipe tasks."""
    try:
        tasks = await dao.get_user_tasks(chat_id=message.chat.id, active_only=True)
        if not tasks:
            await message.answer(
                "📭 <b>У вас нет активных задач отслеживания.</b>\n\n"
                "Чтобы начать мониторинг, просто отправьте сообщение, например:\n"
                "<i>«Алматы - Бангкок на 15 октября до 300$»</i>\n"
                "<i>«Астана - Шымкент на 1 ноября до 20000 тг»</i>"
            )
            return

        lines = [f"📋 <b>Ваши активные мониторинги ({len(tasks)})</b>\n"]
        for t in tasks:
            flight_info = f" (<code>{t['flight_number']}</code>)" if t.get("flight_number") else ""
            target_formatted = f"{t['target_price']:,.0f} ₸".replace(",", " ")
            last_price = f"{t['last_price']:,.0f} ₸".replace(",", " ") if t.get("last_price") else "<i>Еще не проверялся</i>"
            last_check = t.get("last_checked_at") or "<i>В очереди</i>"
            interval_str = f"{t.get('interval_minutes', 5)} мин"

            task_entry = (
                f"🔹 <b>#{t['id']}</b> — <code>{t['origin']}</code> ✈️ <code>{t['destination']}</code>{flight_info}\n"
                f"  • <b>Дата:</b> <code>{t['date']}</code>\n"
                f"  • <b>Целевая цена:</b> ≤ <b>{target_formatted}</b>\n"
                f"  • <b>Интервал:</b> ⏱ {interval_str}\n"
                f"  • <b>Посл. проверка:</b> {last_check}\n"
                f"  • <b>Мин. найденная:</b> {last_price}\n"
                f"  • <i>Отмена:</i> <code>/delete {t['id']}</code>\n"
            )
            lines.append(task_entry)

        await message.answer("\n".join(lines))
    except Exception as e:
        logger.exception("Failed to retrieve user tasks for chat %s: %s", message.chat.id, e)
        await message.answer("❌ Ошибка при получении задач. Пожалуйста, попробуйте позже.")


@router.message(Command("delete", "cancel"))
async def handle_delete(message: Message, command: CommandObject) -> None:
    """Handle /delete or /cancel command to remove a monitoring task."""
    args = command.args
    if not args:
        await message.answer(
            "⚠️ <b>Пожалуйста, укажите ID задачи для удаления.</b>\n\n"
            "<b>Формат:</b> <code>/delete &lt;task_id&gt;</code>\n"
            "<b>Пример:</b> <code>/delete 12</code>\n\n"
            "Используйте <code>/list</code>, чтобы увидеть ID ваших активных задач."
        )
        return

    clean_arg = args.strip().lstrip("#")
    try:
        task_id = int(clean_arg)
    except ValueError:
        await message.answer(f"❌ Неверный ID задачи: <code>{args}</code>. Укажите числовой номер.")
        return

    try:
        deleted = await dao.delete_task(task_id=task_id, chat_id=message.chat.id)
        if deleted:
            await message.answer(
                f"✅ <b>Задача мониторинга #{task_id} удалена.</b>\n"
                "Отслеживание цен по этому маршруту остановлено."
            )
        else:
            await message.answer(
                f"❌ <b>Задача #{task_id} не найдена</b> или не принадлежит вашему аккаунту.\n"
                "Используйте <code>/list</code>, чтобы посмотреть активные задачи."
            )
    except Exception as e:
        logger.exception("Failed to delete task %s for chat %s: %s", task_id, message.chat.id, e)
        await message.answer("❌ Ошибка при удалении задачи. Пожалуйста, попробуйте позже.")


@router.message(SniperStates.waiting_for_flight_text, F.text & ~F.text.startswith("/"))
async def handle_nlp_message(message: Message, state: FSMContext) -> None:
    """Handle natural language flight monitoring requests via AI / Rule NLP engine with Live Preview."""
    user_text = (message.text or "").strip()
    if not user_text:
        return

    # Trigger typing action if message.bot is available
    try:
        if message.bot:
            await message.bot.send_chat_action(chat_id=message.chat.id, action=ChatAction.TYPING)
    except Exception:
        pass

    # Send preliminary status message
    status_msg = await message.answer("⏳ Анализирую запрос через AI...")

    try:
        intent = await parse_flight_request(user_text)
    except Exception as e:
        logger.exception("Error running NLP parser on message '%s': %s", user_text, e)
        intent = None

    if intent is None:
        await status_msg.edit_text(
            "🤔 <b>Не удалось распознать маршрут и дату рейса.</b>\n\n"
            "Пожалуйста, укажите город вылета, назначения и дату (например, <i>«Алматы - Чэнду 21 ноября»</i>).\n\n"
            "Попробуйте написать еще раз:",
            parse_mode="HTML",
        )
        return

    # Valid intent recognized: clear FSM state
    await state.clear()

    await status_msg.edit_text("⏳ Ищу текущие билеты в реальном времени на Aviata.kz...", parse_mode="HTML")

    # Live Preview search via AviataProvider
    live_offers: List[FlightOffer] = []
    try:
        provider = AviataProvider()
        live_offers = await provider.search(
            origin=intent.origin,
            destination=intent.destination,
            date=intent.date,
            flight_number=intent.flight_number,
            direct_only=intent.direct_only,
        )
    except Exception as search_err:
        logger.warning("Live preview search failed: %s", search_err)

    # Determine effective target price
    if intent.target_price is not None:
        effective_target_price = intent.target_price
        if intent.currency_detected and intent.currency_detected != "KZT" and intent.original_price:
            price_note = f"💡 <i>({intent.original_price:g} {intent.currency_detected} ≈ {effective_target_price:,.0f} ₸)</i>\n"
        else:
            price_note = ""
    elif live_offers:
        effective_target_price = min(o.price_kzt for o in live_offers)
        price_note = "💡 <i>(Цена установлена автоматически по мин. текущей цене на рынке)</i>\n"
    else:
        effective_target_price = 50000.0
        price_note = "💡 <i>(Установлена стандартная базовая цена, т.к. билеты не найдены)</i>\n"

    # Format live offers snippet
    if live_offers:
        displayed = live_offers[:4]
        lines = []
        for o in displayed:
            direct_str = "Прямой ⚡" if o.is_direct else f"{o.transfers_count} перес."
            lines.append(f"✈️ {o.airline} ({o.flight_number}): {o.price_kzt:,.0f} ₸ ({direct_str})")
        live_offers_text = "\n".join(lines)
    else:
        live_offers_text = "⚠️ На данный момент билетов в свободной продаже не обнаружено (или рейс распродан)."

    flight_label = f"<code>{intent.flight_number}</code>" if intent.flight_number else "<i>Любой рейс</i>"
    flight_type = "Прямой рейс ⚡" if intent.direct_only else "Любой (вкл. пересадки)"

    token = uuid.uuid4().hex[:12]
    _pending_nlp_tasks[token] = {
        "chat_id": message.chat.id,
        "intent": intent,
        "effective_target_price": effective_target_price,
        "created_at": datetime.now(timezone.utc),
    }

    summary_card = (
        f"📍 <b>Маршрут:</b> <code>{intent.origin}</code> ➡️ <code>{intent.destination}</code> | 📅 <b>Дата:</b> <code>{intent.date}</code>\n"
        f"🔢 <b>Рейс:</b> {flight_label}\n"
        f"🔀 <b>Тип:</b> {flight_type}\n\n"
        "🔎 <b>ТЕКУЩИЕ БИЛЕТЫ В ПРОДАЖЕ:</b>\n"
        f"{live_offers_text}\n\n"
        f"💰 <b>Целевая цена для снайпинга:</b> ≤ <b>{effective_target_price:,.0f} ₸</b>\n"
        f"{price_note}"
        f"⏱ <b>Интервал проверки:</b> каждые {intent.interval_minutes} мин.\n\n"
        "Создать задачу мониторинга?"
    )

    keyboard = InlineKeyboardMarkup(
        inline_keyboard=[
            [
                InlineKeyboardButton(text="✅ Начать мониторинг", callback_data=f"confirm_snipe:{token}"),
                InlineKeyboardButton(text="❌ Отмена", callback_data=f"cancel_snipe:{token}"),
            ]
        ]
    )

    await status_msg.edit_text(summary_card, reply_markup=keyboard, parse_mode="HTML")


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
    target_price = pending.get("effective_target_price") or (intent.target_price if intent.target_price else 50000.0)

    try:
        task_id = await dao.add_task(
            chat_id=chat_id,
            origin=intent.origin,
            destination=intent.destination,
            date=intent.date,
            target_price=target_price,
            flight_number=intent.flight_number,
            max_transfers=0 if intent.direct_only else 99,
            interval_minutes=intent.interval_minutes,
        )

        flight_label = f"<code>{intent.flight_number}</code>" if intent.flight_number else "<i>Любая авиакомпания / рейс</i>"
        formatted_price = f"{target_price:,.0f} ₸".replace(",", " ")

        confirmation_card = (
            "🎯 <b>Снайпер активирован!</b>\n\n"
            f"• <b>ID задачи:</b> <code>#{task_id}</code>\n"
            f"• <b>Маршрут:</b> <code>{intent.origin}</code> ✈️ <code>{intent.destination}</code>\n"
            f"• <b>Дата вылета:</b> <code>{intent.date}</code>\n"
            f"• <b>Целевая цена:</b> ≤ <b>{formatted_price}</b>\n"
            f"• <b>Фильтр рейса:</b> {flight_label}\n"
            f"• <b>Частота проверки:</b> ⏱ <i>Каждые {intent.interval_minutes} мин</i>\n"
            "• <b>Статус:</b> 🟢 <i>Активный мониторинг</i>\n\n"
            "🔔 <i>Вы получите мгновенное уведомление в Telegram, как только цена билета упадет ниже целевой!</i>\n\n"
            f"<i>Для отмены в любой момент:</i> <code>/delete {task_id}</code>"
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
