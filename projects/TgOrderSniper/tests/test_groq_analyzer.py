"""Unit tests for GroqAnalyzer, GroqAnalysisResult, and Pre-Filter Gatekeeper."""

import asyncio
import json
from unittest.mock import AsyncMock, patch

import httpx
import pytest

from backend.core.config import Settings
from backend.filter.groq_analyzer import (
    PREFILTER_STOP_WORDS,
    PREFILTER_TRIGGERS,
    GroqAnalysisResult,
    GroqAnalyzer,
    check_prefilter,
)


def test_groq_analysis_result_normalization():
    """Verifies type normalizations for GroqAnalysisResult fields."""
    res1 = GroqAnalysisResult(
        is_lead=True,
        summary="Написать парсер цен",
        budget=50000,
        tech_stack=["Python", "Playwright"],
        ready_pitch="Здравствуйте! Готов выполнить парсер.",
    )
    assert res1.is_lead is True
    assert res1.summary == "Написать парсер цен"
    assert res1.budget == "50000 руб"
    assert res1.tech_stack == "Python, Playwright"
    assert res1.ready_pitch == "Здравствуйте! Готов выполнить парсер."

    # String budget and tech stack
    res2 = GroqAnalysisResult(
        is_lead=False,
        summary="",
        budget="300$",
        tech_stack="Django",
        ready_pitch="",
    )
    assert res2.is_lead is False
    assert res2.budget == "300$"
    assert res2.tech_stack == "Django"

    # None values
    res3 = GroqAnalysisResult(
        is_lead=False,
        budget=None,
        tech_stack=None,
    )
    assert res3.budget == ""
    assert res3.tech_stack == ""


def test_anti_hallucination_guard_budget_normalization():
    """Verifies that Anti-Hallucination Guard normalizes null/none/empty budgets to empty string."""
    for raw_budget in [None, "null", "NULL", "Null", "none", "None", "", "   ", '""']:
        result = GroqAnalysisResult(budget=raw_budget)  # type: ignore[arg-type]
        assert result.budget == "", f"Expected empty string for {raw_budget!r}, got {result.budget!r}"

    valid_res = GroqAnalysisResult(budget="25 000 руб")
    assert valid_res.budget == "25 000 руб"

    # Verify Anti-Hallucination Guard keywords in SYSTEM_PROMPT
    assert "КАТЕГОРИЧЕСКИ ЗАПРЕЩЕНО придумывать или предполагать бюджет" in GroqAnalyzer.SYSTEM_PROMPT
    assert "строго null" in GroqAnalyzer.SYSTEM_PROMPT


def test_groq_analyzer_default_models():
    """Verifies that GroqAnalyzer defaults to openai/gpt-oss-20b for primary and fallback."""
    cfg = Settings(_env_file=None)
    analyzer = GroqAnalyzer(api_key="test_key", settings=cfg)
    assert analyzer.model == "openai/gpt-oss-20b"
    assert analyzer.fallback_model == "openai/gpt-oss-20b"
    assert analyzer.request_delay == 2.0


def test_prefilter_gatekeeper_triggers():
    """Verifies that all required trigger keywords pass pre-filter."""
    for trigger in PREFILTER_TRIGGERS:
        text = f"Здравствуйте, нам нужен исполнитель: {trigger} для сервиса"
        passed, reason = check_prefilter(text)
        assert passed is True, f"Trigger '{trigger}' failed prefilter: {reason}"
        assert GroqAnalyzer.check_prefilter(text)[0] is True


def test_prefilter_gatekeeper_stop_words():
    """Verifies that stop words reject messages even when triggers are present."""
    for stop_word in PREFILTER_STOP_WORDS:
        text = f"Срочно заказ! Мое {stop_word} прикреплено."
        passed, reason = check_prefilter(text)
        assert passed is False, f"Stop word '{stop_word}' unexpectedly passed: {reason}"
        assert f"'{stop_word}'" in reason


def test_prefilter_gatekeeper_no_triggers_and_word_boundaries():
    """Verifies rejection of messages without commercial triggers and prevents false positives."""
    # Casual chatter without triggers
    passed1, reason1 = check_prefilter("Привет всем! Какая сегодня отличная погода в чате?")
    assert passed1 is False
    assert "No commercial trigger keywords found" in reason1

    # Empty string
    passed_empty, _ = check_prefilter("   ")
    assert passed_empty is False

    # False positive test: 'работа' contains 'бот', but should NOT trigger 'бот'
    passed_work, reason_work = check_prefilter("У меня есть постоянная работа в крупном офисе")
    assert passed_work is False
    assert "No commercial trigger keywords found" in reason_work


