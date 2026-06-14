"""Project Knowledge — generate PROJECT_FACTS.md FROM CODE (P2.4).

PAM should answer precise structural questions about itself (tables, migrations,
endpoints, providers, Telegram) from FACTS, not prose. Prose docs gave weak grounding
and the model hallucinated table names. This module introspects the LIVE code (SQLAlchemy
metadata, Alembic, the FastAPI router registry, config) and renders a deterministic
Markdown facts sheet.

Code is the source of truth: nothing is copied from STATE_CURRENT.md, and anything that
can't be derived is marked `Not Derived` — never invented.

The rendered file is written into the Project Knowledge docs dir and ingested by the SAME
seed as the other docs (`project_docs.seed_project_docs`) → content_sources /
content_chunks → Unified Retrieval. No new table, no second RAG.

Deterministic (sorted, no timestamps) so an unchanged codebase produces a byte-identical
file — no git churn, and the idempotent seed skips re-ingest. Section intro lines embed the
literal Russian question forms (таблицы / миграций / эндпоинты / Unified Retrieval /
Telegram Bot) so the no-Ollama full-text fallback ranks this sheet first for those questions.
"""
from __future__ import annotations

import collections
import logging
import re
from pathlib import Path

from .config import settings

log = logging.getLogger(__name__)

FACTS_FILENAME = "PROJECT_FACTS.md"
NOT_DERIVED = "Not Derived"

_APP_ROOT = Path(__file__).resolve().parent.parent  # /app (содержит alembic/, alembic.ini)

_HEADER = (
    "# PROJECT_FACTS.md\n"
    "> АВТОГЕНЕРАЦИЯ из кода (app/generate_project_facts.py). НЕ редактировать вручную —\n"
    "> код всегда приоритетнее документации. Что нельзя вывести из кода — `Not Derived`."
)


# ── интроспекция (всё детерминированно: сортируем) ──────────────────────────

def _sorted_tables() -> list:
    from .models import Base

    return sorted(Base.metadata.sorted_tables, key=lambda t: t.name)


def _routes() -> list[tuple[str, list[str]]]:
    from fastapi.routing import APIRoute

    from .main import app  # лениво: main импортирует этот модуль

    rs: list[tuple[str, list[str]]] = []
    for r in app.routes:
        if isinstance(r, APIRoute):
            methods = sorted(m for m in r.methods if m not in ("HEAD", "OPTIONS"))
            rs.append((r.path, methods))
    return sorted(rs)


# ── секции ──────────────────────────────────────────────────────────────────

def _facts_overview() -> str:
    from .main import app

    tables = _sorted_tables()
    return "\n".join([
        "## Что такое PAM (overview)",
        f"Имя приложения (FastAPI title): {app.title}.",
        f"Версия: {app.version}.",
        f"Описание (app.description): {app.description or NOT_DERIVED}.",
        f"Структурная сводка: таблиц БД — {len(tables)}; эндпоинтов API — "
        f"{len(_routes())}; провайдеры LLM — groq, openrouter, ollama; "
        f"default provider (config) — {settings.LLM_PROVIDER}.",
    ])


def _facts_tables() -> str:
    tables = _sorted_tables()
    names = [t.name for t in tables]
    lines = [
        "## Таблицы базы данных (database tables)",
        f"Какие таблицы есть в БД: всего {len(names)}.",
        "Список таблиц: " + ", ".join(names) + ".",
        "Поля и внешние ключи (FK) по таблицам:",
    ]
    for t in tables:
        cols = ", ".join(c.name for c in t.columns)
        fks = sorted(f"{fk.parent.name}->{fk.column.table.name}" for fk in t.foreign_keys)
        fk_s = ("; FK: " + ", ".join(fks)) if fks else ""
        lines.append(f"- {t.name} ({len(t.columns)} полей{fk_s}): {cols}")
    return "\n".join(lines)


