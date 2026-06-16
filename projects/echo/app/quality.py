"""Message quality gate.

Every candidate message — LLM or template — passes through `check` before it can be
sent: length bounds, banality (clichés), basic toxicity, and repetition against what
was recently sent. The gate is intentionally simple and deterministic so it is easy
to test and reason about.
"""
from __future__ import annotations

import re

from .roles import ROLES

MIN_CHARS = 3
DEFAULT_MAX_CHARS = 200  # cap when the role is unknown; per-role caps are tighter
SIMILARITY_THRESHOLD = 0.55  # Jaccard over word sets; above this == "too similar"

# Clichés the product explicitly does not want ("motivational junk", quote-bank vibes).
BANNED_CLICHES = (
    "верь в себя",
    "поверь в себя",
    "всё получится",
    "все получится",
    "ты можешь всё",
    "ты можешь все",
    "не сдавайся",
    "будь собой",
    "живи здесь и сейчас",
    "всё будет хорошо",
    "все будет хорошо",
    "каждый день - это новый шанс",
    "каждый день — это новый шанс",
    "каждый день это шанс",
    "мысли позитивно",
    "выйди из зоны комфорта",
    # Character Bible V2 — abstract-wisdom / quote-generator tells.
    "ясность приходит",
    "посмотри на мир",
    "увидеть мир с другой стороны",
    "взглянуть на мир",
    "измени маршрут",
    "иногда жизнь",
    "иногда люди",
    "иногда именно",
    "новых знакомств",
    # Pseudo-depth the Philosopher must never produce (real attributed quotes only).
    "настоящая мудрость",
    "всё происходит не случайно",
    "все происходит не случайно",
    "в жизни бывает",
)

# Aphorism openers: a message that *starts* with one of these is almost always a
# generalisation addressed to no one in particular ("Иногда такие события..."). Echo's
# real registers (Friend/Trainer/Mentor/Challenger) never open this way.
APHORISM_OPENERS = ("иногда", "порой", "зачастую", "не всё", "не все ", "мы редко", "мы боимся")

# Minimal toxicity / manipulation guard. Not a content moderator — just a safety net
# against profanity, self-harm framing and shame-based pushing (spec §19.1).
BANNED_WORDS = (
    "идиот",
    "тупой",
    "ничтожество",
    "ненавиж",
    "убей себя",
    "сдохни",
    "никчёмн",
    "никчемн",
    "ты неудачник",
)

_WORD_RE = re.compile(r"[a-zA-Zа-яёА-ЯЁ0-9]+")


def normalize_words(text: str) -> set[str]:
    return {w.lower() for w in _WORD_RE.findall(text)}


def jaccard(a: set[str], b: set[str]) -> float:
    if not a or not b:
        return 0.0
    inter = len(a & b)
    union = len(a | b)
    return inter / union if union else 0.0


def is_too_similar(text: str, recent: list[str]) -> bool:
    words = normalize_words(text)
    for prev in recent:
        if jaccard(words, normalize_words(prev)) >= SIMILARITY_THRESHOLD:
            return True
    return False


def check(text: str, role_key: str, recent: list[str]) -> tuple[bool, str]:
    """Return (ok, reason). `reason` is "ok" when the message passes."""
    cleaned = text.strip()
    lowered = cleaned.lower()

    if len(cleaned) < MIN_CHARS:
        return False, "too_short"

    max_chars = ROLES[role_key].max_chars if role_key in ROLES else DEFAULT_MAX_CHARS
    if len(cleaned) > max_chars:
        return False, "too_long"

    for cliche in BANNED_CLICHES:
        if cliche in lowered:
            return False, "banal"

    if lowered.startswith(APHORISM_OPENERS):
        return False, "banal"

    for word in BANNED_WORDS:
        if word in lowered:
            return False, "toxic"

    if is_too_similar(cleaned, recent):
        return False, "repetitive"

    return True, "ok"
