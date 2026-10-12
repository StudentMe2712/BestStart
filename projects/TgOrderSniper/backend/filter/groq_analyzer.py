"""Groq LPU AI Lead Analyzer.

Performs ultra-fast classification and scoring of Telegram messages using
Groq LPU chat completion API with JSON mode and automatic fallback.
Extracts summary, budget, tech stack, and generates a ready-to-send pitch.
"""

import json
import logging
from typing import Any

import httpx
from pydantic import BaseModel, Field, field_validator

from backend.core.config import Settings, settings as global_settings

logger = logging.getLogger(__name__)


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
        client: httpx.AsyncClient | None = None,
        timeout: float = 15.0,
        settings: Settings | None = None,
    ) -> None:
        """Initializes GroqAnalyzer.

        Args:
            api_key: Groq API key. If omitted, loaded from settings.
            model: Primary Groq model. If omitted, loaded from settings.
            fallback_model: Fallback Groq model on primary failure. If omitted, loaded from settings.
            client: Optional httpx.AsyncClient instance.
            timeout: HTTP request timeout in seconds.
            settings: Optional Settings instance.
        """
        app_settings = settings or global_settings
        self.api_key = api_key if api_key is not None else app_settings.GROQ_API_KEY
        self.model = model or app_settings.GROQ_MODEL
        self.fallback_model = fallback_model or app_settings.GROQ_FALLBACK_MODEL
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

        if not text or len(text.strip()) < 10:
            return GroqAnalysisResult(is_lead=False)

        raw_data: dict[str, Any] | None = None

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
