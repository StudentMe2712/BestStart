"""Unit tests for web scrapers (Habr, Kwork) and scraper scheduler."""

from unittest.mock import AsyncMock, patch

import httpx
import pytest

from backend.scrapers.base import RawTask
from backend.scrapers.habr_freelance import HabrFreelanceScraper
from backend.scrapers.kwork_playwright import KworkPlaywrightScraper
from backend.scrapers.scheduler import WebScraperScheduler

SAMPLE_HABR_HTML = """
<!DOCTYPE html>
<html>
<body>
    <div class="task_list">
        <article class="task" id="task_591100">
            <div class="task__title">
                <a href="/tasks/591100">Парсер цен для интернет-магазина на Python</a>
            </div>
            <div class="task__price">
                <span class="count">25 000 руб.</span>
            </div>
            <div class="task__client">
                <a href="/users/alex_client">Алексей Заказчик</a>
            </div>
            <div class="task__description">
                Требуется разработать парсер каталога товаров и цен с выгрузкой в Excel.
            </div>
        </article>
        <article class="task" id="task_591101">
            <div class="task__title">
                <a href="/tasks/591101">Бот для Telegram заказов</a>
            </div>
            <div class="task__price">
                <span class="count">Договорная</span>
            </div>
            <div class="task__client">
                <a href="/users/dmitry">Дмитрий</a>
            </div>
            <div class="task__description">
                Разработка Telegram-бота под ключ на aiogram.
            </div>
        </article>
    </div>
</body>
</html>
"""

SAMPLE_KWORK_HTML = """
<!DOCTYPE html>
<html>
<body>
    <div class="projects-list">
        <div class="want-card">
            <div class="want-card__header">
                <a href="/projects/849201/view">Интеграция 1С и Kaspi Магазина через API</a>
            </div>
            <div class="want-card__price">
                до 40 000 ₽
            </div>
            <div class="want-card__user">
                <a href="/user/kaspi_seller">Сергей Kaspi</a>
            </div>
            <div class="want-card__description break-word">
                Нужна автоматическая синхронизация остатков и заказов Kaspi с 1С УТ.
            </div>
        </div>
    </div>
</body>
</html>
"""


def test_raw_task_model() -> None:
    """Verifies RawTask initialization and defaults."""
    task = RawTask(
        source="web:habr_freelance",
        external_id="101",
        title="Скрипт парсинга",
        text="Описание задачи",
        url="https://freelance.habr.com/tasks/101",
        budget="15 000 руб",
        author="Иван",
    )
    assert task.source == "web:habr_freelance"
    assert task.external_id == "101"
    assert task.budget == "15 000 руб"
    assert task.author == "Иван"


@pytest.mark.asyncio
async def test_habr_freelance_scraper_parse_html() -> None:
    """Verifies HabrFreelanceScraper parsing logic on sample HTML."""
    scraper = HabrFreelanceScraper()
    tasks = scraper.parse_html(SAMPLE_HABR_HTML)

    assert len(tasks) == 2

    # Task 1: fixed price
    t1 = tasks[0]
    assert t1.source == "web:habr_freelance"
    assert t1.external_id == "591100"
    assert t1.title == "Парсер цен для интернет-магазина на Python"
    assert t1.url == "https://freelance.habr.com/tasks/591100"
    assert t1.budget == "25 000 руб."
    assert t1.author == "Алексей Заказчик"
    assert "Excel" in t1.text

    # Task 2: negotiated price -> budget should be None
    t2 = tasks[1]
    assert t2.external_id == "591101"
    assert t2.budget is None
    assert t2.author == "Дмитрий"

    await scraper.close()


@pytest.mark.asyncio
async def test_habr_freelance_scraper_network_fetch() -> None:
    """Verifies fetch_new_tasks with mock httpx response and error handling."""
    def mock_handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(status_code=200, text=SAMPLE_HABR_HTML)

    transport = httpx.MockTransport(mock_handler)
    async with httpx.AsyncClient(transport=transport) as client:
        scraper = HabrFreelanceScraper(client=client)
        tasks = await scraper.fetch_new_tasks()
        assert len(tasks) == 2

    # Network error handling: returns empty list
    def error_handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(status_code=500, text="Internal Server Error")

    err_transport = httpx.MockTransport(error_handler)
    async with httpx.AsyncClient(transport=err_transport) as client:
        scraper_err = HabrFreelanceScraper(client=client)
        tasks_err = await scraper_err.fetch_new_tasks()
        assert tasks_err == []


