"""Tests for humanity_lab scoring/aggregation (offline — no API calls)."""
from __future__ import annotations

from eval import humanity_lab as hl
from eval.humanity_lab import HumanVerdict, Scored


def test_repetition_scores_high_for_duplicates():
    msgs = ["Уже вечер.", "Уже вечер.", "Совсем другое сообщение про кофе утром"]
    reps = hl.repetition_scores(msgs)
    assert reps[0] == 10 and reps[1] == 10
    assert reps[2] < 10


def test_repetition_single_message_is_zero():
    assert hl.repetition_scores(["одно"]) == [0]


def test_judge_parse_handles_russian_daily():
    v = hl._parse('{"human": 9, "bot": 1, "annoying": 2, "daily": "да", "why": "живо"}')
    assert v is not None and v.human == 9 and v.bot == 1 and v.daily is True


def test_judge_parse_clamps_and_false_daily():
    v = hl._parse('{"human": 99, "bot": -3, "annoying": 5, "daily": false}')
    assert v is not None and v.human == 10 and v.bot == 0 and v.daily is False


def test_judge_parse_rejects_non_json():
    assert hl._parse("no json here") is None


def test_summarize_counts_fails_and_skips_unjudged():
    scored = [
        Scored("friend", "a", "llm", 2, HumanVerdict(9, 1, 1, True)),
        Scored("friend", "b", "llm", 4, HumanVerdict(3, 8, 7, False)),
        Scored("friend", "c", "template", 0, None),  # unjudged -> excluded from human avg
    ]
    s = hl.summarize(scored)
    assert s.n == 3 and s.judged == 2 and s.fails == 1
    assert s.human == 6.0  # (9 + 3) / 2
    assert s.fail_pct == 50.0


def test_weighted_human_favours_high_weight_roles():
    # Presence (weight 3.0) high, Philosopher (weight 0.6) low → weighted mean sits near
    # Presence, well above the flat mean of the two.
    by_role = {
        "presence": [Scored("presence", "уже вечер", "llm", 0, HumanVerdict(9, 1, 1, True))],
        "philosopher": [Scored("philosopher", "мысль", "llm", 0, HumanVerdict(5, 5, 4, False))],
    }
    weighted = hl.weighted_human(by_role)
    flat = (9 + 5) / 2
    assert weighted > flat  # 3.0*9 + 0.6*5 over 3.6 ≈ 8.33 > 7.0


def test_build_markdown_reports_human_metric():
    by_role = {"friend": [Scored("friend", "что-то ты тихий", "llm", 0, HumanVerdict(9, 1, 1, True))]}
    md = hl.build_markdown(by_role)
    assert "Echo Humanity Lab" in md
    assert "Human Score" in md
    assert "## По ролям" in md
