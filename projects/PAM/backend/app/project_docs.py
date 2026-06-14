"""Project Knowledge — index PAM's own documentation into the existing memory.

PAM should be able to answer questions about itself ("Что такое PAM?", архитектура,
таблицы БД, что реализовано). We do NOT build a second RAG or a new table: the curated
project docs are ingested as ordinary learning material (`ContentSource` kind='text' +
`ContentChunk`) through the SAME pipeline as articles/files (`content._finalize`), so
they participate in Unified Retrieval — vector when Ollama is up, full-text fallback
otherwise (see `routes/chat._retrieve_content_text`).

Idempotent: each doc maps to a stable `url` marker `pamdoc://<name>`; re-seeding skips
unchanged docs and re-ingests changed ones (wipe-and-reinsert via CASCADE). Runs
best-effort on startup (`main.py`) and on demand via `POST /learn/project-docs/reindex`.

Only CURRENT, accurate docs are listed — historical/partly-inaccurate ones
(`VIBE_PROMPT.md`, `implementation-plan.html`) are intentionally excluded so answers
about the project aren't poisoned.
"""
from __future__ import annotations

import logging
from pathlib import Path

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from .content import MAX_TEXT_CHARS, _finalize
from .models import ContentSource

log = logging.getLogger(__name__)

# Стабильный ключ идемпотентности в поле ContentSource.url (иначе оно только у
# article/youtube — коллизий нет).
PAM_DOC_URL_PREFIX = "pamdoc://"

# (имя файла внутри PROJECT_DOCS_DIR, заголовок источника). Имена плоские —
# bind-mount кладёт и docs/STATE_CURRENT.md как STATE_CURRENT.md (см. docker-compose).
PROJECT_DOCS: list[tuple[str, str]] = [
    ("CLAUDE.md", "📖 PAM docs: CLAUDE.md (архитектура, таблицы, источник правды)"),
    ("PROJECT_OVERVIEW.md", "📖 PAM docs: обзор проекта"),
    ("ORCHESTRATOR.md", "📖 PAM docs: оркестратор и приоритеты продукта"),
    ("TODO.md", "📖 PAM docs: TODO / roadmap"),
    ("README.md", "📖 PAM docs: README"),
    ("STATE_CURRENT.md", "📖 PAM docs: текущее состояние"),
]


async def seed_project_docs(
    session: AsyncSession, docs_dir: Path, *, force: bool = False
) -> dict[str, list[str]]:
    """Index curated project docs as ContentSource(kind='text'), idempotently.

    Reuses the existing chunk pipeline (`content._finalize`) — no new RAG, no new table.
    Skips a doc whose stored text is byte-identical (unless `force`). Returns
    `{"ingested": [...], "skipped": [...], "missing": [...]}` (lists of file names).
    """
    result: dict[str, list[str]] = {"ingested": [], "skipped": [], "missing": []}
    for name, title in PROJECT_DOCS:
        path = docs_dir / name
        if not path.is_file():
            result["missing"].append(name)
            continue
        new_text = path.read_text(encoding="utf-8", errors="replace").strip()[:MAX_TEXT_CHARS]
        if not new_text:
            result["missing"].append(name)
            continue
        url = f"{PAM_DOC_URL_PREFIX}{name}"
        existing = (
            await session.execute(select(ContentSource).where(ContentSource.url == url))
        ).scalar_one_or_none()
        if existing is not None and not force and (existing.text or "") == new_text:
            result["skipped"].append(name)
            continue
        if existing is not None:
            await session.delete(existing)  # CASCADE убирает старые content_chunks
            await session.flush()
        src = ContentSource(kind="text", title=title, url=url, status="pending")
        session.add(src)
        await session.flush()
        await _finalize(session, src, title=title, text=new_text)
        result["ingested"].append(name)
    await session.commit()
    if result["ingested"] or result["missing"]:
        log.info("project docs seed: %s", result)
    return result
