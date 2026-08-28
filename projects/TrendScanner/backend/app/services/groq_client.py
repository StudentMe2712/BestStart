"""Groq LLM API Client with strict classification prompt, Russian language enforcement, deep-translator integration, and Deep Report generation."""

import asyncio
import json
import logging
import re
from typing import Any, Dict, List, Optional
import httpx
from deep_translator import GoogleTranslator
from langdetect import DetectorFactory, detect
from langdetect.lang_detect_exception import LangDetectException
from pydantic import BaseModel, Field, field_validator

from app.core.settings import settings

DetectorFactory.seed = 0
logger = logging.getLogger(__name__)

TRANSLATION_SYSTEM_PROMPT = """Ты — профессиональный технический и бизнес-переводчик.
Твоя единственная задача — точно и качественно перевести предложенный текст на русский язык, сохранив оригинальные IT-термины, названия продуктов, брендов и метрики (SaaS, ARR, MRR, AI, Stripe, React, API, B2B, B2C, CRM и т.д.).
Верни ТОЛЬКО качественный русский перевод без каких-либо вводных слов, пояснений, кавычек или markdown-блоков."""

SYSTEM_PROMPT = """Ты — Безжалостный бизнес-аналитик и ИИ-классификатор трендов и микрониш.
Твоя единственная цель — беспристрастно анализировать входящие посты, статьи и обсуждения (которые могут быть на английском или других языках), отсекать инфошум, скам и пустые обещания, и выявлять реальные бизнес-возможности, микро-SaaS идеи и проверенные способы заработка.

КРИТИЧЕСКИЕ ПРАВИЛА ЯЗЫКА (LANGUAGE ENFORCEMENT):
1. ПЕРЕВОД НА РУССКИЙ ОБЯЗАТЕЛЕН. ПОЛНОСТЬЮ И БЕЗ ИСКЛЮЧЕНИЙ!
2. ЕСЛИ ТЫ ВЕРНЕШЬ JSON С АНГЛИЙСКИМ ТЕКСТОМ В ПОЛЯХ `trend_name` ИЛИ `ai_summary`, СИСТЕМА УПАДЕТ. ПЕРЕВОД НА РУССКИЙ ОБЯЗАТЕЛЕН.
3. ЗНАЧЕНИЯ ПОЛЕЙ `trend_name` И `ai_summary` ДОЛЖНЫ БЫТЬ НАПИСАНЫ СТРОГО НА ГРАМОТНОМ РУССКОМ ЯЗЫКЕ! ДАЖЕ ЕСЛИ ВЕСЬ ИСХОДНЫЙ ТЕКСТ ПОЛНОСТЬЮ НА АНГЛИЙСКОМ!
4. Зарубежные термины, имена брендов и названия продуктов (например: Stripe, Supabase, React, B2B, SaaS) разрешено оставлять в оригинале, весь остальной текст (названия ниш, описания, глаголы, выводы) ОБЯЗАТЕЛЬНО переводи и обобщай по-русски.

Критерии оценки:
1. is_trend (bool): true ТОЛЬКО если текст содержит реальный бизнес-кейс, растущий рыночный спрос, микро-SaaS идею, софтверный инструмент, конкретную бизнес-модель или рабочую связку. Если это просто рассуждение, жалоба, банальный совет или спам — false.
2. trend_name (str): Лаконичное название ниши/тренда (2-5 слов СТРОГО НА РУССКОМ ЯЗЫКЕ, например: "Микро-SaaS для клиник", "B2B автоматизация инвойсов"). Если is_trend = false, укажи null.
3. ai_score (int): Оценка жизнеспособности и коммерческого потенциала от 1 до 10:
   - 1-3: Мусор, шум, нерабочая идея, токсичный флуд.
   - 4-6: Посредственная или перегретая идея без четкого УТП.
   - 7-8: Хороший тренд с понятной аудиторией и возможностью быстрой проверки гипотезы.
   - 9-10: Выдающаяся микрониша с высоким чеком/MRR и низким барьером входа.
4. scam_probability (int): Оценка вероятности скама, пирамиды, накрутки или скрытой рекламы от 0 до 100%.
5. ai_summary (str): Сухая, предельно конкретная выжимка СТРОГО НА РУССКОМ ЯЗЫКЕ (1-3 предложения): в чем суть продукта/услуги, кто платит деньги, и какие ключевые риски.

ТРЕБОВАНИЕ К ФОРМАТУ:
Отвечай ИСКЛЮЧИТЕЛЬНО валидным JSON-объектом без каких-либо вводных слов, пояснений и markdown-оберток.

Пример ответа:
{
  "is_trend": true,
  "trend_name": "Парсинг B2B лидов для рекрутеров",
  "ai_score": 8,
  "scam_probability": 10,
  "ai_summary": "Сервис автоматизированного сбора открытых вакансий и контактов HR. Высокий спрос у агентств, низкая стоимость привлечения."
}
"""

