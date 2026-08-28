"""Unit and integration tests for DeepResearchService and Obsidian Vault generation."""

import os
import tempfile
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch
import pytest
import httpx

from app.core.settings import Settings, settings
from app.db.dao import SourcesDAO, TrendsDAO
from app.db.database import init_db
from app.services.deep_research import (
    COMPETITOR_ANALYSIS_SYSTEM_PROMPT,
    DeepResearchService,
    _clean_ddg_url,
    _get_fallback_competitors,
    deep_research_service,
    extract_wikilinks,
    run_deep_research,
    sanitize_vault_filename,
)
from services.deep_research import (
    DeepResearchService as FacadeDeepResearchService,
    run_deep_research as facade_run_deep_research,
)


# ============================================================================
# Test Fixtures
# ============================================================================


@pytest.fixture
def isolated_db(monkeypatch):
    """Provide a clean isolated temporary SQLite database for DAO operations."""
    with tempfile.TemporaryDirectory() as tmpdir:
        temp_db = os.path.join(tmpdir, "test_deep_research.db")
        monkeypatch.setattr(settings, "DATABASE_PATH", temp_db)
        init_db(seed_default_sources=True)
        yield temp_db


@pytest.fixture
def temp_vault_dir(tmp_path):
    """Provide a clean temporary directory as Obsidian Vault root."""
    vault = tmp_path / "Test_Vault"
    vault.mkdir(parents=True, exist_ok=True)
    return str(vault)


# ============================================================================
# Unit Tests: Filename Sanitization & Wikilinks Extraction
# ============================================================================


def test_sanitize_vault_filename_normal():
    """Verify standard trend names are preserved cleanly."""
    assert sanitize_vault_filename("AI Cold Email Bot") == "AI Cold Email Bot"
    assert sanitize_vault_filename("Micro-SaaS for Clinics") == "Micro-SaaS for Clinics"


def test_sanitize_vault_filename_illegal_characters():
    """Verify illegal filesystem characters (Windows/Unix) are stripped safely."""
    raw = 'B2B / SaaS: <Super*Tool> | Fast? "Yes" & Safe\\Path'
    clean = sanitize_vault_filename(raw)
    for bad_char in ['/', '\\', '*', '?', ':', '"', '<', '>', '|']:
        assert bad_char not in clean
    assert "B2B SaaS SuperTool Fast Yes & SafePath" in clean


def test_sanitize_vault_filename_empty_and_whitespace():
    """Verify empty or whitespace strings fallback to unnamed_trend."""
    assert sanitize_vault_filename("") == "unnamed_trend"
    assert sanitize_vault_filename("   \n\t  ") == "unnamed_trend"
    assert sanitize_vault_filename(None) == "unnamed_trend"


def test_sanitize_vault_filename_max_length():
    """Verify long trend names are truncated to max length."""
    long_name = "A" * 120
    truncated = sanitize_vault_filename(long_name, max_length=50)
    assert len(truncated) <= 50


def test_extract_wikilinks_basic():
    """Verify extraction of Obsidian [[Wikilinks]] from markdown."""
    text = "Конкурент использует [[React]] и [[Stripe]] в нише [[B2B SaaS]]."
    links = extract_wikilinks(text)
    assert links == ["React", "Stripe", "B2B SaaS"]


def test_extract_wikilinks_with_aliases_and_duplicates():
    """Verify wikilinks with aliases and duplicates are parsed cleanly."""
    text = (
        "Использует [[PostgreSQL|Postgres DB]] и [[FastAPI]]. "
        "Также поддерживается [[React]] и снова [[FastAPI]] и [[PostgreSQL]]."
    )
    links = extract_wikilinks(text)
    assert links == ["PostgreSQL", "FastAPI", "React"]


def test_extract_wikilinks_empty():
    """Verify empty text or text without wikilinks returns empty list."""
    assert extract_wikilinks("") == []
    assert extract_wikilinks("Обычный текст без ссылок") == []
    assert extract_wikilinks(None) == []


def test_clean_ddg_url():
    """Verify extraction of destination URLs from DuckDuckGo redirect wrapper."""
    ddg_wrapper = "/l/?uddg=https%3A%2F%2Fexample.com%2Fpricing%3Fplan%3Dpro&rut=..."
    assert _clean_ddg_url(ddg_wrapper) == "https://example.com/pricing?plan=pro"
    assert _clean_ddg_url("https://directlink.com") == "https://directlink.com"
    assert _clean_ddg_url("//cdn.example.com") == "https://cdn.example.com"
    assert _clean_ddg_url("") == ""


# ============================================================================
# Unit Tests: Competitor Search (DuckDuckGo & Fallback)
# ============================================================================


