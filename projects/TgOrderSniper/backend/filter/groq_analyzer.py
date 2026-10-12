"""Groq LPU AI Lead Analyzer.

Performs ultra-fast classification and scoring of Telegram messages using
Groq LPU chat completion API with JSON mode, keyword pre-filtering,
and automatic fallback.
Extracts summary, budget, tech stack, and generates a ready-to-send pitch.
"""

import asyncio
import json
import logging
import re
from typing import Any

import httpx
from pydantic import BaseModel, Field, field_validator

from backend.core.config import Settings, settings as global_settings

logger = logging.getLogger(__name__)

PREFILTER_TRIGGERS: tuple[str, ...] = (
    "заказ",
    "нужен",
    "нужна",
    "нужно",
    "нужны",
    "требуется",
    "требуются",
    "разработка",
    "разработать",
    "доработка",
    "доработать",
    "написать",
    "скрипт",
    "бот",
    "1с",
    "1c",
    "спарсить",
    "парсер",
    "ищем",
)

PREFILTER_STOP_WORDS: tuple[str, ...] = (
    "резюме",
    "ищу работу",
    "ищу вакансию",
    "портфолио",
    "курсы",
    "слив курсов",
)


def check_prefilter(text: str) -> tuple[bool, str]:
    """Evaluates whether text passes commercial lead pre-filtering.

    Must match at least one trigger keyword:
    `заказ`, `нужен`, `нужна`, `нужно`, `нужны`, `требуется`, `требуются`,
    `разработка`, `разработать`, `доработка`, `доработать`, `написать`,
    `скрипт`, `бот`, `1с`, `1c`, `спарсить`, `парсер`, `ищем`.

    Must NOT match stop words:
    `резюме`, `ищу работу`, `ищу вакансию`, `портфолио`, `курсы`, `слив курсов`.

    Args:
        text: Input message text to evaluate.

    Returns:
        tuple[bool, str]: (is_passed, reason)
    """
    if not text or not text.strip():
        return False, "Empty or blank text"

    lower = text.lower()

    # 1. Stop words gatekeeper
    for stop_word in PREFILTER_STOP_WORDS:
        if stop_word in lower:
            return False, f"Matched stop word: '{stop_word}'"

    # 2. Trigger keywords matching
    matched_trigger: str | None = None
    for trigger in PREFILTER_TRIGGERS:
        if trigger == "бот":
            # Word boundary check to prevent matching 'работа', 'суббота', etc.
            if re.search(r"\bбот(?:[а-яa-z]{0,4})\b", lower):
                matched_trigger = trigger
                break
        elif trigger in ("1с", "1c"):
            if re.search(r"\b1[сc]\b", lower):
                matched_trigger = trigger
                break
        elif trigger in lower:
            matched_trigger = trigger
            break

    if not matched_trigger:
        return False, "No commercial trigger keywords found"

    return True, f"Passed pre-filter (matched trigger: '{matched_trigger}')"


class GroqAnalysisResult(BaseModel):
    """Structured AI analysis result for a Telegram lead."""

    is_lead: bool = Field(
        default=False,
        description="Whether message is a genuine commercial order or client request",
    )
    summary: str = Field(
        default="",
        description="Concise 1-sentence summary of the task in Russian",
    )
    budget: str = Field(
        default="",
        description="Extracted or approximate budget string",
    )
    tech_stack: str = Field(
        default="",
        description="Required technologies and programming languages",
    )
    ready_pitch: str = Field(
        default="",
        description="Ready-to-send personalized pitch (2-3 sentences) from developer",
    )
    raw_response: dict[str, Any] | None = Field(
        default=None,
        description="Raw JSON payload received from Groq API",
    )

    @field_validator("budget", mode="before")
    @classmethod
    def normalize_budget(cls, value: Any) -> str:
        """Converts integer or numeric budgets to string."""
        if value is None:
            return ""
        if isinstance(value, (int, float)):
            return f"{value:g} руб" if value > 0 else ""
        return str(value).strip()

    @field_validator("tech_stack", mode="before")
    @classmethod
    def normalize_tech_stack(cls, value: Any) -> str:
        """Converts list of technologies into comma-separated string."""
        if value is None:
            return ""
        if isinstance(value, (list, tuple)):
            return ", ".join(str(item).strip() for item in value if item)
        return str(value).strip()

    @field_validator("summary", "ready_pitch", mode="before")
    @classmethod
    def normalize_text_fields(cls, value: Any) -> str:
        """Converts non-string values to stripped strings."""
        if value is None:
            return ""
        return str(value).strip()


