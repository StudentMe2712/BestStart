"""Telegram Bot API Dispatcher.

Formats incoming FilteredOrder instances into responsive HTML alert cards
with interactive inline actions and dispatches them via Telegram Bot API
with exponential retry and rate-limit backoff.
"""

import asyncio
import html
import logging
import re
from typing import Any, Union

import httpx

from backend.filter.engine import FilterEngine, FilteredOrder

logger = logging.getLogger(__name__)


class TelegramBotDispatcher:
    """Asynchronous Telegram Bot API Dispatcher for delivery of order cards.

    Uses direct asynchronous HTTP POST requests to `https://api.telegram.org/bot<token>/sendMessage`
    with HTML parse mode and interactive inline keyboards.
    """

    MAX_TEXT_LENGTH: int = 1500

    def __init__(
        self,
        bot_token: str,
        target_chat_id: Union[int, str],
        client: httpx.AsyncClient | None = None,
        max_retries: int = 3,
        timeout: float = 10.0,
    ) -> None:
        """Initializes dispatcher.

        Args:
            bot_token: Telegram bot authentication token from @BotFather.
            target_chat_id: Target channel/group ID or username.
            client: Optional pre-configured httpx.AsyncClient instance.
            max_retries: Maximum delivery retry attempts upon network/rate-limiting errors.
            timeout: HTTP request timeout in seconds.
        """
        self.bot_token = bot_token
        self.target_chat_id = target_chat_id
        self.max_retries = max_retries
        self.timeout = timeout
        self.base_url = f"https://api.telegram.org/bot{self.bot_token}"

        if client is not None:
            self.client = client
            self._own_client = False
        else:
            self.client = httpx.AsyncClient(timeout=timeout)
            self._own_client = True

        if not self.bot_token:
            logger.warning("[Dispatcher] bot_token is empty. Dispatch requests will fail.")
        if not self.target_chat_id:
            logger.warning("[Dispatcher] target_chat_id is empty or 0.")

    def format_card(self, order: FilteredOrder) -> tuple[str, dict[str, Any]]:
        """Formats FilteredOrder into an HTML card text and an inline keyboard.

        Card Structure:
        - Header:
          - Fire marker: `🔥 НОВЫЙ ЗАКАЗ`
          - Source chat title: `📍 <b>Источник:</b> {chat_title}`
          - Category tags: `🏷️ <b>Категории:</b> #парсер #бот ...`
        - Budget block (if detected): `💰 Бюджет: <b>...</b>`
        - Author block: `@username`, `tg://user?id=...`, or display name
        - Sanitized & truncated task text with highlighted triggers
        - Inline keyboard buttons:
          - `[🔍 Открыть пост]` (direct link to original post)
          - `[💬 Написать заказчику]` (deep-link to client private chat)

        Args:
            order: FilteredOrder instance to format.

        Returns:
            Tuple of (formatted_html_text, reply_markup_dict).
        """
        lines: list[str] = [
            "🔥 <b>НОВЫЙ ЗАКАЗ</b>",
            f"📍 <b>Источник:</b> {html.escape(order.chat_title)}",
        ]

        # Category tags
        if order.category_tags:
            formatted_tags = [
                f"#{t.strip().replace(' ', '_').replace('-', '_')}"
                for t in order.category_tags
                if t.strip()
            ]
            if formatted_tags:
                lines.append(f"🏷️ <b>Категории:</b> {' '.join(formatted_tags)}")

        # Budget block
        if order.detected_budget:
            lines.append(f"💰 Бюджет: <b>{html.escape(order.detected_budget)}</b>")

        # Author block
        author_display = "Не указан"
        if order.author_username:
            clean_username = order.author_username.lstrip("@")
            escaped_username = html.escape(clean_username, quote=True)
            author_display = f'<a href="https://t.me/{escaped_username}">@{escaped_username}</a>'
        elif order.author_id:
            author_title = html.escape(order.author_name or f"ID {order.author_id}")
            author_display = f'<a href="tg://user?id={order.author_id}">{author_title}</a>'
        elif order.author_name:
            author_display = html.escape(order.author_name)

        lines.append(f"👤 <b>Автор:</b> {author_display}")
        lines.append("")

        # Task text: truncate -> html escape -> highlight triggers
        raw_task_text = order.cleaned_text or order.raw_text or ""
        if len(raw_task_text) > self.MAX_TEXT_LENGTH:
            truncated_text = raw_task_text[: self.MAX_TEXT_LENGTH].rstrip() + "..."
        else:
            truncated_text = raw_task_text

        escaped_task_text = html.escape(truncated_text)

        # Highlight matched positive triggers
        highlighted_task_text = self._highlight_triggers(
            escaped_task_text, order.matched_triggers
        )

        lines.append("📝 <b>Задача:</b>")
        lines.append(highlighted_task_text)

        card_text = "\n".join(lines)

        # Build inline keyboard buttons
        buttons: list[dict[str, str]] = []

        # Button 1: [🔍 Открыть пост]
        if order.message_url and order.message_url.startswith(("http://", "https://", "tg://")):
            buttons.append({"text": "🔍 Открыть пост", "url": order.message_url})

        # Button 2: [💬 Написать заказчику]
        customer_url = None
        if order.author_username:
            clean_uname = order.author_username.lstrip("@")
            if clean_uname:
                customer_url = f"https://t.me/{clean_uname}"
        elif order.author_id:
            customer_url = f"tg://user?id={order.author_id}"

        if customer_url:
            buttons.append({"text": "💬 Написать заказчику", "url": customer_url})

        reply_markup: dict[str, Any] = {
            "inline_keyboard": [buttons] if buttons else []
        }

        return card_text, reply_markup

    @staticmethod
    def _highlight_triggers(escaped_text: str, triggers: list[str]) -> str:
        """Wraps matched positive triggers with <b> tags in escaped text."""
        if not triggers:
            return escaped_text

        patterns: list[str] = []
        for trigger in sorted(set(triggers), key=len, reverse=True):
            clean_trig = trigger.strip()
            if not clean_trig:
                continue
            try:
                pat = FilterEngine._build_inflected_trigger_pattern(clean_trig)
            except Exception:
                pat = rf"\b{re.escape(clean_trig)}\b"
            patterns.append(pat)

        if not patterns:
            return escaped_text

        try:
            combined_regex = re.compile(rf"(?i)({'|'.join(patterns)})")
            return combined_regex.sub(r"<b>\1</b>", escaped_text)
        except Exception as exc:
            logger.debug(f"[Dispatcher] Error highlighting triggers: {exc}")
            return escaped_text

    async def dispatch(self, order: FilteredOrder) -> bool:
        """Dispatches formatted order card to the target Telegram chat/group.

        Handles Rate Limiting (HTTP 429 RetryAfter), transient network errors,
        and timeouts with exponential backoff and retry.

        Args:
            order: FilteredOrder instance to dispatch.

        Returns:
            True if dispatched successfully, False otherwise.
        """
        if not self.bot_token or not self.target_chat_id:
            logger.error(
                "[Dispatcher] Dispatch aborted: bot_token or target_chat_id is not set."
            )
            return False

        card_text, reply_markup = self.format_card(order)
        url = f"{self.base_url}/sendMessage"

        payload: dict[str, Any] = {
            "chat_id": self.target_chat_id,
            "text": card_text,
            "parse_mode": "HTML",
            "disable_web_page_preview": True,
        }
        if reply_markup and reply_markup.get("inline_keyboard"):
            payload["reply_markup"] = reply_markup

        for attempt in range(1, self.max_retries + 1):
            try:
                logger.debug(
                    f"[Dispatcher] Sending order {order.message_id} to chat {self.target_chat_id} (attempt {attempt}/{self.max_retries})..."
                )
                response = await self.client.post(url, json=payload)

                # 1. Successful response
                if response.status_code == 200:
                    data = response.json()
                    if data.get("ok"):
                        logger.info(
                            f"[Dispatcher] Successfully dispatched order {order.message_id} "
                            f"(chat: {order.chat_title}) to target {self.target_chat_id}"
                        )
                        return True
                    logger.error(
                        f"[Dispatcher] Telegram Bot API returned ok=false: {data}"
                    )
                    return False

                # 2. Rate Limiting (429 Too Many Requests)
                if response.status_code == 429:
                    retry_after = 1.0
                    try:
                        resp_json = response.json()
                        retry_after = float(
                            resp_json.get("parameters", {}).get("retry_after", 1.0)
                        )
                    except Exception:
                        header_val = response.headers.get("Retry-After")
                        if header_val and header_val.isdigit():
                            retry_after = float(header_val)

                    logger.warning(
                        f"[Dispatcher] Telegram 429 Too Many Requests hit. "
                        f"Backing off for {retry_after}s before retry (attempt {attempt}/{self.max_retries})."
                    )
                    if attempt < self.max_retries:
                        await asyncio.sleep(retry_after)
                        continue
                    logger.error(
                        f"[Dispatcher] Max retries exceeded due to rate limiting for order {order.message_id}."
                    )
                    return False

                # 3. Permanent Client Errors (400, 401, 403, 404, etc.)
                if 400 <= response.status_code < 500:
                    logger.error(
                        f"[Dispatcher] Telegram API Client Error {response.status_code}: {response.text}"
                    )
                    return False

                # 4. Telegram Server Errors (500, 502, 503, 504)
                if response.status_code >= 500:
                    backoff = min(1.0 * (2 ** (attempt - 1)), 10.0)
                    logger.warning(
                        f"[Dispatcher] Telegram Server Error {response.status_code}. "
                        f"Retrying in {backoff}s (attempt {attempt}/{self.max_retries})..."
                    )
                    if attempt < self.max_retries:
                        await asyncio.sleep(backoff)
                        continue
                    logger.error(
                        f"[Dispatcher] Max retries exceeded on server errors for order {order.message_id}."
                    )
                    return False

            except (httpx.RequestError, httpx.TimeoutException) as exc:
                backoff = min(1.0 * (2 ** (attempt - 1)), 10.0)
                logger.warning(
                    f"[Dispatcher] Network exception ({type(exc).__name__}: {exc}) on attempt {attempt}/{self.max_retries}. "
                    f"Retrying in {backoff}s..."
                )
                if attempt < self.max_retries:
                    await asyncio.sleep(backoff)
                    continue
                logger.error(
                    f"[Dispatcher] Network retries exhausted for order {order.message_id}: {exc}"
                )
                return False

            except Exception as exc:
                logger.error(
                    f"[Dispatcher] Unexpected error dispatching order {order.message_id}: {exc}",
                    exc_info=True,
                )
                return False

        return False

    async def close(self) -> None:
        """Closes the underlying httpx.AsyncClient session if managed by this dispatcher."""
        if self._own_client and self.client and not self.client.is_closed:
            await self.client.aclose()
            logger.info("[Dispatcher] HTTP client closed.")

    async def __aenter__(self) -> "TelegramBotDispatcher":
        return self

    async def __aexit__(
        self,
        exc_type: type[BaseException] | None,
        exc_val: BaseException | None,
        exc_tb: Any,
    ) -> None:
        await self.close()
