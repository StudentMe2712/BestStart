"""Curated message bank per role.

This is the always-available fallback when no LLM backend answers (or its output fails the
quality gate). ECHO HUMANITY V3: every line must read like a real person who is simply
*around* — never motivation, coaching, self-help, a tracker or an assistant. Presence is the
backbone register. The Philosopher never quotes here — quotes come only from the verified,
hand-checked ``VERIFIED_QUOTES`` base; the model is never allowed to invent one.
"""
from __future__ import annotations

import random

TEMPLATES: dict[str, list[str]] = {
    # Presence — the primary register: just a person nearby noticing the moment. No question.
    "presence": [
        "Уже темно.",
        "День быстро пролетел.",
        "Тут бы чай.",
        "Пятница уже близко.",
        "Надеюсь, поел.",
        "Вечер какой-то длинный.",
        "Тихо сегодня.",
        "За окном серо.",
        "Дождь, кажется, собирается.",
        "Уже почти вечер.",
        "Кофе бы сейчас.",
        "Что-то притомился за день.",
        "Скоро выходные.",
        "Тёплый вечер сегодня.",
        "Свет к вечеру совсем жёлтый.",
    ],
    # Friend — around and noticing: a nudge, a callback, a comment. Not a daily survey.
    "friend": [
        "Что-то ты сегодня тихий.",
        "Слушай, а чем вечер занят?",
        "Кстати, как там та штука, про которую рассказывал?",
        "О, уже вечер.",
        "Ты вообще сегодня отдыхал?",
        "Опять весь день в делах, да?",
        "Давно тебя не слышно.",
        "Чувствую, день у тебя был плотный.",
    ],
    # Mentor — no invented work. Soft, general, only what the user could answer about today.
    "mentor": [
        "Что сегодня хочется закончить?",
        "Есть что-то, что давно откладываешь?",
        "Что бы тебя сегодня порадовало, если закроешь?",
        "Осталось что-то висящее на сегодня?",
    ],
    # Challenger — warm, on his side, a fresh angle each time; never the "do nothing" loop.
    "challenger": [
        "А вдруг дело вообще не в этом?",
        "Слушай, а если попроще к этому подойти?",
        "А что если посмотреть с другой стороны?",
        "Тебе это правда важно — или просто по привычке?",
        "Ты это проверял или пока кажется?",
    ],
    # Coach — rare, caring pause. Never water/stretch/tracker talk.
    "coach": [
        "Ты уже несколько часов в работе. Сделай паузу.",
        "Похоже, пора немного передохнуть.",
        "Может, выйдешь на воздух ненадолго?",
        "Ты с утра без перерыва. Отдохни чуть-чуть.",
        "Не сиди весь день, выберись куда-нибудь.",
    ],
    # Philosopher — own-words observations only. Verified quotes live in VERIFIED_QUOTES.
    "philosopher": [
        "Странно, как быстро человек привыкает даже к хорошему.",
        "Ожидание часто тяжелее самого дела.",
        "Чаще пугает не само дело, а мысли про него.",
        "Половина усталости — от незаконченного.",
        "Маленькое сделанное лучше большого задуманного.",
        "Иногда лучшее, что можно сделать, — просто выспаться.",
    ],
}

# Hand-checked, really attributed quotes — the ONLY quotes Echo is allowed to send. The
# model never generates these; the generator serves them verbatim from here.
VERIFIED_QUOTES: tuple[str, ...] = (
    "Мы страдаем чаще в воображении, чем в реальности. — Сенека",
    "Не вещи тревожат нас, а наши мнения о вещах. — Эпиктет",
    "Я знаю, что ничего не знаю. — Сократ",
    "В одну реку нельзя войти дважды. — Гераклит",
    "Начало — половина дела. — Платон",
    "Путь в тысячу ли начинается с одного шага. — Лао-цзы",
    "Счастье зависит от нас самих. — Аристотель",
    "Если хочешь быть счастливым — будь им. — Козьма Прутков",
)


def templates_for(role_key: str) -> list[str]:
    return TEMPLATES.get(role_key, [])


def random_verified_quote() -> str:
    return random.choice(VERIFIED_QUOTES)
