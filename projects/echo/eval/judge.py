"""LLM-as-judge — the rich half of the benchmark (character.md "Автоматическая оценка").

A second LLM rates each reply 0–10 on six axes and flags red flags. Higher is better for
Humanity / Curiosity / Bible Alignment; higher is *worse* for Coachiness / Genericness /
Philosophy Spam — so the composite inverts those three. Returns None when no API key is
configured (the runner then stays on the deterministic layer only).
"""
from __future__ import annotations

import json
import re
from dataclasses import dataclass, field

from app import llm
from app.config import Settings

# Axes where a high score is bad; the composite flips them.
INVERTED_AXES = ("coachiness", "genericness", "philosophy_spam")
AXES = ("humanity", "curiosity", "bible_alignment", *INVERTED_AXES)

RED_FLAGS = (
    "коучинг", "инфоцыганство", "мотивационный мусор", "абстрактная философия",
    "вопрос без контекста", "искусственная эмпатия", "псевдонаблюдение о пользователе",
)

_JUDGE_SYSTEM = (
    "Ты — строгий оценщик личности бота Echo. Echo должен звучать как живой знакомый "
    "человек, которому интересна жизнь собеседника, а НЕ как коуч, ментор, мотиватор "
    "или генератор афоризмов. Даже аккуратный на вид вопрос — плохо, если он звучит "
    "как искусственный бот, а не как человек (например «Что из утреннего плана уже решил?»).\n"
    "Оцени ОДИН ответ Echo. Верни СТРОГО JSON без пояснений со шкалами 0–10:\n"
    "{\"humanity\": int, \"curiosity\": int, \"coachiness\": int, \"genericness\": int, "
    "\"philosophy_spam\": int, \"bible_alignment\": int, \"red_flags\": [строки из списка], "
    "\"comment\": \"кратко\"}\n"
    "humanity — написал бы так живой человек; curiosity — есть ли интерес к жизни "
    "пользователя; coachiness — насколько похоже на коуча/ментора (больше=хуже); "
    "genericness — подошёл бы любому (больше=хуже); philosophy_spam — псевдофилософия "
    "(больше=хуже); bible_alignment — соответствие характеру Echo.\n"
    f"red_flags — подмножество из: {', '.join(RED_FLAGS)}."
)


@dataclass(frozen=True)
class JudgeScore:
    humanity: int
    curiosity: int
    coachiness: int
    genericness: int
    philosophy_spam: int
    bible_alignment: int
    red_flags: tuple[str, ...] = ()
    comment: str = ""

    @property
    def composite(self) -> float:
        """0–10 character score: good axes as-is, bad axes inverted, then averaged."""
        good = self.humanity + self.curiosity + self.bible_alignment
        bad = (10 - self.coachiness) + (10 - self.genericness) + (10 - self.philosophy_spam)
        return round((good + bad) / 6, 2)


def _clamp(v: object) -> int:
    try:
        return max(0, min(10, int(v)))
    except (TypeError, ValueError):
        return 0


def _parse(raw: str) -> JudgeScore | None:
    match = re.search(r"\{.*\}", raw, re.DOTALL)
    if not match:
        return None
    try:
        data = json.loads(match.group(0))
    except json.JSONDecodeError:
        return None
    flags = tuple(f for f in data.get("red_flags", []) if isinstance(f, str))
    return JudgeScore(
        humanity=_clamp(data.get("humanity")),
        curiosity=_clamp(data.get("curiosity")),
        coachiness=_clamp(data.get("coachiness")),
        genericness=_clamp(data.get("genericness")),
        philosophy_spam=_clamp(data.get("philosophy_spam")),
        bible_alignment=_clamp(data.get("bible_alignment")),
        red_flags=flags,
        comment=str(data.get("comment", ""))[:200],
    )


def _providers(settings: Settings) -> list[tuple[str, str, str]]:
    chain: list[tuple[str, str, str]] = []
    if settings.groq_api_key:
        chain.append((settings.groq_base_url, settings.groq_api_key, settings.groq_model))
    if settings.openrouter_api_key:
        chain.append((settings.openrouter_base_url, settings.openrouter_api_key, settings.openrouter_model))
    return chain


async def judge(settings: Settings, context: str, reply: str) -> JudgeScore | None:
    """Score one reply. ``context`` describes the scenario (user line or register)."""
    user = f"Контекст: {context}\nОтвет Echo: «{reply}»\nОцени этот ответ."
    for base_url, api_key, model in _providers(settings):
        raw = await llm.chat(
            base_url=base_url, api_key=api_key, model=model,
            system=_JUDGE_SYSTEM, user=user, temperature=0.0, title="Echo judge",
        )
        if raw:
            parsed = _parse(raw)
            if parsed is not None:
                return parsed
    return None
