"""Unit tests for SQLite persistent database and ProcessedLead model."""

from pathlib import Path
import pytest

from backend.db.database import Database
from backend.db.models import ProcessedLead


@pytest.mark.asyncio
async def test_db_init_and_schema_creation(tmp_path: Path) -> None:
    """Verifies that init_db creates the directory, database file, and schema."""
    db_file = tmp_path / "subdir" / "test_sniper.db"
    db = Database(db_path=str(db_file))

    assert not db_file.exists()
    await db.init_db()
    assert db_file.exists()

    await db.close()


@pytest.mark.asyncio
async def test_db_save_and_retrieve_lead(tmp_path: Path) -> None:
    """Verifies saving a lead with full metadata."""
    db_file = tmp_path / "leads.db"
    db = Database(db_path=str(db_file))
    await db.init_db()

    lead: ProcessedLead = await db.save_lead(
        source="web:habr_freelance",
        external_id="12345",
        content_hash="abcde1234567890",
        raw_text="Требуется разработать Telegram-бота на Python",
        url="https://freelance.habr.com/tasks/12345",
        is_lead=True,
        title="Разработка Telegram-бота",
        summary="Бот на Python для заказов",
        budget="30 000 руб",
        tech_stack="Python, Telethon, SQLite",
    )

    assert lead.id is not None
    assert lead.id > 0
    assert lead.source == "web:habr_freelance"
    assert lead.external_id == "12345"
    assert lead.content_hash == "abcde1234567890"
    assert lead.is_lead is True
    assert lead.budget == "30 000 руб"
    assert "ProcessedLead" in repr(lead)

    await db.close()


@pytest.mark.asyncio
async def test_db_deduplication_by_id_and_content_hash(tmp_path: Path) -> None:
    """Verifies duplicate detection by (source, external_id) and cross-source content_hash."""
    db_file = tmp_path / "dedup.db"
    db = Database(db_path=str(db_file))
    await db.init_db()

    # Initially not processed
    is_seen_before = await db.is_already_processed(
        source="telegram:-100123",
        external_id="555",
        content_hash="hash_alpha",
    )
    assert is_seen_before is False

    # Save initial lead
    await db.save_lead(
        source="telegram:-100123",
        external_id="555",
        content_hash="hash_alpha",
        raw_text="Написать парсер данных",
        url="https://t.me/c/123/555",
        is_lead=True,
    )

    # 1. Duplicate check by exact source + external_id
    dup_by_coord = await db.is_already_processed(
        source="telegram:-100123",
        external_id="555",
        content_hash="completely_different_hash",
    )
    assert dup_by_coord is True

    # 2. Duplicate check by content_hash (cross-posted to another channel or web)
    dup_by_hash = await db.is_already_processed(
        source="web:kwork",
        external_id="999",
        content_hash="hash_alpha",
    )
    assert dup_by_hash is True

    # 3. Completely new task should return False
    brand_new = await db.is_already_processed(
        source="web:kwork",
        external_id="1000",
        content_hash="hash_beta",
    )
    assert brand_new is False

    await db.close()


@pytest.mark.asyncio
async def test_db_persistence_across_restarts(tmp_path: Path) -> None:
    """Verifies that leads persist across restart of Database instances."""
    db_file = tmp_path / "restarts.db"

    # Instance 1: init and save
    db1 = Database(db_path=str(db_file))
    await db1.init_db()
    await db1.save_lead(
        source="web:kwork",
        external_id="777",
        content_hash="hash_gamma",
        raw_text="Разработка скрипта парсинга",
        url="https://kwork.ru/projects/777",
        is_lead=True,
    )
    await db1.close()

    # Instance 2: new instance pointing to same file
    db2 = Database(db_path=str(db_file))
    await db2.init_db()

    is_persisted = await db2.is_already_processed(
        source="web:kwork",
        external_id="777",
        content_hash="hash_gamma",
    )
    assert is_persisted is True

    await db2.close()
