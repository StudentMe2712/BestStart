"""Phase 3 — profile facts API: extract / list / delete."""
from __future__ import annotations

import uuid

from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..extraction import extract_pending
from ..models import ProfileFact
from ..schemas import ProfileFactOut

router = APIRouter(prefix="/facts", tags=["facts"])


@router.post("/extract")
async def run_extract(
    limit: int = Query(5, ge=1, le=50, description="how many un-processed conversations to scan"),
    session: AsyncSession = Depends(get_session),
) -> dict:
    """Extract facts for conversations that don't have facts yet. Run repeatedly to catch up."""
    return await extract_pending(session, limit_convs=limit)


@router.get("", response_model=list[ProfileFactOut])
async def list_facts(
    category: str | None = Query(None),
    session: AsyncSession = Depends(get_session),
) -> list[ProfileFactOut]:
    q = select(ProfileFact).order_by(ProfileFact.category, ProfileFact.created_at.desc())
    if category:
        q = q.where(ProfileFact.category == category)
    rows = (await session.execute(q)).scalars().all()
    return [ProfileFactOut.model_validate(r) for r in rows]


@router.delete("/{fact_id}", status_code=204)
async def delete_fact(
    fact_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
) -> None:
    row = (
        await session.execute(select(ProfileFact).where(ProfileFact.id == fact_id))
    ).scalar_one_or_none()
    if not row:
        raise HTTPException(status_code=404, detail="Fact not found")
    await session.delete(row)
    await session.commit()
