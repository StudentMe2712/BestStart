"""End-to-end verification script for Level 10: RLHF Feedback Loop, Trend Database & Auto-Radar."""

import asyncio
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from app.db.database import init_db, get_db_connection
from app.db.dao import SourcesDAO, TrendsDAO
from app.services.groq_client import groq_client, get_rlhf_context_prompt
from app.services.sanitizer import extract_candidate_sources


def test_rlhf_feedback_and_inbox_zero():
    print("=== 1. Testing RLHF Feedback (Likes & Dislikes) & Inbox Zero ===")
    init_db()

    with get_db_connection() as conn:
        conn.execute("DELETE FROM trends WHERE original_text IN (?, ?, ?)", (
            "Новый инструмент автоматизации маркетинга для B2B SaaS",
            "Крипто-канал с сигналами и пампами 100x gem",
            "Старый неотвеченный тренд из прошлого скана"
        ))
        conn.execute("DELETE FROM sources WHERE url = 'https://example.com/rlhf_feed'")

    src_id = SourcesDAO.create("RLHF Test Source", "https://example.com/rlhf_feed", "rss", is_active=True)

    # 1. Create a neutral item in Inbox
    t_inbox = TrendsDAO.create(
        source_id=src_id,
        original_text="Новый инструмент автоматизации маркетинга для B2B SaaS",
        is_trend=True,
        trend_name="B2B Marketing Automation",
        ai_score=8,
        is_new=True,
        user_feedback=0,
    )

    # 2. Verify it is in Inbox
    inbox = TrendsDAO.get_trends(tab="inbox")
    assert any(t["id"] == t_inbox for t in inbox), "t_inbox must be present in Inbox"
    print(f"✓ Trend #{t_inbox} is present in Inbox (is_new=1, user_feedback=0).")

    # 3. Apply Like (+1) -> Must move to Liked and leave Inbox
    TrendsDAO.set_feedback(t_inbox, 1)
    inbox_after_like = TrendsDAO.get_trends(tab="inbox")
    liked_after_like = TrendsDAO.get_trends(tab="liked")
    assert not any(t["id"] == t_inbox for t in inbox_after_like), "t_inbox must leave Inbox after Like!"
    assert any(t["id"] == t_inbox for t in liked_after_like), "t_inbox must appear in Liked after Like!"
    print(f"✓ Like (+1) verified: Trend #{t_inbox} moved to Liked and left Inbox.")

    # 4. Create another item and apply Dislike (-1) -> Must leave Inbox
    t_dislike = TrendsDAO.create(
        source_id=src_id,
        original_text="Крипто-канал с сигналами и пампами 100x gem",
        is_trend=False,
        trend_name="Crypto Pump Spam",
        ai_score=2,
        is_new=True,
        user_feedback=0,
    )
    inbox_before_dislike = TrendsDAO.get_trends(tab="inbox")
    assert any(t["id"] == t_dislike for t in inbox_before_dislike)

    TrendsDAO.set_feedback(t_dislike, -1)
    inbox_after_dislike = TrendsDAO.get_trends(tab="inbox")
    disliked_list = TrendsDAO.get_trends(tab="disliked")
    assert not any(t["id"] == t_dislike for t in inbox_after_dislike), "t_dislike must leave Inbox after Dislike!"
    assert any(t["id"] == t_dislike for t in disliked_list), "t_dislike must appear in Disliked list!"
    print(f"✓ Dislike (-1) verified: Trend #{t_dislike} left Inbox and recorded as penalty.")

    # 5. Archive unrated inbox -> moves to Database
    t_archive_candidate = TrendsDAO.create(
        source_id=src_id,
        original_text="Старый неотвеченный тренд из прошлого скана",
        is_trend=True,
        trend_name="Old Unrated Trend",
        ai_score=6,
        is_new=True,
        user_feedback=0,
    )
    archived = TrendsDAO.archive_previous_inbox()
    print(f"✓ Archived {archived} unrated items to Trend Database.")
    db_items = TrendsDAO.get_trends(tab="database")
    assert any(t["id"] == t_archive_candidate for t in db_items), "Unrated trend must be in Database (is_new=0)!"
    print("✓ Inbox Zero & Trend Database archiving PASSED!\n")


def test_rlhf_context_injection():
    print("=== 2. Testing Dynamic RLHF Context Injection for Groq ===")
    examples = TrendsDAO.get_rlhf_examples(limit_positive=2, limit_negative=2)
    print(f"Fetched RLHF examples: {len(examples['positive'])} positive, {len(examples['negative'])} negative.")
    assert len(examples["positive"]) > 0, "Must have at least 1 positive example"
    assert len(examples["negative"]) > 0, "Must have at least 1 negative example"

    prompt_context = get_rlhf_context_prompt()
    print("Generated RLHF context block:")
    print("-" * 50)
    print(prompt_context)
    print("-" * 50)
    assert "ТЕБЕ ДОСТУПЕН ОПЫТ ПОЛЬЗОВАТЕЛЯ" in prompt_context
    assert "хороших трендов" in prompt_context
    assert "мусора" in prompt_context
    print("✓ Dynamic RLHF Context Injection PASSED!\n")


async def test_auto_discovery_and_translation():
    print("=== 3. Testing Auto-Radar & Translation ===")
    sample_text = (
        "Here is a curated list of AI founders and newsletters: "
        "https://t.me/ai_founders_radar and https://technews.substack.com/p/trends"
    )
    sources = extract_candidate_sources(sample_text)
    print(f"Extracted {len(sources)} auto-discovered sources from text:")
    for s in sources:
        print(f"  - {s['name']} -> {s['url']}")
    assert len(sources) >= 2

    # Test Google Translator
    translated = await groq_client.translate_to_russian("Micro-SaaS ideas for independent developers using AI.")
    print(f"Translated sample: '{translated}'")
    assert any(c in translated for c in "абвгдеёжзийклмнопрстуфхцчшщъыьэюяАБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ")
    print("✓ Auto-Radar & Translation PASSED!\n")


async def main():
    test_rlhf_feedback_and_inbox_zero()
    test_rlhf_context_injection()
    await test_auto_discovery_and_translation()
    print("🎉 ALL LEVEL 10 VERIFICATIONS PASSED 100%! 🎉")


if __name__ == "__main__":
    asyncio.run(main())
