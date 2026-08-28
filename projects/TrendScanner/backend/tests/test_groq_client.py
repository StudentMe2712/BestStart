"""Unit tests for Groq LLM client, JSON parser, and Russian language enforcement."""

import json
import pytest
import httpx
from app.services.groq_client import (
    GroqClient,
    AIClassificationResult,
    extract_json_payload,
    has_untranslated_english,
    get_rlhf_context_prompt,
    SYSTEM_PROMPT,
    DEEP_REPORT_SYSTEM_PROMPT,
)


def test_system_prompt_rules_enforcement():
    """Verify system prompt contains strict CAPS LOCK Russian language rules."""
    assert "ПЕРЕВОД НА РУССКИЙ ОБЯЗАТЕЛЕН" in SYSTEM_PROMPT
    assert "СИСТЕМА УПАДЕТ" in SYSTEM_PROMPT
    assert "СТРОГО НА РУССКОМ ЯЗЫКЕ" in SYSTEM_PROMPT
    assert "ПЕРЕВОД НА РУССКИЙ ОБЯЗАТЕЛЕН" in DEEP_REPORT_SYSTEM_PROMPT


def test_has_untranslated_english_detection():
    """Verify detection of untranslated English text in summaries and trend names."""
    # 1. Pure English summaries without Cyrillic
    assert has_untranslated_english("AI Tools", "The tool is an automated lead finder for marketing agencies.") is True
    assert has_untranslated_english("No-Code", "This is a great SaaS product for freelancers.") is True
    assert has_untranslated_english(None, "In this post we discuss micro-SaaS opportunities and revenue.") is True

    # 2. English sentence starters and English stopwords
    assert has_untranslated_english("Analytics", "We are building an analytics platform for B2B founders.") is True

    # 3. English trend names with English prepositions and 0 Cyrillic
    assert has_untranslated_english("Automated Lead Generation for Agencies", "Анализ рынка показал спрос.") is True
    assert has_untranslated_english("AI Video Tools and Editor", "Сервис создания видео контента.") is True

    # 4. Valid Russian content with acceptable English brand names (Stripe, React, B2B, SaaS)
    assert has_untranslated_english(
        "Парсинг B2B лидов",
        "Сервис автоматизированного сбора открытых вакансий для агентств. Интеграция со Stripe."
    ) is False
    assert has_untranslated_english(
        "Микро-SaaS для клиник",
        "CRM-система на React и FastAPI для частной медицины. Высокий MRR."
    ) is False
    assert has_untranslated_english(None, "Короткое описание на русском языке.") is False


def test_extract_json_payload_direct():
    """Verify standard direct JSON string parsing."""
    raw = '{"is_trend": true, "trend_name": "ИИ Видео", "ai_score": 8, "scam_probability": 5, "ai_summary": "Хороший растущий рынок"}'
    res = extract_json_payload(raw)
    assert isinstance(res, dict)
    assert res["is_trend"] is True
    assert res["ai_score"] == 8


def test_extract_json_payload_markdown_code_block():
    """Verify extracting JSON enclosed in markdown code blocks."""
    raw = """Here is your analysis:
```json
{
  "is_trend": true,
  "trend_name": "No-Code CRM",
  "ai_score": 9,
  "scam_probability": 0,
  "ai_summary": "Растущий спрос среди небольших команд."
}
```
Let me know if you need more details."""
    res = extract_json_payload(raw)
    assert isinstance(res, dict)
    assert res["is_trend"] is True
    assert res["trend_name"] == "No-Code CRM"
    assert res["ai_score"] == 9


def test_extract_json_payload_with_prefix_text():
    """Verify extraction when model adds prefix text before json object."""
    raw = """Sure, here is the JSON output: {"is_trend": false, "trend_name": null, "ai_score": 2, "scam_probability": 80, "ai_summary": "Спам и накрутка крипты."} Hope this helps!"""
    res = extract_json_payload(raw)
    assert isinstance(res, dict)
    assert res["is_trend"] is False
    assert res["ai_score"] == 2
    assert res["scam_probability"] == 80


