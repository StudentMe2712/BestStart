"""Generator tests: template fallback and preference weight bounds."""
import asyncio
import random

from app import db
from app.generator import _pick_template, generate_message
from app.roles import WINDOWS
from app.templates import TEMPLATES, VERIFIED_QUOTES


def test_falls_back_to_template_without_api_keys(settings):
    text, source = asyncio.run(generate_message(settings, "mentor", WINDOWS[0], []))
    assert source == "template"
    assert text in TEMPLATES["mentor"]


def test_philosopher_without_keys_yields_verified_quote_or_observation(settings):
    # No keys -> the model path returns nothing, so every philosopher message is either a
    # hand-checked verified quote (served verbatim) or an observation template. The model
    # never invents a quote.
    random.seed(0)
    sources = set()
    for _ in range(40):
        text, source = asyncio.run(generate_message(settings, "philosopher", WINDOWS[0], []))
        sources.add(source)
        if source == "verified-quote":
            assert text in VERIFIED_QUOTES
        else:
            assert source == "template" and text in TEMPLATES["philosopher"]
    assert "verified-quote" in sources and "template" in sources


def test_pick_template_returns_from_bank_even_when_all_recent():
    bank = TEMPLATES["coach"]
    pick = _pick_template("coach", recent=list(bank))
    assert pick in bank


def test_adjust_weight_respects_bounds(fresh_db):
    for _ in range(60):
        db.adjust_weight("mentor", 1.2)
    assert db.get_roles()["mentor"]["weight"] <= 3.0

    for _ in range(60):
        db.adjust_weight("mentor", 0.7)
    assert db.get_roles()["mentor"]["weight"] >= 0.2
