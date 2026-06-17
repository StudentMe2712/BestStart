"""Curated message bank per role.

This is the always-available fallback when no LLM backend answers (or its output fails the
quality gate). ECHO HUMANITY V4: every line must read like a real person who is simply
*around* — never motivation, coaching, self-help, a tracker or an assistant. Presence is the
backbone register. Friend is split into sub-mode banks so the offline fallback keeps questions
rare. The Philosopher never lets the model quote — verified quotes *and* hand-checked facts
come only from the curated bases below, served verbatim; the model writes plain observations.
"""
from __future__ import annotations

import random

# Presence — the primary register: just a person nearby noticing the moment. No question.
_PRESENCE = [
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
    "Что-то день сегодня суматошный.",
    "Время как-то быстро идёт.",
    "Надеюсь, всё нормально.",
]

# Friend (V4) splits into sub-modes so the fallback honours "questions are rare": mostly
# comments, occasionally a question, rarely a joke. The memory mode has no static bank — it
# needs real life-facts, so offline it falls back to comments.
_FRIEND_COMMENT = [
    "Что-то ты сегодня тихий.",
    "О, уже вечер.",
    "Опять весь день в делах, да?",
    "Давно тебя не слышно.",
    "Чувствую, день у тебя был плотный.",
    "Похоже, сегодня без передышки совсем.",
    "Ну ты и трудяга сегодня.",
    "Тихий ты какой-то с утра.",
]
_FRIEND_QUESTION = [
    "Слушай, а чем вечер занят?",
    "Ты вообще сегодня отдыхал?",
    "А это правда важно — или просто по привычке?",
    "А вдруг дело вообще не в этом?",
    "Не пора уже закругляться на сегодня?",
]
_FRIEND_JOKE = [
    "Тут бы кофе и ничего не делать.",
    "Понедельники явно придумал какой-то злодей.",
    "Сегодня официально день ничегонеделания, я решил.",
    "Кажется, диван по тебе скучает.",
]
FRIEND_BY_MODE: dict[str, list[str]] = {
    "comment": _FRIEND_COMMENT,
    "question": _FRIEND_QUESTION,
    "joke": _FRIEND_JOKE,
    "memory": _FRIEND_COMMENT,  # no facts offline -> degrade to a plain comment
}

# Coach (V4) — a movement nudge during work hours: get up, walk, pull-up bar. Never
# water/stretch/warm-up/tracker talk (the quality gate also blocks those phrases).
_COACH = [
    "Пора пройтись.",
    "Турник сегодня будет?",
    "Засиделся наверное.",
    "Пошли немного подвигаемся.",
    "Спина живая? 🙂",
    "Ты уже несколько часов в кресле. Встань, пройдись.",
    "Выберись на воздух ненадолго.",
]

# Philosopher — own-words observations only. Verified quotes/facts live in the bases below.
_PHILOSOPHER = [
    "Странно, как быстро человек привыкает даже к хорошему.",
    "Ожидание часто тяжелее самого дела.",
    "Чаще пугает не само дело, а мысли про него.",
    "Половина усталости — от незаконченного.",
    "Маленькое сделанное лучше большого задуманного.",
    "Иногда лучшее, что можно сделать, — просто выспаться.",
]

TEMPLATES: dict[str, list[str]] = {
    "presence": _PRESENCE,
    "friend": [*_FRIEND_COMMENT, *_FRIEND_QUESTION, *_FRIEND_JOKE],
    "coach": _COACH,
    "philosopher": _PHILOSOPHER,
}

# Hand-checked, really attributed quotes — the model never generates these; the generator
# serves them verbatim. Real attribution only.
VERIFIED_QUOTES: tuple[str, ...] = (
    "Мы страдаем чаще в воображении, чем в реальности. — Сенека",
    "Не вещи тревожат нас, а наши мнения о вещах. — Эпиктет",
    "Я знаю, что ничего не знаю. — Сократ",
    "В одну реку нельзя войти дважды. — Гераклит",
    "Начало — половина дела. — Платон",
    "Путь в тысячу ли начинается с одного шага. — Лао-цзы",
    "Счастье зависит от нас самих. — Аристотель",
    "Пока мы откладываем жизнь, она проходит. — Сенека",
)

# Hand-checked, interesting facts about real people / history (V4 rule №6). Served verbatim
# like quotes — the model must never invent these (hallucination risk).
VERIFIED_FACTS: tuple[str, ...] = (
    "Леонардо да Винчи вёл списки из сотен вопросов, на которые хотел найти ответ.",
    "Марк Аврелий вёл записи для себя, а не для читателей — так появились «Размышления».",
    "Дарвин почти двадцать лет не решался опубликовать свою теорию.",
    "Эйнштейн говорил, что у него нет особого таланта — только страстное любопытство.",
    "Фейнман любил повторять, что проще всего обмануть самого себя.",
    "Менделеев увидел свою таблицу во сне — но перед этим годами над ней работал.",
)

# The verbatim pool the Philosopher draws from when it serves a curated line instead of a
# model observation.
VERIFIED: tuple[str, ...] = VERIFIED_QUOTES + VERIFIED_FACTS


def templates_for(role_key: str) -> list[str]:
    return TEMPLATES.get(role_key, [])


def friend_templates(mode: str) -> list[str]:
    """Fallback bank for a given Friend sub-mode (memory degrades to comments offline)."""
    return FRIEND_BY_MODE.get(mode, _FRIEND_COMMENT)


def random_verified() -> str:
    """A hand-checked quote or interesting fact, served verbatim (model never invents these)."""
    return random.choice(VERIFIED)
