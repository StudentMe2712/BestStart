"""Generator tests: template fallback, Friend sub-modes, Philosopher verbatim, weight bounds."""
import asyncio
import random
from collections import Counter

from app import db, generator
from app.generator import _friend_form_and_sub, _pick_template, generate_message
from app.roles import WINDOWS, pick_friend_mode
from app.templates import TEMPLATES, VERIFIED


def test_falls_back_to_template_without_api_keys(settings):
    text, source = asyncio.run(generate_message(settings, "friend", WINDOWS[0], []))
    assert source == "template"
    assert text in TEMPLATES["friend"]


def test_philosopher_without_keys_yields_verbatim_or_observation(settings):
    # No keys -> the model path returns nothing, so every philosopher message is either a
    # hand-checked verified quote/fact (served verbatim) or an observation template. The model
    # never invents a quote or a fact.
    random.seed(0)
    sources = set()
    for _ in range(40):
        text, source = asyncio.run(generate_message(settings, "philosopher", WINDOWS[0], []))
        sources.add(source)
        if source == "verified":
            assert text in VERIFIED
        else:
            assert source == "template" and text in TEMPLATES["philosopher"]
    assert "verified" in sources and "template" in sources


def test_pick_template_returns_from_bank_even_when_all_recent():
    bank = TEMPLATES["coach"]
    pick = _pick_template("coach", recent=list(bank))
    assert pick in bank


def test_friend_memory_mode_injects_facts(monkeypatch):
    monkeypatch.setattr(generator, "pick_friend_mode", lambda *a, **k: "memory")
    form, sub = _friend_form_and_sub(["есть девушка, видится по пятницам"])
    assert sub == "memory"
    assert "есть девушка" in form  # the stored fact is woven into the prompt


def test_friend_memory_falls_back_to_comment_without_facts(monkeypatch):
    monkeypatch.setattr(generator, "pick_friend_mode", lambda *a, **k: "memory")
    _form, sub = _friend_form_and_sub([])
    assert sub == "comment"  # no facts -> degrade to a plain comment


def test_friend_mode_distribution_favours_comments_over_questions():
    rng = random.Random(0)
    counts = Counter(pick_friend_mode(rng) for _ in range(3000))
    # Target shares: comment 40 > memory 30 > question 20 > joke 10.
    assert counts["comment"] > counts["memory"] > counts["question"] > counts["joke"]


def test_adjust_weight_respects_bounds(fresh_db):
    for _ in range(60):
        db.adjust_weight("friend", 1.2)
    assert db.get_roles()["friend"]["weight"] <= 3.0

    for _ in range(60):
        db.adjust_weight("friend", 0.7)
    assert db.get_roles()["friend"]["weight"] >= 0.2
