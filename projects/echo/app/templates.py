"""Curated message bank per role.

This is the always-available fallback when no LLM backend answers (or its output
fails the quality gate). Character Bible V2: Echo is a close friend who writes first.
Friend (curiosity about the user's life) is the default register; everything is short,
addressed to *him*, and never an aphorism or motivational line. The Philosopher bank is
the one exception that quotes — and only real, attributed quotes, never pseudo-depth.
"""
from __future__ import annotations

TEMPLATES: dict[str, list[str]] = {
    # Friend — the main register: curiosity about his life, minimum morality.
    "friend": [
        "Как вообще день?",
        "Чем сейчас занят?",
        "Что хорошего сегодня было?",
        "Как настроение?",
        "Поел нормально?",
        "Какие планы на вечер?",
        "Как там пятница, ждёшь уже?",
        "Что нового у тебя?",
        "Как ты? Просто интересно, без повода.",
        "Как сам, держишься?",
        "Выходные как прошли?",
    ],
    # Trainer — short, present-moment body nudges. No stats, no streaks, no habits.
    "coach": [
        "Воды.",
        "Турник сегодня будет?",
        "Разомни плечи.",
        "Встань на пару минут.",
        "Отойди от монитора.",
        "Отжимания сегодня были?",
        "Подъём 🙂",
        "Сделай пару подходов и возвращайся.",
    ],
    # Mentor — concrete questions about today's real work. An experienced colleague.
    "mentor": [
        "Что сегодня самое важное?",
        "Есть зависшие задачи?",
        "Что реально стоит закрыть сегодня?",
        "Что тормозит работу?",
        "На чём застрял?",
        "Над чем сейчас сидишь?",
        "Какую одну вещь точно стоит добить сегодня?",
    ],
    # Challenger — rare, gentle, never aggressive. One uncomfortable question.
    "challenger": [
        "Ты уверен, что проблема именно там?",
        "А если ничего не менять?",
        "Что будет, если оставить всё как есть?",
        "Ты точно решаешь правильную проблему?",
    ],
    # Presence — ambient "I'm here" statements, never a question. Just a person nearby
    # noticing the moment (Bible V2: presence is often better than any question).
    "presence": [
        "Уже вечер.",
        "День какой-то длинный сегодня.",
        "Пятница всё ближе 🙂",
        "Тут бы сейчас кофе.",
        "Уже почти обед.",
        "Что-то сегодня жарко.",
        "Надеюсь, успел поесть.",
        "Тихо сегодня.",
    ],
    # Philosopher — rarest. Real attributed quotes or one short observation only.
    "philosopher": [
        "«Мы страдаем чаще в воображении, чем в реальности.» — Сенека. Как думаешь, правда?",
        "«Не вещи тревожат нас, а наши мнения о вещах.» — Эпиктет.",
        "«Я знаю, что ничего не знаю.» — Сократ.",
        "«Счастье зависит от нас самих.» — Аристотель.",
        "«Наша жизнь есть то, что мы думаем о ней.» — Марк Аврелий.",
        "«В одну реку нельзя войти дважды.» — Гераклит.",
        "«Знающий не говорит, говорящий не знает.» — Лао-цзы.",
        "«Рана — это место, через которое в тебя входит свет.» — Руми.",
        "«Начало — самая важная часть работы.» — Платон.",
        "Странно, как быстро человек привыкает к хорошему.",
    ],
}


def templates_for(role_key: str) -> list[str]:
    return TEMPLATES.get(role_key, [])
