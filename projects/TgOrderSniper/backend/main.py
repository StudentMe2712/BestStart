"""TgOrderSniper Application Entry Point.

Combines the MTProto Listener, Message Deduplication Cache,
Filter Engine, and Telegram Bot Dispatcher into a real-time event pipeline
with graceful shutdown and structured logging.
"""

import asyncio
import hashlib
import logging
import re
import signal
import sys
from typing import Any

from backend.bot.dispatcher import TelegramBotDispatcher
from backend.core.config import Settings, get_settings
from backend.core.dedup import MessageDedup
from backend.db import Database
from backend.filter.engine import FilterEngine, FilteredOrder
from backend.filter.groq_analyzer import GroqAnalysisResult, GroqAnalyzer
from backend.listener.client import TelegramListener
from backend.listener.global_search import GlobalSearchEngine
from backend.scrapers import RawTask, WebScraperScheduler

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
    """Coordinates deduplication, filtering, AI scoring, persistent storage, and dispatching."""

    def __init__(
        self,
        dedup: MessageDedup,
        filter_engine: FilterEngine,
        dispatcher: TelegramBotDispatcher,
        groq_analyzer: GroqAnalyzer | None = None,
        db: Database | None = None,
    ) -> None:
        """Initializes processing pipeline with core components, optional AI analyzer, and DB."""
        self.dedup = dedup
        self.filter_engine = filter_engine
        self.dispatcher = dispatcher
        self.groq_analyzer = groq_analyzer
        self.db = db

    async def process_message(self, data: dict[str, Any]) -> FilteredOrder | None:
        """Processes an incoming Telegram message through the unified pipeline.

        1. Fast & Persistent Deduplication: Checks LRU/TTL cache and SQLite DB.
        2. Rule Pre-Filtering: Evaluates negative gatekeeper and positive demand triggers.
        3. Groq AI Scoring: Scores lead and generates pitch.
        4. Save to SQLite: Records lead state persistently.
        5. Dispatch: Dispatches valid order card to target chat via Bot API.

        Args:
            data: Structured message dictionary from TelegramListener or search.

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

        source = f"telegram:{chat_id}"
        external_id = str(message_id)
        content_hash = (
            MessageDedup.compute_text_hash(text)
            or hashlib.sha256(MessageDedup.normalize_text(text).encode("utf-8")).hexdigest()
        )

        # 1. Deduplication (LRU in-memory + SQLite persistent)
        if self.dedup.is_duplicate(chat_id=chat_id, message_id=message_id, text=text):
            logger.debug(
                f"[Pipeline] Deduplicated already processed message: {chat_id}:{message_id}"
            )
            return None

        if self.db and await self.db.is_already_processed(
            source=source, external_id=external_id, content_hash=content_hash
        ):
            logger.debug(
                f"[Pipeline] Persistent DB dedup dropped message: {source}:{external_id}"
            )
            return None

        # 2. Rule Filtering & AI Classification
        sender = data.get("sender") or {
            "id": data.get("author_id"),
            "username": data.get("author_username"),
            "name": data.get("author_name"),
        }

        order: FilteredOrder | None = None

        if self.groq_analyzer and self.groq_analyzer.is_configured:
            # Quick negative pre-filters
            if not text or len(text.strip()) < self.filter_engine.min_length:
                logger.debug(
                    f"[Pipeline] Dropped message {message_id}: length < {self.filter_engine.min_length}"
                )
                return None

            if self.filter_engine.check_negative_gatekeeper(text):
                logger.debug(
                    f"[Pipeline] Dropped message {message_id}: negative gatekeeper matched"
                )
                return None

            # Primary commercial/tech pre-filter
            matched_triggers = self.filter_engine.match_positive_triggers(text)
            matched_categories = self.filter_engine.match_categories(text)
            if not matched_triggers and not matched_categories:
                logger.debug(
                    f"[Pipeline] Skipped Groq for message {message_id} in '{chat_title}': "
                    "no commercial triggers or tech categories found"
                )
                return None

            # AI classification and pitch generation
            ai_analysis = await self.groq_analyzer.analyze(text)
            if not ai_analysis.is_lead:
                logger.info(
                    f"[Pipeline] AI rejected message {message_id} in '{chat_title}': classified as not a lead"
                )
                if self.db:
                    await self.db.save_lead(
                        source=source,
                        external_id=external_id,
                        content_hash=content_hash,
                        raw_text=text,
                        url=data.get("message_url", ""),
                        is_lead=False,
                        title=chat_title,
                        summary=ai_analysis.summary,
                        budget=ai_analysis.budget,
                        tech_stack=ai_analysis.tech_stack,
                    )
                return None

            # Build FilteredOrder enriched with AI analysis
            order = self.filter_engine.evaluate(
                text=text,
                chat_title=chat_title,
                chat_id=chat_id,
                message_id=message_id,
                sender=sender,
                message_url=data.get("message_url", ""),
            )
            if order is None:
                order = FilteredOrder(
                    chat_id=chat_id,
                    chat_title=chat_title,
                    message_id=message_id,
                    raw_text=text,
                    cleaned_text=self.filter_engine.clean_text(text),
                    author_id=data.get("author_id"),
                    author_username=data.get("author_username"),
                    author_name=data.get("author_name"),
                    message_url=data.get("message_url", ""),
                    matched_triggers=["ai_verified"],
                    detected_budget=ai_analysis.budget or self.filter_engine.extract_budget(text),
                    category_tags=self.filter_engine.match_categories(text),
                    ai_analysis=ai_analysis,
                )
            else:
                order.ai_analysis = ai_analysis
        else:
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

        # 4. Save to Persistent SQLite DB
        if self.db:
            await self.db.save_lead(
                source=source,
                external_id=external_id,
                content_hash=content_hash,
                raw_text=text,
                url=data.get("message_url", ""),
                is_lead=True,
                title=order.chat_title,
                summary=order.ai_analysis.summary if order.ai_analysis else None,
                budget=(
                    order.ai_analysis.budget
                    if order.ai_analysis and order.ai_analysis.budget
                    else order.detected_budget
                ),
                tech_stack=order.ai_analysis.tech_stack if order.ai_analysis else None,
            )

        # 5. Dispatch to Target Chat
        logger.info(
            f"[Pipeline] Valid order identified ({order.message_id} in '{order.chat_title}'). "
            f"Triggers: {order.matched_triggers} | Dispatching to target chat..."
        )
        dispatch_success = await self.dispatcher.dispatch(order, ai_analysis=order.ai_analysis)
        if dispatch_success:
            logger.info(f"[Pipeline] Successfully dispatched order {order.message_id}.")
        else:
            logger.error(
                f"[Pipeline] Failed to dispatch order {order.message_id} to target chat."
            )

        return order

    async def process_task(self, task: RawTask) -> FilteredOrder | None:
        """Processes a scraped web task through the unified pipeline.

        1. Fast & Persistent Deduplication: Checks LRU/TTL cache and SQLite DB.
        2. Rule Pre-Filtering: Evaluates negative gatekeeper and positive demand triggers.
        3. Groq AI Scoring: Scores task and generates personalized pitch.
        4. Save to SQLite: Records lead state persistently.
        5. Dispatch: Dispatches valid order card to target chat via Bot API.

        Args:
            task: RawTask instance from web scrapers.

        Returns:
            FilteredOrder if valid and passed filters, otherwise None.
        """
        source = task.source
        external_id = str(task.external_id)
        combined_text = (
            f"{task.title}\n{task.text}"
            if task.title and task.title != task.text
            else (task.text or task.title or "")
        )

        if not combined_text.strip():
            logger.debug("[Pipeline] Dropping empty web task from %s:%s", source, external_id)
            return None

        content_hash = (
            MessageDedup.compute_text_hash(combined_text)
            or hashlib.sha256(MessageDedup.normalize_text(combined_text).encode("utf-8")).hexdigest()
        )

        # Convert alphanumeric external_id to integer for in-memory cache
        digits_only = re.sub(r"\D", "", external_id)
        int_id = int(digits_only) if digits_only else abs(hash(external_id)) % (2**31)

        # 1. Deduplication (LRU in-memory + SQLite persistent)
        if self.dedup.is_duplicate(chat_id=source, message_id=int_id, text=combined_text):
            logger.debug(
                "[Pipeline] In-memory dedup dropped web task %s:%s", source, external_id
            )
            return None

        if self.db and await self.db.is_already_processed(
            source=source, external_id=external_id, content_hash=content_hash
        ):
            logger.debug(
                "[Pipeline] Persistent DB dedup dropped web task %s:%s", source, external_id
            )
            return None

        # 2. Rule Pre-Filtering
        if len(combined_text.strip()) < self.filter_engine.min_length:
            logger.debug(
                "[Pipeline] Dropped web task %s: length < %d", external_id, self.filter_engine.min_length
            )
            return None

        if self.filter_engine.check_negative_gatekeeper(combined_text):
            logger.debug(
                "[Pipeline] Dropped web task %s: negative gatekeeper matched", external_id
            )
            return None

        matched_triggers = self.filter_engine.match_positive_triggers(combined_text)
        matched_categories = self.filter_engine.match_categories(combined_text)
        if not matched_triggers and not matched_categories:
            logger.debug(
                "[Pipeline] Dropped web task %s: no commercial triggers or tech categories found", external_id
            )
            return None

        # 3. Groq AI Scoring
        ai_analysis: GroqAnalysisResult | None = None
        if self.groq_analyzer and self.groq_analyzer.is_configured:
            ai_analysis = await self.groq_analyzer.analyze(combined_text)
            if not ai_analysis.is_lead:
                logger.info(
                    "[Pipeline] AI rejected web task %s (%s): classified as not a lead",
                    external_id,
                    source,
                )
                if self.db:
                    await self.db.save_lead(
                        source=source,
                        external_id=external_id,
                        content_hash=content_hash,
                        raw_text=combined_text,
                        url=task.url,
                        is_lead=False,
                        title=task.title,
                        summary=ai_analysis.summary,
                        budget=ai_analysis.budget,
                        tech_stack=ai_analysis.tech_stack,
                    )
                return None

        # Determine effective budget and title
        detected_budget = (
            (ai_analysis.budget if ai_analysis and ai_analysis.budget else None)
            or task.budget
            or self.filter_engine.extract_budget(combined_text)
        )

        source_clean = source.replace("web:", "").replace("_", " ").capitalize()
        card_title = f"[{source_clean}] {task.title}"

        order = FilteredOrder(
            chat_id=source,
            chat_title=card_title,
            message_id=int_id,
            raw_text=combined_text,
            cleaned_text=self.filter_engine.clean_text(combined_text),
            author_id=None,
            author_username=None,
            author_name=task.author or "Заказчик",
            message_url=task.url,
            matched_triggers=matched_triggers if matched_triggers else ["web_order"],
            detected_budget=detected_budget,
            category_tags=matched_categories,
            ai_analysis=ai_analysis,
        )

        # 4. Save to Persistent SQLite DB
        if self.db:
            await self.db.save_lead(
                source=source,
                external_id=external_id,
                content_hash=content_hash,
                raw_text=combined_text,
                url=task.url,
                is_lead=True,
                title=task.title,
                summary=ai_analysis.summary if ai_analysis else None,
                budget=detected_budget,
                tech_stack=ai_analysis.tech_stack if ai_analysis else None,
            )

        # 5. Dispatch to Target Chat
        logger.info(
            "[Pipeline] Valid web order identified (%s in '%s'). Dispatching to target chat...",
            external_id,
            source,
        )
        dispatch_success = await self.dispatcher.dispatch(order, ai_analysis=ai_analysis)
        if dispatch_success:
            logger.info("[Pipeline] Successfully dispatched web order %s.", external_id)
        else:
            logger.error("[Pipeline] Failed to dispatch web order %s to target chat.", external_id)

        return order


async def run_service(settings: Settings | None = None) -> None:
    """Runs the TgOrderSniper service loop until shutdown requested.

    Args:
        settings: Optional Settings instance. If omitted, cached global settings are loaded.
    """
    app_settings = settings or get_settings()

    logger.info("==================================================")
    logger.info("         OmniLeadSniper Engine Starting           ")
    logger.info("==================================================")
    logger.info(f"Target Chat ID: {app_settings.TARGET_CHAT_ID}")
    logger.info(f"Channels to Watch: {app_settings.CHANNELS_TO_WATCH or 'ALL Available'}")
    logger.info(f"Dedup LRU Cache Size: {app_settings.DEDUP_CACHE_SIZE}")
    logger.info(f"SQLite DB Path: {app_settings.DB_PATH}")
    logger.info(
        f"Web Scrapers Enabled: {app_settings.ENABLE_WEB_SCRAPERS} (Interval: {app_settings.WEB_SCRAPE_INTERVAL_MINUTES} min)"
    )
    logger.info(
        f"Groq Model: {app_settings.GROQ_MODEL} (Fallback: {app_settings.GROQ_FALLBACK_MODEL})"
    )
    queries_count = (
        len(app_settings.SEARCH_QUERIES)
        if isinstance(app_settings.SEARCH_QUERIES, list)
        else 1
    )
    logger.info(
        f"Search Polling Interval: {app_settings.SEARCH_INTERVAL_MINUTES} min | Queries: {queries_count}"
    )

    # 1. Initialize Components
    db = Database(db_path=app_settings.DB_PATH)
    await db.init_db()

    dedup = MessageDedup(
        max_size=app_settings.DEDUP_CACHE_SIZE,
        ttl_seconds=app_settings.DEDUP_TTL_SECONDS,
    )
    filter_engine = FilterEngine()
    dispatcher = TelegramBotDispatcher(
        bot_token=app_settings.TG_BOT_TOKEN,
        target_chat_id=app_settings.TARGET_CHAT_ID,
    )
    groq_analyzer = GroqAnalyzer(settings=app_settings)
    listener = TelegramListener(settings=app_settings)

    # 2. Initialize Unified Processing Pipeline
    pipeline = OrderProcessingPipeline(
        dedup=dedup,
        filter_engine=filter_engine,
        dispatcher=dispatcher,
        groq_analyzer=groq_analyzer,
        db=db,
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

    search_engine: GlobalSearchEngine | None = None
    search_task: asyncio.Task[None] | None = None
    scraper_scheduler: WebScraperScheduler | None = None

    # 4. Start Telethon Listener, Global Search & Web Scrapers
    try:
        await listener.start(message_handler_callback=pipeline.process_message)
        logger.info("✅ OmniLeadSniper listener successfully activated and listening for orders.")

        tasks = [
            asyncio.create_task(listener.run_until_disconnected()),
            asyncio.create_task(stop_event.wait()),
        ]

        # Launch GlobalSearchEngine if Telethon client is available
        if listener.client and listener.is_connected:
            search_engine = GlobalSearchEngine(
                client=listener.client,
                settings=app_settings,
            )
            search_task = asyncio.create_task(
                search_engine.start_polling(pipeline.process_message)
            )
            tasks.append(search_task)

        # Launch Web Scrapers Scheduler if enabled
        if app_settings.ENABLE_WEB_SCRAPERS:
            scraper_scheduler = WebScraperScheduler(
                on_task_callback=pipeline.process_task,
                interval_minutes=app_settings.WEB_SCRAPE_INTERVAL_MINUTES,
                settings=app_settings,
            )
            await scraper_scheduler.start()
            logger.info("✅ Web scrapers scheduler started.")

        done, pending = await asyncio.wait(
            tasks,
            return_when=asyncio.FIRST_COMPLETED,
        )

        for task in pending:
            task.cancel()

    except asyncio.CancelledError:
        logger.info("[Service] Main execution loop cancelled.")
    except Exception as exc:
        logger.error(f"[Service] Fatal error running OmniLeadSniper: {exc}", exc_info=True)
    finally:
        logger.info("[Service] Shutting down OmniLeadSniper components...")
        if scraper_scheduler:
            await scraper_scheduler.stop()
        if search_engine:
            search_engine.stop()
        if search_task and not search_task.done():
            search_task.cancel()
        await listener.stop()
        await groq_analyzer.close()
        await dispatcher.close()
        await db.close()

        stats = dedup.get_stats()
        logger.info(f"[Service] Dedup Cache Stats at shutdown: {stats}")
        logger.info("==================================================")
        logger.info("         OmniLeadSniper Engine Stopped            ")
        logger.info("==================================================")



def main(settings: Settings | None = None) -> None:
    """CLI Entry Point."""
    setup_logging()
    try:
        asyncio.run(run_service(settings=settings))
    except KeyboardInterrupt:
        logger.info("[Service] Stopped by KeyboardInterrupt.")


if __name__ == "__main__":
    main()