@pytest.mark.asyncio
async def test_groq_analyzer_prefilter_drops_before_http_request():
    """Verifies that messages failing pre-filter return is_lead=False without any HTTP call."""
    called = False

    def handler(request: httpx.Request) -> httpx.Response:
        nonlocal called
        called = True
        return httpx.Response(status_code=200, json={})

    transport = httpx.MockTransport(handler)
    async with httpx.AsyncClient(transport=transport) as client:
        analyzer = GroqAnalyzer(api_key="test_key", client=client, request_delay=0)

        # 1. Non-commercial chat without commercial triggers (len >= 50)
        res1 = await analyzer.analyze(
            "Привет всем участникам чата! Какая сегодня замечательная и теплая погода за окном?"
        )
        assert res1.is_lead is False
        assert called is False

        # 2. Resume / job seeker with stop word (len >= 50)
        res2 = await analyzer.analyze(
            "Срочно требуется разработчик под проект! Вот мое резюме программиста на Python."
        )
        assert res2.is_lead is False
        assert called is False

        # 3. Message shorter than 50 chars with commercial trigger
        res3 = await analyzer.analyze("Нужен бот срочно на Python!")
        assert res3.is_lead is False
        assert called is False


@pytest.mark.asyncio
async def test_groq_analyzer_short_text_dropped_before_http_request():
    """Verifies that messages shorter than 50 characters are dropped before calling Groq API."""
    called = False

    def handler(request: httpx.Request) -> httpx.Response:
        nonlocal called
        called = True
        return httpx.Response(status_code=200, json={})

    transport = httpx.MockTransport(handler)
    async with httpx.AsyncClient(transport=transport) as client:
        analyzer = GroqAnalyzer(api_key="test_key", client=client, request_delay=0)

        # Commercial trigger present, but length is 29 chars (< 50)
        short_lead = "Срочно нужен телеграм-бот!"
        assert len(short_lead.strip()) < 50
        res = await analyzer.analyze(short_lead)
        assert res.is_lead is False
        assert called is False

        # 49 chars length with trigger
        text_49 = "Срочно требуется разработчик бота на Python123456"
        assert len(text_49.strip()) == 49
        res_49 = await analyzer.analyze(text_49)
        assert res_49.is_lead is False
        assert called is False


@pytest.mark.asyncio
async def test_groq_analyzer_unconfigured():
    """Ensures analyzer without API key returns is_lead=False immediately."""
    analyzer = GroqAnalyzer(api_key="")
    assert analyzer.is_configured is False
    result = await analyzer.analyze(
        "Срочно требуется опытный разработчик Telegram-ботов на Python и FastAPI под ключ"
    )
    assert result.is_lead is False
    assert result.summary == ""
    await analyzer.close()


@pytest.mark.asyncio
async def test_groq_analyzer_empty_text():
    """Ensures analyzer with empty or short text returns is_lead=False."""
    analyzer = GroqAnalyzer(api_key="valid_key")
    result = await analyzer.analyze("коротко")
    assert result.is_lead is False
    await analyzer.close()


@pytest.mark.asyncio
async def test_groq_analyzer_analyze_lead_success():
    """Verifies successful lead classification via primary model openai/gpt-oss-20b."""
    mock_payload = {
        "choices": [
            {
                "message": {
                    "content": json.dumps(
                        {
                            "is_lead": True,
                            "summary": "Разработка бота для заказа еды",
                            "budget": "40 000 руб",
                            "tech_stack": "Python, aiogram",
                            "ready_pitch": "Добрый день! Специализируюсь на Telegram-ботах на aiogram. Готов реализовать проект под ключ.",
                        }
                    )
                }
            }
        ]
    }

    def handler(request: httpx.Request) -> httpx.Response:
        assert request.headers["Authorization"] == "Bearer test_key"
        body = json.loads(request.content.decode("utf-8"))
        assert body["model"] == "openai/gpt-oss-20b"
        assert body["response_format"] == {"type": "json_object"}
        return httpx.Response(status_code=200, json=mock_payload)

    transport = httpx.MockTransport(handler)
    async with httpx.AsyncClient(transport=transport) as client:
        analyzer = GroqAnalyzer(
            api_key="test_key",
            model="openai/gpt-oss-20b",
            client=client,
            request_delay=0,
        )
        lead_text = "Срочно требуется опытный разработчик Telegram-ботов на Python и FastAPI под ключ"
        assert len(lead_text.strip()) >= 50
        result = await analyzer.analyze(lead_text)
        assert result.is_lead is True
        assert result.summary == "Разработка бота для заказа еды"
        assert result.budget == "40 000 руб"
        assert result.tech_stack == "Python, aiogram"
        assert "Добрый день!" in result.ready_pitch


# Backward-compatible alias
test_groq_analyzer_primary_success = test_groq_analyzer_analyze_lead_success