@pytest.mark.asyncio
async def test_search_competitors_ddg_html_success():
    """Test search_competitors parses DuckDuckGo HTML results successfully."""
    service = DeepResearchService()

    fake_html = """
    <html>
      <body>
        <div class="result results_links results_links_deep web-result">
          <a class="result__a" href="/l/?uddg=https%3A%2F%2Fcompetitor1.io">Competitor One - AI Leads</a>
          <a class="result__snippet">Leading automated lead generator for B2B teams.</a>
        </div>
        <div class="result results_links results_links_deep web-result">
          <a class="result__a" href="https://competitor2.com">Competitor Two CRM</a>
          <a class="result__snippet">Simple CRM and cold outreach engine.</a>
        </div>
      </body>
    </html>
    """

    mock_resp = MagicMock(spec=httpx.Response)
    mock_resp.status_code = 200
    mock_resp.text = fake_html

    with patch("httpx.AsyncClient.post", new_callable=AsyncMock) as mock_post:
        mock_post.return_value = mock_resp
        results = await service.search_competitors(query="B2B Lead Finder", max_results=2)

    assert len(results) == 2
    assert results[0]["title"] == "Competitor One - AI Leads"
    assert results[0]["url"] == "https://competitor1.io"
    assert "Leading automated lead generator" in results[0]["snippet"]
    assert results[1]["title"] == "Competitor Two CRM"
    assert results[1]["url"] == "https://competitor2.com"


@pytest.mark.asyncio
async def test_search_competitors_ddg_lite_fallback():
    """Test search_competitors falls back to DDG Lite when HTML yields 0 items."""
    service = DeepResearchService()

    fake_lite_html = """
    <html>
      <body>
        <table>
          <tr>
            <td><a class="result-link" href="https://litecompetitor.com">Lite Comp</a></td>
          </tr>
          <tr>
            <td class="result-snippet">Lite snippet description of tool.</td>
          </tr>
        </table>
      </body>
    </html>
    """

    # First call (HTML) returns empty page, second call (Lite) returns result
    resp_empty = MagicMock(spec=httpx.Response)
    resp_empty.status_code = 200
    resp_empty.text = "<html><body>No results</body></html>"

    resp_lite = MagicMock(spec=httpx.Response)
    resp_lite.status_code = 200
    resp_lite.text = fake_lite_html

    with patch("httpx.AsyncClient.post", new_callable=AsyncMock) as mock_post:
        mock_post.side_effect = [resp_empty, resp_lite]
        results = await service.search_competitors(query="Telegram Bot SaaS", max_results=1)

    assert len(results) == 1
    assert results[0]["title"] == "Lite Comp"
    assert results[0]["url"] == "https://litecompetitor.com"
    assert results[0]["snippet"] == "Lite snippet description of tool."


@pytest.mark.asyncio
async def test_search_competitors_network_failure_uses_quality_fallback():
    """Test search_competitors returns high quality fallback results on network error."""
    service = DeepResearchService()

    with patch("httpx.AsyncClient.post", new_callable=AsyncMock) as mock_post:
        mock_post.side_effect = httpx.ConnectError("Network unreachable")
        results = await service.search_competitors(query="AI Voice Agent", max_results=5)

    assert len(results) == 5
    for item in results:
        assert "title" in item and item["title"]
        assert "snippet" in item and item["snippet"]
        assert "url" in item and item["url"]
    assert any("AI Voice" in r["title"] or "Voice" in r["title"] or "Leader" in r["title"] for r in results)


@pytest.mark.asyncio
async def test_search_competitors_empty_query():
    """Test search_competitors with empty query returns fallback without crashing."""
    service = DeepResearchService()
    results = await service.search_competitors(query="", max_results=3)
    assert len(results) == 3
    assert all("title" in r for r in results)


def test_get_fallback_competitors():
    """Verify heuristic fallback competitor generator."""
    items = _get_fallback_competitors("Invoice Parser AI", max_results=4)
    assert len(items) == 4
    assert all(isinstance(x, dict) for x in items)
    assert all("title" in x and "snippet" in x and "url" in x for x in items)


# ============================================================================
# Unit Tests: Competitor Analysis via Groq
# ============================================================================