def test_extract_json_payload_invalid():
    """Verify None is returned for completely unparseable content."""
    assert extract_json_payload("") is None
    assert extract_json_payload(None) is None
    assert extract_json_payload("Plain text with no braces at all") is None


def test_ai_classification_result_validation():
    """Verify validation and score clamping."""
    result = AIClassificationResult(
        is_trend=True,
        trend_name="Тестовый тренд",
        ai_score=15,  # Should clamp to 10
        scam_probability=-5,  # Should clamp to 0
        ai_summary="Корректное описание на русском",
    )
    assert result.ai_score == 10
    assert result.scam_probability == 0


@pytest.mark.asyncio
async def test_groq_client_classify_text_success(monkeypatch):
    """Test successful classification via mocked Groq API in Russian."""
    mock_payload = {
        "choices": [
            {
                "message": {
                    "content": json.dumps(
                        {
                            "is_trend": True,
                            "trend_name": "Автоматизированный SEO аудит",
                            "ai_score": 8,
                            "scam_probability": 10,
                            "ai_summary": "Инструмент с высоким коммерческим потенциалом для агентств.",
                        }
                    )
                }
            }
        ]
    }

    class MockResponse:
        status_code = 200

        def raise_for_status(self):
            pass

        def json(self):
            return mock_payload

    class MockAsyncClient:
        def __init__(self, *args, **kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, exc_type, exc_val, exc_tb):
            pass

        async def post(self, url, **kwargs):
            return MockResponse()

    monkeypatch.setattr(httpx, "AsyncClient", MockAsyncClient)

    client = GroqClient(api_key="gsk_mock_test_key_123")
    res = await client.classify_text("Some text about automated SEO tools.")

    assert res is not None
    assert res.is_trend is True
    assert res.trend_name == "Автоматизированный SEO аудит"
    assert res.ai_score == 8
    assert res.scam_probability == 10


@pytest.mark.asyncio
async def test_groq_client_retry_on_english_output(monkeypatch):
    """Test direct translation of English fields when LLM returns English in trend_name or ai_summary."""
    import sys
    gc_module = sys.modules["app.services.groq_client"]
    monkeypatch.setattr(
        gc_module,
        "_translate_chunks_sync",
        lambda text: "Генератор ИИ-видео для маркетологов" if "Video" in text else "Автоматизированный сервис создания рекламных видео для TikTok."
    )

    class MockEnglishResponse:
        @property
        def status_code(self):
            return 200

        def raise_for_status(self):
            pass

        def json(self):
            return {
                "choices": [
                    {
                        "message": {
                            "content": json.dumps(
                                {
                                    "is_trend": True,
                                    "trend_name": "AI Video Generator for Marketers",
                                    "ai_score": 9,
                                    "scam_probability": 5,
                                    "ai_summary": "The tool is an automated video generator for TikTok ads.",
                                }
                            )
                        }
                    }
                ]
            }

    class MockAsyncClient:
        def __init__(self, *args, **kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, exc_type, exc_val, exc_tb):
            pass

        async def post(self, url, **kwargs):
            return MockEnglishResponse()

    monkeypatch.setattr(httpx, "AsyncClient", MockAsyncClient)

    client = GroqClient(api_key="gsk_mock_test_key_123")
    res = await client.classify_text("Текст о сервисе генерации ИИ-видео для рекламы.")

    assert res is not None
    assert res.is_trend is True
    assert res.trend_name == "Генератор ИИ-видео для маркетологов"
    assert "Автоматизированный сервис" in res.ai_summary




