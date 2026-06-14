"""Project Memory (P2) — Telegram bot: capture + chat (stage 2 / P2.2).

Long polling (без webhook/домена/внешнего сервера) — работает на локальной машине
рядом с бэкендом. Тонкий HTTP-клиент: НЕ ходит в БД напрямую, а шлёт в backend.

Два режима, оба через backend (никакой логики памяти в боте):
  • ЗАХВАT (по умолчанию) — текст/ссылка/код → `POST /memory/items`,
    документ/фото → `POST /memory/items/file`. AI-теггер дозаполнит summary/tags/type.
  • ЧАT — `/ask <вопрос>` → `POST /chat` (RAG по ВСЕЙ памяти PAM: profile_facts +
    разговоры + memory_items + …). Один непрерывный тред на пользователя (хранится
    conversation_id в памяти процесса), синхронизируется с веб-чатом как pam-беседа.
    `/new` — начать новый тред.

Запуск: как сервис `bot` в docker-compose (рядом с backend), либо вручную
    cd backend && python -m app.telegram_bot
Требует в backend/.env: TELEGRAM_BOT_TOKEN, TELEGRAM_ALLOWED_USER_ID
(+ BACKEND_URL — в compose `http://backend:8000`, иначе по умолч. http://localhost:8000).
Голосовые в V1 не обрабатываются. Доступ — только у TELEGRAM_ALLOWED_USER_ID.

Зависимость: aiogram>=3 (см. pyproject). Бот импортируется только при явном запуске,
поэтому его отсутствие не влияет на backend.
"""
from __future__ import annotations

import asyncio
import json
import logging

import httpx

from .config import settings

log = logging.getLogger("pam.telegram")

_TIMEOUT = httpx.Timeout(60.0)
_CHAT_TIMEOUT = httpx.Timeout(180.0)  # ответ LLM может идти десятки секунд
_TG_LIMIT = 4000  # запас под лимит сообщения Telegram (4096)

# Непрерывный тред чата на пользователя: telegram uid -> conversation_id (pam-беседа).
# Живёт в памяти процесса: после перезапуска бота тред начинается заново (или /new).
_threads: dict[int, str] = {}


def _denied(message) -> bool:
    """True, если отправитель не входит в allow-list (единственный пользователь)."""
    uid = getattr(getattr(message, "from_user", None), "id", None)
    return not settings.TELEGRAM_ALLOWED_USER_ID or uid != settings.TELEGRAM_ALLOWED_USER_ID


async def _post_text(content: str, source_ref: str) -> bool:
    async with httpx.AsyncClient(timeout=_TIMEOUT) as client:
        r = await client.post(
            f"{settings.BACKEND_URL}/memory/items",
            json={"source": "telegram", "source_ref": source_ref, "content": content},
        )
        r.raise_for_status()
    return True


async def _post_file(data: bytes, filename: str, source_ref: str) -> bool:
    async with httpx.AsyncClient(timeout=_TIMEOUT) as client:
        r = await client.post(
            f"{settings.BACKEND_URL}/memory/items/file",
            params={"source": "telegram", "source_ref": source_ref},
            files={"file": (filename, data, "application/octet-stream")},
        )
        r.raise_for_status()
    return True


async def _chat(question: str, conv_id: str | None) -> tuple[str, str | None, str | None]:
    """Спросить `/chat` (SSE, RAG по всей памяти PAM). Возврат: (ответ, conv_id, ошибка).

    Токены копим в полный ответ (Telegram не стримит по-токенно); conversation_id из
    события `done` — продолжение того же треда; ответ заодно сохраняется бэкендом как
    pam-беседа (та самая синхронизация с веб-чатом).
    """
    payload: dict = {"message": question}
    if conv_id:
        payload["conversation_id"] = conv_id
    answer, new_conv, err = "", conv_id, None
    async with httpx.AsyncClient(timeout=_CHAT_TIMEOUT) as client:
        async with client.stream("POST", f"{settings.BACKEND_URL}/chat", json=payload) as r:
            if r.status_code >= 400:
                body = (await r.aread()).decode("utf-8", "replace")[:300]
                return "", conv_id, f"backend {r.status_code}: {body}"
            async for line in r.aiter_lines():
                if not line.startswith("data: "):
                    continue
                try:
                    obj = json.loads(line[6:])
                except json.JSONDecodeError:
                    continue
                if "token" in obj:
                    answer += obj["token"]
                elif "error" in obj:
                    err = obj["error"]
                elif obj.get("done"):
                    new_conv = obj.get("conversation_id") or conv_id
    return answer.strip(), new_conv, err


def _chunks(text: str, n: int = _TG_LIMIT):
    """Разбить длинный ответ под лимит сообщения Telegram."""
    for i in range(0, len(text), n):
        yield text[i : i + n]