def test_kwork_playwright_scraper_parse_html() -> None:
    """Verifies Kwork HTML parsing logic."""
    scraper = KworkPlaywrightScraper()
    tasks = scraper.parse_html(SAMPLE_KWORK_HTML)

    assert len(tasks) == 1
    t = tasks[0]
    assert t.source == "web:kwork"
    assert t.external_id == "849201"
    assert t.title == "Интеграция 1С и Kaspi Магазина через API"
    assert t.url == "https://kwork.ru/projects/849201/view"
    assert t.budget == "до 40 000 ₽"
    assert t.author == "Сергей Kaspi"
    assert "Kaspi" in t.text


@pytest.mark.asyncio
async def test_kwork_playwright_scraper_browser_failure_graceful() -> None:
    """Verifies graceful exception handling if Playwright fails to launch browser."""
    scraper = KworkPlaywrightScraper()

    # Mock playwright launch raising exception
    with patch("playwright.async_api.async_playwright") as mock_pw_ctx:
        mock_pw = AsyncMock()
        mock_pw.chromium.launch.side_effect = RuntimeError("Chromium binary not found")
        mock_pw_ctx.return_value.__aenter__.return_value = mock_pw

        tasks = await scraper.fetch_new_tasks()
        assert tasks == []


@pytest.mark.asyncio
async def test_kwork_playwright_scraper_success() -> None:
    """Verifies fetch_new_tasks when Playwright page loads properly."""
    scraper = KworkPlaywrightScraper()

    with patch("playwright.async_api.async_playwright") as mock_pw_ctx:
        mock_pw = AsyncMock()
        mock_browser = AsyncMock()
        mock_context = AsyncMock()
        mock_page = AsyncMock()

        mock_page.content = AsyncMock(return_value=SAMPLE_KWORK_HTML)
        mock_context.new_page = AsyncMock(return_value=mock_page)
        mock_browser.new_context = AsyncMock(return_value=mock_context)
        mock_pw.chromium.launch = AsyncMock(return_value=mock_browser)
        mock_pw_ctx.return_value.__aenter__.return_value = mock_pw

        tasks = await scraper.fetch_new_tasks()
        assert len(tasks) == 1
        assert tasks[0].external_id == "849201"


@pytest.mark.asyncio
async def test_web_scraper_scheduler_lifecycle_and_callback() -> None:
    """Verifies WebScraperScheduler execution, callback invocation, and lifecycle."""
    mock_scraper1 = AsyncMock()
    mock_task1 = RawTask(
        source="web:mock1",
        external_id="1",
        title="Тест 1",
        text="Текст 1",
        url="https://example.com/1",
    )
    mock_scraper1.fetch_new_tasks = AsyncMock(return_value=[mock_task1])

    mock_scraper2 = AsyncMock()
    mock_task2 = RawTask(
        source="web:mock2",
        external_id="2",
        title="Тест 2",
        text="Текст 2",
        url="https://example.com/2",
    )
    mock_scraper2.fetch_new_tasks = AsyncMock(return_value=[mock_task2])

    callback_mock = AsyncMock()

    scheduler = WebScraperScheduler(
        scrapers=[mock_scraper1, mock_scraper2],
        on_task_callback=callback_mock,
        interval_minutes=10,
    )

    # 1. Test run_once
    all_tasks = await scheduler.run_once()
    assert len(all_tasks) == 2
    assert callback_mock.await_count == 2
    callback_mock.assert_any_await(mock_task1)
    callback_mock.assert_any_await(mock_task2)

    # 2. Test lifecycle start and stop
    with patch("asyncio.sleep", new_callable=AsyncMock) as mock_sleep:
        # Avoid infinite loop in test
        mock_sleep.side_effect = [None, Exception("Stop loop")]

        await scheduler.start()
        assert scheduler.is_running is True

        # Stop
        await scheduler.stop()
        assert scheduler.is_running is False


@pytest.mark.asyncio
async def test_web_scraper_scheduler_resilience_to_scraper_errors() -> None:
    """Verifies that an exception in one scraper does not halt others."""
    failing_scraper = AsyncMock()
    failing_scraper.fetch_new_tasks.side_effect = RuntimeError("Scraper crashed")

    working_scraper = AsyncMock()
    task = RawTask(
        source="web:habr",
        external_id="10",
        title="Задача",
        text="Описание",
        url="https://habr.com",
    )
    working_scraper.fetch_new_tasks = AsyncMock(return_value=[task])

    callback = AsyncMock()
    scheduler = WebScraperScheduler(
        scrapers=[failing_scraper, working_scraper],
        on_task_callback=callback,
        interval_minutes=5,
    )

    results = await scheduler.run_once()
    assert len(results) == 1
    assert results[0].external_id == "10"
    assert callback.await_count == 1