DEEP_REPORT_SYSTEM_PROMPT = """Ты — ведущий венчурный аналитик и продуктовый стратег в сфере технологического бизнеса и Micro-SaaS.
Твоя задача — сгенерировать детальный, исчерпывающий, профессионально и красиво структурированный аналитический отчет по предложенному бизнес-тренду СТРОГО НА РУССКОМ ЯЗЫКЕ.

КРИТИЧЕСКОЕ ПРАВИЛО:
ОТЧЕТ ДОЛЖЕН БЫТЬ ПОЛНОСТЬЮ НА РУССКОМ ЯЗЫКЕ. ПЕРЕВОД НА РУССКИЙ ОБЯЗАТЕЛЕН.
Если исходный текст на английском, сделай глубокий аналитический синтез и перевод на русский язык.

Используй чистый Markdown с аккуратным форматированием, списками и акцентами.

Обязательная структура отчета:
### 🎯 1. Суть и ценность продукта
- В чем фундаментальная идея и какую главную боль клиентов решает продукт?
- Почему этот тренд актуален и растет прямо сейчас?

### 👥 2. Целевая аудитория и сегменты
- Кто конкретно является покупателем (B2B сегменты, соло-фаундеры, агентства, Prosumers)?
- Готовность платить (Willingness to Pay) и диапазон среднего чека / тарифных планов.

### ⚠️ 3. Риски и барьеры входа
- Уровень рыночной конкуренции и техническая сложность реализации.
- Потенциальные уязвимости и регуляторные риски.

### 🚀 4. План запуска MVP за 2 недели
- Пошаговый план быстрой проверки продуктовой гипотезы.
- Рекомендуемый технологический стек (no-code / low-code / FastAPI / React).

### 💰 5. Модель монетизации и юнит-экономика
- Основные потоки выручки (подписка, транзакционная комиссия, usage-based).
- Ожидаемая маржинальность и ориентировочные сроки окупаемости.
"""


class AIClassificationResult(BaseModel):
    """Structured result of LLM classification."""

    is_trend: bool = Field(..., description="Whether content is confirmed as a business trend/niche")
    trend_name: Optional[str] = Field(default=None, description="Short name of the trend in Russian")
    ai_score: int = Field(default=1, description="Viability score from 1 to 10")
    scam_probability: int = Field(default=0, description="Estimated scam risk %")
    ai_summary: str = Field(..., description="Concise analytical summary in Russian")
    raw_response: Optional[str] = Field(default=None, description="Raw LLM response string")

    @field_validator("ai_score", mode="before")
    @classmethod
    def clamp_score(cls, v: Any) -> int:
        try:
            val = int(v)
            return max(1, min(10, val))
        except (ValueError, TypeError):
            return 1

    @field_validator("scam_probability", mode="before")
    @classmethod
    def clamp_scam(cls, v: Any) -> int:
        try:
            val = int(v)
            return max(0, min(100, val))
        except (ValueError, TypeError):
            return 0