@pytest.mark.asyncio
async def test_groq_client_retry_on_429(monkeypatch):
    """Test exponential backoff retry on HTTP 429 rate limit."""
    call_count = 0

    class Mock429ThenSuccessResponse:
        def __init__(self, count):
            self.count = count

        @property
        def status_code(self):
            return 429 if self.count == 1 else 200

        @property
        def headers(self):
            return {"Retry-After": "0.01"} if self.count == 1 else {}

        def raise_for_status(self):
            pass

        def json(self):
            return {
                "choices": [
                    {
                        "message": {
                            "content": '{"is_trend": true, "trend_name": "Успешный повтор", "ai_score": 7, "scam_probability": 5, "ai_summary": "Успешно обработано после повторной попытки."}'
                        }
                    }
                ]
            }

    class MockAsyncClient:
        def __init__(self, *args, **kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, exc_type, exc_val, exc_tb):
            pass

        async def post(self, url, **kwargs):
            nonlocal call_count
            call_count += 1
            return Mock429ThenSuccessResponse(call_count)

    monkeypatch.setattr(httpx, "AsyncClient", MockAsyncClient)

    client = GroqClient(api_key="gsk_mock_test_key_123", retry_delay=0.01)
    res = await client.classify_text("Валидный контент для анализа на русском языке.")

    assert call_count == 2
    assert res is not None
    assert res.trend_name == "Успешный повтор"


def test_detect_language_detection():
    """Test language detection utility with Russian, English, and short texts."""
    from app.services.groq_client import detect_language

    assert detect_language("Это сервис для автоматизации маркетинга в Telegram.") == "ru"
    assert detect_language("This is a micro-SaaS tool for automated invoicing and payments.") == "en"
    assert detect_language("") == "ru"


@pytest.mark.asyncio
async def test_groq_client_translate_to_russian(monkeypatch):
    """Test fast translation step using deep-translator GoogleTranslator."""
    from app.services.groq_client import GroqClient
    import sys
    gc_module = sys.modules["app.services.groq_client"]
    monkeypatch.setattr(
        gc_module,
        "_translate_chunks_sync",
        lambda text: "Это автоматизированный инструмент лидогенерации."
    )

    client = GroqClient(api_key="gsk_mock_test_key_123")
    translated = await client.translate_to_russian("This is an automated lead generation tool.")
    assert "автоматизированный" in translated


@pytest.mark.asyncio
async def test_groq_client_translate_fallback_to_groq(monkeypatch):
    """Test fallback to Groq when GoogleTranslator fails."""
    import sys
    gc_module = sys.modules["app.services.groq_client"]

    def mock_fail_translate(text):
        raise RuntimeError("Google API network down")

    monkeypatch.setattr(gc_module, "_translate_chunks_sync", mock_fail_translate)

    class MockTranslationResponse:
        @property
        def status_code(self):
            return 200

        def raise_for_status(self):
            pass

        def json(self):
            return {
                "choices": [
                    {
                        "message": {
                            "content": "Это автоматизированный сервис сбора лидов."
                        }
                    }
                ]
            }

    class MockAsyncClient:
        def __init__(self, *args, **kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, exc_type, exc_val, exc_tb):
            pass

        async def post(self, url, **kwargs):
            return MockTranslationResponse()

    monkeypatch.setattr(httpx, "AsyncClient", MockAsyncClient)

    client = GroqClient(api_key="gsk_mock_test_key_123")
    translated = await client.translate_to_russian("This is an automated lead generation tool.")
    assert "автоматизированный сервис" in translated




@pytest.mark.asyncio
async def test_groq_client_missing_key():
    """Verify that client returns None on missing API key instead of crashing."""
    client = GroqClient(api_key="")
    res = await client.classify_text("Sample text")
    assert res is None


@pytest.mark.asyncio
async def test_translate_to_russian_empty_and_already_russian():
    """Verify translate_to_russian skips translation for empty and already Russian text."""
    from app.services.groq_client import translate_to_russian

    # Empty / whitespace
    assert await translate_to_russian("") == ""
    assert await translate_to_russian("   ") == "   "

    # Already Russian
    ru_text = "Это уже русский текст с описанием бизнес-модели SaaS."
    assert await translate_to_russian(ru_text) == ru_text


