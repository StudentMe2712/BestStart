"""One-time interactive Telethon login. Run once before /parse can work:

    python -m parsertg.login

It asks for your phone number and the login code Telegram sends you (plus your 2FA
password if you have one), then writes the session file referenced by SESSION_PATH.
After that the bot reuses the session — no further prompts."""
from __future__ import annotations

import asyncio
import logging

from . import parser
from .config import load_settings


async def _main() -> None:
    settings = load_settings()
    if not settings.api_id or not settings.api_hash:
        raise SystemExit(
            "Сначала заполни TELETHON_API_ID и TELETHON_API_HASH в .env "
            "(получить на https://my.telegram.org → API development tools)."
        )
    client = parser.build_client(settings)
    await client.start()  # prompts for phone + code (+ 2FA password if set)
    me = await client.get_me()
    print(f"OK — авторизован как {me.first_name} (id={me.id}). Сессия: {settings.session_path}.session")
    await client.disconnect()


if __name__ == "__main__":
    logging.basicConfig(level="INFO")
    asyncio.run(_main())
