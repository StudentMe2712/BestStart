"""Global Telegram Search Engine and Poller based on Telethon.

Provides periodic keyword search across all Telegram public channels,
supergroups, and dynamic user dialogs using MTProto SearchGlobalRequest.
Protects against flood limits, filters by run timestamp (min_date),
and feeds discovered messages into the processing pipeline.
"""

import asyncio
from datetime import datetime, timezone
import logging
from typing import Any, Awaitable, Callable, Union

from telethon import TelegramClient, functions, types, utils
from telethon.errors import FloodWaitError

from backend.core.config import Settings, settings as global_settings
from backend.listener.client import TelegramListener

logger = logging.getLogger(__name__)


class GlobalSearchEngine:
    """Telegram Global Search Engine and poller.

    Periodically scans Telegram using `messages.SearchGlobalRequest` for
    configured search queries, extracting lead messages and feeding them
    into the order processing pipeline.
    """

    def __init__(
        self,
        client: TelegramClient | None = None,
        settings: Settings | None = None,
    ) -> None:
        """Initializes GlobalSearchEngine with Telethon client and settings.

        Args:
            client: Connected Telethon TelegramClient instance.
            settings: Settings instance. If omitted, uses global cached settings.
        """
        self.settings = settings or global_settings
        self.client = client
        self.last_run_timestamp: datetime | None = None
        self._is_running: bool = False
        self._known_dialogs: dict[int, Any] = {}

    @property
    def is_running(self) -> bool:
        """Checks if poller is currently active."""
        return self._is_running

    async def scan_dialogs(self) -> list[Any]:
        """Discovers and caches all dialogs (channels/groups) the client participates in.

        Returns:
            List of discovered dialog entities.
        """
        if not self.client or not self.client.is_connected():
            logger.debug("[GlobalSearch] Cannot scan dialogs: client not connected.")
            return []

        discovered: list[Any] = []
        try:
            logger.info("[GlobalSearch] Scanning user dialogs to discover active chats...")
            async for dialog in self.client.iter_dialogs():
                dialog_id = dialog.id
                self._known_dialogs[dialog_id] = dialog.entity
                discovered.append(dialog.entity)

            logger.info(
                f"[GlobalSearch] Dialog scan completed: {len(discovered)} dialogs indexed."
            )
        except FloodWaitError as err:
            wait_time = getattr(err, "seconds", 60) + 1
            logger.warning(
                f"[GlobalSearch] FloodWaitError during dialog scan. Sleeping for {wait_time}s..."
            )
            await asyncio.sleep(wait_time)
        except Exception as exc:
            logger.error(f"[GlobalSearch] Failed to scan dialogs: {exc}", exc_info=True)

        return discovered

    def extract_message_data(
        self,
        message: Any,
        chats: dict[int, Any] | None = None,
        users: dict[int, Any] | None = None,
    ) -> dict[str, Any]:
        """Extracts normalized message metadata from a Telethon Message object.

        Args:
            message: Telethon Message instance.
            chats: Optional dictionary mapping chat/channel IDs to entities.
            users: Optional dictionary mapping user IDs to entities.

        Returns:
            Structured dictionary matching the listener event data format.
        """
        chats_map = chats or {}
        users_map = users or {}

        message_id: int = getattr(message, "id", 0)
        raw_text: str = getattr(message, "message", None) or getattr(message, "text", "") or ""

        # Resolve chat ID and chat entity
        peer = getattr(message, "peer_id", None)
        chat_id: Union[int, str] = 0
        chat_entity: Any = None

        if peer is not None:
            try:
                chat_id = utils.get_peer_id(peer)
            except Exception:
                chat_id = getattr(peer, "channel_id", 0) or getattr(peer, "chat_id", 0)

            # Look up in chats_map or known dialogs
            raw_target_id = (
                getattr(peer, "channel_id", None)
                or getattr(peer, "chat_id", None)
                or getattr(peer, "user_id", None)
            )
            if raw_target_id in chats_map:
                chat_entity = chats_map[raw_target_id]
            elif chat_id in self._known_dialogs:
                chat_entity = self._known_dialogs[chat_id]
            elif raw_target_id in users_map:
                chat_entity = users_map[raw_target_id]

        chat_title = ""
        if chat_entity is not None:
            chat_title = (
                getattr(chat_entity, "title", None)
                or getattr(chat_entity, "username", None)
                or str(chat_id)
            )
        else:
            chat_title = str(chat_id)

        # Resolve author/sender
        from_id = getattr(message, "from_id", None)
        author_id: int | None = None
        sender_entity: Any = None

        if from_id is not None:
            author_id = getattr(from_id, "user_id", None)
            if author_id is None:
                author_id = getattr(from_id, "channel_id", None)
            if author_id in users_map:
                sender_entity = users_map[author_id]
            elif author_id in chats_map:
                sender_entity = chats_map[author_id]
        elif getattr(message, "sender_id", None):
            author_id = message.sender_id
            if author_id in users_map:
                sender_entity = users_map[author_id]

        author_username: str | None = None
        author_name: str | None = None

        if sender_entity is not None:
            author_username = getattr(sender_entity, "username", None)
            first_name = getattr(sender_entity, "first_name", "") or ""
            last_name = getattr(sender_entity, "last_name", "") or ""
            full_name = f"{first_name} {last_name}".strip()
            if not full_name:
                full_name = (
                    getattr(sender_entity, "title", "")
                    or author_username
                    or (str(author_id) if author_id else "Unknown")
                )
            author_name = full_name
        elif author_id:
            author_name = f"User {author_id}"

        message_url = TelegramListener.build_message_url(chat_entity, chat_id, message_id)

        return {
            "chat_id": chat_id,
            "chat_title": chat_title,
            "message_id": message_id,
            "text": raw_text,
            "author_id": author_id,
            "author_username": author_username,
            "author_name": author_name,
            "message_url": message_url,
            "sender": sender_entity,
            "chat": chat_entity,
            "raw_event": message,
        }

    async def search_iteration(
        self,
        on_message_callback: Callable[[dict[str, Any]], Awaitable[None]],
    ) -> int:
        """Runs a single search iteration across all configured search queries.

        Args:
            on_message_callback: Async callback invoked for every discovered message.

        Returns:
            Total count of discovered messages dispatched to the callback.
        """
        if self.client is None or not self.client.is_connected():
            logger.warning("[GlobalSearch] Client is not connected. Skipping search iteration.")
            return 0

        iteration_start = datetime.now(timezone.utc)
        min_date = self.last_run_timestamp
        queries = (
            self.settings.SEARCH_QUERIES
            if isinstance(self.settings.SEARCH_QUERIES, list)
            else [self.settings.SEARCH_QUERIES]
        )

        total_processed = 0
        logger.info(
            f"[GlobalSearch] Starting search iteration for {len(queries)} queries "
            f"(filtering messages newer than {min_date.isoformat() if min_date else 'beginning'})..."
        )

        for query in queries:
            clean_query = query.strip()
            if not clean_query:
                continue

            logger.debug(f"[GlobalSearch] Executing global search query: '{clean_query}'")
            offset_id = 0
            offset_peer: Any = types.InputPeerEmpty()
            offset_rate = 0
            max_pages = 5
            page = 0

            while page < max_pages:
                try:
                    request = functions.messages.SearchGlobalRequest(
                        q=clean_query,
                        filter=types.InputMessagesFilterEmpty(),
                        min_date=min_date,
                        max_date=None,
                        offset_rate=offset_rate,
                        offset_peer=offset_peer,
                        offset_id=offset_id,
                        limit=50,
                    )
                    result = await self.client(request)
                except FloodWaitError as flood_err:
                    wait_time = getattr(flood_err, "seconds", 60) + 1
                    logger.warning(
                        f"[GlobalSearch] Telegram FloodWaitError! Sleeping for {wait_time}s..."
                    )
                    await asyncio.sleep(wait_time)
                    continue
                except Exception as exc:
                    logger.error(
                        f"[GlobalSearch] Error searching query '{clean_query}': {exc}",
                        exc_info=True,
                    )
                    break

                messages = getattr(result, "messages", [])
                if not messages:
                    break

                chats_map = {
                    c.id: c
                    for c in getattr(result, "chats", [])
                    if getattr(c, "id", None) is not None
                }
                users_map = {
                    u.id: u
                    for u in getattr(result, "users", [])
                    if getattr(u, "id", None) is not None
                }

                page_processed = 0
                older_than_min_date = False

                for msg in messages:
                    # Skip empty / service messages without text
                    text = getattr(msg, "message", None) or getattr(msg, "text", None)
                    if not text:
                        continue

                    # Filter by min_date
                    msg_date = getattr(msg, "date", None)
                    if min_date and msg_date and msg_date < min_date:
                        older_than_min_date = True
                        break

                    data = self.extract_message_data(msg, chats=chats_map, users=users_map)
                    try:
                        await on_message_callback(data)
                        page_processed += 1
                        total_processed += 1
                    except Exception as cb_exc:
                        logger.error(
                            f"[GlobalSearch] Callback error for message {msg.id}: {cb_exc}",
                            exc_info=True,
                        )

                if older_than_min_date or len(messages) < 50:
                    break

                # Prepare pagination for next page
                last_msg = messages[-1]
                offset_id = last_msg.id
                try:
                    if getattr(last_msg, "peer_id", None):
                        offset_peer = await self.client.get_input_entity(last_msg.peer_id)
                    else:
                        offset_peer = types.InputPeerEmpty()
                except Exception:
                    offset_peer = types.InputPeerEmpty()

                page += 1

            # Pause between search queries to respect Telegram limits
            await asyncio.sleep(self.settings.SEARCH_RATE_LIMIT_DELAY)

        self.last_run_timestamp = iteration_start
        logger.info(
            f"[GlobalSearch] Search iteration completed. Total new messages processed: {total_processed}"
        )
        return total_processed

    async def start_polling(
        self,
        on_message_callback: Callable[[dict[str, Any]], Awaitable[None]],
    ) -> None:
        """Starts the periodic background polling loop.

        Args:
            on_message_callback: Callback invoked when new messages are found.
        """
        self._is_running = True
        interval_seconds = max(1, self.settings.SEARCH_INTERVAL_MINUTES * 60)
        logger.info(
            f"[GlobalSearch] Starting continuous search polling loop "
            f"(interval: {self.settings.SEARCH_INTERVAL_MINUTES} min / {interval_seconds}s)..."
        )

        # Initial scan of user dialogs
        await self.scan_dialogs()

        while self._is_running:
            try:
                await self.search_iteration(on_message_callback)
            except asyncio.CancelledError:
                logger.info("[GlobalSearch] Polling loop cancelled.")
                break
            except Exception as exc:
                logger.error(f"[GlobalSearch] Unexpected error during iteration: {exc}", exc_info=True)

            try:
                # Sleep interval with periodic checks for shutdown
                for _ in range(int(interval_seconds)):
                    if not self._is_running:
                        break
                    await asyncio.sleep(1)
            except asyncio.CancelledError:
                break

        logger.info("[GlobalSearch] Polling loop stopped.")

    def stop(self) -> None:
        """Stops the search poller loop."""
        self._is_running = False