def build_dispatcher():
    """Собрать aiogram Dispatcher с обработчиками. Импорт aiogram — лениво."""
    from aiogram import Dispatcher, F
    from aiogram.filters import Command, CommandStart
    from aiogram.types import Message

    dp = Dispatcher()

    @dp.message(CommandStart())
    async def on_start(message: "Message") -> None:
        if _denied(message):
            return
        await message.answer(
            "PAM на связи.\n"
            "• Текст / ссылка / код / файл / фото — сохраню в память "
            "(теги и тип проставлю автоматически).\n"
            "• /ask <вопрос> — отвечу с учётом всей твоей памяти PAM "
            "(синхронизируется с веб-чатом).\n"
            "• /new — начать новый тред чата.\n"
            "(Голосовые пока не поддерживаются.)"
        )

    # Команды регистрируем ДО общего обработчика текста (F.text), иначе «/ask …»
    # перехватился бы захватом: aiogram берёт первый подошедший обработчик.
    @dp.message(Command("new"))
    async def on_new(message: "Message") -> None:
        if _denied(message):
            return
        _threads.pop(message.from_user.id, None)
        await message.answer("Начал новый тред ✓ Следующий /ask — с чистой историей.")

    @dp.message(Command("ask"))
    async def on_ask(message: "Message") -> None:
        if _denied(message):
            return
        parts = (message.text or "").split(maxsplit=1)
        question = parts[1].strip() if len(parts) > 1 else ""
        if not question:
            await message.answer("Напиши вопрос после команды: /ask <вопрос>")
            return
        uid = message.from_user.id
        try:
            await message.bot.send_chat_action(message.chat.id, "typing")
        except Exception:  # noqa: BLE001 — индикатор «печатает» не критичен
            pass
        try:
            answer, conv_id, err = await _chat(question, _threads.get(uid))
        except Exception as e:  # noqa: BLE001
            log.warning("chat ask failed: %s", e)
            await message.answer(f"Не удалось ответить: {e}")
            return
        if conv_id:
            _threads[uid] = conv_id  # держим один непрерывный тред
        if not answer:
            await message.answer(f"Ошибка модели: {err}" if err else "Пустой ответ модели.")
            return
        for ch in _chunks(answer):
            await message.answer(ch)

    @dp.message(F.voice | F.audio | F.video_note)
    async def on_voice(message: "Message") -> None:
        if _denied(message):
            return
        await message.answer("Голосовые/аудио в этой версии не поддерживаются. Пришли текстом или файлом.")

    @dp.message(F.document)
    async def on_document(message: "Message") -> None:
        if _denied(message):
            return
        doc = message.document
        try:
            file = await message.bot.get_file(doc.file_id)
            buf = await message.bot.download_file(file.file_path)
            data = buf.read()
            await _post_file(data, doc.file_name or "file", str(message.message_id))
            await message.answer(f"Сохранил документ «{doc.file_name or 'файл'}» ✓")
        except Exception as e:  # noqa: BLE001
            log.warning("document capture failed: %s", e)
            await message.answer(f"Не удалось сохранить документ: {e}")

    @dp.message(F.photo)
    async def on_photo(message: "Message") -> None:
        if _denied(message):
            return
        try:
            photo = message.photo[-1]  # самое большое разрешение
            file = await message.bot.get_file(photo.file_id)
            buf = await message.bot.download_file(file.file_path)
            data = buf.read()
            await _post_file(data, f"photo_{message.message_id}.jpg", str(message.message_id))
            await message.answer("Сохранил изображение ✓ (распознаю текст/опишу)")
        except Exception as e:  # noqa: BLE001
            log.warning("photo capture failed: %s", e)
            await message.answer(f"Не удалось сохранить изображение: {e}")

    @dp.message(F.text)
    async def on_text(message: "Message") -> None:
        if _denied(message):
            return
        text = (message.text or "").strip()
        if not text:
            return
        try:
            await _post_text(text, str(message.message_id))
            await message.answer("Сохранил в память ✓ (теги и тип проставлю автоматически)")
        except Exception as e:  # noqa: BLE001
            log.warning("text capture failed: %s", e)
            await message.answer(f"Не удалось сохранить: {e}")

    return dp


async def main() -> None:
    logging.basicConfig(level=logging.INFO)
    if not settings.TELEGRAM_BOT_TOKEN:
        raise SystemExit("TELEGRAM_BOT_TOKEN не задан (backend/.env).")
    if not settings.TELEGRAM_ALLOWED_USER_ID:
        raise SystemExit("TELEGRAM_ALLOWED_USER_ID не задан (backend/.env) — бот никого не пустит.")
    from aiogram import Bot

    bot = Bot(settings.TELEGRAM_BOT_TOKEN)
    dp = build_dispatcher()
    try:
        from aiogram.types import BotCommand

        await bot.set_my_commands([
            BotCommand(command="ask", description="Спросить PAM (ответ с памятью)"),
            BotCommand(command="new", description="Новый тред чата"),
        ])
    except Exception as e:  # noqa: BLE001 — меню команд не критично
        log.warning("set_my_commands failed: %s", e)
    log.info("PAM Telegram bot: long polling started (allowed uid=%s)", settings.TELEGRAM_ALLOWED_USER_ID)
    await dp.start_polling(bot)


if __name__ == "__main__":
    asyncio.run(main())
