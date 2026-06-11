"""Phase 2 — manual indexing trigger (backfill chunks + embed pending)."""
from __future__ import annotations

from fastapi import APIRouter, Depends
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..indexing import index_pending

router = APIRouter(prefix="/index", tags=["index"])


@router.post("/run")
async def run_index(session: AsyncSession = Depends(get_session)) -> dict[str, int]:
    """Create chunks for un-chunked messages and embed pending chunks.

    Returns {chunks_created, embedded, remaining}. Also runs automatically in
    the background worker (see main.py lifespan).
    """
    return await index_pending(session)
