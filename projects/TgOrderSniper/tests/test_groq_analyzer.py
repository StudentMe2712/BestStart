"""Unit tests for GroqAnalyzer and GroqAnalysisResult."""

import json
import httpx
import pytest


from backend.filter.groq_analyzer import GroqAnalysisResult, GroqAnalyzer


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


@pytest.mark.asyncio
async def test_groq_analyzer_unconfigured():
    """Ensures analyzer without API key returns is_lead=False immediately."""
    analyzer = GroqAnalyzer(api_key="")
    assert analyzer.is_configured is False
    result = await analyzer.analyze("Нужен бот срочно")
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
async def test_groq_analyzer_primary_success():
    """Verifies successful lead classification via primary model."""
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
        )
        result = await analyzer.analyze("Срочно нужен телеграм бот для доставки еды")
        assert result.is_lead is True
        assert result.summary == "Разработка бота для заказа еды"
        assert result.budget == "40 000 руб"
        assert result.tech_stack == "Python, aiogram"
        assert "Добрый день!" in result.ready_pitch


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
        )
        result = await analyzer.analyze("Требуется интеграция с Kaspi")
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
        )
        result = await analyzer.analyze("Требуется разработчик")
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
        analyzer = GroqAnalyzer(api_key="test_key", client=client)
        result = await analyzer.analyze("Нужен бот")
        assert result.is_lead is False
