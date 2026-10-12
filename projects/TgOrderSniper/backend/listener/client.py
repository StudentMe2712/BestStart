"""Telethon MTProto Telegram Listener.

Provides real-time event-driven message stream listening from specified
Telegram channels and groups with zero polling latency and automatic reconnects.
"""

import asyncio
import logging
from typing import Any, Awaitable, Callable, Union

from telethon import TelegramClient, events
from telethon.sessions import StringSession

from backend.core.config import Settings, settings as global_settings

logger = logging.getLogger(__name__)


class TelegramListener:
    """Asynchronous MTProto listener based on Telethon.

    Listens to real-time events.NewMessage from monitored channels,
    extracts message metadata, builds deep links, and forwards
    data to the registered processing pipeline.
    """

    def __init__(self, settings: Settings | None = None) -> None:
        """Initializes listener with application settings.

        Args:
            settings: Settings instance. If omitted, uses global cached settings.
        """
        self.settings = settings or global_settings
        self._handler_callback: Callable[[dict[str, Any]], Awaitable[None]] | None = None
        self._is_running: bool = False

        # Initialize session: StringSession or SQLite session file
        if self.settings.TG_STRING_SESSION and self.settings.TG_STRING_SESSION.strip():
            raw_str = self.settings.TG_STRING_SESSION.strip()
            try:
                session = StringSession(raw_str)
                logger.info("[Listener] Initialized using Telethon StringSession.")
            except Exception as exc:
                logger.error(
                    f"[Listener] Failed to parse TG_STRING_SESSION: {exc}. Falling back to empty in-memory StringSession."
                )
                session = StringSession()
        else:
            session = self.settings.TG_SESSION_NAME
            logger.info(
                f"[Listener] Initialized using file session: '{self.settings.TG_SESSION_NAME}.session'"
            )

        if self.settings.TG_API_ID and self.settings.TG_API_HASH:
            self.client: TelegramClient | None = TelegramClient(
                session,
                api_id=self.settings.TG_API_ID,
                api_hash=self.settings.TG_API_HASH,
                connection_retries=None,  # Infinite retries on network drops
                retry_delay=5,
                auto_reconnect=True,
            )
        else:
            logger.warning(
                "[Listener] TG_API_ID or TG_API_HASH is not configured. "
                "Telethon client deferred until credentials are provided."
            )
            self.client = None

    @property
    def is_connected(self) -> bool:
        """Checks if client is currently connected to Telegram MTProto servers."""
        return bool(self.client and self.client.is_connected())

    @property
    def is_running(self) -> bool:
        """Checks if listener event loop is running."""
        return self._is_running

    @staticmethod
    def build_message_url(chat: Any, chat_id: Union[int, str], message_id: int) -> str:
        """Constructs a clickable web URL for a Telegram message.

        Handles public channels with @usernames and private groups/channels (t.me/c/...).

        Args:
            chat: Telethon Chat/Channel entity (if available).
            chat_id: ID or username of the chat.
            message_id: Telegram message ID.

        Returns:
            Direct link string (e.g. 'https://t.me/channel/123' or 'https://t.me/c/1234567890/123').
        """
        # 1. Public chat username
        username = getattr(chat, "username", None)
        if username:
            return f"https://t.me/{username}/{message_id}"

        # 2. String chat_id
        if isinstance(chat_id, str):
            clean_str = chat_id.strip()
            if clean_str.startswith("@"):
                return f"https://t.me/{clean_str[1:]}/{message_id}"
            if clean_str.startswith("-100"):
                return f"https://t.me/c/{clean_str[4:]}/{message_id}"
            if clean_str.startswith("-"):
                return f"https://t.me/c/{clean_str[1:]}/{message_id}"
            if clean_str.isdigit():
                return f"https://t.me/c/{clean_str}/{message_id}"
            return f"https://t.me/{clean_str}/{message_id}"

        # 3. Numeric chat_id
        if isinstance(chat_id, int):
            str_id = str(chat_id)
            if str_id.startswith("-100"):
                return f"https://t.me/c/{str_id[4:]}/{message_id}"
            if str_id.startswith("-"):
                return f"https://t.me/c/{str_id[1:]}/{message_id}"
            return f"https://t.me/c/{str_id}/{message_id}"

        return ""

    async def extract_message_data(self, event: events.NewMessage.Event) -> dict[str, Any]:
        """Extracts structured metadata from a NewMessage event.

        Args:
            event: Telethon NewMessage event.

        Returns:
            Dictionary with normalized message attributes.
        """
        message = event.message
        chat_id = event.chat_id
        message_id = message.id
        raw_text = message.text or message.raw_text or ""

        # Safely resolve chat entity
        chat = None
        try:
            chat = await event.get_chat()
        except Exception as exc:
            logger.debug(f"[Listener] Unable to get chat entity for {chat_id}: {exc}")

        chat_title = ""
        if chat:
            chat_title = (
                getattr(chat, "title", None)
                or getattr(chat, "username", None)
                or str(chat_id)
            )
        else:
            chat_title = str(chat_id)

        # Safely resolve sender entity
        sender = None
        try:
            sender = await event.get_sender()
        except Exception as exc:
            logger.debug(f"[Listener] Unable to get sender entity for message {message_id}: {exc}")

        sender_id = event.sender_id
        sender_username = None
        sender_name = None

        if sender:
            sender_id = getattr(sender, "id", sender_id)
            sender_username = getattr(sender, "username", None)
            first_name = getattr(sender, "first_name", "") or ""
            last_name = getattr(sender, "last_name", "") or ""
            full_name = f"{first_name} {last_name}".strip()
            if not full_name:
                full_name = (
                    getattr(sender, "title", "")
                    or sender_username
                    or (str(sender_id) if sender_id else "Unknown")
                )
            sender_name = full_name

        message_url = self.build_message_url(chat, chat_id, message_id)

        return {
            "chat_id": chat_id,
            "chat_title": chat_title,
            "message_id": message_id,
            "text": raw_text,
            "author_id": sender_id,
            "author_username": sender_username,
            "author_name": sender_name,
            "message_url": message_url,
            "sender": sender,
            "chat": chat,
            "raw_event": event,
        }

    async def start(
        self,
        message_handler_callback: Callable[[dict[str, Any]], Awaitable[None]] | None = None,
    ) -> None:
        """Starts the Telethon client and registers incoming message event handlers.

        Args:
            message_handler_callback: Async callback function invoked on each new message.
        """
        if self.client is None:
            raise ValueError(
                "Cannot start TelegramListener: TG_API_ID and TG_API_HASH must be configured."
            )

        self._handler_callback = message_handler_callback
        chats = self.settings.CHANNELS_TO_WATCH if self.settings.CHANNELS_TO_WATCH else None

        logger.info(
            f"[Listener] Registering events.NewMessage handler (watching {len(chats) if chats else 'ALL'} chats)"
        )

        @self.client.on(events.NewMessage(chats=chats))
        async def on_new_message(event: events.NewMessage.Event) -> None:
            try:
                data = await self.extract_message_data(event)
                if self._handler_callback:
                    await self._handler_callback(data)
            except Exception as exc:
                logger.error(
                    f"[Listener] Unhandled exception in message handler: {exc}",
                    exc_info=True,
                )

        logger.info("[Listener] Connecting to Telegram MTProto...")
        if not self.client.is_connected():
            await self.client.connect()

        self._is_running = True

        if not await self.client.is_user_authorized():
            logger.warning(
                "[Listener] Client is connected but NOT authorized! "
                "Please run interactive authentication or provide valid TG_STRING_SESSION."
            )
        else:
            me = await self.client.get_me()
            username_display = f"@{me.username}" if getattr(me, "username", None) else f"ID:{me.id}"
            logger.info(f"[Listener] Authorized successfully as user: {username_display}")

    async def run_until_disconnected(self) -> None:
        """Keeps listener running until disconnected or cancelled."""
        if not self.client:
            return
        try:
            await self.client.run_until_disconnected()
        except asyncio.CancelledError:
            logger.info("[Listener] Run loop cancelled.")
        finally:
            await self.stop()

    async def stop(self) -> None:
        """Gracefully disconnects client and halts listener."""
        self._is_running = False
        if self.client and self.client.is_connected():
            logger.info("[Listener] Gracefully disconnecting Telethon client...")
            await self.client.disconnect()
            logger.info("[Listener] Telethon client disconnected.")