@pytest.mark.asyncio
async def test_analyze_competitors_groq_success():
    """Test analyze_competitors formats prompt and parses Groq markdown response."""
    service = DeepResearchService()

    mock_markdown = """
### 🏢 1. Обзор ключевых конкурентов и продуктов
- **[[LeadGenius]]**: Платформа B2B лидогенерации на стеке [[React]] и [[PostgreSQL]].
- **[[Apollo.io]]**: База контактов и автоматизация аутрича через [[Stripe]] биллинг.

### 💵 2. Ценообразование и модели монетизации
- Тарифы от $49/мес за пользователя.

### ⚠️ 3. Слабые места
- Высокая цена для микро-бизнеса.

### 🚀 4. Рыночное окно возможностей
- Нишевый [[Micro-SaaS]] с интеграцией в [[Telegram]].
""".strip()

    snippets = [
        {"title": "LeadGenius", "snippet": "B2B lead prospecting tool", "url": "https://leadgenius.com"},
        {"title": "Apollo.io", "snippet": "Sales intelligence platform", "url": "https://apollo.io"},
    ]

    with patch("app.services.deep_research.groq_client._call_api_with_retry", new_callable=AsyncMock) as mock_groq:
        mock_groq.return_value = mock_markdown

        report = await service.analyze_competitors(
            trend_name="B2B Lead Finder",
            ai_summary="Сервис поиска целевых контактов.",
            snippets=snippets,
        )

        assert report == mock_markdown
        mock_groq.assert_called_once()

        # Check prompt args
        call_args, call_kwargs = mock_groq.call_args
        messages = call_args[0] if call_args else call_kwargs.get("messages")
        assert len(messages) == 2
        assert messages[0]["role"] == "system"
        assert messages[0]["content"] == COMPETITOR_ANALYSIS_SYSTEM_PROMPT
        assert "B2B Lead Finder" in messages[1]["content"]
        assert "LeadGenius" in messages[1]["content"]
        assert "Apollo.io" in messages[1]["content"]


@pytest.mark.asyncio
async def test_analyze_competitors_groq_failure_uses_fallback_markdown():
    """Test analyze_competitors falls back to structured Wikilink markdown on Groq failure."""
    service = DeepResearchService()

    snippets = [
        {"title": "TestComp", "snippet": "Test snippet description", "url": "https://test.com"},
    ]

    with patch("app.services.deep_research.groq_client._call_api_with_retry", new_callable=AsyncMock) as mock_groq:
        mock_groq.side_effect = RuntimeError("Groq API 503 Service Unavailable")

        report = await service.analyze_competitors(
            trend_name="AI Video Generator",
            ai_summary="Генерация видео для соцсетей.",
            snippets=snippets,
        )

        assert report is not None
        assert "### 🏢 1. Обзор ключевых конкурентов" in report
        assert "[[TestComp]]" in report
        assert "[[Stripe]]" in report
        assert "[[FastAPI]]" in report


# ============================================================================
# Unit Tests: Vault Note Generation
# ============================================================================


def test_generate_vault_note_structure(temp_vault_dir):
    """Test generate_vault_note creates valid Obsidian markdown note with frontmatter and wikilinks."""
    service = DeepResearchService()

    comp_analysis = """
### 🏢 1. Обзор ключевых конкурентов
- **[[Synthesia]]**: AI аватары на стеке [[React]].
- **[[HeyGen]]**: Видеогенерация с API через [[Stripe]].
""".strip()

    note_path = service.generate_vault_note(
        trend_id=42,
        trend_name="AI Video Avatars",
        ai_summary="Сервис создания видео-аватаров для обучения сотрудников.",
        ai_score=9,
        scam_probability=5,
        source_url="https://news.ycombinator.com/item?id=12345",
        competitor_analysis=comp_analysis,
        vault_dir=temp_vault_dir,
    )

    assert os.path.exists(note_path)
    assert note_path.endswith(os.path.join("02_Trends", "AI Video Avatars.md"))

    content = Path(note_path).read_text(encoding="utf-8")

    # 1. Frontmatter checks
    assert content.startswith("---\n")
    assert 'title: "AI Video Avatars"' in content
    assert "tags: [trend, ai-radar, market-research]" in content
    assert "created:" in content
    assert "ai_score: 9" in content
    assert "scam_probability: 5" in content
    assert 'source_url: "https://news.ycombinator.com/item?id=12345"' in content

    # 2. Body header and sections
    assert "# [[AI Video Avatars]]" in content
    assert "## 📌 Исходная выжимка тренда" in content
    assert "Сервис создания видео-аватаров для обучения сотрудников." in content
    assert "## 🔍 Анализ конкурентов и рыночный отчет (Deep Web Search)" in content
    assert "[[Synthesia]]" in content
    assert "[[HeyGen]]" in content

    # 3. Related entities section
    assert "## 🔗 Связанные сущности" in content
    assert "- [[Synthesia]]" in content
    assert "- [[React]]" in content
    assert "- [[HeyGen]]" in content
    assert "- [[Stripe]]" in content


