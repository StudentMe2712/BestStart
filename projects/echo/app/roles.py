"""Role archetypes for Echo.

Echo is one person, not a panel of bots: a close friend who occasionally writes.
Roles are *registers* of that one personality — Friend (the default), Trainer, Mentor,
Challenger — not separate characters. Windows bias which register is more likely at a
given time of day. Challenger is reactive only: it never goes out on a schedule.
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


ROLES: dict[str, RoleSpec] = {
    "friend": RoleSpec(
        "friend", "Друг",
        "близкий друг, которому искренне интересна его жизнь: спрашиваешь, как он, "
        "что нового, как прошло; радуешься за него; без морали, выводов и советов",
        "одна короткая фраза или вопрос, часто одно предложение", 160, 1.8,
    ),
    "coach": RoleSpec(
        "coach", "Тренер",
        "лёгкий живой пинок про тело прямо сейчас: подъём, вода, размяться, встать, "
        "турник; коротко и по-доброму, как друг, а не как приложение",
        "очень коротко: 2–6 слов или одно короткое предложение", 110, 1.0,
    ),
    "mentor": RoleSpec(
        "mentor", "Наставник",
        "про его реальные дела сегодня: что важно, что зависло, что закроешь; "
        "конкретный рабочий вопрос — без абстракций про путь, успех и цели",
        "один короткий вопрос", 160, 1.0,
    ),
    "challenger": RoleSpec(
        "challenger", "Челленджер",
        "редкий аккуратный неудобный вопрос по реальному поводу, ты на его стороне; "
        "только когда он застрял или сомневается — не для того, чтобы поддеть",
        "один короткий вопрос", 160, 1.0,
        scheduled=False,
    ),
}


@dataclass(frozen=True)
class Window:
    key: str
    label: str       # russian label used in the prompt
    hint: str        # short context handed to the model
    base_prob: float  # per-tick probability of sending in this window


WINDOWS: tuple[Window, ...] = (
    Window("morning", "утро", "начало дня, ясная голова", 0.10),
    Window("lunch", "обед", "середина дня, пауза, еда", 0.09),
    Window("day", "день", "рабочий день, дела и задачи", 0.08),
    Window("evening", "вечер", "вечер, спад темпа, как прошёл день", 0.10),
)

_WINDOW_BY_KEY = {w.key: w for w in WINDOWS}

# Which roles get a boost in which window (multiplier; default 1.0 elsewhere).
# Friend dominates the emotional bookends of the day (morning/evening) on purpose.
WINDOW_BIAS: dict[str, dict[str, float]] = {
    "morning": {"coach": 1.5, "friend": 1.2, "mentor": 1.2},
    "lunch": {"friend": 1.6, "coach": 1.2},
    "day": {"mentor": 1.5, "coach": 1.3, "friend": 1.1},
    "evening": {"friend": 1.7},
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
