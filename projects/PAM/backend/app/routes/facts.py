"""Phase 3 — profile facts API: extract / list / review (Fact Review Queue, P0).

Перед попаданием в долгую память факт проходит проверку. Извлечённые факты
лежат как `pending_review`; в чат подмешиваются только принятые (approved/edited).
Этот роутер — операции очереди: список по статусу, сводка, approve/reject/edit.
"""
from __future__ import annotations

import uuid

from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..extraction import extract_pending
from ..models import (
    FACT_APPROVED,
    FACT_EDITED,
    FACT_REJECTED,
    FACT_STATUSES,
    ProfileFact,
)
from ..schemas import ProfileFactOut, ProfileFactUpdate

router = APIRouter(prefix="/facts", tags=["facts"])


async def _get_fact(session: AsyncSession, fact_id: uuid.UUID) -> ProfileFact:
    row = (
        await session.execute(select(ProfileFact).where(ProfileFact.id == fact_id))
    ).scalar_one_or_none()
    if not row:
        raise HTTPException(status_code=404, detail="Fact not found")
    return row


async def _set_status(
    session: AsyncSession, fact_id: uuid.UUID, status: str
) -> ProfileFact:
    fact = await _get_fact(session, fact_id)
    fact.status = status
    await session.commit()
    await session.refresh(fact)
    return fact


@router.post("/extract")
async def run_extract(
    limit: int = Query(5, ge=1, le=50, description="how many un-processed conversations to scan"),
    session: AsyncSession = Depends(get_session),
) -> dict:
    """Extract facts for conversations that don't have facts yet. Run repeatedly to catch up.

    Новые факты создаются в статусе pending_review и попадают в очередь проверки.
    """
    return await extract_pending(session, limit_convs=limit)


@router.get("/counts")
async def fact_counts(session: AsyncSession = Depends(get_session)) -> dict[str, int]:
    """Сводка по статусам (для бейджей очереди проверки)."""
    rows = (
        await session.execute(
            select(ProfileFact.status, func.count()).group_by(ProfileFact.status)
        )
    ).all()
    counts = {s: 0 for s in FACT_STATUSES}
    for status, n in rows:
        counts[status] = int(n)
    return counts


@router.get("", response_model=list[ProfileFactOut])
async def list_facts(
    category: str | None = Query(None),
    status: str | None = Query(
        None, description="pending_review | approved | rejected | edited"
    ),
    session: AsyncSession = Depends(get_session),
) -> list[ProfileFactOut]:
    q = select(ProfileFact).order_by(ProfileFact.category, ProfileFact.created_at.desc())
    if category:
        q = q.where(ProfileFact.category == category)
    if status:
        if status not in FACT_STATUSES:
            raise HTTPException(status_code=422, detail="Unknown status")
        q = q.where(ProfileFact.status == status)
    rows = (await session.execute(q)).scalars().all()
    return [ProfileFactOut.model_validate(r) for r in rows]


@router.post("/{fact_id}/approve", response_model=ProfileFactOut)
async def approve_fact(
    fact_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
) -> ProfileFactOut:
    """Принять факт — он начинает подмешиваться в память чата."""
    fact = await _set_status(session, fact_id, FACT_APPROVED)
    return ProfileFactOut.model_validate(fact)


@router.post("/{fact_id}/reject", response_model=ProfileFactOut)
async def reject_fact(
    fact_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
) -> ProfileFactOut:
    """Отклонить факт — он не попадёт в память и скрыт из профиля."""
    fact = await _set_status(session, fact_id, FACT_REJECTED)
    return ProfileFactOut.model_validate(fact)


@router.patch("/{fact_id}", response_model=ProfileFactOut)
async def edit_fact(
    fact_id: uuid.UUID,
    payload: ProfileFactUpdate,
    session: AsyncSession = Depends(get_session),
) -> ProfileFactOut:
    """Поправить формулировку/категорию факта и принять его (статус edited)."""
    fact = await _get_fact(session, fact_id)
    if payload.content is not None:
        content = payload.content.strip()
        if not content:
            raise HTTPException(status_code=422, detail="content cannot be empty")
        fact.content = content
    if payload.category is not None:
        category = payload.category.strip()[:64]
        if category:
            fact.category = category
    fact.status = FACT_EDITED
    await session.commit()
    await session.refresh(fact)
    return ProfileFactOut.model_validate(fact)


@router.delete("/{fact_id}", status_code=204)
async def delete_fact(
    fact_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
) -> None:
    row = await _get_fact(session, fact_id)
    await session.delete(row)
    await session.commit()
