"""Search routes — full-text (Phase 1), semantic + hybrid (Phase 2)."""
from __future__ import annotations

import logging

from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..indexing import embed_text
from ..models import Chunk, Conversation, Message
from ..schemas import SearchHit

log = logging.getLogger(__name__)

router = APIRouter(prefix="/search", tags=["search"])

# Reciprocal Rank Fusion constant (standard default).
RRF_K = 60


@router.get("", response_model=list[SearchHit])
async def search_messages(
    q: str = Query(..., min_length=1, description="search query"),
    source: str | None = Query(None),
    limit: int = Query(30, ge=1, le=100),
    session: AsyncSession = Depends(get_session),
) -> list[SearchHit]:
    """Full-text search using PostgreSQL `websearch_to_tsquery`.

    Returns the most relevant messages with conversation context and a snippet.
    """
    tsquery = func.websearch_to_tsquery("simple", q)
    rank = func.ts_rank(Message.content_tsv, tsquery)
    snippet = func.ts_headline(
        "simple",
        Message.content,
        tsquery,
        "MaxFragments=2, MaxWords=20, MinWords=5, StartSel=«, StopSel=»",
    )

    stmt = (
        select(
            Conversation.id.label("conversation_id"),
            Message.id.label("message_id"),
            Conversation.source,
            Conversation.title,
            Message.role,
            snippet.label("snippet"),
            Message.sent_at,
            rank.label("rank"),
        )
        .join(Conversation, Conversation.id == Message.conversation_id)
        .where(Message.content_tsv.op("@@")(tsquery))
        .order_by(rank.desc())
        .limit(limit)
    )
    if source:
        stmt = stmt.where(Conversation.source == source)

    rows = (await session.execute(stmt)).all()
    return [
        SearchHit(
            conversation_id=r.conversation_id,
            message_id=r.message_id,
            source=r.source,
            title=r.title,
            role=r.role,
            snippet=r.snippet or "",
            sent_at=r.sent_at,
            rank=float(r.rank or 0.0),
        )
        for r in rows
    ]


@router.get("/semantic", response_model=list[SearchHit])
async def semantic_search(
    q: str = Query(..., min_length=1, description="search query"),
    source: str | None = Query(None),
    limit: int = Query(30, ge=1, le=100),
    session: AsyncSession = Depends(get_session),
) -> list[SearchHit]:
    """Semantic search over message chunks via cosine distance (pgvector `<=>`).

    Embeds the query with the local Ollama model, then returns the chunks whose
    embeddings are closest. Falls back with 503 if Ollama is unavailable.
    """
    try:
        qvec = await embed_text(q)
    except Exception as e:  # noqa: BLE001
        raise HTTPException(
            status_code=503, detail=f"Embeddings unavailable (Ollama): {e}"
        )

    dist = Chunk.embedding.cosine_distance(qvec)
    stmt = (
        select(
            Conversation.id.label("conversation_id"),
            Message.id.label("message_id"),
            Conversation.source,
            Conversation.title,
            Message.role,
            Chunk.content.label("snippet"),
            Message.sent_at,
            dist.label("dist"),
        )
        .join(Message, Message.id == Chunk.message_id)
        .join(Conversation, Conversation.id == Message.conversation_id)
        .where(Chunk.embedding.is_not(None))
        .order_by(dist.asc())
        .limit(limit)
    )
    if source:
        stmt = stmt.where(Conversation.source == source)

    rows = (await session.execute(stmt)).all()
    return [
        SearchHit(
            conversation_id=r.conversation_id,
            message_id=r.message_id,
            source=r.source,
            title=r.title,
            role=r.role,
            snippet=(r.snippet[:300] + "…") if len(r.snippet) > 300 else r.snippet,
            sent_at=r.sent_at,
            rank=round(1.0 - float(r.dist), 4),  # similarity (1.0 = identical)
        )
        for r in rows
    ]


