"""Unit tests for web scrapers (Kwork, Freelancehunt, FL.ru, Habr) and scraper scheduler."""

from unittest.mock import AsyncMock, patch

import httpx
import pytest

from backend.scrapers.base import RawTask
from backend.scrapers.fl_ru import FlRuScraper
from backend.scrapers.freelancehunt import FreelancehuntScraper
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

SAMPLE_FREELANCEHUNT_HTML = """
<!DOCTYPE html>
<html>
<body>
    <div class="projects-list">
        <article class="widget project-card" data-project-id="1658408">
            <header class="project-header">
                <h2 class="text-desktop-h5">
                    <a class="project-link" href="https://freelancehunt.com/project/horoshop-stvorennya-saytu/1658408.html">
                        Хорошоп (создание сайта)
                    </a>
                </h2>
            </header>
            <a class="project-link" href="https://freelancehunt.com/project/horoshop-stvorennya-saytu/1658408.html">
                <div class="project-stats">
                    <span class="project-stat budget">5000 UAH</span>
                </div>
                <p class="project-description">
                    Добрый день! Ищу специалиста для доработки интернет-магазина METAS на Хорошопе.
                </p>
            </a>
            <div class="project-user">
                <a href="/employer/alex">Алекс Заказчик</a>
            </div>
        </article>
        <article class="widget project-card" data-project-id="1658409">
            <header class="project-header">
                <h2 class="text-desktop-h5">
                    <a class="project-link" href="/project/telegram-bot/1658409.html">
                        Бот для уведомлений
                    </a>
                </h2>
            </header>
            <a class="project-link" href="/project/telegram-bot/1658409.html">
                <div class="project-stats">
                    <span class="project-stat budget">По договоренности</span>
                </div>
                <p class="project-description">
                    Разработать бота с вебхуком на FastAPI.
                </p>
            </a>
        </article>
    </div>
</body>
</html>
"""

SAMPLE_FL_RU_HTML = """
<!DOCTYPE html>
<html>
<body>
    <div class="b-layout">
        <div id="project-item100500" class="b-post b-post_padtop_15">
            <h2 class="b-post__title">
                <a class="b-post__link" href="/projects/100500/parser-dannyh.html">Парсер данных для маркетплейса</a>
            </h2>
            <div class="b-post__price">
                <span class="b-post__bold">35 000 руб.</span>
            </div>
            <div class="b-user">
                <a class="b-user__link" href="/users/client_1/">Максим Заказчик</a>
            </div>
            <div class="b-post__body">
                <div class="b-post__txt">Необходимо спарсить каталог товаров и загрузить в базу данных.</div>
            </div>
        </div>
        <div id="project-item100501" class="b-post b-post_padtop_15">
            <h2 class="b-post__title">
                <a class="b-post__link" href="/projects/100501/bot-v-telegram.html">Telegram бот под ключ</a>
            </h2>
            <div class="b-post__price">
                <span class="b-post__bold">По договоренности</span>
            </div>
            <div class="b-post__body">
                <div class="b-post__txt">Бот для приема заявок от клиентов.</div>
            </div>
        </div>
    </div>
</body>
</html>
"""

SAMPLE_FL_RU_MAINTENANCE_HTML = """
<!DOCTYPE html>
<html>
<head><title>Технические работы</title></head>
<body>
    <p>На сайте проводятся технические работы. Приносим свои извинения.</p>
</body>
</html>
"""


def test_raw_task_model() -> None:
    """Verifies RawTask initialization and defaults."""
    task = RawTask(
        source="web:kwork",
        external_id="101",
        title="Скрипт парсинга",
        text="Описание задачи",
        url="https://kwork.ru/projects/101",
        budget="15 000 руб",
        author="Иван",
    )
    assert task.source == "web:kwork"
    assert task.external_id == "101"
    assert task.budget == "15 000 руб"
    assert task.author == "Иван"


# ============================================================================
# Kwork Playwright Scraper Tests
# ============================================================================


def test_kwork_playwright_scraper_parse_html() -> None:
    """Verifies Kwork HTML parsing logic and link normalization."""
    scraper = KworkPlaywrightScraper()
    tasks = scraper.parse_html(SAMPLE_KWORK_HTML)

    assert len(tasks) == 1
    t = tasks[0]
    assert t.source == "web:kwork"
    assert t.external_id == "849201"
    assert t.title == "Интеграция 1С и Kaspi Магазина через API"
    assert t.url == "https://kwork.ru/projects/849201"
    assert t.budget == "до 40 000 ₽"
    assert t.author == "Сергей Kaspi"
    assert "Kaspi" in t.text


@pytest.mark.asyncio
async def test_kwork_playwright_scraper_browser_failure_graceful() -> None:
    """Verifies graceful exception handling if Playwright fails to launch browser."""
    scraper = KworkPlaywrightScraper()

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
        assert tasks[0].url == "https://kwork.ru/projects/849201"


