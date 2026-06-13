"""Observability / quality panel (P0) — система в одном месте.

Два местных источника: живые счётчики из таблиц данных (разговоры, чанки,
эмбеддинги, факты по статусам) + агрегаты по лёгкому логу `events` (вызовы
провайдеров, фолбэки, времена ответа, ошибки). Только чтение.
"""
from __future__ import annotations

from datetime import datetime, timedelta, timezone

from fastapi import APIRouter, Depends, Query
from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..metrics import record_event
from ..models import (
    FACT_STATUSES,
    Chunk,
    ContentChunk,
    ContentSource,
    Conversation,
    Course,
    Event,
    Message,
    ProfileFact,
)
from ..schemas import CaptureFailedIn

router = APIRouter(prefix="/stats", tags=["stats"])

# Веса компонент Memory Health Score (в сумме 1.0).
HEALTH_WEIGHTS = {"capture": 0.35, "indexing": 0.25, "review": 0.20, "stability": 0.20}


def _pct(part: int, whole: int) -> int:
    """Доля part/whole в %, 100 если whole==0 (нечего покрывать = здорово)."""
    return round(part / whole * 100) if whole else 100


def _reliability(ok: int, failed: int) -> int:
    return _pct(ok, ok + failed)


def compute_health(components: dict[str, int]) -> int:
    """Взвешенный composite 0..100 из компонент здоровья памяти."""
    return round(sum(components[k] * w for k, w in HEALTH_WEIGHTS.items()))


def health_label(score: int) -> str:
    return "good" if score >= 80 else "ok" if score >= 50 else "poor"


async def _count(session: AsyncSession, stmt) -> int:
    return int((await session.execute(stmt)).scalar_one())


@router.post("/capture-failed")
async def capture_failed(payload: CaptureFailedIn) -> dict:
    """Расширение репортит перманентный сброс захвата (после исчерпания ретраев).

    Закрывает observability-gap: сколько разговоров НЕ удалось захватить.
    Best-effort (как и весь лог `events`).
    """
    await record_event(
        "capture_failure",
        provider=payload.source,
        status="error",
        detail=payload.reason,
    )
    return {"ok": True}


@router.get("")
async def get_stats(
    days: int = Query(7, ge=1, le=90, description="окно агрегации событий, дней"),
    session: AsyncSession = Depends(get_session),
) -> dict:
    since = datetime.now(timezone.utc) - timedelta(days=days)

    # --- memory (chat RAG) ---
    conversations = await _count(
        session, select(func.count()).select_from(Conversation)
    )
    messages = await _count(session, select(func.count()).select_from(Message))
    chunks_total = await _count(session, select(func.count()).select_from(Chunk))
    chunks_embedded = await _count(
        session,
        select(func.count()).select_from(Chunk).where(Chunk.embedding.is_not(None)),
    )

    # --- lecturer ---
    sources = await _count(
        session, select(func.count()).select_from(ContentSource)
    )
    cc_total = await _count(
        session, select(func.count()).select_from(ContentChunk)
    )
    cc_embedded = await _count(
        session,
        select(func.count())
        .select_from(ContentChunk)
        .where(ContentChunk.embedding.is_not(None)),
    )
    courses = await _count(session, select(func.count()).select_from(Course))

    # --- facts by review status ---
    fact_rows = (
        await session.execute(
            select(ProfileFact.status, func.count()).group_by(ProfileFact.status)
        )
    ).all()
    facts = {s: 0 for s in FACT_STATUSES}
    for st, n in fact_rows:
        facts[st] = int(n)
    facts["total"] = sum(facts[s] for s in FACT_STATUSES)

    # --- events: provider call counts (last `days`) ---
    prov_rows = (
        await session.execute(
            select(Event.provider, Event.status, func.count())
            .where(Event.created_at >= since, Event.provider.is_not(None))
            .group_by(Event.provider, Event.status)
        )
    ).all()
    providers = [
        {"provider": p, "status": s, "count": int(n)} for (p, s, n) in prov_rows
    ]

    # --- events: timing by kind (avg + p95) ---
    timing_rows = (
        await session.execute(
            select(
                Event.kind,
                func.count(),
                func.avg(Event.duration_ms),
                func.percentile_cont(0.95).within_group(Event.duration_ms.asc()),
            )
            .where(Event.created_at >= since, Event.duration_ms.is_not(None))
            .group_by(Event.kind)
        )
    ).all()
    timing = [
        {
            "kind": k,
            "count": int(n),
            "avg_ms": int(avg or 0),
            "p95_ms": int(p95 or 0),
        }
        for (k, n, avg, p95) in timing_rows
    ]

    fallbacks = await _count(
        session,
        select(func.count())
        .select_from(Event)
        .where(Event.created_at >= since, Event.status == "fallback"),
    )
    errors = await _count(
        session,
        select(func.count())
        .select_from(Event)
        .where(Event.created_at >= since, Event.status == "error"),
    )

    recent_rows = (
        await session.execute(
            select(Event.kind, Event.provider, Event.detail, Event.created_at)
            .where(Event.status.in_(("error", "fallback")))
            .order_by(Event.created_at.desc())
            .limit(10)
        )
    ).all()
    recent_errors = [
        {"kind": k, "provider": p, "detail": d, "created_at": c.isoformat()}
        for (k, p, d, c) in recent_rows
    ]

    # --- Capture reliability: успешные импорты vs перманентные сбросы захвата ---
    captures_ok = await _count(
        session,
        select(func.count())
        .select_from(Event)
        .where(Event.created_at >= since, Event.kind == "import"),
    )
    captures_failed = await _count(
        session,
        select(func.count())
        .select_from(Event)
        .where(Event.created_at >= since, Event.kind == "capture_failure"),
    )
    capture_reliability = _reliability(captures_ok, captures_failed)

    # --- Memory Health Score: composite из 4 компонент ---
    total_events = await _count(
        session,
        select(func.count()).select_from(Event).where(Event.created_at >= since),
    )
    error_rate = (
        round((errors + fallbacks) / total_events * 100) if total_events else 0
    )
    facts_decided = facts["approved"] + facts["rejected"] + facts["edited"]
    components = {
        "capture": capture_reliability,
        "indexing": _pct(chunks_embedded + cc_embedded, chunks_total + cc_total),
        "review": _pct(facts_decided, facts["total"]),
        "stability": 100 - error_rate,
    }
    health_score = compute_health(components)

    return {
        "days": days,
        "memory": {
            "conversations": conversations,
            "messages": messages,
            "chunks_total": chunks_total,
            "chunks_embedded": chunks_embedded,
            "chunks_pending": chunks_total - chunks_embedded,
        },
        "lecturer": {
            "sources": sources,
            "content_chunks_total": cc_total,
            "content_chunks_embedded": cc_embedded,
            "content_chunks_pending": cc_total - cc_embedded,
            "courses": courses,
        },
        "facts": facts,
        "events": {
            "providers": providers,
            "fallbacks": fallbacks,
            "errors": errors,
            "timing": timing,
            "recent_errors": recent_errors,
            "capture": {
                "ok": captures_ok,
                "failed": captures_failed,
                "reliability": capture_reliability,
            },
        },
        "health": {
            "score": health_score,
            "label": health_label(health_score),
            "components": components,
        },
    }