def extract_json_payload(raw_text: str) -> Optional[Any]:
    """
    Extract and decode JSON from arbitrary LLM output.
    Handles Markdown code blocks (```json ... ```), leading/trailing explanations,
    and unescaped substrings.
    """
    if not raw_text or not raw_text.strip():
        return None

    cleaned = raw_text.strip()

    # 1. Direct JSON parse attempt
    try:
        return json.loads(cleaned)
    except Exception:
        pass

    # 2. Extract from markdown code block ```json ... ``` or ``` ... ```
    code_block_match = re.search(r"```(?:json)?\s*([\s\S]*?)\s*```", cleaned, re.IGNORECASE)
    if code_block_match:
        try:
            return json.loads(code_block_match.group(1).strip())
        except Exception:
            cleaned = code_block_match.group(1).strip()

    # 3. Find outermost JSON object {...} or array [...]
    first_brace = cleaned.find("{")
    last_brace = cleaned.rfind("}")
    first_bracket = cleaned.find("[")
    last_bracket = cleaned.rfind("]")

    if first_brace != -1 and last_brace != -1 and (first_bracket == -1 or first_brace < first_bracket):
        json_candidate = cleaned[first_brace : last_brace + 1]
        try:
            return json.loads(json_candidate)
        except Exception:
            pass

    if first_bracket != -1 and last_bracket != -1:
        json_candidate = cleaned[first_bracket : last_bracket + 1]
        try:
            return json.loads(json_candidate)
        except Exception:
            pass

    try:
        fixed = re.sub(r"(?<!\\)'", '"', cleaned[first_brace : last_brace + 1] if first_brace != -1 else cleaned)
        return json.loads(fixed)
    except Exception:
        pass

    logger.warning("Failed to extract valid JSON from LLM response: %s", raw_text[:200])
    return None


def has_untranslated_english(trend_name: Optional[str], ai_summary: Optional[str]) -> bool:
    """
    Check if LLM output contains untranslated English text in trend_name or ai_summary.
    Detects:
    1. Complete absence of Cyrillic letters in summaries.
    2. English sentence starters (e.g. 'The', 'This', 'In', 'We', 'It', 'There', 'When', 'Why', 'How') with low Cyrillic.
    3. Prominent English functional/connecting words (e.g. 'the', 'and', 'is', 'are', 'for', 'with', 'this', 'that').
    4. Multi-word English trend names lacking Cyrillic.
    """
    if not ai_summary and not trend_name:
        return False

    sentence_starter_regex = re.compile(
        r"(?:^|[\.\?!]\s+)(The|This|These|Those|An|A|In|On|It|We|They|There|Here|When|Why|How|You|Our|Their|If|As|With|For|By)\s+",
        re.IGNORECASE,
    )
    english_stopwords_regex = re.compile(
        r"\b(the|and|is|are|was|were|this|that|with|for|from|have|has|had|will|would|can|could|should|been|about|which|their|there|they|what|when|where|who|why|how)\b",
        re.IGNORECASE,
    )

    # 1. Check ai_summary
    if ai_summary and len(ai_summary.strip()) > 8:
        summary_clean = ai_summary.strip()
        has_cyrillic = bool(re.search(r"[а-яА-ЯёЁ]", summary_clean))

        # If there are no Cyrillic letters at all in the summary, it is definitely untranslated English
        if not has_cyrillic:
            return True

        # If it starts with common English sentence starters or contains English sentence structures
        if sentence_starter_regex.search(summary_clean):
            latin_count = len(re.findall(r"[a-zA-Z]", summary_clean))
            cyrillic_count = len(re.findall(r"[а-яА-ЯёЁ]", summary_clean))
            if latin_count >= cyrillic_count or len(english_stopwords_regex.findall(summary_clean)) >= 2:
                return True

        # If 3 or more English grammatical stop words appear in summary
        if len(english_stopwords_regex.findall(summary_clean)) >= 3:
            return True

    # 2. Check trend_name (if present)
    if trend_name and len(trend_name.strip()) > 3:
        tn_clean = trend_name.strip()
        has_cyrillic_tn = bool(re.search(r"[а-яА-ЯёЁ]", tn_clean))

        # Check for English prepositions/connectors in trend_name (e.g. "Tools for Creators", "SaaS and AI")
        if re.search(r"\b(for|and|with|the|in|of|to|a|an|is|by)\b", tn_clean, re.IGNORECASE) and not has_cyrillic_tn:
            return True

        # If trend name has 3 or more words with 0 Cyrillic characters
        words = tn_clean.split()
        if len(words) >= 3 and not has_cyrillic_tn:
            return True

    return False


