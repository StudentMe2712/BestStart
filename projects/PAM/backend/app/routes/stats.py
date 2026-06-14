"""Observability / quality panel (P0) — система в одном месте.

Два местных источника: живые счётчики из таблиц данных (разговоры, чанки,
эмбеддинги, факты по статусам) + агрегаты по лёгкому логу `events` (вызовы
провайдеров, фолбэки, времена ответа, ошибки). Только чтение.
"""
from __future__ import annotations

from datetime import datetime, timedelta, timezone

from fastapi import APIRouter, Depends, Query
from sqlalchemy import and_, func, or_, select
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..metrics import record_event
from ..models import (
    FACT_STATUSES,
    LINK_KIND_ITEM,
    MEMORY_ACTIVE,
    Chunk,
    ContentChunk,
    ContentSource,
    Conversation,
    Course,
    Event,
    MemoryItem,
    MemoryLink,
    Message,
    ProfileFact,
)
from ..schemas import CaptureFailedIn

router = APIRouter(prefix="/stats", tags=["stats"])

# Веса компонент СИСТЕМНОГО health (наблюдаемость; в сумме 1.0). НЕ путать с
# memory_health ниже — это про работу провайдеров/индексации/ревью, а не про
# качество самой памяти.
HEALTH_WEIGHTS = {"capture": 0.35, "indexing": 0.25, "review": 0.20, "stability": 0.20}

# ── Memory Health — отдельная метрика КАЧЕСТВА памяти (memory_items) ──────────
# Насколько память заполнена/связана/пригодна к retrieval, а не зашумлена.
# Веса в сумме 1.0; связи и retrieval весят больше (новая ценность P2.x).
MEMORY_HEALTH_WEIGHTS = {
    "summary": 0.15, "tags": 0.15, "project": 0.10, "linked": 0.20,
    "importance": 0.10, "retrieval": 0.20, "content": 0.10,
}
IMPORTANCE_OK = 3              # importance >= порога = «не мелочь»
WEAK_SPOT_THRESHOLD = 60      # компонент ниже → попадает в «слабые места»
MEMORY_HEALTH_WEAK_LABELS = {
    "summary": "много items без summary",
    "tags": "много items без тегов",
    "project": "часть items без проекта",
    "linked": "мало связей между items",
    "importance": "много items низкой важности",
    "retrieval": "низкое покрытие retrieval",
    "content": "есть items с пустым content",
}


def compute_memory_health(components: dict[str, int]) -> int:
    """Взвешенный composite 0..100 из компонент качества памяти."""
    return round(sum(components[k] * w for k, w in MEMORY_HEALTH_WEIGHTS.items()))


def memory_health_label(score: int, total_items: int) -> str:
    """Метка качества памяти. Пустая память — НЕ «здорова» (отдельный label)."""
    if total_items == 0:
        return "empty"
    return "good" if score >= 80 else "ok" if score >= 50 else "poor"


def memory_health_weak_spots(components: dict[str, int]) -> list[str]:
    """Человекочитаемые слабые места — компоненты ниже порога."""
    return [
        MEMORY_HEALTH_WEAK_LABELS[k]
        for k in MEMORY_HEALTH_WEIGHTS
        if components.get(k, 0) < WEAK_SPOT_THRESHOLD
    ]


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

    # --- Memory Health: качество памяти (memory_items, active) ---
    active = MemoryItem.status == MEMORY_ACTIVE
    content_ok = func.length(func.btrim(MemoryItem.content)) > 0
    mi_total = await _count(
        session, select(func.count()).select_from(MemoryItem).where(active)
    )
    if mi_total:
        async def _mi(*conds) -> int:
            return await _count(
                session,
                select(func.count()).select_from(MemoryItem).where(active, *conds),
            )

        link_exists = (
            select(MemoryLink.id)
            .where(
                or_(
                    and_(
                        MemoryLink.source_kind == LINK_KIND_ITEM,
                        MemoryLink.source_id == MemoryItem.id,
                    ),
                    and_(
                        MemoryLink.target_kind == LINK_KIND_ITEM,
                        MemoryLink.target_id == MemoryItem.id,
                    ),
                )
            )
            .exists()
        )
        mi = {
            "summary": _pct(
                await _mi(
                    MemoryItem.summary.is_not(None),
                    func.length(func.btrim(MemoryItem.summary)) > 0,
                ),
                mi_total,
            ),
            "tags": _pct(
                await _mi(func.jsonb_array_length(MemoryItem.tags) > 0), mi_total
            ),
            "project": _pct(await _mi(MemoryItem.project_id.is_not(None)), mi_total),
            "linked": _pct(await _mi(link_exists), mi_total),
            "importance": _pct(
                await _mi(MemoryItem.importance >= IMPORTANCE_OK), mi_total
            ),
            "retrieval": _pct(
                await _mi(MemoryItem.content_tsv.is_not(None), content_ok), mi_total
            ),
            "content": _pct(await _mi(content_ok), mi_total),
        }
        mh_score = compute_memory_health(mi)
    else:
        mi = {k: 0 for k in MEMORY_HEALTH_WEIGHTS}
        mh_score = 0
    memory_health = {
        "score": mh_score,
        "label": memory_health_label(mh_score, mi_total),
        "total_items": mi_total,
        "components": mi,
        "weak_spots": memory_health_weak_spots(mi) if mi_total else [],
    }

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
        "memory_health": memory_health,
    }
