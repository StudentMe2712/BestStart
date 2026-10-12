"""Unit tests for web scrapers (Upwork, Weblancer, Freelancehunt, FL.ru, Habr) and scraper scheduler."""

from unittest.mock import AsyncMock, patch

import httpx
import pytest

from backend.scrapers.base import RawTask
from backend.scrapers.fl_ru import FlRuScraper
from backend.scrapers.freelancehunt import FreelancehuntScraper
from backend.scrapers.habr_freelance import HabrFreelanceScraper
from backend.scrapers.scheduler import WebScraperScheduler
from backend.scrapers.upwork_rss import UpworkRssScraper
from backend.scrapers.weblancer import WeblancerScraper

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

SAMPLE_UPWORK_RSS = """<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0">
  <channel>
    <title>Upwork Job Search - python</title>
    <link>https://www.upwork.com/ab/feed/jobs/rss?q=python&amp;sort=recency</link>
    <description>Jobs matching query: python</description>
    <item>
      <title>Python Developer for Telegram Bot - Upwork</title>
      <link>https://www.upwork.com/jobs/Python-Developer-for-Telegram-Bot_~01abc123def456/</link>
      <description><![CDATA[Looking for an experienced Python developer to build a Telegram bot.<br /><br /><b>Hourly Range</b>: $25.00-$50.00<br /><b>Posted On</b>: October 12, 2026 10:00 UTC<br /><b>Category</b>: Scripting &amp; Automation<br /><b>Skills</b>: Python, Telegram API]]></description>
      <guid isPermaLink="true">https://www.upwork.com/jobs/Python-Developer-for-Telegram-Bot_~01abc123def456/</guid>
      <pubDate>Mon, 12 Oct 2026 10:00:00 +0000</pubDate>
    </item>
    <item>
      <title>Web Scraping Automation Script</title>
      <link>https://www.upwork.com/jobs/~01xyz987654321/</link>
      <description><![CDATA[Need a script to scrape product prices from an ecommerce website.<br /><br /><b>Budget</b>: $500<br /><b>Posted On</b>: October 12, 2026 09:30 UTC<br /><b>Category</b>: Web Scraping]]></description>
      <guid isPermaLink="false">~01xyz987654321</guid>
      <pubDate>Mon, 12 Oct 2026 09:30:00 +0000</pubDate>
    </item>
  </channel>
</rss>
"""

