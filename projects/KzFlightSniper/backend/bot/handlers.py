from datetime import datetime, timezone
import logging
from typing import Any, Dict, List, Optional, Tuple

from aiogram import F, Router
from aiogram.enums import ChatAction
from aiogram.filters import Command, CommandObject, CommandStart
from aiogram.filters.callback_data import CallbackData
from aiogram.fsm.context import FSMContext
from aiogram.fsm.state import State, StatesGroup
from aiogram.types import CallbackQuery, InlineKeyboardButton, InlineKeyboardMarkup, Message

from backend.bot.nlp_parser import parse_interval_nlp, parse_search_query
from backend.core.models import FlightOffer, ParsedFlightIntent
from backend.db.dao import FlightSniperDAO
from backend.providers.aviasales_provider import AviasalesProvider

logger = logging.getLogger("kzflight_sniper.bot.handlers")
router = Router(name="flight_sniper_handlers")
dao = FlightSniperDAO()


class SniperStates(StatesGroup):
    """FSM States for 2-step Flight Sniper conversation flow."""

    waiting_for_search_query = State()
    waiting_for_interval = State()


class FlightSelectCallback(CallbackData, prefix="fl_sel"):
    """Callback data for selecting a flight from search results."""

    flight_idx: int


class MonitorFlightCallback(CallbackData, prefix="fl_mon"):
    """Callback data to proceed with monitoring the chosen flight."""

    flight_idx: int


class QuickIntervalCallback(CallbackData, prefix="fl_int"):
    """Callback data for quick preset intervals (5, 10, 30, 60 min)."""

    minutes: int


class StepBackCallback(CallbackData, prefix="fl_back"):
    """Callback data for navigating backwards in FSM wizard."""

    to_step: str  # "flights" or "interval"


class ConfirmSnipeCallback(CallbackData, prefix="fl_conf"):
    """Callback data to commit snipe task creation into database."""

    pass


class CancelSnipeCallback(CallbackData, prefix="fl_canc"):
    """Callback data to cancel current snipe creation session."""

    pass


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


def _get_flight_attr(flight: Any, key: str, default: Any = None) -> Any:
    """Helper to get attribute from dict or Pydantic FlightOffer model."""
    if isinstance(flight, dict):
        return flight.get(key, default)
    return getattr(flight, key, default)


def build_flight_list_keyboard(offers: List[Any]) -> InlineKeyboardMarkup:
    """Build inline keyboard listing all available flight offers with cancel button."""
    buttons = []
    for i, o in enumerate(offers):
        airline = _get_flight_attr(o, "airline", "Рейс")
        flight_num = _get_flight_attr(o, "flight_number", "")
        price = _get_flight_attr(o, "price_kzt", 0.0)
        formatted_p = f"{price:,.0f} ₸".replace(",", " ")
        btn_text = f"✈️ {airline} {flight_num} - {formatted_p}"
        buttons.append([
            InlineKeyboardButton(
                text=btn_text,
                callback_data=FlightSelectCallback(flight_idx=i).pack(),
            )
        ])
    buttons.append([
        InlineKeyboardButton(
            text="❌ Отмена",
            callback_data=CancelSnipeCallback().pack(),
        )
    ])
    return InlineKeyboardMarkup(inline_keyboard=buttons)


def build_flight_details_keyboard(flight_idx: int) -> InlineKeyboardMarkup:
    """Build keyboard for flight detail view with Monitor and Back buttons."""
    return InlineKeyboardMarkup(
        inline_keyboard=[
            [
                InlineKeyboardButton(
                    text="🎯 Мониторить этот рейс",
                    callback_data=MonitorFlightCallback(flight_idx=flight_idx).pack(),
                )
            ],
            [
                InlineKeyboardButton(
                    text="⬅️ К списку рейсов",
                    callback_data=StepBackCallback(to_step="flights").pack(),
                )
            ],
        ]
    )


