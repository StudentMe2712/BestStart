"""Role archetypes for Echo.

Each role defines the voice, length and quality bounds of a message. Roles are the
unit the scheduler picks and the generator writes in. Windows bias which roles are
more likely at a given time of day.
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


ROLES: dict[str, RoleSpec] = {
    "mentor": RoleSpec(
        "mentor", "Наставник",
        "спокойный наставник: даёшь направление мысли, без давления и нравоучений",
        "2–3 коротких предложения", 320, 1.0,
    ),
    "provocateur": RoleSpec(
        "provocateur", "Провокатор",
        "мягкий интеллектуальный провокатор: задаёшь неудобный, но честный вопрос",
        "1–2 предложения, заканчивай вопросом", 240, 1.0,
    ),
    "brainstorm": RoleSpec(
        "brainstorm", "Брейнштормер",
        "запускаешь мысль гипотезой «а что если…», открываешь неожиданный угол",
        "1–2 предложения", 260, 1.0,
    ),
    "friend": RoleSpec(
        "friend", "Друг",
        "тёплый, человечный, поддерживающий — без приторности и пафоса",
        "1–2 коротких предложения", 220, 1.0,
    ),
    "inspirer": RoleSpec(
        "inspirer", "Вдохновитель",
        "краткая собственная мысль-наблюдение, не цитата и не лозунг",
        "1–2 предложения", 240, 1.0,
    ),
    "coach": RoleSpec(
        "coach", "Тренер",
        "про тело и действие: вода, движение, пауза, дыхание; мягко зовёшь сделать что-то прямо сейчас",
        "1–2 очень коротких предложения", 180, 1.0,
    ),
    "observer": RoleSpec(
        "observer", "Наблюдатель",
        "короткое точное наблюдение про привычки, внимание, ритм дня",
        "одно предложение", 200, 0.7,
    ),
    "philosopher": RoleSpec(
        "philosopher", "Философ",
        "глубокая, но ясная мысль; без пафоса и заумности",
        "1–2 предложения", 280, 0.7,
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
    Window("evening", "вечер", "вечер, спад темпа, время подумать", 0.10),
)

_WINDOW_BY_KEY = {w.key: w for w in WINDOWS}

# Which roles get a boost in which window (multiplier; default 1.0 elsewhere).
WINDOW_BIAS: dict[str, dict[str, float]] = {
    "morning": {"provocateur": 1.4, "brainstorm": 1.4, "mentor": 1.2, "observer": 1.2},
    "lunch": {"friend": 1.6, "coach": 1.3},
    "day": {"coach": 1.6, "mentor": 1.2, "friend": 1.1, "observer": 1.1},
    "evening": {"philosopher": 1.6, "inspirer": 1.5, "mentor": 1.2},
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
