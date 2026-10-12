"""TgOrderSniper Application Entry Point.

Combines the MTProto Listener, Message Deduplication Cache,
Filter Engine, and Telegram Bot Dispatcher into a real-time event pipeline
with graceful shutdown and structured logging.
"""

import asyncio
import logging
import signal
import sys
from typing import Any

from backend.bot.dispatcher import TelegramBotDispatcher
from backend.core.config import Settings, get_settings
from backend.core.dedup import MessageDedup
from backend.filter.engine import FilterEngine, FilteredOrder
from backend.listener.client import TelegramListener

logger = logging.getLogger("TgOrderSniper")


def setup_logging(level: int = logging.INFO) -> None:
    """Configures structured console logging."""
    formatter = logging.Formatter(
        fmt="%(asctime)s | %(levelname)-7s | %(name)s | %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    )
    handler = logging.StreamHandler(sys.stdout)
    handler.setFormatter(formatter)

    root_logger = logging.getLogger()
    root_logger.setLevel(level)
    # Remove previous handlers to avoid duplicate log entries
    root_logger.handlers.clear()
    root_logger.addHandler(handler)


class OrderProcessingPipeline:
    """Coordinates deduplication, filtering, and dispatching for incoming messages."""

    def __init__(
        self,
        dedup: MessageDedup,
        filter_engine: FilterEngine,
        dispatcher: TelegramBotDispatcher,
    ) -> None:
        """Initializes processing pipeline with core components."""
        self.dedup = dedup
        self.filter_engine = filter_engine
        self.dispatcher = dispatcher

    async def process_message(self, data: dict[str, Any]) -> FilteredOrder | None:
        """Processes an incoming Telegram message through the pipeline.

        1. Fast Deduplication: Checks LRU/TTL cache by coordinates and text hash.
        2. Rule Filtering: Evaluates negative gatekeeper and positive demand triggers.
        3. Dispatch: Dispatches valid orders to target chat via Bot API.

        Args:
            data: Structured message dictionary from TelegramListener.

        Returns:
            FilteredOrder if valid and passed filters, otherwise None.
        """
        chat_id = data.get("chat_id")
        message_id = data.get("message_id")
        text = data.get("text", "")
        chat_title = data.get("chat_title", "")

        if chat_id is None or message_id is None:
            logger.debug("[Pipeline] Dropping event without chat_id or message_id.")
            return None

        # 1. Fast Deduplication
        if self.dedup.is_duplicate(chat_id=chat_id, message_id=message_id, text=text):
            logger.debug(
                f"[Pipeline] Deduplicated already processed message: {chat_id}:{message_id}"
            )
            return None

        # 2. Rule Filtering & Metadata Extraction
        sender = data.get("sender") or {
            "id": data.get("author_id"),
            "username": data.get("author_username"),
            "name": data.get("author_name"),
        }

        order = self.filter_engine.evaluate(
            text=text,
            chat_title=chat_title,
            chat_id=chat_id,
            message_id=message_id,
            sender=sender,
            message_url=data.get("message_url", ""),
        )

        if order is None:
            return None

        # 3. Dispatch to Target Chat
        logger.info(
            f"[Pipeline] Valid order identified ({order.message_id} in '{order.chat_title}'). "
            f"Triggers: {order.matched_triggers} | Dispatching to target chat..."
        )
        dispatch_success = await self.dispatcher.dispatch(order)
        if dispatch_success:
            logger.info(f"[Pipeline] Successfully dispatched order {order.message_id}.")
        else:
            logger.error(
                f"[Pipeline] Failed to dispatch order {order.message_id} to target chat."
            )

        return order


async def run_service(settings: Settings | None = None) -> None:
    """Runs the TgOrderSniper service loop until shutdown requested.

    Args:
        settings: Optional Settings instance. If omitted, cached global settings are loaded.
    """
    app_settings = settings or get_settings()

    logger.info("==================================================")
    logger.info("           TgOrderSniper Engine Starting           ")
    logger.info("==================================================")
    logger.info(f"Target Chat ID: {app_settings.TARGET_CHAT_ID}")
    logger.info(f"Channels to Watch: {app_settings.CHANNELS_TO_WATCH or 'ALL Available'}")
    logger.info(f"Dedup LRU Cache Size: {app_settings.DEDUP_CACHE_SIZE}")

    # 1. Initialize Components
    dedup = MessageDedup(
        max_size=app_settings.DEDUP_CACHE_SIZE,
        ttl_seconds=app_settings.DEDUP_TTL_SECONDS,
    )
    filter_engine = FilterEngine()
    dispatcher = TelegramBotDispatcher(
        bot_token=app_settings.TG_BOT_TOKEN,
        target_chat_id=app_settings.TARGET_CHAT_ID,
    )
    listener = TelegramListener(settings=app_settings)

    # 2. Initialize Processing Pipeline
    pipeline = OrderProcessingPipeline(
        dedup=dedup,
        filter_engine=filter_engine,
        dispatcher=dispatcher,
    )

    # 3. Setup Graceful Shutdown Event
    stop_event = asyncio.Event()

    def handle_signal(sig_num: int, _frame: Any) -> None:
        logger.info(
            f"[Service] Received termination signal ({sig_num}). Initiating graceful shutdown..."
        )
        stop_event.set()

    # Register cross-platform signal handlers
    for sig in (signal.SIGINT, getattr(signal, "SIGTERM", None)):
        if sig is not None:
            try:
                signal.signal(sig, handle_signal)
            except (ValueError, OSError) as exc:
                logger.debug(f"[Service] Could not register signal handler for {sig}: {exc}")

    # 4. Start Telethon Listener
    try:
        await listener.start(message_handler_callback=pipeline.process_message)
        logger.info("✅ TgOrderSniper listener successfully activated and listening for orders.")

        # Run until shutdown signal or disconnection
        listener_task = asyncio.create_task(listener.run_until_disconnected())
        stop_task = asyncio.create_task(stop_event.wait())

        done, pending = await asyncio.wait(
            [listener_task, stop_task],
            return_when=asyncio.FIRST_COMPLETED,
        )

        for task in pending:
            task.cancel()

    except asyncio.CancelledError:
        logger.info("[Service] Main execution loop cancelled.")
    except Exception as exc:
        logger.error(f"[Service] Fatal error running TgOrderSniper: {exc}", exc_info=True)
    finally:
        logger.info("[Service] Shutting down TgOrderSniper components...")
        await listener.stop()
        await dispatcher.close()

        stats = dedup.get_stats()
        logger.info(f"[Service] Dedup Cache Stats at shutdown: {stats}")
        logger.info("==================================================")
        logger.info("           TgOrderSniper Engine Stopped            ")
        logger.info("==================================================")


def main() -> None:
    """CLI Entry Point."""
    setup_logging()
    try:
        asyncio.run(run_service())
    except KeyboardInterrupt:
        logger.info("[Service] Stopped by KeyboardInterrupt.")


if __name__ == "__main__":
    main()
