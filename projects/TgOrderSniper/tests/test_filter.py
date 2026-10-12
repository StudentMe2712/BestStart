"""Unit tests for FilterEngine and FilteredOrder model."""

import pytest
from backend.filter.engine import FilterEngine, FilteredOrder


@pytest.fixture
def engine():
    return FilterEngine()


def test_length_cutoff(engine):
    """Ensures short messages (< 15 chars) are immediately dropped."""
    assert engine.evaluate("Привет", "Freelance", 1, 100) is None
    assert engine.evaluate("Нужен бот", "Freelance", 1, 101) is None  # 9 chars
    assert engine.evaluate("   ", "Freelance", 1, 102) is None


def test_negative_gatekeeper_resumes(engine):
    """Verifies that resume submissions are rejected."""
    resume_samples = [
        "Привет всем! Ищу работу junior python разработчиком. Опыт работы: 6 месяцев. Портфолио в лс.",
        "Обо мне: fullstack разработчик ищет интересные проекты. Мое резюме прикреплено.",
        "Готов работать над парсерами. Мои навыки: python, beautifulsoup. Ищу заказы.",
        "Ищу стажировку или проект для портфолио. Готов к сотрудничеству.",
    ]
    for text in resume_samples:
        order = engine.evaluate(text, "Job Board", 1, 10)
        assert order is None, f"Expected resume to be dropped: {text}"


def test_negative_gatekeeper_spam(engine):
    """Verifies that spam, crypto, casino and gambling ads are rejected."""
    spam_samples = [
        "Подпишись на закрытый канал! Бесплатный вебинар и крипта со скидкой! Пассивный доход от 1000$!",
        "Точные сигналы на ставках в 1win! Заработок от 50 000 руб в день! Вход в канал по ссылке.",
        "Слив курсов по арбитражу! Легкие деньги без вложений. Дарю гайд всем подписчикам!",
    ]
    for text in spam_samples:
        order = engine.evaluate(text, "Spam Channel", 1, 20)
        assert order is None, f"Expected spam to be dropped: {text}"


def test_positive_order_evaluation(engine):
    """Verifies successful matching of commercial client requests."""
    text = (
        "Срочно требуется разработчик на Python! "
        "Нужно сделать Telegram-бота для парсинга цен с каталога. "
        "Бюджет: 35 000 руб. ТЗ отправлю в личку."
    )
    order = engine.evaluate(
        text=text,
        chat_title="IT Freelance Orders",
        chat_id="-1001234567",
        message_id=555,
        sender={"id": 999, "username": "client_boss", "name": "Алексей Иванов"},
        message_url="https://t.me/c/1234567/555",
    )

    assert isinstance(order, FilteredOrder)
    assert order.chat_title == "IT Freelance Orders"
    assert order.chat_id == "-1001234567"
    assert order.message_id == 555
    assert order.author_username == "client_boss"
    assert order.author_name == "Алексей Иванов"
    assert order.message_url == "https://t.me/c/1234567/555"

    # Triggers matched
    assert "срочно" in order.matched_triggers
    assert "требуется" in order.matched_triggers or "нужно" in order.matched_triggers

    # Budget extracted
    assert order.detected_budget == "35 000 руб"

    # Category tags
    assert "python" in order.category_tags
    assert "телеграм бот" in order.category_tags
    assert "парсер" in order.category_tags


def test_budget_extraction_patterns(engine):
    """Verifies various budget regex matches."""
    cases = [
        ("Нужен скрипт. Бюджет: 15 000 руб. Срочно.", "15 000 руб"),
        ("Требуется доработка сайта. Оплата 500$.", "500$"),
        ("Разработать бота. Стоимость: 25к.", "25к"),
        ("Интеграция API, бюджет от 20 000 до 40 000 руб.", "от 20 000 до 40 000 руб"),
        ("Сделать парсер, оплата: 100000 kzt.", "100000 kzt"),
        ("Доработать модуль 1С, бюджет 50000.", "50000"),
    ]
    for text, expected in cases:
        budget = engine.extract_budget(text)
        assert budget == expected, f"Failed for text: '{text}', got: '{budget}'"


def test_text_cleaning(engine):
    """Verifies that redundant newlines and whitespace are cleanly formatted."""
    dirty_text = (
        "   Нужен парсер на Python.   \n\n\n\n\n"
        "Срочно собрать базу данных.   \n\n\n"
        "Бюджет: 10 000 руб.   "
    )
    cleaned = engine.clean_text(dirty_text)
    expected = (
        "Нужен парсер на Python.\n\n"
        "Срочно собрать базу данных.\n\n"
        "Бюджет: 10 000 руб."
    )
    assert cleaned == expected
