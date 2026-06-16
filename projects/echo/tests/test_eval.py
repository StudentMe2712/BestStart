"""Character benchmark tests: catalog integrity, penalty direction, dry-run plumbing."""
import asyncio

from app.roles import ROLES
from eval.judge import JudgeScore
from eval.penalties import scan
from eval.report import build
from eval.runner import run
from eval.scenarios import CATALOG, REACTION, proactive_scenarios


def test_catalog_is_well_formed():
    assert len(CATALOG) >= 25
    seen = set()
    for s in CATALOG:
        assert s.id not in seen, f"duplicate id {s.id}"
        seen.add(s.id)
        assert s.role in ROLES, f"{s.id}: unknown role {s.role}"
        assert s.good and s.bad, f"{s.id}: needs good and bad exemplars"
        if s.kind == REACTION:
            assert s.user_input, f"{s.id}: reaction needs user_input"
        else:
            assert s.window, f"{s.id}: proactive needs a window"


def test_every_register_is_covered_by_a_proactive_scenario():
    covered = {s.role for s in proactive_scenarios()}
    assert covered == set(ROLES), f"missing registers: {set(ROLES) - covered}"


def test_penalty_catches_aphorism_exemplars_and_clears_good_ones():
    # An obviously-bad aphorism opener is flagged...
    assert scan("Иногда ожидание делает встречу ценнее.", "friend").hits
    # ...and a natural friend reply is clean.
    clean = scan("О, круто 🙂 Какие планы?", "friend")
    assert clean.passed and not clean.hits


def test_judge_composite_inverts_bad_axes():
    # Perfect human reply: high good axes, zero bad axes -> 10.
    best = JudgeScore(10, 10, 0, 0, 0, 10)
    assert best.composite == 10.0
    # Pure coach: low good axes, max bad axes -> 0.
    worst = JudgeScore(0, 0, 10, 10, 10, 0)
    assert worst.composite == 0.0


def test_dry_run_produces_template_candidates_and_a_report(settings):
    # No API keys in test settings -> dry-run path: proactive template generation only.
    result = asyncio.run(run(settings, samples=2, use_llm=False, limit=None))
    assert result.mode == "dry-run"
    assert result.exemplars.bad_total > 0 and result.exemplars.good_total > 0
    # Reaction scenarios are skipped without a live model; proactive ones yield templates.
    assert all(c.kind != REACTION for c in result.candidates)
    assert result.candidates, "proactive template generation should produce candidates"
    md = build(result, samples=2, temps=(0.0,))
    assert "Echo Character Benchmark" in md and "Самопроверка оценщика" in md