# ============================================================================
# Freelancehunt Scraper Tests
# ============================================================================


def test_freelancehunt_scraper_parse_html() -> None:
    """Verifies Freelancehunt HTML parsing logic."""
    scraper = FreelancehuntScraper()
    tasks = scraper.parse_html(SAMPLE_FREELANCEHUNT_HTML)

    assert len(tasks) == 2

    # Task 1: Fixed budget
    t1 = tasks[0]
    assert t1.source == "web:freelancehunt"
    assert t1.external_id == "1658408"
    assert t1.title == "Хорошоп (создание сайта)"
    assert t1.url == "https://freelancehunt.com/project/horoshop-stvorennya-saytu/1658408.html"
    assert t1.budget == "5000 UAH"
    assert t1.author == "Алекс Заказчик"
    assert "интернет-магазина METAS" in t1.text

    # Task 2: Negotiated budget
    t2 = tasks[1]
    assert t2.source == "web:freelancehunt"
    assert t2.external_id == "1658409"
    assert t2.title == "Бот для уведомлений"
    assert t2.url == "https://freelancehunt.com/project/telegram-bot/1658409.html"
    assert t2.budget is None
    assert "FastAPI" in t2.text


@pytest.mark.asyncio
async def test_freelancehunt_scraper_httpx_fetch() -> None:
    """Verifies Freelancehunt fetch_new_tasks with mock httpx client."""
    def mock_handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(status_code=200, text=SAMPLE_FREELANCEHUNT_HTML)

    transport = httpx.MockTransport(mock_handler)
    async with httpx.AsyncClient(transport=transport) as client:
        scraper = FreelancehuntScraper(client=client)
        tasks = await scraper.fetch_new_tasks()
        assert len(tasks) == 2
        assert tasks[0].external_id == "1658408"

    # Network error handling
    def error_handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(status_code=500, text="Internal Server Error")

    err_transport = httpx.MockTransport(error_handler)
    async with httpx.AsyncClient(transport=err_transport) as client:
        scraper_err = FreelancehuntScraper(client=client)
        tasks_err = await scraper_err.fetch_new_tasks()
        assert tasks_err == []


@pytest.mark.asyncio
async def test_freelancehunt_scraper_playwright_success() -> None:
    """Verifies Freelancehunt fetch_new_tasks with Playwright."""
    scraper = FreelancehuntScraper()

    with patch("playwright.async_api.async_playwright") as mock_pw_ctx:
        mock_pw = AsyncMock()
        mock_browser = AsyncMock()
        mock_context = AsyncMock()
        mock_page = AsyncMock()

        mock_page.content = AsyncMock(return_value=SAMPLE_FREELANCEHUNT_HTML)
        mock_context.new_page = AsyncMock(return_value=mock_page)
        mock_browser.new_context = AsyncMock(return_value=mock_context)
        mock_pw.chromium.launch = AsyncMock(return_value=mock_browser)
        mock_pw_ctx.return_value.__aenter__.return_value = mock_pw

        tasks = await scraper.fetch_new_tasks()
        assert len(tasks) == 2
        assert tasks[0].external_id == "1658408"


@pytest.mark.asyncio
async def test_freelancehunt_scraper_playwright_fallback() -> None:
    """Verifies that Playwright failure falls back to standalone HTTP request."""
    scraper = FreelancehuntScraper()

    with patch("playwright.async_api.async_playwright") as mock_pw_ctx, \
         patch.object(scraper, "_fetch_with_standalone_httpx", new_callable=AsyncMock) as mock_fallback:
        mock_pw = AsyncMock()
        mock_pw.chromium.launch.side_effect = RuntimeError("Playwright failed")
        mock_pw_ctx.return_value.__aenter__.return_value = mock_pw
        mock_fallback.return_value = []

        tasks = await scraper.fetch_new_tasks()
        assert tasks == []
        mock_fallback.assert_awaited_once()


# ============================================================================
# FL.ru Scraper Tests
# ============================================================================


def test_fl_ru_scraper_parse_html() -> None:
    """Verifies FL.ru HTML parsing logic."""
    scraper = FlRuScraper()
    tasks = scraper.parse_html(SAMPLE_FL_RU_HTML)

    assert len(tasks) == 2

    # Task 1: Fixed price
    t1 = tasks[0]
    assert t1.source == "web:fl_ru"
    assert t1.external_id == "100500"
    assert t1.title == "Парсер данных для маркетплейса"
    assert t1.url == "https://www.fl.ru/projects/100500/parser-dannyh.html"
    assert t1.budget == "35 000 руб."
    assert t1.author == "Максим Заказчик"
    assert "спарсить каталог товаров" in t1.text

    # Task 2: Negotiated price
    t2 = tasks[1]
    assert t2.source == "web:fl_ru"
    assert t2.external_id == "100501"
    assert t2.budget is None
    assert "приема заявок" in t2.text


