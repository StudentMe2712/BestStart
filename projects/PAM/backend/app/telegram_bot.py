"""Project Memory (P2) — Telegram capture bot (stage 2).

Long polling (без webhook/домена/внешнего сервера) — работает на локальной машине
рядом с бэкендом. Тонкий HTTP-клиент: НЕ ходит в БД напрямую, а шлёт в backend
(`POST /memory/items` для текста/ссылок/кода, `POST /memory/items/file` для
документов/фото). AI-теггер на стороне бэкенда дозаполнит summary/tags/type.

Запуск (отдельный процесс, рядом с uvicorn):
    cd backend && python -m app.telegram_bot
Требует в backend/.env: TELEGRAM_BOT_TOKEN, TELEGRAM_ALLOWED_USER_ID
(+ опц. BACKEND_URL, по умолчанию http://localhost:8000). Голосовые в V1 не
обрабатываются (см. решение P2). Доступ — только у TELEGRAM_ALLOWED_USER_ID.

Зависимость: aiogram>=3 (см. pyproject). На машине без неё бот не запускается —
это не влияет на backend (бот импортируется только при явном запуске).
"""
from __future__ import annotations

import asyncio
import logging

import httpx

from .config import settings

log = logging.getLogger("pam.telegram")

_TIMEOUT = httpx.Timeout(60.0)


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


def build_dispatcher():
    """Собрать aiogram Dispatcher с обработчиками. Импорт aiogram — лениво."""
    from aiogram import Dispatcher, F
    from aiogram.filters import CommandStart
    from aiogram.types import Message

    dp = Dispatcher()

    @dp.message(CommandStart())
    async def on_start(message: "Message") -> None:
        if _denied(message):
            return
        await message.answer(
            "PAM на связи. Пришли текст, ссылку, код или документ — сохраню в память "
            "и автоматически проставлю теги и тип. (Голосовые пока не поддерживаются.)"
        )

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
    log.info("PAM Telegram bot: long polling started (allowed uid=%s)", settings.TELEGRAM_ALLOWED_USER_ID)
    await dp.start_polling(bot)


if __name__ == "__main__":
    asyncio.run(main())
