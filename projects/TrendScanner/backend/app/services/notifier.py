"""Telegram Push Notification Service for high-value trend alerts (Level 4)."""

import html
import logging
from typing import Optional
import httpx

from app.core.settings import settings

logger = logging.getLogger(__name__)


class TelegramNotifier:
    """Lightweight async Telegram Bot push notification client using httpx."""

    def __init__(
        self,
        bot_token: Optional[str] = None,
        chat_id: Optional[str] = None,
        timeout: float = 10.0,
    ) -> None:
        self.bot_token = bot_token if bot_token is not None else settings.TG_BOT_TOKEN
        self.chat_id = chat_id if chat_id is not None else settings.TG_CHAT_ID
        self.timeout = timeout

    def _get_api_url(self) -> str:
        token = self.bot_token or settings.TG_BOT_TOKEN
        return f"https://api.telegram.org/bot{token}/sendMessage"

    def format_alert_message(
        self,
        trend_name: str,
        ai_score: int,
        scam_probability: int,
        ai_summary: str,
        source_url: Optional[str] = None,
        mention_count: int = 1,
    ) -> str:
        """Construct structured HTML formatted alert message."""
        clean_title = html.escape(trend_name or "Новый тренд")
        clean_summary = html.escape(ai_summary or "Нет описания")
        url_text = source_url or "#"

        mention_badge = f"\n🔥 <b>Упоминаний на площадках:</b> {mention_count}" if mention_count > 1 else ""

        message = (
            "💎 <b>ОБНАРУЖЕН ПЕРСПЕКТИВНЫЙ ТРЕНД</b> 💎\n\n"
            f"📌 <b>Название:</b> {clean_title}\n"
            f"⭐ <b>AI Score:</b> {ai_score}/10\n"
            f"🛡 <b>Scam Risk:</b> {scam_probability}%"
            f"{mention_badge}\n\n"
            "📝 <b>Аналитическое резюме:</b>\n"
            f"{clean_summary}\n\n"
            f'🔗 <a href="{url_text}">Открыть первоисточник</a>'
        )
        return message

    async def send_trend_alert(
        self,
        trend_name: str,
        ai_score: int,
        scam_probability: int,
        ai_summary: str,
        source_url: Optional[str] = None,
        mention_count: int = 1,
    ) -> bool:
        """Send push notification to Telegram chat if credentials are configured."""
        token = self.bot_token or settings.TG_BOT_TOKEN
        chat = self.chat_id or settings.TG_CHAT_ID

        if not token or not chat:
            logger.debug("Telegram alert skipped: TG_BOT_TOKEN or TG_CHAT_ID is not configured.")
            return False

        message_html = self.format_alert_message(
            trend_name=trend_name,
            ai_score=ai_score,
            scam_probability=scam_probability,
            ai_summary=ai_summary,
            source_url=source_url,
            mention_count=mention_count,
        )

        payload = {
            "chat_id": chat,
            "text": message_html,
            "parse_mode": "HTML",
            "disable_web_page_preview": False,
        }

        try:
            async with httpx.AsyncClient(timeout=self.timeout) as client:
                response = await client.post(self._get_api_url(), json=payload)
                if response.status_code == 200:
                    logger.info("Telegram push alert sent successfully for trend '%s'", trend_name)
                    return True
                else:
                    logger.warning(
                        "Failed to send Telegram alert (HTTP %d): %s",
                        response.status_code,
                        response.text,
                    )
                    return False
        except Exception as exc:
            logger.error("Error sending Telegram alert for '%s': %s", trend_name, exc)
            return False


notifier = TelegramNotifier()
