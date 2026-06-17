"""One-time interactive Telethon login — explicit, verbose flow.

Run once before /parse can work:
    python -m parsertg.login

It asks for your phone number and the login code Telegram sends (plus your 2FA password
if you have one), writes the session file referenced by SESSION_PATH, then the bot reuses
that session with no further prompts.

We deliberately use the explicit connect -> send_code_request -> sign_in flow (NOT
client.start), so every step is logged and we can guarantee a USER login: a bot session
can't read channel history, which defeats the whole purpose."""
from __future__ import annotations

import asyncio
import logging
from getpass import getpass

from telethon.errors import SessionPasswordNeededError

from . import parser
from .config import load_settings


def _mask(phone: str) -> str:
    return phone[:4] + "…" + phone[-2:] if len(phone) > 6 else phone


async def _main() -> None:
    settings = load_settings()
    if not settings.api_id or not settings.api_hash:
        raise SystemExit(
            "Сначала заполни TELETHON_API_ID и TELETHON_API_HASH в .env "
            "(получить на https://my.telegram.org → API development tools)."
        )

    client = parser.build_client(settings)

    # 1) Connect (does not sign in by itself).
    await client.connect()
    print("Connected: OK")

    # Reuse an existing valid USER session if there is one.
    if await client.is_user_authorized():
        me = await client.get_me()
        if me.bot:
            await client.log_out()  # disconnects + removes the wrong bot session file
            raise SystemExit(
                "Существующая сессия — БОТ, она удалена. Запусти снова и введи номер телефона."
            )
        print(f"Уже авторизован как {me.first_name} (id={me.id}) — новый логин не нужен.")
        await client.disconnect()
        return

    print("User login flow")
    phone = input("Номер телефона (+7…), НЕ токен бота: ").strip()
    if ":" in phone:
        await client.disconnect()
        raise SystemExit("Это похоже на токен бота. Нужен НОМЕР ТЕЛЕФОНА твоего аккаунта.")

    # 2) Request the login code.
    print(f"Sending code to {_mask(phone)}")
    sent = await client.send_code_request(phone)
    # 3) Confirm Telegram returned a phone_code_hash (needed to complete sign-in).
    print(f"Code hash received: {'есть' if sent.phone_code_hash else 'нет'}")

    # 4) Sign in with the code (+ 2FA password if the account has one).
    code = input("Код из Telegram: ").strip()
    try:
        await client.sign_in(phone=phone, code=code, phone_code_hash=sent.phone_code_hash)
    except SessionPasswordNeededError:
        print("Включена 2FA — нужен облачный пароль.")
        await client.sign_in(password=getpass("Пароль 2FA: "))

    me = await client.get_me()
    if me.bot:
        await client.log_out()
        raise SystemExit("Вход выполнен как БОТ — сессия удалена. Перелогинься номером телефона.")
    print(f"OK — авторизован как {me.first_name} (id={me.id}). Сессия: {settings.session_path}.session")
    await client.disconnect()


if __name__ == "__main__":
    logging.basicConfig(level="INFO")  # keep Telethon's own connection logs visible
    asyncio.run(_main())
