"""Saved ("Избранное") message routes — snapshots that survive re-ingest."""
from __future__ import annotations

import uuid

from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..models import SavedMessage
from ..schemas import SavedMessageIn, SavedMessageOut

router = APIRouter(prefix="/saved", tags=["saved"])


@router.post("", response_model=SavedMessageOut, status_code=201)
async def save_message(
    payload: SavedMessageIn,
    session: AsyncSession = Depends(get_session),
) -> SavedMessageOut:
    """Save a snapshot of a message to the user's favourites."""
    row = SavedMessage(**payload.model_dump())
    session.add(row)
    await session.commit()
    await session.refresh(row)
    return SavedMessageOut.model_validate(row)


@router.get("", response_model=list[SavedMessageOut])
async def list_saved(
    source: str | None = Query(None, description="filter: chatgpt|claude|gemini"),
    limit: int = Query(100, ge=1, le=500),
    offset: int = Query(0, ge=0),
    session: AsyncSession = Depends(get_session),
) -> list[SavedMessageOut]:
    """List saved messages, newest-first."""
    q = (
        select(SavedMessage)
        .order_by(SavedMessage.created_at.desc())
        .limit(limit)
        .offset(offset)
    )
    if source:
        q = q.where(SavedMessage.source == source)
    rows = (await session.execute(q)).scalars().all()
    return [SavedMessageOut.model_validate(r) for r in rows]


@router.delete("/{saved_id}", status_code=204)
async def delete_saved(
    saved_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
) -> None:
    row = (
        await session.execute(
            select(SavedMessage).where(SavedMessage.id == saved_id)
        )
    ).scalar_one_or_none()
    if not row:
        raise HTTPException(status_code=404, detail="Saved message not found")
    await session.delete(row)
    await session.commit()