def detect_language(text: str) -> str:
    """
    Detect the primary language of the text.
    Returns 'ru' if Cyrillic letters are predominant or Russian/Slavic is detected.
    Returns 'en' or detected ISO language code otherwise.
    """
    if not text or not text.strip():
        return "ru"

    clean_text = text.strip()
    cyrillic_chars = len(re.findall(r"[а-яА-ЯёЁ]", clean_text))
    latin_chars = len(re.findall(r"[a-zA-Z]", clean_text))

    # Fast path: If text is predominantly Cyrillic, classify as Russian
    if cyrillic_chars > 20 and cyrillic_chars >= latin_chars:
        return "ru"

    try:
        lang = detect(clean_text)
        if lang in ("ru", "uk", "be", "bg", "mk", "sr") and cyrillic_chars > 5:
            return "ru"
        return lang
    except (LangDetectException, Exception):
        if cyrillic_chars > latin_chars:
            return "ru"
        return "en"


def _translate_chunks_sync(text: str) -> str:
    """Synchronously translate text to Russian using deep-translator GoogleTranslator.
    Handles chunking if text > 4000 characters: splits into paragraphs/chunks <= 4000 chars,
    translates each, and joins with newlines.
    """
    if not text or not text.strip():
        return text

    translator = GoogleTranslator(source="auto", target="ru")

    if len(text) <= 4000:
        res = translator.translate(text)
        return res.strip() if res else text

    paragraphs = text.split("\n")
    chunks: List[str] = []
    current_chunk: List[str] = []
    current_len = 0

    for para in paragraphs:
        if len(para) > 4000:
            if current_chunk:
                chunks.append("\n".join(current_chunk))
                current_chunk = []
                current_len = 0
            start = 0
            while start < len(para):
                end = min(start + 4000, len(para))
                chunks.append(para[start:end])
                start = end
        else:
            if current_len + len(para) + 1 > 4000:
                chunks.append("\n".join(current_chunk))
                current_chunk = [para]
                current_len = len(para)
            else:
                current_chunk.append(para)
                current_len += len(para) + 1

    if current_chunk:
        chunks.append("\n".join(current_chunk))

    translated_parts = []
    for chunk in chunks:
        if chunk.strip():
            part = translator.translate(chunk)
            translated_parts.append(part.strip() if part else chunk)
        else:
            translated_parts.append(chunk)

    return "\n".join(translated_parts).strip()


async def translate_to_russian(text: str, fallback_client: Optional["GroqClient"] = None) -> str:
    """
    Translate text to Russian using deep-translator (GoogleTranslator).
    - If text is empty or predominantly Russian, returns as-is.
    - Runs in background thread via asyncio.to_thread to avoid blocking asyncio event loop.
    - Handles chunking for texts > 4000 chars.
    - If GoogleTranslator fails, logs warning and falls back to Groq or original text.
    """
    if not text or not text.strip():
        return text

    if detect_language(text) == "ru":
        return text

    # 1. Primary: GoogleTranslator via deep-translator
    try:
        translated = await asyncio.to_thread(_translate_chunks_sync, text)
        if translated and translated.strip():
            return translated.strip()
    except Exception as exc:
        logger.warning("GoogleTranslator failed: %s. Attempting fallback translation...", exc)

    # 2. Fallback: Groq LLM (if client available)
    client_to_use = fallback_client or groq_client
    if client_to_use and client_to_use.api_key:
        try:
            return await client_to_use._translate_groq_fallback(text)
        except Exception as groq_exc:
            logger.warning("Groq translation fallback failed: %s. Returning original text.", groq_exc)

    return text


