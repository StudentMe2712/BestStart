"""Deterministic penalty layer — the free, instant half of the benchmark.

Reuses the production quality gate (``app.quality``) so the benchmark measures exactly
what ships, then adds the two extra opener tells listed in character.md's "Автоматические
штрафы" (``важно помнить…`` / ``каждый день…``). This layer only catches *phrase* tells;
subtle coachiness / generic-ness is the LLM judge's job (see judge.py).
"""
from __future__ import annotations

from dataclasses import dataclass

from app.quality import APHORISM_OPENERS, BANNED_CLICHES, BANNED_WORDS, check

# character.md adds these as automatic penalties; they are openers (penalise only at the
# start of a line) to avoid false-positives on legit questions like "тренируешься каждый день?".
EXTRA_OPENERS = ("важно помнить", "каждый день", "настоящая сила", "успех приходит")


@dataclass(frozen=True)
class PenaltyResult:
    passed: bool                 # would the production gate let this through?
    gate_reason: str             # "ok" | too_short | too_long | banal | toxic | repetitive
    hits: tuple[str, ...]        # the specific banned phrases / openers that matched


def scan(text: str, role_key: str = "friend", recent: list[str] | None = None) -> PenaltyResult:
    """Run the production gate + collect every phrase tell that fired (for the ban-list report)."""
    lowered = text.strip().lower()
    hits: list[str] = []

    for cliche in BANNED_CLICHES:
        if cliche in lowered:
            hits.append(cliche)
    for opener in APHORISM_OPENERS:
        if lowered.startswith(opener):
            hits.append(f"^{opener}")
    for opener in EXTRA_OPENERS:
        if lowered.startswith(opener):
            hits.append(f"^{opener}")
    for word in BANNED_WORDS:
        if word in lowered:
            hits.append(word)

    ok, reason = check(text, role_key, recent or [])
    return PenaltyResult(passed=ok, gate_reason=reason, hits=tuple(hits))