def test_translate_chunks_sync_chunking_large_text(monkeypatch):
    """Verify _translate_chunks_sync splits text > 4000 chars into smaller chunks."""
    import sys
    from unittest.mock import MagicMock

    mock_translator_instance = MagicMock()
    mock_translator_instance.translate.side_effect = lambda t: f"[RU] {t[:10]}"

    gc_module = sys.modules["app.services.groq_client"]
    monkeypatch.setattr(
        gc_module,
        "GoogleTranslator",
        lambda source, target: mock_translator_instance,
    )

    # Construct text with multiple paragraphs exceeding 4000 characters
    large_text = "\n\n".join([f"Paragraph {i}: " + ("A" * 1000) for i in range(6)])
    assert len(large_text) > 6000

    result = gc_module._translate_chunks_sync(large_text)
    assert "[RU]" in result
    assert mock_translator_instance.translate.call_count >= 2


def test_get_rlhf_context_prompt_empty(monkeypatch):
    """Verify get_rlhf_context_prompt returns empty string when no examples exist."""
    from app.db.dao import TrendsDAO

    monkeypatch.setattr(TrendsDAO, "get_rlhf_examples", lambda **kwargs: {"positive": [], "negative": []})
    assert get_rlhf_context_prompt() == ""

    monkeypatch.setattr(TrendsDAO, "get_rlhf_examples", lambda **kwargs: None)
    assert get_rlhf_context_prompt() == ""


def test_get_rlhf_context_prompt_with_examples(monkeypatch):
    """Verify get_rlhf_context_prompt formats positive and negative examples with calibration directives."""
    from app.db.dao import TrendsDAO

    mock_examples = {
        "positive": [
            {
                "trend_name": "Микро-SaaS для клиник",
                "ai_summary": "CRM для частных врачей и клиник.",
                "ai_score": 9,
            }
        ],
        "negative": [
            {
                "trend_name": "Крипто спам",
                "ai_summary": "Пирамида и накрутка токенов.",
                "ai_score": 1,
            }
        ],
    }

    monkeypatch.setattr(TrendsDAO, "get_rlhf_examples", lambda **kwargs: mock_examples)
    prompt = get_rlhf_context_prompt()

    assert "ТЕБЕ ДОСТУПЕН ОПЫТ ПОЛЬЗОВАТЕЛЯ (RLHF FEEDBACK):" in prompt
    assert "Вот примеры хороших трендов (+1, высокая ценность и жизнеспособность):" in prompt
    assert '- "Микро-SaaS для клиник": CRM для частных врачей и клиник. (Оценка: 9/10)' in prompt
    assert "Вот примеры мусора / нерелевантного контента (-1, штраф и инфошум):" in prompt
    assert '- "Крипто спам": Пирамида и накрутка токенов. (Оценка: 1/10)' in prompt
    assert "Инструкция по калибровке:" in prompt
    assert "1. Проанализируй новый текст." in prompt
    assert "2. Если он похож на мусорные примеры (-1)" in prompt
    assert "3. Если он похож на хорошие примеры (+1)" in prompt
    assert "4. Верни результат СТРОГО на русском языке в формате JSON." in prompt


def test_get_rlhf_context_prompt_only_positive(monkeypatch):
    """Verify get_rlhf_context_prompt when only positive examples exist."""
    from app.db.dao import TrendsDAO

    mock_examples = {
        "positive": [
            {
                "trend_name": "B2B парсинг вакансий",
                "ai_summary": "Парсер контактов HR.",
                "ai_score": 8,
            }
        ],
        "negative": [],
    }

    monkeypatch.setattr(TrendsDAO, "get_rlhf_examples", lambda **kwargs: mock_examples)
    prompt = get_rlhf_context_prompt()

    assert "Вот примеры хороших трендов (+1" in prompt
    assert "Вот примеры мусора" not in prompt
    assert "Инструкция по калибровке:" in prompt


