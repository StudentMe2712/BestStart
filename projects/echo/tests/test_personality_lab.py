"""Tests for the personality_lab mode (offline — no API calls)."""
from __future__ import annotations

import asyncio

import pytest

from eval import personality_lab as lab


def test_cluster_merges_near_duplicates_and_counts():
    messages = [
        "Как прошёл день?",
        "Как прошёл твой день?",   # near-duplicate of the first
        "Как прошёл день сегодня?",  # near-duplicate of the first
        "Что нового у тебя?",
        "Воды попей.",
    ]
    patterns = lab.cluster_messages(messages, threshold=0.5)

    # Three distinct clusters: the "как прошёл день" family, "что нового", "воды".
    assert len(patterns) == 3
    top = patterns[0]
    assert top.count == 3
    assert top.variants == 3
    # Sorted by count descending.
    assert [p.count for p in patterns] == [3, 1, 1]


def test_cluster_representative_is_most_frequent_surface_form():
    # All four share enough words to land in one cluster; the exact form "Воды попей."
    # appears three times, so it must win as the representative.
    messages = ["Воды попей.", "Воды попей.", "Воды немного попей.", "Воды попей."]
    patterns = lab.cluster_messages(messages, threshold=0.5)

    assert len(patterns) == 1
    assert patterns[0].text == "Воды попей."
    assert patterns[0].count == 4
    assert patterns[0].variants == 2


def test_cluster_empty_input():
    assert lab.cluster_messages([], threshold=0.5) == []


def test_build_markdown_has_english_role_headers_and_patterns_section():
    results = {role: [f"{role} message"] for role in lab.ROLE_ORDER}
    md = lab.build_markdown(results, top_n=20, threshold=0.5)

    # Every role gets an English H1 section, in the requested order.
    for header in ("# Friend", "# Presence", "# Coach", "# Mentor", "# Challenger", "# Philosopher"):
        assert header in md
    assert md.index("# Friend") < md.index("# Presence") < md.index("# Coach")
    # No scoring / judge language leaks into the report.
    assert "score" not in md.lower()
    assert "judge" not in md.lower()
    # Patterns section is present with per-role subsections.
    assert "# Паттерны" in md
    assert "## Friend" in md


def test_generate_for_role_returns_exact_count(monkeypatch):
    async def fake_generate(settings, role_key, window, recent, temperature=0.9):
        return (f"{role_key}-{window.key}", "template")

    monkeypatch.setattr(lab, "generate_message", fake_generate)

    msgs = asyncio.run(
        lab.generate_for_role(None, "friend", 100, concurrency=8, temperature=0.9)
    )
    assert len(msgs) == 100
    assert all(isinstance(m, str) and m for m in msgs)
    # Windows are rotated, so the batch is not a single repeated string.
    assert len(set(msgs)) > 1