SAMPLE_WEBLANCER_HTML = """<!DOCTYPE html>
<html>
<body>
    <div class="container-fluid cols_table show_visited">
        <div class="row click_container">
            <div class="col-sm-7">
                <h2 class="title">
                    <a href="/jobs/veb-programmirovanie-31/telegram-bot-dlya-zakazov-123456/">Telegram бот для заказов</a>
                </h2>
                <div class="text_field text-muted">
                    Необходимо разработать бота на Python aiogram с интеграцией Google Таблиц.
                </div>
                <div class="user-info">
                    <a href="/users/client123/">Иван Заказчик</a>
                </div>
            </div>
            <div class="col-sm-2 amount title">
                150 $
            </div>
        </div>
        <div class="row click_container">
            <div class="col-sm-7">
                <h2 class="title">
                    <a href="/jobs/skript-parsera-123457/">Скрипт парсера цен</a>
                </h2>
                <div class="text_field text-muted">
                    Парсер каталога товаров с маркетплейсов.
                </div>
                <div class="user-info">
                    <a href="/users/petr/">Петр</a>
                </div>
            </div>
            <div class="col-sm-2 amount title">
                По договоренности
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
        source="web:upwork",
        external_id="101",
        title="Скрипт парсинга",
        text="Описание задачи",
        url="https://www.upwork.com/jobs/~01abc",
        budget="$500",
        author="Client",
    )
    assert task.source == "web:upwork"
    assert task.external_id == "101"
    assert task.budget == "$500"
    assert task.author == "Client"


# ============================================================================
# Upwork RSS Scraper Tests
# ============================================================================


def test_upwork_rss_scraper_parse_rss() -> None:
    """Verifies Upwork RSS parsing logic for fixed budget and hourly range."""
    scraper = UpworkRssScraper()
    tasks = scraper.parse_rss(SAMPLE_UPWORK_RSS)

    assert len(tasks) == 2

    # Task 1: Hourly rate
    t1 = tasks[0]
    assert t1.source == "web:upwork"
    assert t1.external_id == "~01abc123def456"
    assert t1.title == "Python Developer for Telegram Bot"
    assert "https://www.upwork.com/jobs/Python-Developer-for-Telegram-Bot_~01abc123def456/" in t1.url
    assert t1.budget == "$25.00-$50.00"
    assert "Looking for an experienced Python developer" in t1.text
    assert "<br />" not in t1.text  # HTML stripped

    # Task 2: Fixed budget
    t2 = tasks[1]
    assert t2.source == "web:upwork"
    assert t2.external_id == "~01xyz987654321"
    assert t2.title == "Web Scraping Automation Script"
    assert t2.budget == "$500"
    assert "Need a script to scrape product prices" in t2.text


def test_upwork_rss_scraper_parse_rss_empty_or_malformed() -> None:
    """Verifies Upwork RSS scraper returns empty list on invalid or empty XML."""
    scraper = UpworkRssScraper()
    assert scraper.parse_rss("") == []
    assert scraper.parse_rss("   ") == []
    assert scraper.parse_rss("<not-xml>something</invalid>") == []


@pytest.mark.asyncio
async def test_upwork_rss_scraper_httpx_fetch() -> None:
    """Verifies Upwork fetch_new_tasks with mock httpx client and deduplication."""
    def mock_handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(status_code=200, text=SAMPLE_UPWORK_RSS)

    transport = httpx.MockTransport(mock_handler)
    async with httpx.AsyncClient(transport=transport) as client:
        # Pass 2 feeds returning identical items to test deduplication
        scraper = UpworkRssScraper(
            feed_urls=["https://www.upwork.com/feed1", "https://www.upwork.com/feed2"],
            client=client,
        )
        tasks = await scraper.fetch_new_tasks()
        # Should be deduplicated by external_id
        assert len(tasks) == 2
        assert tasks[0].external_id == "~01abc123def456"
        assert tasks[1].external_id == "~01xyz987654321"

    # Network error handling
    def error_handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(status_code=500, text="Internal Server Error")

    err_transport = httpx.MockTransport(error_handler)
    async with httpx.AsyncClient(transport=err_transport) as client:
        scraper_err = UpworkRssScraper(client=client)
        tasks_err = await scraper_err.fetch_new_tasks()
        assert tasks_err == []


# ============================================================================
# Weblancer Scraper Tests
# ============================================================================


def test_weblancer_scraper_parse_html() -> None:
    """Verifies Weblancer HTML parsing logic for project cards."""
    scraper = WeblancerScraper()
    tasks = scraper.parse_html(SAMPLE_WEBLANCER_HTML)

    assert len(tasks) == 2

    # Task 1: Fixed price
    t1 = tasks[0]
    assert t1.source == "web:weblancer"
    assert t1.external_id == "123456"
    assert t1.title == "Telegram бот для заказов"
    assert t1.url == "https://www.weblancer.net/jobs/veb-programmirovanie-31/telegram-bot-dlya-zakazov-123456/"
    assert t1.budget == "150 $"
    assert t1.author == "Иван Заказчик"
    assert "aiogram" in t1.text

    # Task 2: Negotiated price
    t2 = tasks[1]
    assert t2.source == "web:weblancer"
    assert t2.external_id == "123457"
    assert t2.title == "Скрипт парсера цен"
    assert t2.url == "https://www.weblancer.net/jobs/skript-parsera-123457/"
    assert t2.budget is None
    assert t2.author == "Петр"
    assert "маркетплейсов" in t2.text


def test_weblancer_scraper_parse_html_empty() -> None:
    """Verifies Weblancer scraper returns empty list on invalid or empty HTML."""
    scraper = WeblancerScraper()
    assert scraper.parse_html("") == []
    assert scraper.parse_html("<html><body><p>No jobs</p></body></html>") == []


@pytest.mark.asyncio
async def test_weblancer_scraper_httpx_fetch() -> None:
    """Verifies Weblancer fetch_new_tasks with mock httpx client."""
    def mock_handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(status_code=200, text=SAMPLE_WEBLANCER_HTML)

    transport = httpx.MockTransport(mock_handler)
    async with httpx.AsyncClient(transport=transport) as client:
        scraper = WeblancerScraper(client=client)
        tasks = await scraper.fetch_new_tasks()
        assert len(tasks) == 2
        assert tasks[0].external_id == "123456"

    # Network error handling
    def error_handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(status_code=500, text="Internal Server Error")

    err_transport = httpx.MockTransport(error_handler)
    async with httpx.AsyncClient(transport=err_transport) as client:
        scraper_err = WeblancerScraper(client=client)
        tasks_err = await scraper_err.fetch_new_tasks()
        assert tasks_err == []


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
    """Verifies default scrapers are Freelancehunt, Upwork, Weblancer, and FL.ru without Habr or Kwork."""
    scheduler = WebScraperScheduler()
    scraper_classes = [s.__class__.__name__ for s in scheduler.scrapers]

    assert "FreelancehuntScraper" in scraper_classes
    assert "UpworkRssScraper" in scraper_classes
    assert "WeblancerScraper" in scraper_classes
    assert "FlRuScraper" in scraper_classes
    assert "KworkPlaywrightScraper" not in scraper_classes
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
        source="web:upwork",
        external_id="10",
        title="Задача",
        text="Описание",
        url="https://www.upwork.com/jobs/~01xyz",
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
