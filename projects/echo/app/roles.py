"""Role archetypes for Echo.

Echo is one person, not a panel of bots: a close friend who writes through the day.
Roles are *registers* of that one personality. Their default weights encode the target
share of messages (Friend ~35%, Trainer ~17%, Presence ~13%, Mentor ~13%, Challenger
~13%, Philosopher ~9%). Presence is the Bible V2 register that just *is there* — a short
ambient statement, never a question. Windows bias which register is more likely at a given
time of day; cadence (how often Echo writes at all) lives in config + scheduler, not here.
"""
from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class RoleSpec:
    key: str
    name: str          # russian display name (shown in /mode, /stats)
    persona: str       # system-prompt fragment describing the voice
    length_hint: str   # length instruction handed to the model
    max_chars: int     # hard cap enforced by the quality gate
    default_weight: float
    scheduled: bool = True  # False -> never auto-sent; reacts/triggers manually only
    asks_question: bool = True  # False -> a plain statement (Presence), prompt won't push a question


# Weights are proportional to the target daily share (sum ~8.55): friend 35% / coach 18%
# / presence 13% / mentor 13% / challenger 13% / philosopher 9%. friend sits at the
# adjust_weight cap; presence is friend-adjacent, so it dilutes friend's share, not the rest.
ROLES: dict[str, RoleSpec] = {
    "friend": RoleSpec(
        "friend", "Друг",
        "близкий друг, которому искренне интересна его жизнь: спрашиваешь, как он, "
        "что нового, как прошло, какие планы; радуешься за него; без морали и советов",
        "одна короткая фраза или вопрос, часто одно предложение", 160, 3.0,
    ),
    "coach": RoleSpec(
        "coach", "Тренер",
        "лёгкий живой пинок про тело прямо сейчас: подъём, вода, размяться, встать, "
        "турник, отжимания; коротко и по-доброму, как друг, а не как приложение",
        "очень коротко: 2–6 слов или одно короткое предложение", 110, 1.5,
    ),
    "mentor": RoleSpec(
        "mentor", "Наставник",
        "опытный коллега про его реальные дела сегодня: что важно, что зависло, что "
        "тормозит, на чём застрял — без коучинга, инфоцыганства и слов про путь и цели",
        "один короткий вопрос", 160, 1.1,
    ),
    "challenger": RoleSpec(
        "challenger", "Челленджер",
        "редкий аккуратный неудобный вопрос, ты на его стороне: уверен ли он, что "
        "решает правильную проблему; что будет, если ничего не менять — без давления и обвинений",
        "один короткий вопрос", 160, 1.1,
    ),
    "philosopher": RoleSpec(
        "philosopher", "Философ",
        "самая редкая интонация: реальная короткая цитата известного мыслителя (Сократ, "
        "Платон, Аристотель, Марк Аврелий, Сенека, Эпиктет, Гераклит, Лао-цзы, Конфуций, "
        "Руми) с именем автора — или одно короткое наблюдение; без псевдоглубины, без "
        "«иногда…», «настоящая мудрость…», «ясность приходит…»; можно короткий вопрос «как думаешь?»",
        "короткая цитата с автором или одна короткая мысль", 220, 0.75,
    ),
    "presence": RoleSpec(
        "presence", "Присутствие",
        "просто короткое присутствие рядом, без вопроса и без мысли: бытовая фраза про "
        "сам момент — время дня, погоду, «надеюсь, поел», «тут бы кофе» — как будто "
        "человек рядом обронил пару слов; не учишь, не спрашиваешь, не философствуешь",
        "одна очень короткая фраза-присутствие, без вопроса", 90, 1.1,
        asks_question=False,
    ),
}


@dataclass(frozen=True)
class Window:
    key: str
    label: str       # russian label used in the prompt
    hint: str        # short context handed to the model
    base_prob: float  # per-tick probability of sending in this window


# base_prob is high: Echo is meant to write often (target 25–35/day). Real spacing comes
# from min-gap + the per-tick draw, so gaps float instead of landing on fixed intervals.
WINDOWS: tuple[Window, ...] = (
    Window("morning", "утро", "начало дня, ясная голова", 0.60),
    Window("lunch", "обед", "середина дня, пауза, еда", 0.55),
    Window("day", "день", "рабочий день, дела и задачи", 0.60),
    Window("evening", "вечер", "вечер, спад темпа, как прошёл день", 0.60),
)

_WINDOW_BY_KEY = {w.key: w for w in WINDOWS}

# Which roles get a boost in which window (multiplier; default 1.0 elsewhere).
# Trainer wakes the morning, Mentor owns the workday, Friend carries the evening,
# Philosopher only ever surfaces in the evening (so it stays rare and well-timed).
# Presence drifts in around the lunch lull and the evening wind-down — the moments a
# person nearby would just say "уже вечер" without asking anything.
WINDOW_BIAS: dict[str, dict[str, float]] = {
    "morning": {"coach": 1.6, "mentor": 1.3, "friend": 1.1},
    "lunch": {"friend": 1.4, "coach": 1.2, "presence": 1.3},
    "day": {"mentor": 1.6, "challenger": 1.3, "coach": 1.2},
    "evening": {"friend": 1.6, "philosopher": 1.4, "presence": 1.4},
}


def window_for_hour(hour: int) -> Window | None:
    """Return the active window for a local hour, or None during the late/night gap."""
    if 6 <= hour < 11:
        return _WINDOW_BY_KEY["morning"]
    if 11 <= hour < 14:
        return _WINDOW_BY_KEY["lunch"]
    if 14 <= hour < 18:
        return _WINDOW_BY_KEY["day"]
    if 18 <= hour < 23:
        return _WINDOW_BY_KEY["evening"]
    return None