def _facts_migrations() -> str:
    try:
        from alembic.config import Config
        from alembic.script import ScriptDirectory

        cfg = Config(str(_APP_ROOT / "alembic.ini"))
        cfg.set_main_option("script_location", str(_APP_ROOT / "alembic"))  # cwd-независимо
        script = ScriptDirectory.from_config(cfg)
        heads = list(script.get_heads())
        revs = list(script.walk_revisions())  # head -> base
        lines = [
            "## Миграции (Alembic migrations)",
            f"Сколько миграций: всего {len(revs)}.",
            f"Migration head: {', '.join(heads) if heads else NOT_DERIVED}.",
            "Цепочка миграций (base -> head):",
        ]
        for r in reversed(revs):
            lines.append(f"- {r.revision}: {(r.doc or '').strip()[:70]}")
        return "\n".join(lines)
    except Exception as e:  # noqa: BLE001 — не выводимо → честный Not Derived
        log.warning("facts: migrations not derived: %s", e)
        return "\n".join([
            "## Миграции (Alembic migrations)",
            f"Сколько миграций: {NOT_DERIVED}.",
            f"Migration head: {NOT_DERIVED}.",
        ])


def _facts_routes() -> str:
    routes = _routes()
    by_prefix = collections.Counter(
        "/" + p.split("/")[1] for p, _m in routes if len(p) > 1
    )
    lines = [
        "## API / роуты (endpoints)",
        f"Какие эндпоинты существуют: всего {len(routes)} эндпоинтов API.",
        "Группы (router prefix): "
        + ", ".join(f"{k}={v}" for k, v in sorted(by_prefix.items())) + ".",
        "Маршруты (метод путь):",
    ]
    for p, m in routes:
        lines.append(f"- {' '.join(m) or 'GET'} {p}")
    return "\n".join(lines)


def _facts_memory() -> str:
    from .routes import chat as C

    def has(name: str) -> bool:
        return hasattr(C, name)

    rows = [
        ("conversations (разговоры)",
         has("_retrieve_messages") or has("_retrieve_messages_text"),
         "вектор + строгий текстовый fallback"),
        ("lecturer materials + project knowledge (content_sources)",
         has("_retrieve_content") or has("_retrieve_content_text"),
         "вектор + строгий текстовый fallback"),
        ("memory_items", has("search_memory_items"), "полнотекст + теги"),
        ("memory_links", has("expand_memory_links"), "1-hop расширение по связям"),
        ("profile_facts", has("_profile_facts"), "только accepted (approved/edited)"),
        ("saved / избранное", has("_retrieve_saved"), "полнотекст (по чипу use_saved)"),
        ("courses / курсы", has("_retrieve_courses"), "полнотекст (по чипу use_courses)"),
    ]
    lines = [
        "## Компоненты памяти и Unified Retrieval",
        "Что входит в Unified Retrieval (компонент: участие и механизм):",
    ]
    for name, ok, how in rows:
        lines.append(f"- {name}: {'✓ участвует' if ok else '— не участвует'} ({how})")
    return "\n".join(lines)


def _facts_llm() -> str:
    return "\n".join([
        "## LLM инфраструктура",
        "Доступные провайдеры (код): groq, openrouter, ollama.",
        f"Default provider (config LLM_PROVIDER): {settings.LLM_PROVIDER}.",
        f"Модели: groq={settings.GROQ_MODEL}; groq_json={settings.GROQ_JSON_MODEL}; "
        f"groq_vision={settings.GROQ_VISION_MODEL}; openrouter={settings.OPENROUTER_MODEL}; "
        f"ollama_chat={settings.OLLAMA_CHAT_MODEL}; embeddings={settings.EMBED_MODEL}.",
        "Hybrid-роутинг (llm.route_provider): длинные/«тяжёлые» запросы → openrouter, "
        "иначе groq (если задан ключ OpenRouter).",
        "Fallback цепочка: provider-to-provider переключения НЕТ (закрытый набор). "
        "Деградации: hybrid-роутинг; без Ollama разговоры/материалы → текстовый "
        "fallback; vision → text pre-pass.",
    ])