def get_rlhf_context_prompt() -> str:
    """
    Build dynamic RLHF context prompt from user feedback (liked trends and garbage/noise items).
    Returns formatted context string for Groq classification prompt calibration, or "" if no examples exist.
    """
    try:
        from app.db.dao import TrendsDAO

        examples = TrendsDAO.get_rlhf_examples(limit_positive=2, limit_negative=2)
    except Exception as exc:
        logger.warning("Failed to retrieve RLHF examples for prompt injection: %s", exc)
        return ""

    if not examples:
        return ""

    positive_examples: List[Dict[str, Any]] = []
    negative_examples: List[Dict[str, Any]] = []

    if isinstance(examples, dict):
        positive_examples = examples.get("positive") or []
        negative_examples = examples.get("negative") or []
    elif isinstance(examples, (list, tuple)) and len(examples) == 2:
        positive_examples = examples[0] or []
        negative_examples = examples[1] or []

    if not positive_examples and not negative_examples:
        return ""

    blocks = ["ТЕБЕ ДОСТУПЕН ОПЫТ ПОЛЬЗОВАТЕЛЯ (RLHF FEEDBACK):"]

    if positive_examples:
        pos_lines = ["Вот примеры хороших трендов (+1, высокая ценность и жизнеспособность):"]
        for item in positive_examples:
            name = item.get("trend_name") or "Перспективный тренд"
            summary = item.get("ai_summary")
            if not summary and item.get("original_text"):
                orig = item["original_text"].strip()
                summary = orig[:140] + ("..." if len(orig) > 140 else "")
            if not summary:
                summary = "Высокий коммерческий потенциал и понятная бизнес-модель."
            score = item.get("ai_score") if item.get("ai_score") is not None else 8
            pos_lines.append(f'- "{name}": {summary} (Оценка: {score}/10)')
        blocks.append("\n".join(pos_lines))

    if negative_examples:
        neg_lines = ["Вот примеры мусора / нерелевантного контента (-1, штраф и инфошум):"]
        for item in negative_examples:
            name = item.get("trend_name") or "Инфошум / Нерелевантно"
            summary = item.get("ai_summary")
            if not summary and item.get("original_text"):
                orig = item["original_text"].strip()
                summary = orig[:140] + ("..." if len(orig) > 140 else "")
            if not summary:
                summary = "Низкая ценность, флуд или отсутствие бизнес-модели."
            score = item.get("ai_score") if item.get("ai_score") is not None else 1
            neg_lines.append(f'- "{name}": {summary} (Оценка: {score}/10)')
        blocks.append("\n".join(neg_lines))

    calibration_instructions = (
        "Инструкция по калибровке:\n"
        "1. Проанализируй новый текст.\n"
        "2. Если он похож на мусорные примеры (-1), содержит спам, пустые рассуждения или нерабочие схемы — ЖЕСТКО СНИЖАЙ ai_score (1-4) и повышай scam_probability.\n"
        "3. Если он похож на хорошие примеры (+1), содержит реальную микро-SaaS идею, проверенный кейс или растущий рыночный спрос — ПОВЫШАЙ ai_score (7-10).\n"
        "4. Верни результат СТРОГО на русском языке в формате JSON."
    )
    blocks.append(calibration_instructions)

    return "\n\n".join(blocks)