def build_interval_keyboard() -> InlineKeyboardMarkup:
    """Build keyboard with quick interval options and Back button."""
    return InlineKeyboardMarkup(
        inline_keyboard=[
            [
                InlineKeyboardButton(text="5 мин", callback_data=QuickIntervalCallback(minutes=5).pack()),
                InlineKeyboardButton(text="10 мин", callback_data=QuickIntervalCallback(minutes=10).pack()),
                InlineKeyboardButton(text="30 мин", callback_data=QuickIntervalCallback(minutes=30).pack()),
                InlineKeyboardButton(text="1 час", callback_data=QuickIntervalCallback(minutes=60).pack()),
            ],
            [
                InlineKeyboardButton(text="⬅️ Назад", callback_data=StepBackCallback(to_step="flights").pack()),
            ],
        ]
    )


def build_confirmation_keyboard() -> InlineKeyboardMarkup:
    """Build keyboard for final snipe confirmation."""
    return InlineKeyboardMarkup(
        inline_keyboard=[
            [
                InlineKeyboardButton(
                    text="⬅️ Назад",
                    callback_data=StepBackCallback(to_step="interval").pack(),
                ),
                InlineKeyboardButton(
                    text="✅ Подтвердить",
                    callback_data=ConfirmSnipeCallback().pack(),
                ),
                InlineKeyboardButton(
                    text="❌ Отменить",
                    callback_data=CancelSnipeCallback().pack(),
                ),
            ]
        ]
    )


def format_flight_details_card(flight: Any) -> str:
    """Format detailed HTML card for a selected flight offer."""
    airline = _get_flight_attr(flight, "airline", "N/A")
    flight_num = _get_flight_attr(flight, "flight_number", "N/A")
    origin = _get_flight_attr(flight, "origin", "")
    dest = _get_flight_attr(flight, "destination", "")
    dep_time = _get_flight_attr(flight, "departure_time", "N/A")
    arr_time = _get_flight_attr(flight, "arrival_time", "N/A")
    transfers = _get_flight_attr(flight, "transfers_count", 0)
    transfers_str = "Прямой ⚡" if transfers == 0 else f"{transfers} пересад."
    price = float(_get_flight_attr(flight, "price_kzt", 0.0))
    price_fmt = f"{price:,.0f} ₸".replace(",", " ")

    return (
        "✈️ <b>Детали выбранного рейса:</b>\n\n"
        f"• <b>Авиакомпания:</b> {airline}\n"
        f"• <b>Рейс:</b> <code>{flight_num}</code>\n"
        f"• <b>Маршрут:</b> <code>{origin}</code> ➡️ <code>{dest}</code>\n"
        f"• <b>Время:</b> 🛫 {dep_time} — 🛬 {arr_time}\n"
        f"• <b>Пересадки:</b> {transfers_str}\n"
        f"• <b>Текущая цена:</b> <b>{price_fmt}</b>\n\n"
        "Нажмите <b>«🎯 Мониторить этот рейс»</b> для настройки интервала проверки."
    )