@router.get("/hybrid", response_model=list[SearchHit])
async def hybrid_search(
    q: str = Query(..., min_length=1, description="search query"),
    source: str | None = Query(None),
    limit: int = Query(30, ge=1, le=100),
    session: AsyncSession = Depends(get_session),
) -> list[SearchHit]:
    """Hybrid search: fuse full-text and semantic rankings with RRF.

    Reciprocal Rank Fusion: score(msg) = Σ 1/(K + rank_in_each_list). Robust to
    the two scores being on different scales. Degrades to text-only if Ollama
    (embeddings) is unavailable.
    """
    POOL = 50  # how many to pull from each ranker before fusing

    # --- full-text ranking (message-level) ---
    tsquery = func.websearch_to_tsquery("simple", q)
    rank = func.ts_rank(Message.content_tsv, tsquery)
    snippet = func.ts_headline(
        "simple",
        Message.content,
        tsquery,
        "MaxFragments=2, MaxWords=20, MinWords=5, StartSel=«, StopSel=»",
    )
    text_stmt = (
        select(
            Conversation.id.label("cid"),
            Message.id.label("mid"),
            Conversation.source,
            Conversation.title,
            Message.role,
            snippet.label("snip"),
            Message.sent_at,
        )
        .join(Conversation, Conversation.id == Message.conversation_id)
        .where(Message.content_tsv.op("@@")(tsquery))
        .order_by(rank.desc())
        .limit(POOL)
    )
    if source:
        text_stmt = text_stmt.where(Conversation.source == source)
    text_rows = (await session.execute(text_stmt)).all()

    # --- semantic ranking (chunk-level, deduped to best chunk per message) ---
    sem_rows = []
    try:
        qvec = await embed_text(q)
        dist = Chunk.embedding.cosine_distance(qvec)
        sem_stmt = (
            select(
                Conversation.id.label("cid"),
                Message.id.label("mid"),
                Conversation.source,
                Conversation.title,
                Message.role,
                Chunk.content.label("snip"),
                Message.sent_at,
            )
            .join(Message, Message.id == Chunk.message_id)
            .join(Conversation, Conversation.id == Message.conversation_id)
            .where(Chunk.embedding.is_not(None))
            .order_by(dist.asc())
            .limit(POOL)
        )
        if source:
            sem_stmt = sem_stmt.where(Conversation.source == source)
        sem_rows = (await session.execute(sem_stmt)).all()
    except Exception as e:  # noqa: BLE001 — degrade to text-only
        log.warning("hybrid: semantic part unavailable, text-only: %s", e)

    # --- RRF fusion by message_id ---
    scores: dict = {}
    info: dict = {}

    for pos, r in enumerate(text_rows):
        scores[r.mid] = scores.get(r.mid, 0.0) + 1.0 / (RRF_K + pos)
        if r.mid not in info:
            info[r.mid] = {
                "cid": r.cid, "source": r.source, "title": r.title,
                "role": r.role, "snip": r.snip or "", "sent_at": r.sent_at,
            }

    sem_pos = 0
    seen: set = set()
    for r in sem_rows:
        if r.mid in seen:
            continue
        seen.add(r.mid)
        scores[r.mid] = scores.get(r.mid, 0.0) + 1.0 / (RRF_K + sem_pos)
        sem_pos += 1
        if r.mid not in info:
            snip = (r.snip[:300] + "…") if len(r.snip or "") > 300 else (r.snip or "")
            info[r.mid] = {
                "cid": r.cid, "source": r.source, "title": r.title,
                "role": r.role, "snip": snip, "sent_at": r.sent_at,
            }

    ranked = sorted(scores.items(), key=lambda kv: kv[1], reverse=True)[:limit]
    return [
        SearchHit(
            conversation_id=info[mid]["cid"],
            message_id=mid,
            source=info[mid]["source"],
            title=info[mid]["title"],
            role=info[mid]["role"],
            snippet=info[mid]["snip"],
            sent_at=info[mid]["sent_at"],
            rank=round(score, 5),
        )
        for mid, score in ranked
    ]
