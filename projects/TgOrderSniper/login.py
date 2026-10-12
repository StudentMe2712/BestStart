"""
Авторизация через QR-код без отправки СМС и кодов.
Запуск: python login_qr.py
"""
import asyncio
import os
from dotenv import load_dotenv
from telethon import TelegramClient
import qrcode

load_dotenv()

API_ID = int(os.getenv("TG_API_ID"))
API_HASH = os.getenv("TG_API_HASH")
SESSION_NAME = os.getenv("TG_SESSION_NAME", "OrderParser")

# Маскируемся под официальный клиент Telegram Desktop
client = TelegramClient(
    SESSION_NAME,
    API_ID,
    API_HASH,
    device_model="Desktop",
    system_version="Windows 11",
    app_version="5.6.3 x64",
    lang_code="ru",
    system_lang_code="ru"
)

async def main():
    print("=" * 65)
    print("      Авторизация TgOrderSniper через QR-код (Без СМС)")
    print("=" * 65)
    
    await client.connect()
    
    if await client.is_user_authorized():
        print("[✓] Сессия уже авторизована!")
        return

    qr_login = await client.qr_login()
    
    print("\n[!] Сканируйте QR-код ниже в мобильном приложении Telegram:")
    print("    Настройки -> Устройства -> Подключить устройство\n")
    
    # Отрисовка QR в терминале
    qr = qrcode.QRCode()
    qr.add_data(qr_login.url)
    qr.print_ascii(invert=True)
    
    try:
        # Ожидание подтверждения с телефона (таймаут 120 секунд)
        await qr_login.wait(timeout=120)
    except Exception as e:
        print(f"\n[!] Ошибка или истекло время ожидания: {e}")
        return

    me = await client.get_me()
    print("\n" + "=" * 65)
    print(f" [✓] Успешный вход под: @{me.username or me.first_name}!")
    print(f" Файл сессии сохранен: {SESSION_NAME}.session")
    print("=" * 65)

if __name__ == "__main__":
    asyncio.run(main())