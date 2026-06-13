"""Observability — best-effort event recording for the quality panel (P0).

`record_event` appends one row to `events` in its own short-lived session and
SWALLOWS all errors: metrics must never break or slow a real request. Call it at
boundaries (LLM call, embed batch, import, chat, lecturer) — not inside hot loops.
"""
from __future__ import annotations

import logging

from .db import AsyncSessionLocal
from .models import Event

log = logging.getLogger(__name__)

# Известные виды событий (для справки/консистентности; не enforced на уровне БД).
EVENT_KINDS = (
    "chat",
    "extraction",
    "embed",
    "import",
    "lecturer",
    "reformat",  # фоновое «причесать текст» Лектора (своя строка таймингов)
    "vision",  # распознавание вложенной картинки (vision-провайдер)
    "vision_fallback",
    "capture_failure",
)


async def record_event(
    kind: str,
    *,
    provider: str | None = None,
    status: str = "ok",
    duration_ms: int | None = None,
    detail: str | None = None,
) -> None:
    """Append one observability event. Best-effort: never raises."""
    try:
        async with AsyncSessionLocal() as session:
            session.add(
                Event(
                    kind=kind,
                    provider=provider,
                    status=status,
                    duration_ms=duration_ms,
                    detail=detail[:255] if detail else None,
                )
            )
            await session.commit()
    except Exception as e:  # noqa: BLE001 — metrics must never break a request
        log.debug("record_event(%s) failed: %s", kind, e)