class GroqClient:
    """HTTP Client for Groq Cloud API with rate-limiting backoff, Russian language enforcement, and deep report generator."""

    GROQ_API_URL = "https://api.groq.com/openai/v1/chat/completions"

    def __init__(
        self,
        api_key: Optional[str] = None,
        model: Optional[str] = None,
        timeout: float = 35.0,
        max_retries: Optional[int] = None,
        retry_delay: Optional[float] = None,
    ) -> None:
        self.api_key = api_key if api_key is not None else settings.GROQ_API_KEY
        self.model = model or settings.GROQ_MODEL
        self.timeout = timeout
        self.max_retries = max_retries or settings.GROQ_MAX_RETRIES
        self.retry_delay = retry_delay or settings.GROQ_RETRY_DELAY_SECONDS

    async def _call_api_with_retry(
        self,
        messages: List[Dict[str, str]],
        temperature: float = 0.1,
        json_mode: bool = True,
        model_override: Optional[str] = None,
    ) -> str:
        """Call Groq API with exponential backoff on HTTP 429 and server errors."""
        if not self.api_key:
            raise ValueError("GROQ_API_KEY is not configured.")

        headers = {
            "Authorization": f"Bearer {self.api_key}",
            "Content-Type": "application/json",
        }
        payload: Dict[str, Any] = {
            "model": model_override or self.model,
            "messages": messages,
            "temperature": temperature,
        }
        if json_mode:
            payload["response_format"] = {"type": "json_object"}

        last_exception: Optional[Exception] = None

        for attempt in range(self.max_retries):
            try:
                async with httpx.AsyncClient(timeout=self.timeout) as client:
                    response = await client.post(
                        self.GROQ_API_URL,
                        headers=headers,
                        json=payload,
                    )

                    # Handle Rate Limit (429)
                    if response.status_code == 429:
                        retry_after = response.headers.get("Retry-After")
                        wait_seconds = float(retry_after) if retry_after else self.retry_delay * (2**attempt)
                        logger.warning(
                            "Groq Rate Limit (429) encountered. Retrying in %.2f seconds (attempt %d/%d)...",
                            wait_seconds,
                            attempt + 1,
                            self.max_retries,
                        )
                        await asyncio.sleep(wait_seconds)
                        continue

                    # Handle 5xx server errors
                    if response.status_code >= 500:
                        wait_seconds = self.retry_delay * (2**attempt)
                        logger.warning(
                            "Groq Server Error (%d). Retrying in %.2f seconds...",
                            response.status_code,
                            wait_seconds,
                        )
                        await asyncio.sleep(wait_seconds)
                        continue

                    response.raise_for_status()
                    data = response.json()
                    choices = data.get("choices", [])
                    if choices:
                        return choices[0]["message"]["content"]
                    raise ValueError("Groq API returned empty choices list.")

            except (httpx.RequestError, httpx.TimeoutException) as net_err:
                last_exception = net_err
                wait_seconds = self.retry_delay * (2**attempt)
                logger.warning(
                    "Network error during Groq API call: %s. Retrying in %.2f seconds...",
                    net_err,
                    wait_seconds,
                )
                await asyncio.sleep(wait_seconds)
            except Exception as err:
                last_exception = err
                logger.error("Non-retryable error during Groq API call: %s", err)
                raise

        raise RuntimeError(f"Groq API call failed after {self.max_retries} attempts: {last_exception}")

    async def _translate_groq_fallback(self, text: str) -> str:
        """Fallback translation using Groq LLM API."""
        if not self.api_key or not text or not text.strip():
            return text

        messages = [
            {"role": "system", "content": TRANSLATION_SYSTEM_PROMPT},
            {
                "role": "user",
                "content": f"Переведи следующий текст на русский язык, сохранив IT-термины и бренды:\n\n{text}",
            },
        ]
        try:
            translated = await self._call_api_with_retry(
                messages,
                temperature=0.1,
                json_mode=False,
                model_override=getattr(settings, "GROQ_MODEL_TRANSLATE", "llama-3.1-8b-instant"),
            )
            return translated.strip() if translated else text
        except Exception as exc:
            logger.warning("Groq translation fallback failed: %s", exc)
            return text

    async def translate_to_russian(self, text: str) -> str:
        """
        Translate text to Russian using deep-translator GoogleTranslator.
        - If text is empty or predominantly Russian, returns as-is.
        - Uses chunking for text > 4000 chars.
        - Runs in thread via asyncio.to_thread.
        - Falls back to Groq or original text on failure.
        """
        return await translate_to_russian(text, fallback_client=self)

    async def classify_text(self, text: str) -> Optional[AIClassificationResult]:
        """
        Classify a single text item using Groq API with Dynamic RLHF context calibration.
        Ensures 100% Russian output via automatic translation if untranslated English is detected.
        """
        if not text or not text.strip():
            return None

        # Pre-translate text to Russian via GoogleTranslator
        text_for_analysis = await self.translate_to_russian(text)

        # Dynamic RLHF context injection
        rlhf_context = get_rlhf_context_prompt()

        if rlhf_context:
            user_content = (
                f"{rlhf_context}\n\n"
                f"Проанализируй следующий текст и верни результат СТРОГО на русском языке в формате JSON:\n\n"
                f"{text_for_analysis}"
            )
        else:
            user_content = (
                f"Проанализируй следующий текст и верни результат СТРОГО на русском языке в формате JSON:\n\n"
                f"{text_for_analysis}"
            )

        initial_messages = [
            {"role": "system", "content": SYSTEM_PROMPT},
            {
                "role": "user",
                "content": user_content,
            },
        ]

        try:
            raw_response = await self._call_api_with_retry(initial_messages, json_mode=True)
            parsed_json = extract_json_payload(raw_response)

            if not isinstance(parsed_json, dict):
                logger.warning("Groq response could not be parsed into dict: %s", raw_response[:200])
                return None

            is_trend = bool(parsed_json.get("is_trend", False))
            trend_name = parsed_json.get("trend_name") if is_trend else None
            ai_score = int(parsed_json.get("ai_score", 1))
            scam_probability = int(parsed_json.get("scam_probability", 0))
            ai_summary = str(parsed_json.get("ai_summary", "Анализ не дал содержательного описания."))

            # Validation: Check for untranslated English text markers
            if has_untranslated_english(trend_name, ai_summary):
                logger.warning(
                    "Groq returned English text in trend_name ('%s') or ai_summary ('%s'). Translating directly to Russian...",
                    trend_name,
                    ai_summary[:60] if ai_summary else "",
                )
                if trend_name:
                    trend_name = await self.translate_to_russian(trend_name)
                if ai_summary:
                    ai_summary = await self.translate_to_russian(ai_summary)

            return AIClassificationResult(
                is_trend=is_trend,
                trend_name=trend_name,
                ai_score=ai_score,
                scam_probability=scam_probability,
                ai_summary=ai_summary,
                raw_response=raw_response,
            )
        except Exception as exc:
            logger.error("Error during text classification via Groq: %s", exc)
            return None

    async def generate_deep_report(self, text: str, trend_name: str = "") -> Optional[str]:
        """Generate an extensive, beautifully structured analytical report on a trend in Russian."""
        if not text or not text.strip():
            return None

        prompt_title = f" (Тема: {trend_name})" if trend_name else ""
        messages = [
            {"role": "system", "content": DEEP_REPORT_SYSTEM_PROMPT},
            {
                "role": "user",
                "content": f"Подготовь глубокий аналитический отчет по следующему бизнес-тренду{prompt_title}:\n\n{text}",
            },
        ]

        try:
            report_markdown = await self._call_api_with_retry(messages, temperature=0.3, json_mode=False)
            return report_markdown.strip()
        except Exception as exc:
            logger.error("Error generating deep report via Groq: %s", exc)
            return None


# Global singleton client
groq_client = GroqClient()
