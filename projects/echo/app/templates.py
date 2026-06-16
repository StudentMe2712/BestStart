"""Curated message bank per role.

This is the always-available fallback when no LLM backend answers (or its output
fails the quality gate). Character Bible V2: Echo is a close friend who writes first.
Friend (curiosity about the user's life) is the default register; everything is short,
addressed to *him*, and never an aphorism or motivational line.
"""
from __future__ import annotations

TEMPLATES: dict[str, list[str]] = {
    # Friend — the main register: curiosity about his life, minimum morality.
    "friend": [
        "Как вообще день?",
        "Что хорошего сегодня было?",
        "Как ты? Просто интересно, без повода.",
        "Чем сейчас занят?",
        "Как настроение?",
        "Что-нибудь интересное за день?",
        "Поел нормально?",
        "Как сам, держишься?",
        "Что нового у тебя?",
    ],
    # Trainer — short, present-moment body nudges. No stats, no streaks, no habits.
    "coach": [
        "Подъём 🙂",
        "Воды.",
        "Турник сегодня будет?",
        "Разомни плечи.",
        "Встань на минуту, потянись.",
        "Отойди от экрана, посмотри в окно.",
        "Сделай пару подходов и возвращайся.",
        "Выпей воды и подыши.",
    ],
    # Mentor — concrete questions about today's real work. No abstractions.
    "mentor": [
        "Что сегодня самое важное?",
        "Есть что-то зависшее?",
        "Что реально закроешь до обеда?",
        "Над чем сейчас сидишь?",
        "Что главное на сегодня?",
        "Что-нибудь застряло, мешает?",
        "Какую одну вещь точно стоит добить сегодня?",
    ],
    # Challenger — reactive only (not scheduled). Used via /now and as reaction seeds.
    "challenger": [
        "Ты уверен, что проблема именно там?",
        "А если вообще это не делать — что будет?",
        "Что будет, если оставить как есть?",
        "Точно сейчас об этом стоит думать?",
    ],
}


def templates_for(role_key: str) -> list[str]:
    return TEMPLATES.get(role_key, [])
