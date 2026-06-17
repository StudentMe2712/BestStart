"""Life-facts store + auto-extraction tests (V4 rule №2). Offline — no real API calls."""
import asyncio
from datetime import datetime, timezone

from app import db, generator

_TS = "2026-06-17T12:00:00+00:00"


def test_add_and_list_fact(fresh_db):
    fid = db.add_fact("есть девушка, видится по пятницам", _TS)
    assert fid is not None
    facts = db.list_facts()
    assert [f["text"] for f in facts] == ["есть девушка, видится по пятницам"]


def test_add_fact_skips_blank_and_near_duplicates(fresh_db):
    assert db.add_fact("   ", _TS) is None
    db.add_fact("занимается спортом по утрам", _TS)
    # A near-duplicate is rejected; a genuinely different fact is stored.
    assert db.add_fact("занимается спортом по утрам", _TS) is None
    assert db.add_fact("учится на программиста", _TS) is not None
    assert len(db.list_facts()) == 2


def test_delete_fact(fresh_db):
    fid = db.add_fact("любит горы", _TS)
    assert db.delete_fact(fid) is True
    assert db.list_facts() == []
    assert db.delete_fact(fid) is False  # already gone


def test_fact_store_is_capped(fresh_db):
    for i in range(db.MAX_FACTS + 8):
        db.add_fact(f"хобби{i} занятие{i} вещь{i}", _TS)
    assert len(db.list_facts()) == db.MAX_FACTS


def test_minutes_since_role(fresh_db):
    now = datetime.now(timezone.utc)
    assert db.minutes_since_role("coach", now) is None
    db.log_message(now.isoformat(), "coach", "template", "Пора пройтись.", None)
    since = db.minutes_since_role("coach", now)
    assert since is not None and since >= 0


def test_parse_facts_extracts_json_array():
    assert generator._parse_facts('Вот факты: ["есть девушка", "спорт"]') == ["есть девушка", "спорт"]


def test_parse_facts_handles_garbage_and_empty():
    assert generator._parse_facts("никаких фактов") == []
    assert generator._parse_facts("[]") == []


def test_extract_is_noop_without_keys(fresh_db, settings):
    stored = asyncio.run(generator.extract_and_store_facts(settings, "что-то написал", _TS))
    assert stored == []
    assert db.fact_texts() == []


def test_extract_stores_parsed_facts(fresh_db, settings, monkeypatch):
    async def fake_chat(**kwargs):
        return '["есть девушка", "занимается спортом"]'

    monkeypatch.setattr(generator, "_providers", lambda s: [("llm-test", "u", "k", "m")])
    monkeypatch.setattr(generator.llm, "chat", fake_chat)

    stored = asyncio.run(generator.extract_and_store_facts(settings, "в пятницу к девушке еду", _TS))
    assert "есть девушка" in stored
    assert any("девушка" in f for f in db.fact_texts())