class GroqAnalyzer:
    """Async AI analyzer using Groq LPU Chat Completion API."""

    GROQ_API_URL: str = "https://api.groq.com/openai/v1/chat/completions"
    DEFAULT_MODEL: str = "openai/gpt-oss-20b"
    DEFAULT_FALLBACK_MODEL: str = "openai/gpt-oss-20b"

    SYSTEM_PROMPT: str = (
        "Ты — опытный ИИ-классификатор коммерческих заказов и лидов в Telegram для Python-разработчика.\n"
        "Твоя задача — проанализировать входящий текст из Telegram-чата и определить, является ли он РЕАЛЬНЫМ КОММЕРЧЕСКИМ ЗАКАЗОМ/ПРОЕКТОМ от заказчика.\n\n"
        "ПРАВИЛА КЛАССИФИКАЦИИ:\n"
        "1. Если сообщение НЕ является заказом (резюме, поиск работы соискателем, реклама, спам, инфобизнес, курсы, вакансия в штат в офис на фуллтайм, сомнительные схемы) -> верни `\"is_lead\": false`.\n"
        "2. Если сообщение ЯВЛЯЕТСЯ реальным заказом/проектом/подработкой/ТЗ от клиента -> верни `\"is_lead\": true` и заполни поля:\n"
        "   - `summary`: краткая суть задачи в 1 предложение на русском языке.\n"
        "   - `budget`: примерный бюджет задачи (если указан в тексте, иначе пустая строка \"\").\n"
        "   - `tech_stack`: требуемый стек технологий через запятую (например: \"Python, Telegram Bot, PostgreSQL\").\n"
        "   - `ready_pitch`: готовый персонализированный профессиональный отклик от 1-го лица разработчика заказчику (2-3 предложения, уверенно, кратко и по делу, на русском языке).\n\n"
        "ФОРМАТ ОТВЕТА:\n"
        "Строго валидный JSON с ключами: `is_lead` (boolean), `summary` (string), `budget` (string), `tech_stack` (string), `ready_pitch` (string)."
    )

    def __init__(
        self,
        api_key: str | None = None,
        model: str | None = None,
        fallback_model: str | None = None,
        request_delay: float | None = None,
        client: httpx.AsyncClient | None = None,
        timeout: float = 15.0,
        settings: Settings | None = None,
    ) -> None:
        """Initializes GroqAnalyzer.

        Args:
            api_key: Groq API key. If omitted, loaded from settings.
            model: Primary Groq model. If omitted, loaded from settings.
            fallback_model: Fallback Groq model on primary failure. If omitted, loaded from settings.
            request_delay: Pause in seconds before Groq API call to avoid 429 rate limit.
            client: Optional httpx.AsyncClient instance.
            timeout: HTTP request timeout in seconds.
            settings: Optional Settings instance.
        """
        app_settings = settings or global_settings
        self.api_key = api_key if api_key is not None else app_settings.GROQ_API_KEY
        self.model = model or getattr(app_settings, "GROQ_MODEL", self.DEFAULT_MODEL)
        self.fallback_model = fallback_model or getattr(
            app_settings, "GROQ_FALLBACK_MODEL", self.DEFAULT_FALLBACK_MODEL
        )
        self.request_delay: float = (
            request_delay
            if request_delay is not None
            else getattr(app_settings, "GROQ_REQUEST_DELAY", 1.0)
        )
        self.timeout = timeout

        if client is not None:
            self.client = client
            self._own_client = False
        else:
            self.client = httpx.AsyncClient(timeout=timeout)
            self._own_client = True

    @property
    def is_configured(self) -> bool:
        """Checks if Groq API key is present."""
        return bool(self.api_key and self.api_key.strip())

    @staticmethod
    def check_prefilter(text: str) -> tuple[bool, str]:
        """Evaluates whether text passes commercial lead pre-filtering."""
        return check_prefilter(text)

    async def _call_groq_api(self, model: str, text: str) -> dict[str, Any]:
        """Performs HTTP POST request to Groq chat completions endpoint."""
        headers = {
            "Authorization": f"Bearer {self.api_key.strip()}",
            "Content-Type": "application/json",
        }
        payload = {
            "model": model,
            "messages": [
                {"role": "system", "content": self.SYSTEM_PROMPT},
                {"role": "user", "content": f"Текст сообщения:\n{text}"},
            ],
            "response_format": {"type": "json_object"},
            "temperature": 0.2,
            "max_tokens": 800,
        }

        response = await self.client.post(self.GROQ_API_URL, headers=headers, json=payload)
        if response.status_code == 429:
            retry_after_str = response.headers.get("Retry-After", "")
            try:
                retry_after = float(retry_after_str) if retry_after_str else 3.0
                if retry_after <= 0:
                    retry_after = 3.0
            except (ValueError, TypeError):
                retry_after = 3.0

            logger.warning(
                f"[GroqAnalyzer] Rate limit (429) hit for model '{model}'. "
                f"Retrying after {retry_after:.1f}s..."
            )
            await asyncio.sleep(retry_after)
            response = await self.client.post(self.GROQ_API_URL, headers=headers, json=payload)

        response.raise_for_status()
        data = response.json()
        return data

    async def analyze(self, text: str) -> GroqAnalysisResult:
        """Analyzes Telegram message text using Groq LLM with fallback support.

        Args:
            text: Raw Telegram message text.

        Returns:
            GroqAnalysisResult object with classification and pitch.
        """
        if not self.is_configured:
            logger.debug("[GroqAnalyzer] API key not configured, skipping AI analysis.")
            return GroqAnalysisResult(is_lead=False)

        # Pre-Filter Gatekeeper: drop non-leads immediately without making HTTP request
        passed, reason = self.check_prefilter(text)
        if not passed:
            logger.debug(f"[GroqAnalyzer] Pre-filter rejected message: {reason}")
            return GroqAnalysisResult(is_lead=False)

        if len(text.strip()) < 10:
            return GroqAnalysisResult(is_lead=False)

        raw_data: dict[str, Any] | None = None

        # Pause between Groq API calls to avoid rate limits
        if self.request_delay > 0:
            await asyncio.sleep(self.request_delay)

        # Attempt 1: Primary Model
        try:
            logger.debug(f"[GroqAnalyzer] Requesting analysis via primary model '{self.model}'...")
            raw_data = await self._call_groq_api(self.model, text)
        except Exception as exc:
            logger.warning(
                f"[GroqAnalyzer] Primary model '{self.model}' failed: {exc}. "
                f"Falling back to '{self.fallback_model}'..."
            )
            # Attempt 2: Fallback Model
            try:
                if self.request_delay > 0:
                    await asyncio.sleep(self.request_delay)
                raw_data = await self._call_groq_api(self.fallback_model, text)
                logger.info(
                    f"[GroqAnalyzer] Fallback model '{self.fallback_model}' succeeded."
                )
            except Exception as fb_exc:
                logger.error(
                    f"[GroqAnalyzer] Both primary and fallback models failed: {fb_exc}",
                    exc_info=True,
                )
                return GroqAnalysisResult(is_lead=False)

        # Parse response content
        try:
            choice = raw_data["choices"][0]
            content = choice["message"]["content"]
            parsed_json = json.loads(content)

            return GroqAnalysisResult(
                is_lead=bool(parsed_json.get("is_lead", False)),
                summary=str(parsed_json.get("summary", "") or ""),
                budget=str(parsed_json.get("budget", "") or ""),
                tech_stack=parsed_json.get("tech_stack", "") or "",
                ready_pitch=str(parsed_json.get("ready_pitch", "") or ""),
                raw_response=raw_data,
            )
        except Exception as parse_exc:
            logger.error(
                f"[GroqAnalyzer] Failed to parse model JSON response: {parse_exc}. Raw content: {raw_data}",
                exc_info=True,
            )
            return GroqAnalysisResult(is_lead=False, raw_response=raw_data)

    async def close(self) -> None:
        """Closes internal HTTP client if owned."""
        if self._own_client and self.client and not self.client.is_closed:
            await self.client.aclose()

    async def __aenter__(self) -> "GroqAnalyzer":
        return self

    async def __aexit__(
        self,
        exc_type: type[BaseException] | None,
        exc_val: BaseException | None,
        exc_tb: Any,
    ) -> None:
        await self.close()