def test_fl_ru_scraper_maintenance_page_graceful() -> None:
    """Verifies FL.ru scraper gracefully returns empty list on maintenance pages."""
    scraper = FlRuScraper()
    tasks = scraper.parse_html(SAMPLE_FL_RU_MAINTENANCE_HTML)
    assert tasks == []


@pytest.mark.asyncio
async def test_fl_ru_scraper_httpx_fetch() -> None:
    """Verifies FL.ru fetch_new_tasks with mock httpx client."""
    def mock_handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(status_code=200, text=SAMPLE_FL_RU_HTML)

    transport = httpx.MockTransport(mock_handler)
    async with httpx.AsyncClient(transport=transport) as client:
        scraper = FlRuScraper(client=client)
        tasks = await scraper.fetch_new_tasks()
        assert len(tasks) == 2
        assert tasks[0].external_id == "100500"

    # Network error handling
    def error_handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(status_code=500, text="Internal Server Error")

    err_transport = httpx.MockTransport(error_handler)
    async with httpx.AsyncClient(transport=err_transport) as client:
        scraper_err = FlRuScraper(client=client)
        tasks_err = await scraper_err.fetch_new_tasks()
        assert tasks_err == []


@pytest.mark.asyncio
async def test_fl_ru_scraper_playwright_success() -> None:
    """Verifies FL.ru fetch_new_tasks with Playwright."""
    scraper = FlRuScraper()

    with patch("playwright.async_api.async_playwright") as mock_pw_ctx:
        mock_pw = AsyncMock()
        mock_browser = AsyncMock()
        mock_context = AsyncMock()
        mock_page = AsyncMock()

        mock_page.content = AsyncMock(return_value=SAMPLE_FL_RU_HTML)
        mock_context.new_page = AsyncMock(return_value=mock_page)
        mock_browser.new_context = AsyncMock(return_value=mock_context)
        mock_pw.chromium.launch = AsyncMock(return_value=mock_browser)
        mock_pw_ctx.return_value.__aenter__.return_value = mock_pw

        tasks = await scraper.fetch_new_tasks()
        assert len(tasks) == 2
        assert tasks[0].external_id == "100500"


# ============================================================================
# Habr Freelance Backward Compatibility Tests
# ============================================================================


@pytest.mark.asyncio
async def test_habr_freelance_scraper_parse_html() -> None:
    """Verifies HabrFreelanceScraper parsing logic on sample HTML."""
    scraper = HabrFreelanceScraper()
    tasks = scraper.parse_html(SAMPLE_HABR_HTML)

    assert len(tasks) == 2
    t1 = tasks[0]
    assert t1.source == "web:habr_freelance"
    assert t1.external_id == "591100"
    assert t1.title == "Парсер цен для интернет-магазина на Python"
    assert t1.url == "https://freelance.habr.com/tasks/591100"
    assert t1.budget == "25 000 руб."
    assert t1.author == "Алексей Заказчик"
    assert "Excel" in t1.text

    await scraper.close()


# ============================================================================
# Web Scraper Scheduler Tests
# ============================================================================


def test_web_scraper_scheduler_default_scrapers_list() -> None:
    """Verifies default scrapers are Kwork, Freelancehunt, and FL.ru without Habr."""
    scheduler = WebScraperScheduler()
    scraper_classes = [s.__class__.__name__ for s in scheduler.scrapers]

    assert "KworkPlaywrightScraper" in scraper_classes
    assert "FreelancehuntScraper" in scraper_classes
    assert "FlRuScraper" in scraper_classes
    assert "HabrFreelanceScraper" not in scraper_classes


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

    all_tasks = await scheduler.run_once()
    assert len(all_tasks) == 2
    assert callback_mock.await_count == 2
    callback_mock.assert_any_await(mock_task1)
    callback_mock.assert_any_await(mock_task2)

    with patch("asyncio.sleep", new_callable=AsyncMock) as mock_sleep:
        mock_sleep.side_effect = [None, Exception("Stop loop")]

        await scheduler.start()
        assert scheduler.is_running is True

        await scheduler.stop()
        assert scheduler.is_running is False


@pytest.mark.asyncio
async def test_web_scraper_scheduler_resilience_to_scraper_errors() -> None:
    """Verifies that an exception in one scraper does not halt others."""
    failing_scraper = AsyncMock()
    failing_scraper.fetch_new_tasks.side_effect = RuntimeError("Scraper crashed")

    working_scraper = AsyncMock()
    task = RawTask(
        source="web:kwork",
        external_id="10",
        title="Задача",
        text="Описание",
        url="https://kwork.ru/projects/10",
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
