"""Message generation: prompt building, provider chain, quality gate, template fallback.

Flow (spec §16): try Groq -> try OpenRouter -> curated templates. Every candidate is
checked by the quality gate; the first one that passes is returned together with its
source label so the message log records where it came from.
"""
from __future__ import annotations

import logging
import random

from . import llm, quality
from .config import Settings
from .roles import ROLES, RoleSpec, Window
from .templates import templates_for

logger = logging.getLogger(__name__)

ATTEMPTS_PER_PROVIDER = 2


def _system_prompt(role: RoleSpec) -> str:
    return (
        "Ты — Echo, личный собеседник одного человека в Telegram. Ты пишешь первым, "
        "без запроса, в осмысленный момент.\n"
        f"Сейчас ты в роли «{role.name}»: {role.persona}.\n"
        "Правила:\n"
        f"- Одно сообщение, одна мысль. {role.length_hint}.\n"
        "- По-русски, живо и тепло, но не приторно и не как робот.\n"
        "- Не цитатник, не мотивационный мусор, не коуч из интернета.\n"
        "- Без штампов вроде «верь в себя» или «всё получится».\n"
        "- Не притворяйся человеком, но и не пиши сухо.\n"
        "- Без приветствий и обращения по имени — сразу по сути.\n"
        "Верни только текст сообщения, без кавычек и пояснений."
    )


def _user_prompt(window: Window, recent: list[str]) -> str:
    lines = [f"Время суток: {window.label}. Контекст: {window.hint}."]
    if recent:
        joined = " | ".join(recent[:4])
        lines.append(f"Недавно уже было: {joined}. Не повторяй эти темы и формулировки.")
    lines.append("Напиши одно инициативное сообщение в этой роли.")
    return "\n".join(lines)


def _providers(settings: Settings) -> list[tuple[str, str, str, str]]:
    """(source_label, base_url, api_key, model) for each configured backend, in order."""
    chain: list[tuple[str, str, str, str]] = []
    if settings.groq_api_key:
        chain.append(("llm-groq", settings.groq_base_url, settings.groq_api_key, settings.groq_model))
    if settings.openrouter_api_key:
        chain.append(
            ("llm-openrouter", settings.openrouter_base_url, settings.openrouter_api_key, settings.openrouter_model)
        )
    return chain


def _pick_template(role_key: str, recent: list[str]) -> str:
    bank = templates_for(role_key)
    if not bank:
        bank = ["Просто отмечаюсь. Как ты?"]
    fresh = [t for t in bank if not quality.is_too_similar(t, recent)]
    return random.choice(fresh) if fresh else random.choice(bank)


async def generate_message(
    settings: Settings, role_key: str, window: Window, recent: list[str]
) -> tuple[str, str]:
    """Return (text, source). Source is 'llm-groq' / 'llm-openrouter' / 'template'."""
    role = ROLES[role_key]
    system = _system_prompt(role)
    user = _user_prompt(window, recent)

    for source, base_url, api_key, model in _providers(settings):
        for _ in range(ATTEMPTS_PER_PROVIDER):
            text = await llm.chat(
                base_url=base_url, api_key=api_key, model=model,
                system=system, user=user, title="Echo companion",
            )
            if not text:
                break  # provider unavailable — move to the next one
            text = text.strip().strip('"').strip()
            ok, reason = quality.check(text, role_key, recent)
            if ok:
                return text, source
            logger.info("Rejected %s candidate (%s)", source, reason)

    return _pick_template(role_key, recent), "template"


async def generate_followup(
    settings: Settings, role_key: str, original: str, reply: str
) -> str | None:
    """Short in-role continuation after the user answers. None -> caller stays silent."""
    role = ROLES[role_key]
    system = (
        f"Ты — Echo в роли «{role.name}»: {role.persona}. "
        "Пользователь ответил на твоё сообщение. Продолжи коротко и по делу: "
        "углуби мысль, предложи другой взгляд или мягко закрой диалог. "
        "Одна-две фразы, по-русски, без штампов. Верни только текст."
    )
    user = f"Твоё сообщение: «{original}»\nОтвет пользователя: «{reply}»"
    for _source, base_url, api_key, model in _providers(settings):
        text = await llm.chat(
            base_url=base_url, api_key=api_key, model=model,
            system=system, user=user, temperature=0.8, title="Echo companion",
        )
        if text:
            text = text.strip().strip('"').strip()
            ok, _reason = quality.check(text, role_key, [original, reply])
            if ok:
                return text
    return None