@pytest.mark.asyncio
async def test_groq_analyzer_fallback_on_primary_error():
    """Verifies that primary model failure triggers the fallback model."""
    call_count = 0

    def handler(request: httpx.Request) -> httpx.Response:
        nonlocal call_count
        call_count += 1
        body = json.loads(request.content.decode("utf-8"))
        if call_count == 1:
            assert body["model"] == "primary-model"
            return httpx.Response(status_code=500, json={"error": "Model overloaded"})

        assert body["model"] == "fallback-model"
        return httpx.Response(
            status_code=200,
            json={
                "choices": [
                    {
                        "message": {
                            "content": json.dumps(
                                {
                                    "is_lead": True,
                                    "summary": "Интеграция Kaspi API",
                                    "budget": "150 000 kzt",
                                    "tech_stack": "Python, FastAPI",
                                    "ready_pitch": "Здравствуйте! Имею опыт работы с Kaspi API.",
                                }
                            )
                        }
                    }
                ]
            },
        )

    transport = httpx.MockTransport(handler)
    async with httpx.AsyncClient(transport=transport) as client:
        analyzer = GroqAnalyzer(
            api_key="test_key",
            model="primary-model",
            fallback_model="fallback-model",
            client=client,
            request_delay=0,
        )
        lead_text = "Срочно требуется опытный разработчик для интеграции с Kaspi API под ключ"
        assert len(lead_text.strip()) >= 50
        result = await analyzer.analyze(lead_text)
        assert result.is_lead is True
        assert result.summary == "Интеграция Kaspi API"
        assert call_count == 2


@pytest.mark.asyncio
async def test_groq_analyzer_both_models_fail():
    """Verifies that when both primary and fallback fail, is_lead=False is returned gracefully."""
    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(status_code=503, json={"error": "Service unavailable"})

    transport = httpx.MockTransport(handler)
    async with httpx.AsyncClient(transport=transport) as client:
        analyzer = GroqAnalyzer(
            api_key="test_key",
            model="model-1",
            fallback_model="model-2",
            client=client,
            request_delay=0,
        )
        lead_text = "Срочно требуется опытный разработчик Telegram-ботов на Python и FastAPI под ключ"
        assert len(lead_text.strip()) >= 50
        result = await analyzer.analyze(lead_text)
        assert result.is_lead is False


@pytest.mark.asyncio
async def test_groq_analyzer_invalid_json():
    """Verifies handling when model returns non-JSON content."""
    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(
            status_code=200,
            json={"choices": [{"message": {"content": "not valid json"}}]},
        )

    transport = httpx.MockTransport(handler)
    async with httpx.AsyncClient(transport=transport) as client:
        analyzer = GroqAnalyzer(api_key="test_key", client=client, request_delay=0)
        lead_text = "Срочно требуется опытный разработчик Telegram-ботов на Python и FastAPI под ключ"
        assert len(lead_text.strip()) >= 50
        result = await analyzer.analyze(lead_text)
        assert result.is_lead is False


@pytest.mark.asyncio
async def test_groq_analyzer_retry_on_429_success():
    """Verifies that 429 rate limit triggers Retry-After wait and succeeds on retry."""
    call_count = 0

    mock_payload = {
        "choices": [
            {
                "message": {
                    "content": json.dumps(
                        {
                            "is_lead": True,
                            "summary": "Разработка бота",
                            "budget": "20 000 руб",
                            "tech_stack": "Python",
                            "ready_pitch": "Готов выполнить проект.",
                        }
                    )
                }
            }
        ]
    }

    def handler(request: httpx.Request) -> httpx.Response:
        nonlocal call_count
        call_count += 1
        if call_count == 1:
            return httpx.Response(
                status_code=429,
                headers={"Retry-After": "0.01"},
                json={"error": "Rate limit exceeded"},
            )
        return httpx.Response(status_code=200, json=mock_payload)

    transport = httpx.MockTransport(handler)
    async with httpx.AsyncClient(transport=transport) as client:
        analyzer = GroqAnalyzer(api_key="test_key", client=client, request_delay=0)
        lead_text = "Срочно требуется опытный разработчик Telegram-ботов на Python и FastAPI под ключ"
        assert len(lead_text.strip()) >= 50
        result = await analyzer.analyze(lead_text)
        assert result.is_lead is True
        assert result.summary == "Разработка бота"
        assert call_count == 2