def format_confirmation_card(data: Dict[str, Any]) -> str:
    """Format final confirmation card before saving snipe task."""
    origin = data.get("origin", "")
    dest = data.get("destination", "")
    date_str = data.get("date", "")
    selected = data.get("selected_flight", {})
    airline = _get_flight_attr(selected, "airline", "N/A")
    flight_num = _get_flight_attr(selected, "flight_number", "N/A")
    price = float(_get_flight_attr(selected, "price_kzt", 0.0))
    target_price = float(data.get("target_price", price))
    interval = int(data.get("interval_minutes", 5))

    price_fmt = f"{price:,.0f} ₸".replace(",", " ")
    target_fmt = f"{target_price:,.0f} ₸".replace(",", " ")

    return (
        "📋 <b>Подтверждение параметров мониторинга</b>\n\n"
        f"• <b>Маршрут:</b> <code>{origin}</code> ✈️ <code>{dest}</code>\n"
        f"• <b>Дата вылета:</b> <code>{date_str}</code>\n"
        f"• <b>Авиакомпания:</b> {airline}\n"
        f"• <b>Рейс:</b> <code>{flight_num}</code>\n"
        f"• <b>Текущая цена:</b> {price_fmt}\n"
        f"• <b>Целевая цена:</b> ≤ <b>{target_fmt}</b>\n"
        f"• <b>Интервал проверки:</b> ⏱ <i>Каждые {interval} мин</i>\n\n"
        "Запустить снайпер для этого рейса?"
    )


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
    """Handle /start command with greeting, NLP capabilities, and quick-start button."""
    welcome_text = (
        "🦅 <b>Добро пожаловать в KzFlightSniper!</b>\n"
        "Я — твой умный помощник для перехвата дешевых авиабилетов (Air Astana, FlyArystan, SCAT и др.).\n\n"
        "💬 <b>Нажмите кнопку ниже или отправьте маршрут для создания мониторинга:</b>\n"
        "• <i>«Алматы - Чэнду 21 ноября»</i>\n"
        "• <i>«Астана - Шымкент на 1 ноября»</i>\n"
        "• <i>«Алматы - Бангкок 15 октября»</i>\n\n"
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
    """Activate FSM state waiting_for_search_query for user to submit flight search."""
    await state.set_state(SniperStates.waiting_for_search_query)
    guide_text = (
        "💬 <b>Куда и когда вы планируете лететь?</b>\n\n"
        "Напишите маршрут и дату обычным текстом.\n\n"
        "<b>Например:</b>\n"
        "• <i>«Алматы - Чэнду 21 ноября»</i>\n"
        "• <i>«Астана в Сеул завтра»</i>\n"
        "• <i>«Из Шымкента в Стамбул 25 декабря»</i>"
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
        "Нажмите 🎯 «Создать мониторинг» или просто напишите сообщение:\n"
        "• <i>«Алматы - Чэнду 21 ноября»</i>\n"
        "• <i>«Астана в Сеул завтра»</i>\n"
        "• <i>«Из Актау в Дубай 25 декабря»</i>\n\n"
        "<b>⚡ Основные команды:</b>\n"
        "• <code>/list</code> — Показать все активные задачи отслеживания\n"
        "• <code>/delete &lt;id&gt;</code> или <code>/cancel &lt;id&gt;</code> — Отменить задачу (например: <code>/delete 42</code>)\n"
        "• <code>/help</code> — Показать эту справку\n"
        f"{KAZAKHSTAN_AIRPORTS_INFO}"
    )
    await message.answer(help_text)


@router.message(Command("snipe"))
async def handle_snipe(message: Message, command: CommandObject) -> None:
    """Handle /snipe command to register a new flight monitoring task directly."""
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
            "💡 <i>Совет: Вы также можете нажать кнопку «🎯 Создать мониторинг» для интерактивного выбора рейса!</i>"
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
                "Чтобы начать мониторинг, нажмите кнопку «🎯 Создать мониторинг» в /start или отправьте маршрут."
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


# ============================================================================
# Step 1: Direction & Date Parsing + Live Flight List
# ============================================================================

@router.message(SniperStates.waiting_for_search_query, F.text & ~F.text.startswith("/"))
async def handle_search_query_message(message: Message, state: FSMContext) -> None:
    """Handle natural language flight query, run Live Search via AviasalesProvider, and render flight list."""
    user_text = (message.text or "").strip()
    if not user_text:
        return

    try:
        if message.bot:
            await message.bot.send_chat_action(chat_id=message.chat.id, action=ChatAction.TYPING)
    except Exception:
        pass

    status_msg = await message.answer("⏳ Выполняю Live-поиск рейсов...")

    try:
        intent = await parse_search_query(user_text)
    except Exception as e:
        logger.exception("Error running parse_search_query on message '%s': %s", user_text, e)
        intent = None

    if intent is None:
        await status_msg.edit_text(
            "🤔 <b>Не удалось распознать маршрут и дату рейса.</b>\n\n"
            "Пожалуйста, укажите город вылета, назначения и дату (например, <i>«Алматы - Чэнду 21 ноября»</i>).\n\n"
            "Попробуйте написать еще раз:",
            parse_mode="HTML",
        )
        return

    offers: List[FlightOffer] = []
    try:
        provider = AviasalesProvider()
        offers = await provider.search(
            origin=intent.origin,
            destination=intent.destination,
            date=intent.date,
            direct_only=intent.direct_only,
            flight_number=intent.flight_number,
        )
    except Exception as search_err:
        logger.warning("Live preview search failed: %s", search_err)

    if not offers:
        cancel_kb = InlineKeyboardMarkup(
            inline_keyboard=[
                [InlineKeyboardButton(text="❌ Отмена", callback_data=CancelSnipeCallback().pack())]
            ]
        )
        await status_msg.edit_text(
            f"⚠️ По маршруту <code>{intent.origin}</code> ✈️ <code>{intent.destination}</code> на <code>{intent.date}</code> "
            "билетов в свободной продаже не обнаружено (или рейс распродан).\n\n"
            "Попробуйте ввести другой маршрут/дату или нажмите «Отмена».",
            reply_markup=cancel_kb,
            parse_mode="HTML",
        )
        return

    offers_dump = [o.model_dump() for o in offers]
    await state.update_data(
        origin=intent.origin,
        destination=intent.destination,
        date=intent.date,
        direct_only=intent.direct_only,
        offers=offers_dump,
    )

    list_text = (
        f"✈️ <b>Найдено рейсов ({len(offers)})</b> по маршруту <code>{intent.origin}</code> ✈️ <code>{intent.destination}</code> на <code>{intent.date}</code>:\n\n"
        "Выберите рейс для настройки мониторинга:"
    )
    keyboard = build_flight_list_keyboard(offers_dump)
    await status_msg.edit_text(list_text, reply_markup=keyboard, parse_mode="HTML")


# Alias for backward-compatibility in tests
handle_nlp_message = handle_search_query_message


# ============================================================================
# Step 2: Flight Details & "Мониторить этот рейс"
# ============================================================================

@router.callback_query(FlightSelectCallback.filter())
async def handle_flight_select_callback(
    callback: CallbackQuery,
    callback_data: FlightSelectCallback,
    state: FSMContext,
) -> None:
    """Handle flight selection from the search results list and display detailed flight card."""
    data = await state.get_data()
    offers = data.get("offers", [])

    if not offers or callback_data.flight_idx >= len(offers):
        await callback.answer("⚠️ Рейс не найден. Попробуйте выполнить поиск заново.", show_alert=True)
        return

    selected = offers[callback_data.flight_idx]
    await state.update_data(
        selected_flight_idx=callback_data.flight_idx,
        selected_flight=selected,
    )

    card_text = format_flight_details_card(selected)
    keyboard = build_flight_details_keyboard(callback_data.flight_idx)

    await callback.answer()
    if callback.message:
        await callback.message.edit_text(card_text, reply_markup=keyboard, parse_mode="HTML")


# ============================================================================
# Step 3: Interval configuration
# ============================================================================

@router.callback_query(MonitorFlightCallback.filter())
async def handle_monitor_flight_callback(
    callback: CallbackQuery,
    callback_data: MonitorFlightCallback,
    state: FSMContext,
) -> None:
    """Transition to waiting_for_interval state and prompt for check frequency."""
    await state.set_state(SniperStates.waiting_for_interval)
    prompt_text = (
        "⏱ <b>Настройка интервала проверки</b>\n\n"
        "Напишите, как часто проверять цену? (например: <i>каждые 10 минут, раз в час</i>)\n"
        "или выберите один из быстрых вариантов ниже:"
    )
    keyboard = build_interval_keyboard()
    await callback.answer()
    if callback.message:
        await callback.message.edit_text(prompt_text, reply_markup=keyboard, parse_mode="HTML")


@router.message(SniperStates.waiting_for_interval, F.text & ~F.text.startswith("/"))
async def handle_interval_text_message(message: Message, state: FSMContext) -> None:
    """Handle custom text check interval, set target price, and render confirmation card."""
    user_text = (message.text or "").strip()
    if not user_text:
        return

    interval = await parse_interval_nlp(user_text)
    data = await state.get_data()
    selected_flight = data.get("selected_flight")

    if not selected_flight:
        await message.answer(
            "⚠️ Данные рейса не найдены. Пожалуйста, начните поиск заново через /start.",
            parse_mode="HTML",
        )
        await state.clear()
        return

    target_price = float(_get_flight_attr(selected_flight, "price_kzt", 50000.0))
    await state.update_data(interval_minutes=interval, target_price=target_price)

    updated_data = await state.get_data()
    card_text = format_confirmation_card(updated_data)
    keyboard = build_confirmation_keyboard()

    await message.answer(card_text, reply_markup=keyboard, parse_mode="HTML")


@router.callback_query(QuickIntervalCallback.filter())
async def handle_quick_interval_callback(
    callback: CallbackQuery,
    callback_data: QuickIntervalCallback,
    state: FSMContext,
) -> None:
    """Handle quick preset interval button, set target price, and render confirmation card."""
    interval = callback_data.minutes
    data = await state.get_data()
    selected_flight = data.get("selected_flight")

    if not selected_flight:
        await callback.answer("⚠️ Данные рейса не найдены. Начните поиск заново.", show_alert=True)
        await state.clear()
        return

    target_price = float(_get_flight_attr(selected_flight, "price_kzt", 50000.0))
    await state.update_data(interval_minutes=interval, target_price=target_price)

    updated_data = await state.get_data()
    card_text = format_confirmation_card(updated_data)
    keyboard = build_confirmation_keyboard()

    await callback.answer()
    if callback.message:
        await callback.message.edit_text(card_text, reply_markup=keyboard, parse_mode="HTML")


# ============================================================================
# Step 4: Confirm, Back & Cancel Callbacks
# ============================================================================

@router.callback_query(ConfirmSnipeCallback.filter())
async def handle_confirm_snipe_callback(
    callback: CallbackQuery,
    callback_data: Optional[Any] = None,
    state: Optional[FSMContext] = None,
) -> None:
    """Handle snipe confirmation, save task into database, and clear FSM state."""
    data = await state.get_data() if state else {}
    selected_flight = data.get("selected_flight")

    if not data or not selected_flight:
        await callback.answer("⚠️ Сессия истекла или данные не найдены. Попробуйте снова.", show_alert=True)
        if state:
            await state.clear()
        return

    chat_id = callback.message.chat.id if callback.message else 0
    origin = data.get("origin", _get_flight_attr(selected_flight, "origin", ""))
    destination = data.get("destination", _get_flight_attr(selected_flight, "destination", ""))
    date_str = data.get("date", "")
    flight_number = _get_flight_attr(selected_flight, "flight_number")
    target_price = float(data.get("target_price", _get_flight_attr(selected_flight, "price_kzt", 50000.0)))
    interval_minutes = int(data.get("interval_minutes", 5))
    max_transfers = int(_get_flight_attr(selected_flight, "transfers_count", 0))

    try:
        task_id = await dao.add_task(
            chat_id=chat_id,
            origin=origin,
            destination=destination,
            date=date_str,
            target_price=target_price,
            flight_number=flight_number,
            max_transfers=max_transfers,
            interval_minutes=interval_minutes,
        )

        if state:
            await state.clear()

        flight_label = f"<code>{flight_number}</code>" if flight_number else "<i>Любой рейс</i>"
        formatted_price = f"{target_price:,.0f} ₸".replace(",", " ")
        airline_label = _get_flight_attr(selected_flight, "airline", "N/A")

        confirmation_card = (
            "🎯 <b>Снайпер активирован!</b>\n\n"
            f"• <b>ID задачи:</b> <code>#{task_id}</code>\n"
            f"• <b>Маршрут:</b> <code>{origin}</code> ✈️ <code>{destination}</code>\n"
            f"• <b>Дата вылета:</b> <code>{date_str}</code>\n"
            f"• <b>Авиакомпания:</b> {airline_label}\n"
            f"• <b>Рейс:</b> {flight_label}\n"
            f"• <b>Целевая цена:</b> ≤ <b>{formatted_price}</b>\n"
            f"• <b>Частота проверки:</b> ⏱ <i>Каждые {interval_minutes} мин</i>\n"
            "• <b>Статус:</b> 🟢 <i>Активный мониторинг</i>\n\n"
            "🔔 <i>Вы получите мгновенное уведомление в Telegram, как только цена билета упадет ниже целевой!</i>\n\n"
            f"<i>Для отмены в любой момент:</i> <code>/delete {task_id}</code>"
        )

        await callback.answer("🎯 Задача успешно создана!")
        if callback.message:
            await callback.message.edit_text(confirmation_card, reply_markup=None, parse_mode="HTML")

    except Exception as e:
        logger.exception("Failed to insert task from ConfirmSnipeCallback: %s", e)
        await callback.answer("❌ Ошибка создания задачи. Пожалуйста, попробуйте позже.", show_alert=True)


@router.callback_query(StepBackCallback.filter())
async def handle_step_back_callback(
    callback: CallbackQuery,
    callback_data: StepBackCallback,
    state: FSMContext,
) -> None:
    """Handle step back navigation between FSM steps."""
    data = await state.get_data()

    if callback_data.to_step == "flights":
        offers = data.get("offers", [])
        if offers:
            origin = data.get("origin", "")
            dest = data.get("destination", "")
            date_str = data.get("date", "")
            list_text = (
                f"✈️ <b>Найдено рейсов ({len(offers)})</b> по маршруту <code>{origin}</code> ✈️ <code>{dest}</code> на <code>{date_str}</code>:\n\n"
                "Выберите рейс для настройки мониторинга:"
            )
            keyboard = build_flight_list_keyboard(offers)
            await state.set_state(SniperStates.waiting_for_search_query)
            await callback.answer()
            if callback.message:
                await callback.message.edit_text(list_text, reply_markup=keyboard, parse_mode="HTML")
        else:
            await state.set_state(SniperStates.waiting_for_search_query)
            await callback.answer()
            if callback.message:
                await callback.message.edit_text(
                    "💬 <b>Напишите маршрут и дату для поиска:</b> (например, <i>«Алматы - Чэнду 21 ноября»</i>)",
                    reply_markup=None,
                    parse_mode="HTML",
                )

    elif callback_data.to_step == "interval":
        await state.set_state(SniperStates.waiting_for_interval)
        prompt_text = (
            "⏱ <b>Настройка интервала проверки</b>\n\n"
            "Напишите, как часто проверять цену? (например: <i>каждые 10 минут, раз в час</i>)\n"
            "или выберите один из быстрых вариантов ниже:"
        )
        keyboard = build_interval_keyboard()
        await callback.answer()
        if callback.message:
            await callback.message.edit_text(prompt_text, reply_markup=keyboard, parse_mode="HTML")


@router.callback_query(CancelSnipeCallback.filter())
@router.callback_query(F.data.startswith("cancel_snipe"))
async def handle_cancel_snipe_callback(
    callback: CallbackQuery,
    callback_data: Optional[Any] = None,
    state: Optional[FSMContext] = None,
) -> None:
    """Handle cancellation of snipe task creation in FSM flow."""
    if state:
        await state.clear()

    await callback.answer("Отменено")
    if callback.message:
        await callback.message.edit_text("❌ <b>Создание задачи отменено.</b>", reply_markup=None, parse_mode="HTML")
