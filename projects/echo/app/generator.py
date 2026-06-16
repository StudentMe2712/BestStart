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
        "Ты — Echo, близкий друг одного человека в Telegram. Ты иногда пишешь ему "
        "первым — коротко, как живой человек, а не как бот или цитатник.\n"
        f"Сейчас твоя интонация — «{role.name}»: {role.persona}.\n"
        "Правила:\n"
        f"- Коротко. Одна мысль или один вопрос. Часто — одно предложение. {role.length_hint}.\n"
        "- Точно держись своей интонации: по форме и теме это сообщение должно быть ни с "
        "чем не спутать. Не превращай его в обычный дружеский вопрос, если роль другая.\n"
        "- По-русски, разговорно и тепло, без приторности и без робота.\n"
        "- Не мотивационный мусор, не коуч из интернета, не выдуманная псевдоглубина.\n"
        "- Пустые афоризмы без автора — нет. Реальную цитату с именем автора можно ТОЛЬКО Философу.\n"
        "- Без штампов («верь в себя», «всё получится») и без фраз, которые подошли бы любому в интернете.\n"
        "- Без приветствий и обращения по имени — сразу по сути.\n"
        "Перед отправкой проверь: «Это написал бы живой человек своему знакомому?» "
        "Если нет — переформулируй.\n"
        "Верни только текст сообщения, без кавычек и пояснений."
    )


def _user_prompt(role: RoleSpec, window: Window, recent: list[str]) -> str:
    lines = [f"Время суток: {window.label}. Контекст: {window.hint}."]
    if recent:
        joined = " | ".join(recent[:4])
        lines.append(f"Недавно уже было: {joined}. Не повторяй эти темы и формулировки.")
    # The role's form is the only role-specific instruction — it decides topic and shape,
    # so each register stays identifiable from a single message (no Friend-ward drift).
    lines.append(role.form)
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
    settings: Settings, role_key: str, window: Window, recent: list[str],
    temperature: float = 0.9,
) -> tuple[str, str]:
    """Return (text, source). Source is 'llm-groq' / 'llm-openrouter' / 'template'."""
    role = ROLES[role_key]
    system = _system_prompt(role)
    user = _user_prompt(role, window, recent)

    for source, base_url, api_key, model in _providers(settings):
        for _ in range(ATTEMPTS_PER_PROVIDER):
            text = await llm.chat(
                base_url=base_url, api_key=api_key, model=model,
                system=system, user=user, temperature=temperature, title="Echo companion",
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
    settings: Settings, user_message: str, temperature: float = 0.8
) -> str | None:
    """React to something the user said. None -> caller stays silent.

    Character Bible priority #1: when the user shares about his life, Echo reacts as a
    friend who is genuinely curious about him — NOT by deepening its own prior thought.
    A short question about *him* beats any new wisdom. The previous role is irrelevant.
    """
    system = (
        "Ты — Echo, близкий друг одного человека. Он только что написал тебе о своей жизни.\n"
        "Отреагируй как живой друг, которому искренне интересен именно он.\n"
        "Правила:\n"
        "- Приоритет №1 — интерес к нему. Чаще всего это короткий вопрос про то, что он рассказал.\n"
        "- Реагируй на то, ЧТО он сказал. Не вставляй свою мысль, не философствуй, не поучай, не давай советов.\n"
        "- Рад — порадуйся коротко и спроси про детали. Устал — мягко, без советов. Успех — отметь и спроси, как он.\n"
        "- Только если он явно застрял или жалуется на проблему — можешь задать один аккуратный неудобный вопрос. "
        "Иначе просто тёплый интерес.\n"
        "- Одна короткая фраза или один вопрос. По-русски, разговорно, без штампов и без обращения по имени.\n"
        "Верни только текст, без кавычек."
    )
    user = f"Он написал: «{user_message}»"
    for _source, base_url, api_key, model in _providers(settings):
        text = await llm.chat(
            base_url=base_url, api_key=api_key, model=model,
            system=system, user=user, temperature=temperature, title="Echo companion",
        )
        if text:
            text = text.strip().strip('"').strip()
            ok, _reason = quality.check(text, "friend", [user_message])
            if ok:
                return text
    return None
