"""Tests for Strict Translation and 'No English' Checkpoint Filter in Ingestion Pipeline."""

import pytest
from unittest.mock import AsyncMock, patch

from app.services.sanitizer import (
    TextSanitizer,
    has_untranslated_english_markers,
    is_predominantly_cyrillic,
    sanitize_and_translate_content,
)


def test_is_predominantly_cyrillic_with_russian():
    """Test Cyrillic detection on Russian text containing standard IT terms."""
    ru_text = "Новый Micro-SaaS инструмент для автоматизации B2B инвойсов и работы со Stripe. Достигли $10k ARR."
    assert is_predominantly_cyrillic(ru_text) is True


def test_is_predominantly_cyrillic_with_english():
    """Test Cyrillic detection on pure English text."""
    en_text = "We built an autonomous AI agent for customer support. It connects with Zendesk and Intercom APIs."
    assert is_predominantly_cyrillic(en_text) is False


def test_has_untranslated_english_markers():
    """Test English stop words and grammatical markers detection."""
    en_text = "The platform is built for creators with high monthly recurring revenue."
    assert has_untranslated_english_markers(en_text) is True

    clean_ru = "Платформа создана для создателей контента с высоким ежемесячным регулярным доходом."
    assert has_untranslated_english_markers(clean_ru) is False


@pytest.mark.asyncio
async def test_sanitize_and_translate_content_translates_english():
    """Test that English text gets auto-translated to Russian before DB queue."""
    raw_english = (
        "We launched a new Micro-SaaS platform for dental clinics. "
        "The system integrates with Stripe to automate patient invoices. "
        "Our team hit $15k MRR in just two months of operation."
    )

    mock_translate = AsyncMock(
        return_value=(
            "Мы запустили новую платформу Micro-SaaS для стоматологических клиник. "
            "Система интегрируется со Stripe для автоматизации счетов пациентов. "
            "Наша команда достигла $15k MRR всего за два месяца работы."
        )
    )

    res = await sanitize_and_translate_content(raw_english, min_length=50, translate_func=mock_translate)
    assert res.is_valid is True
    assert "Мы запустили новую платформу" in res.cleaned_text
    assert is_predominantly_cyrillic(res.cleaned_text) is True
    assert res.reject_reason is None
    mock_translate.assert_called_once()


@pytest.mark.asyncio
async def test_sanitize_and_translate_content_drops_untranslated_english():
    """Test that text still containing English markers after translation attempt is dropped (No English rule)."""
    raw_english = (
        "This is an untranslatable English post that will fail translation. "
        "The and with for this that system is completely in English."
    )

    mock_translate = AsyncMock(return_value=raw_english)

    res = await sanitize_and_translate_content(raw_english, min_length=50, translate_func=mock_translate)
    assert res.is_valid is False
    assert res.reject_reason == "untranslated_english"


@pytest.mark.asyncio
async def test_sanitize_and_translate_content_accepts_russian_directly():
    """Test that already-Russian text is accepted without redundant translation calls."""
    clean_ru = (
        "Сервис автоматической аналитики трендов на основе искусственного интеллекта. "
        "Помогает находить прибыльные микрониши для соло-фаундеров и B2B команд."
    )

    mock_translate = AsyncMock()
    res = await sanitize_and_translate_content(clean_ru, min_length=50, translate_func=mock_translate)
    assert res.is_valid is True
    assert res.cleaned_text == clean_ru
    mock_translate.assert_not_called()