def test_generate_vault_note_with_special_characters_in_title(temp_vault_dir):
    """Test generate_vault_note sanitizes filename when title contains quotes and slashes."""
    service = DeepResearchService()

    note_path = service.generate_vault_note(
        trend_id=99,
        trend_name='B2B / AI "Super-Bot": Fast?',
        ai_summary="Автоматизация Telegram каналов.",
        ai_score=8,
        scam_probability=10,
        source_url=None,
        competitor_analysis="Анализ конкурентов в нише [[Telegram]] ботов.",
        vault_dir=temp_vault_dir,
    )

    assert os.path.exists(note_path)
    file_name = Path(note_path).name
    assert "/" not in file_name
    assert '"' not in file_name
    assert "?" not in file_name
    assert ":" not in file_name

    content = Path(note_path).read_text(encoding="utf-8")
    assert 'source_url: ""' in content
    assert "## 🔗 Связанные сущности" in content
    assert "- [[Telegram]]" in content


# ============================================================================
# Integration Tests: run_deep_research End-to-End
# ============================================================================


@pytest.mark.asyncio
async def test_run_deep_research_end_to_end(isolated_db, temp_vault_dir):
    """Test full run_deep_research pipeline: DAO -> Search -> Groq -> Vault File -> DB Update."""
    source = SourcesDAO.get_all()[0]
    trend_id = TrendsDAO.create(
        source_id=source["id"],
        original_text="Micro-SaaS for cold outreach personalization on LinkedIn.",
        is_trend=True,
        trend_name="LinkedIn Outreach AI",
        ai_score=8,
        scam_probability=5,
        ai_summary="Генератор персонализированных сообщений для LinkedIn.",
        source_url="https://reddit.com/r/SaaS/123",
    )

    mock_comp_analysis = """
### 🏢 1. Обзор ключевых конкурентов
- **[[Dripify]]**: LinkedIn автоматизация на стеке [[React]].
- **[[Expandi]]**: Облачный комбайн с монетизацией через [[Stripe]].
""".strip()

    with patch.object(deep_research_service, "search_competitors", new_callable=AsyncMock) as mock_search, \
         patch.object(deep_research_service, "analyze_competitors", new_callable=AsyncMock) as mock_analyze:

        mock_search.return_value = [
            {"title": "Dripify", "snippet": "Dripify LinkedIn tool", "url": "https://dripify.io"},
            {"title": "Expandi", "snippet": "Expandi outreach", "url": "https://expandi.io"},
        ]
        mock_analyze.return_value = mock_comp_analysis

        result = await run_deep_research(
            trend_id=trend_id,
            db_path=isolated_db,
            vault_dir=temp_vault_dir,
        )

    assert result["success"] is True
    assert result["trend_id"] == trend_id
    assert result["trend_name"] == "LinkedIn Outreach AI"
    assert result["competitor_analysis"] == mock_comp_analysis
    assert len(result["snippets"]) == 2

    # Verify note was created on disk
    note_path = result["vault_note_path"]
    assert os.path.exists(note_path)
    file_content = Path(note_path).read_text(encoding="utf-8")
    assert "# [[LinkedIn Outreach AI]]" in file_content
    assert "[[Dripify]]" in file_content

    # Verify detailed_report in SQLite DB was updated
    trend_db = TrendsDAO.get_by_id(trend_id)
    assert trend_db is not None
    assert trend_db["detailed_report"] == mock_comp_analysis


@pytest.mark.asyncio
async def test_run_deep_research_invalid_trend_id(isolated_db, temp_vault_dir):
    """Test run_deep_research raises ValueError for non-existent trend ID."""
    with pytest.raises(ValueError, match="Trend with ID #99999 not found"):
        await run_deep_research(
            trend_id=99999,
            db_path=isolated_db,
            vault_dir=temp_vault_dir,
        )


# ============================================================================
# Facade & Settings Tests
# ============================================================================


def test_facade_imports_and_compatibility():
    """Verify backend/services/deep_research.py exposes identical interface."""
    assert FacadeDeepResearchService is DeepResearchService
    assert facade_run_deep_research is run_deep_research


def test_settings_vault_dir_default_and_override(tmp_path, monkeypatch):
    """Verify Settings handles VAULT_DIR default, local resolution, and env override."""
    # 1. Custom override via environment
    custom_vault = str(tmp_path / "My_Custom_Vault")
    monkeypatch.setenv("VAULT_DIR", custom_vault)
    app_settings = Settings(_env_file=None)
    assert app_settings.VAULT_DIR == custom_vault

    # 2. Default local fallback when not in docker
    monkeypatch.delenv("VAULT_DIR", raising=False)
    monkeypatch.delenv("RUNNING_IN_DOCKER", raising=False)
    default_settings = Settings(_env_file=None)
    assert "TrendScanner_Vault" in default_settings.VAULT_DIR or default_settings.VAULT_DIR == "/app/vault"