@pytest.mark.asyncio
async def test_groq_analyzer_retry_on_429_absent_retry_after():
    """Verifies that 429 without Retry-After header waits 3-5 seconds and retries."""
    call_count = 0

    mock_payload = {
        "choices": [
            {
                "message": {
                    "content": json.dumps(
                        {
                            "is_lead": True,
                            "summary": "Написание скрипта",
                            "budget": "25 000 руб",
                            "tech_stack": "Python",
                            "ready_pitch": "Здравствуйте! Напишу скрипт быстро и качественно.",
                        }
                    )
                }
            }
        ]
    }

    def handler(request: httpx.Request) -> httpx.Response:
        nonlocal call_count
        call_count += 1
        if call_count == 1:
            # 429 without Retry-After header
            return httpx.Response(
                status_code=429,
                headers={},
                json={"error": "Rate limit exceeded"},
            )
        return httpx.Response(status_code=200, json=mock_payload)

    transport = httpx.MockTransport(handler)
    async with httpx.AsyncClient(transport=transport) as client:
        analyzer = GroqAnalyzer(api_key="test_key", client=client, request_delay=0)
        with patch("asyncio.sleep", new_callable=AsyncMock) as mock_sleep:
            lead_text = "Срочно требуется написать сложный скрипт автоматизации на Python и FastAPI"
            assert len(lead_text.strip()) >= 50
            result = await analyzer.analyze(lead_text)
            assert result.is_lead is True
            assert call_count == 2
            # Verify sleep was invoked with a wait between 3 and 5 seconds
            assert mock_sleep.await_count == 1
            slept_time = mock_sleep.call_args[0][0]
            assert 3.0 <= slept_time <= 5.0


@pytest.mark.asyncio
async def test_groq_analyzer_retry_on_429_then_fallback():
    """Verifies that persistent 429 on primary triggers fallback model."""
    call_count = 0

    def handler(request: httpx.Request) -> httpx.Response:
        nonlocal call_count
        call_count += 1
        body = json.loads(request.content.decode("utf-8"))
        if body["model"] == "primary-model":
            return httpx.Response(
                status_code=429,
                headers={"Retry-After": "0.01"},
                json={"error": "Rate limit exceeded"},
            )
        # Fallback model succeeds
        return httpx.Response(
            status_code=200,
            json={
                "choices": [
                    {
                        "message": {
                            "content": json.dumps(
                                {
                                    "is_lead": True,
                                    "summary": "Парсер сайта",
                                    "budget": "10 000 руб",
                                    "tech_stack": "Python, BS4",
                                    "ready_pitch": "Здравствуйте! Сделаю парсер.",
                                }
                            )
                        }
                    }
                ]
            },
        )

    transport = httpx.MockTransport(handler)
    async with httpx.AsyncClient(transport=transport) as client:
        analyzer = GroqAnalyzer(
            api_key="test_key",
            model="primary-model",
            fallback_model="fallback-model",
            client=client,
            request_delay=0,
        )
        lead_text = "Срочно требуется написать быстрый парсер интернет-магазинов на Python под ключ"
        assert len(lead_text.strip()) >= 50
        result = await analyzer.analyze(lead_text)
        assert result.is_lead is True
        assert result.summary == "Парсер сайта"
        # 2 attempts on primary (initial + retry) + 1 on fallback = 3 total
        assert call_count == 3


@pytest.mark.asyncio
async def test_groq_analyzer_lock_prevents_parallel_requests():
    """Verifies that asyncio.Lock prevents parallel requests to Groq API."""
    concurrent_requests = 0
    max_concurrent_requests = 0

    async def mock_handler(request: httpx.Request) -> httpx.Response:
        nonlocal concurrent_requests, max_concurrent_requests
        concurrent_requests += 1
        max_concurrent_requests = max(max_concurrent_requests, concurrent_requests)
        await asyncio.sleep(0.05)
        concurrent_requests -= 1
        return httpx.Response(
            status_code=200,
            json={
                "choices": [
                    {
                        "message": {
                            "content": json.dumps(
                                {
                                    "is_lead": True,
                                    "summary": "Тестовый заказ",
                                    "budget": "10 000 руб",
                                    "tech_stack": "Python",
                                    "ready_pitch": "Готов помочь.",
                                }
                            )
                        }
                    }
                ]
            },
        )

    transport = httpx.MockTransport(mock_handler)
    async with httpx.AsyncClient(transport=transport) as client:
        analyzer = GroqAnalyzer(api_key="test_key", client=client, request_delay=0)
        lead_text = "Срочно требуется опытный разработчик Telegram-ботов на Python и FastAPI под ключ"
        results = await asyncio.gather(
            analyzer.analyze(lead_text),
            analyzer.analyze(lead_text),
            analyzer.analyze(lead_text),
        )
        assert all(r.is_lead for r in results)
        assert max_concurrent_requests == 1


def test_groq_analyzer_request_delay_init():
    """Checks request_delay initialization from settings or custom argument."""
    cfg = Settings(_env_file=None)
    analyzer_default = GroqAnalyzer(api_key="key", settings=cfg)
    assert analyzer_default.request_delay == 2.0

    analyzer_custom = GroqAnalyzer(api_key="key", request_delay=0.5)
    assert analyzer_custom.request_delay == 0.5

