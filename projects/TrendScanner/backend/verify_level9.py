"""End-to-end verification script for Level 9: Bulletproof Translation, Trend Database, and Auto-Growing Radar."""

import asyncio
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from app.db.database import init_db, get_db_connection
from app.db.dao import SourcesDAO, TrendsDAO
from app.services.groq_client import groq_client
from app.services.sanitizer import sanitizer, extract_candidate_sources


async def test_translation():
    print("=== 1. Testing Bulletproof Translation (deep-translator) ===")
    sample_en = (
        "SaaS boilerplate for solo founders building AI agents. "
        "Automated recurring billing with Stripe, authentication via Supabase, and pre-built React components. "
        "Generating $12,000 MRR in month three."
    )
    
    translated = await groq_client.translate_to_russian(sample_en)
    print(f"Original text:\n{sample_en}\n")
    print(f"Translated Russian text:\n{translated}\n")
    assert len(translated) > 0
    assert any(c in translated for c in "абвгдеёжзийклмнопрстуфхцчшщъыьэюяАБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ"), "Translation must contain Cyrillic!"
    print("✓ Translation test PASSED!\n")


def test_inbox_zero_and_database():
    print("=== 2. Testing Database 'is_new' and Inbox Zero ===")
    init_db()
    
    with get_db_connection() as conn:
        conn.execute("DELETE FROM trends WHERE original_text LIKE 'Микро-SaaS для автоматизации юридических документов%' OR original_text LIKE 'B2B AI-ассистент для службы поддержки интернет-магазинов%'")
        conn.execute("DELETE FROM sources WHERE url LIKE 'https://example.com/feed_level9%'")
    
    src_id = SourcesDAO.create("Test Radar Source Level 9", "https://example.com/feed_level9", "rss", is_active=True)
    
    t1 = TrendsDAO.create(
        source_id=src_id,
        original_text="Микро-SaaS для автоматизации юридических документов",
        is_trend=True,
        trend_name="Юридический Micro-SaaS",
        ai_score=8,
        scam_probability=5,
        ai_summary="Платформа генерации договоров для малого бизнеса.",
        is_new=True,
        is_liked=False,
    )
    
    t2_liked = TrendsDAO.create(
        source_id=src_id,
        original_text="B2B AI-ассистент для службы поддержки интернет-магазинов",
        is_trend=True,
        trend_name="AI-поддержка e-commerce",
        ai_score=9,
        scam_probability=0,
        ai_summary="Чат-бот с RAG по базе знаний магазина.",
        is_new=True,
        is_liked=True,
    )
    
    print(f"Created Inbox trends: t1={t1} (unliked), t2_liked={t2_liked} (liked)")
    
    inbox_trends = TrendsDAO.get_trends(tab="inbox")
    inbox_ids = [t["id"] for t in inbox_trends]
    assert t1 in inbox_ids, "t1 must be in Inbox"
    assert t2_liked not in inbox_ids, "t2_liked must not be in Inbox"
    print("✓ Inbox correctly contains only new unliked trends.")
    
    archived_count = TrendsDAO.archive_previous_inbox()
    print(f"Archived {archived_count} trends from Inbox to Trend Database.")
    
    db_trends = TrendsDAO.get_trends(tab="database")
    db_ids = [t["id"] for t in db_trends]
    assert t1 in db_ids, "t1 must now be in Trend Database (is_new=0)!"
    
    inbox_after = TrendsDAO.get_trends(tab="inbox")
    inbox_after_ids = [t["id"] for t in inbox_after]
    assert t1 not in inbox_after_ids, "t1 must no longer be in Inbox!"
    
    search_res = TrendsDAO.get_trends(tab="database", search_query="юридических")
    assert any(t["id"] == t1 for t in search_res), "Search in Database must find t1!"
    print("✓ Trend Database search and archiving PASSED!\n")


def test_auto_discovery():
    print("=== 3. Testing Auto-Growing Radar (Link Extraction) ===")
    sample_post = """
    Check out these amazing resources for indie hackers:
    1. Telegram channel: https://t.me/indie_hackers_daily
    2. Substack publication: https://theprompting.substack.com/p/ai-trends
    3. Medium article: https://medium.com/@techguru/scaling-fastapi
    4. Discussion on Hacker News: https://news.ycombinator.com/item?id=99887766
    """
    
    candidates = extract_candidate_sources(sample_post)
    print(f"Extracted {len(candidates)} candidate sources:")
    for c in candidates:
        print(f"  - [{c['source_type']}] {c['name']} -> {c['url']}")
        
    assert len(candidates) >= 4, "Must extract all 4 candidate sources!"
    urls = [c["url"] for c in candidates]
    assert "https://t.me/indie_hackers_daily" in urls
    assert any("substack.com" in u for u in urls)
    assert any("medium.com" in u for u in urls)
    
    for c in candidates:
        if not SourcesDAO.exists_by_url(c["url"]):
            sid = SourcesDAO.create(c["name"], c["url"], c["source_type"], is_active=True)
            print(f"  ✓ Auto-registered source #{sid}: {c['name']}")
            
    auto_sources = [s for s in SourcesDAO.get_all() if s["source_type"] == "auto_discovered"]
    assert len(auto_sources) > 0, "Auto-discovered sources must be present in DB!"
    print("✓ Auto-Growing Radar PASSED!\n")


async def main():
    await test_translation()
    test_inbox_zero_and_database()
    test_auto_discovery()
    print("🎉 ALL LEVEL 9 VERIFICATIONS PASSED SUCCESSFULLY! 🎉")


if __name__ == "__main__":
    asyncio.run(main())
