"""Runner — generates replies for each scenario and scores them.

Dry run (no API / ``use_llm=False``) does two zero-cost things:
  1. Evaluator self-check: run the deterministic penalty layer over every labeled
     good/bad exemplar in the catalog → proves the scorer points the right way.
  2. Template smoke: generate proactive replies via the template fallback and score them.

Real run (``use_llm=True`` with keys) additionally generates reaction + proactive replies
across the sample/temperature sweep and adds the LLM judge.
"""
from __future__ import annotations

from dataclasses import dataclass

from app.config import Settings
from app.generator import generate_followup, generate_message
from app.roles import WINDOWS, Window

from . import judge as judge_mod
from .penalties import PenaltyResult, scan
from .scenarios import CATALOG, REACTION, Scenario

_WINDOW_BY_KEY = {w.key: w for w in WINDOWS}


@dataclass
class Candidate:
    scenario_id: str
    category: str
    role: str
    kind: str
    source: str
    reply: str
    temperature: float
    penalty: PenaltyResult
    judge: judge_mod.JudgeScore | None = None


@dataclass
class ExemplarCheck:
    good_total: int = 0
    good_flagged: int = 0          # false positives: a "good" line the gate would reject
    bad_total: int = 0
    bad_caught: int = 0            # true positives: a "bad" line the deterministic layer flagged


@dataclass
class RunResult:
    mode: str                      # "dry-run" | "llm-judged"
    candidates: list[Candidate]
    exemplars: ExemplarCheck


def _context(s: Scenario) -> str:
    if s.kind == REACTION and s.user_input:
        return f"пользователь написал: «{s.user_input}»"
    return f"проактивное сообщение в роли «{s.role}», время суток: {s.window}"


def _check_exemplars(catalog: tuple[Scenario, ...]) -> ExemplarCheck:
    chk = ExemplarCheck()
    for s in catalog:
        for line in s.good:
            chk.good_total += 1
            r = scan(line, s.role)
            if r.hits or not r.passed:
                chk.good_flagged += 1
        for line in s.bad:
            chk.bad_total += 1
            r = scan(line, s.role)
            if r.hits or not r.passed:
                chk.bad_caught += 1
    return chk


async def _generate(settings: Settings, s: Scenario, temp: float) -> tuple[str, str] | None:
    """Produce one reply for a scenario, or None if nothing came back."""
    if s.kind == REACTION and s.user_input:
        text = await generate_followup(settings, s.user_input, temperature=temp)
        return (text, "llm-followup") if text else None
    window: Window = _WINDOW_BY_KEY.get(s.window or "day", WINDOWS[2])
    text, source = await generate_message(settings, s.role, window, [], temperature=temp)
    return (text, source)


async def run(
    settings: Settings, *, samples: int = 5, temps: tuple[float, ...] = (0.7, 0.9),
    use_llm: bool = True, limit: int | None = None,
) -> RunResult:
    has_keys = bool(settings.groq_api_key or settings.openrouter_api_key)
    live = use_llm and has_keys
    catalog = CATALOG[:limit] if limit else CATALOG

    exemplars = _check_exemplars(catalog)
    candidates: list[Candidate] = []

    for s in catalog:
        # Reaction replies need a live model (no template fallback for follow-ups).
        if s.kind == REACTION and not live:
            continue
        sweep = temps if live else (0.0,)
        for temp in sweep:
            for _ in range(samples):
                produced = await _generate(settings, s, temp)
                if produced is None:
                    continue
                reply, source = produced
                penalty = scan(reply, s.role)
                score = await judge_mod.judge(settings, _context(s), reply) if live else None
                candidates.append(Candidate(
                    scenario_id=s.id, category=s.category, role=s.role, kind=s.kind,
                    source=source, reply=reply, temperature=temp, penalty=penalty, judge=score,
                ))

    return RunResult(
        mode="llm-judged" if live else "dry-run",
        candidates=candidates,
        exemplars=exemplars,
    )
