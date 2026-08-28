"""Interactive Telegram Authentication Script for TrendScanner (Level 3).

Usage inside Docker container:
    docker exec -it trendscanner-backend python login_telegram.py

Usage standalone:
    python login_telegram.py
"""

import asyncio
import os
import sys
from pathlib import Path

from telethon import TelegramClient
from telethon.errors import (
    ApiIdInvalidError,
    PasswordHashInvalidError,
    PhoneCodeExpiredError,
    PhoneCodeInvalidError,
    PhoneNumberInvalidError,
    SessionPasswordNeededError,
)

from app.core.settings import settings


async def authenticate() -> None:
    """Run interactive Telegram client authentication and persist SQLite session."""
    print("=" * 65)
    print("      TrendScanner - Telegram Interactive Authentication CLI")
    print("=" * 65)

    # 1. API ID
    api_id = settings.TG_API_ID
    if not api_id:
        val = input("[?] Enter Telegram API ID (numeric): ").strip()
        try:
            api_id = int(val)
        except ValueError:
            print("[!] ERROR: API ID must be a valid integer.")
            sys.exit(1)
    else:
        print(f"[*] Loaded TG_API_ID from settings: {api_id}")

    # 2. API HASH
    api_hash = settings.TG_API_HASH
    if not api_hash or not api_hash.strip():
        api_hash = input("[?] Enter Telegram API HASH: ").strip()
        if not api_hash:
            print("[!] ERROR: API HASH cannot be empty.")
            sys.exit(1)
    else:
        masked_hash = api_hash[:4] + "..." + api_hash[-4:] if len(api_hash) > 8 else "***"
        print(f"[*] Loaded TG_API_HASH from settings: {masked_hash}")

    # 3. Phone Number
    phone = settings.TG_PHONE
    if not phone or not phone.strip():
        phone = input("[?] Enter Phone Number with country code (e.g. +1234567890): ").strip()
        if not phone:
            print("[!] ERROR: Phone number cannot be empty.")
            sys.exit(1)
    else:
        print(f"[*] Loaded TG_PHONE from settings: {phone}")

    # 4. Session File Path
    session_path = settings.TG_SESSION_PATH
    Path(session_path).parent.mkdir(parents=True, exist_ok=True)
    print(f"[*] Target Session File: {session_path}")

    # 5. Initialize Telegram Client
    client = TelegramClient(session_path, api_id, api_hash)

    try:
        print("\n[*] Connecting to Telegram gateway...")
        await client.connect()

        if not await client.is_user_authorized():
            print(f"[*] Starting authentication handshake for: {phone}")
            await client.start(phone=phone)
        else:
            print("[✓] Active session already authorized.")

        # 6. Verify Authorization and print user info
        if await client.is_user_authorized():
            me = await client.get_me()
            first_name = getattr(me, "first_name", "") or ""
            last_name = getattr(me, "last_name", "") or ""
            full_name = f"{first_name} {last_name}".strip() or "N/A"
            username = getattr(me, "username", None)
            username_str = f"@{username}" if username else "(no username)"
            user_id = getattr(me, "id", "Unknown")

            print("\n" + "=" * 65)
            print(" [✓] Telegram Authentication Successful!")
            print(f"     - Name:      {full_name}")
            print(f"     - Username:  {username_str}")
            print(f"     - User ID:   {user_id}")
            print(f"     - Session:   {session_path}")
            print(" Session file is saved and ready for TrendScanner worker pipelines.")
            print("=" * 65 + "\n")
        else:
            print("[!] Authorization failed. Please re-run the script.")
            sys.exit(1)

    except ApiIdInvalidError:
        print("[!] ERROR: The api_id/api_hash combination is invalid.")
        sys.exit(1)
    except PhoneNumberInvalidError:
        print("[!] ERROR: The phone number is invalid.")
        sys.exit(1)
    except (PhoneCodeInvalidError, PhoneCodeExpiredError):
        print("[!] ERROR: The verification code entered is invalid or expired.")
        sys.exit(1)
    except PasswordHashInvalidError:
        print("[!] ERROR: The 2-step verification cloud password entered is incorrect.")
        sys.exit(1)
    except Exception as exc:
        print(f"[!] ERROR: Unexpected exception during authentication: {exc}")
        sys.exit(1)
    finally:
        try:
            if client.is_connected():
                await client.disconnect()
        except Exception:
            pass


def main() -> None:
    """Synchronous entry point."""
    try:
        asyncio.run(authenticate())
    except KeyboardInterrupt:
        print("\n[!] Authentication cancelled by user.")
        sys.exit(0)


if __name__ == "__main__":
    main()