def _facts_telegram() -> str:
    src = ""
    try:
        src = (Path(__file__).resolve().parent / "telegram_bot.py").read_text(
            encoding="utf-8", errors="replace"
        )
    except Exception:  # noqa: BLE001
        src = ""
    cmds = sorted(
        set(re.findall(r'Command\(["\']([a-zA-Z_]+)["\']', src))
        | set(re.findall(r'command=["\']([a-zA-Z_]+)["\']', src))
    )
    cmds_s = ", ".join("/" + c for c in cmds) if cmds else NOT_DERIVED
    configured = bool(settings.TELEGRAM_BOT_TOKEN) and bool(settings.TELEGRAM_ALLOWED_USER_ID)
    return "\n".join([
        "## Telegram Bot",
        "Как работает Telegram Bot (бот PAM):",
        f"- Команды: {cmds_s}.",
        "- Режимы: захват (текст/ссылка/код/файл/фото → memory item) и чат "
        "(/ask → POST /chat, RAG по всей памяти PAM).",
        f"- Configured (token + allowed user заданы): {configured}.",
        f"- Polling на этой машине (config TELEGRAM_BOT_OWNER): {settings.TELEGRAM_BOT_OWNER}.",
        "- Запуск: отдельный процесс (python -m app.telegram_bot), long polling без webhook.",
        "- Голосовые/аудио: не поддерживаются (V1).",
    ])


def _facts_features() -> str:
    prefixes = {"/" + p.split("/")[1] for p, _m in _routes() if len(p) > 1}
    feat_map = [
        ("/chat", "Чат с памятью (SSE RAG) + вложения"),
        ("/conversations", "Импорт/захват разговоров (идемпотентный UPSERT)"),
        ("/search", "Поиск: полнотекст + семантика"),
        ("/learn", "Лектор: материалы + генерация курсов + project knowledge ingest"),
        ("/facts", "Profile facts + Fact Review Queue (P0)"),
        ("/memory", "Memory items + links + recall (Project Memory P2)"),
        ("/projects", "Project Memory: проекты и scope"),
        ("/saved", "Избранное (saved messages)"),
        ("/stats", "Observability: events + Memory Health (P0)"),
        ("/timeline", "Timeline памяти"),
        ("/index", "Индексация/эмбеддинги (ручной триггер)"),
    ]
    lines = ["## Implemented Features (факты, без маркетинга)"]
    for pref, label in feat_map:
        if pref in prefixes:
            lines.append(f"- {label} ({pref})")
    return "\n".join(lines)


# ── сборка + запись ─────────────────────────────────────────────────────────

def build_facts_md() -> str:
    """Render the full facts sheet from code introspection (deterministic)."""
    sections = [
        _HEADER,
        _facts_overview(),
        _facts_tables(),
        _facts_migrations(),
        _facts_routes(),
        _facts_memory(),
        _facts_llm(),
        _facts_telegram(),
        _facts_features(),
    ]
    return "\n\n".join(s.strip() for s in sections) + "\n"


def regenerate(docs_dir: Path) -> Path | None:
    """Write PROJECT_FACTS.md into `docs_dir` from code. Best-effort (None on failure)."""
    try:
        md = build_facts_md()
    except Exception as e:  # noqa: BLE001 — генерация не на критическом пути старта
        log.warning("project facts generation failed: %s", e)
        return None
    target = docs_dir / FACTS_FILENAME
    try:
        target.write_text(md, encoding="utf-8")
        return target
    except Exception as e:  # noqa: BLE001 — например, read-only mount
        log.warning("project facts write failed (%s): %s", target, e)
        return None


if __name__ == "__main__":  # бутстрап / ручная проверка: вывести в stdout
    print(build_facts_md())