def test_get_rlhf_context_prompt_only_negative(monkeypatch):
    """Verify get_rlhf_context_prompt when only negative examples exist."""
    from app.db.dao import TrendsDAO

    mock_examples = {
        "positive": [],
        "negative": [
            {
                "trend_name": "Флуд в чате",
                "ai_summary": "Бессмысленный спор.",
                "ai_score": 2,
            }
        ],
    }

    monkeypatch.setattr(TrendsDAO, "get_rlhf_examples", lambda **kwargs: mock_examples)
    prompt = get_rlhf_context_prompt()

    assert "Вот примеры хороших трендов" not in prompt
    assert "Вот примеры мусора / нерелевантного контента (-1" in prompt
    assert "Инструкция по калибровке:" in prompt


def test_get_rlhf_context_prompt_exception_safety(monkeypatch):
    """Verify get_rlhf_context_prompt catches exceptions and returns empty string."""
    from app.db.dao import TrendsDAO

    def mock_broken(**kwargs):
        raise RuntimeError("Database connection failure")

    monkeypatch.setattr(TrendsDAO, "get_rlhf_examples", mock_broken)
    assert get_rlhf_context_prompt() == ""


@pytest.mark.asyncio
async def test_classify_text_with_rlhf_and_pretranslation(monkeypatch):
    """Verify classify_text pre-translates English text and injects RLHF prompt into Groq payload."""
    import sys
    from app.db.dao import TrendsDAO

    gc_module = sys.modules["app.services.groq_client"]
    monkeypatch.setattr(
        gc_module,
        "_translate_chunks_sync",
        lambda text: "Переведенный русский текст о микросервисах."
    )

    mock_rlhf = {
        "positive": [
            {
                "trend_name": "Пример успеха",
                "ai_summary": "Отличный SaaS.",
                "ai_score": 9,
            }
        ],
        "negative": [],
    }
    monkeypatch.setattr(TrendsDAO, "get_rlhf_examples", lambda **kwargs: mock_rlhf)

    captured_payloads = []

    class MockAsyncClient:
        def __init__(self, *args, **kwargs):
            pass

        async def __aenter__(self):
            return self

        async def __aexit__(self, exc_type, exc_val, exc_tb):
            pass

        async def post(self, url, **kwargs):
            captured_payloads.append(kwargs.get("json", {}))

            class MockResponse:
                status_code = 200

                def raise_for_status(self):
                    pass

                def json(self):
                    return {
                        "choices": [
                            {
                                "message": {
                                    "content": json.dumps(
                                        {
                                            "is_trend": True,
                                            "trend_name": "Микросервисы SaaS",
                                            "ai_score": 8,
                                            "scam_probability": 0,
                                            "ai_summary": "Платформа для мониторинга микросервисов.",
                                        }
                                    )
                                }
                            }
                        ]
                    }

            return MockResponse()

    monkeypatch.setattr(httpx, "AsyncClient", MockAsyncClient)

    client = GroqClient(api_key="gsk_mock_test_key_123")
    res = await client.classify_text("English text about microservices monitoring tools.")

    assert res is not None
    assert res.is_trend is True
    assert res.trend_name == "Микросервисы SaaS"
    assert res.ai_score == 8

    # Verify captured prompt contained RLHF context and pre-translated text
    assert len(captured_payloads) == 1
    sent_messages = captured_payloads[0]["messages"]
    user_message = next(m["content"] for m in sent_messages if m["role"] == "user")
    assert "ТЕБЕ ДОСТУПЕН ОПЫТ ПОЛЬЗОВАТЕЛЯ (RLHF FEEDBACK):" in user_message
    assert "Пример успеха" in user_message
    assert "Переведенный русский текст о микросервисах." in user_message




