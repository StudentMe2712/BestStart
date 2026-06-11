"""Phase 2 — RAG indexing: chunking + embeddings (local Ollama).

- `chunk_text` splits a message into chunks (~paragraph-aware).
- `embed_text` calls the local Ollama embedding model (`nomic-embed-text`).
- `create_missing_chunks` backfills chunks for messages that have none.
- `embed_pending` fills embeddings for chunks where it's still NULL.
- `index_pending` = backfill + embed, used by both the manual endpoint and the
  background worker loop (see `main.py` lifespan).

Everything is local-first: embeddings run on the user's machine via Ollama.
"""
from __future__ import annotations

import logging

import httpx
from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from .config import settings
from .models import Chunk, Message

log = logging.getLogger(__name__)

MAX_CHARS = 1000


def chunk_text(text: str, max_chars: int = MAX_CHARS) -> list[str]:
    """Split text into <=max_chars chunks, preferring paragraph boundaries."""
    text = (text or "").strip()
    if not text:
        return []
    if len(text) <= max_chars:
        return [text]

    chunks: list[str] = []
    buf = ""
    for para in text.split("\n\n"):
        para = para.strip()
        if not para:
            continue
        if len(para) > max_chars:
            if buf:
                chunks.append(buf)
                buf = ""
            for i in range(0, len(para), max_chars):
                chunks.append(para[i : i + max_chars])
            continue
        if buf and len(buf) + len(para) + 2 > max_chars:
            chunks.append(buf)
            buf = para
        else:
            buf = f"{buf}\n\n{para}" if buf else para
    if buf:
        chunks.append(buf)
    return chunks


async def embed_text(text: str) -> list[float]:
    """Get a 768-dim embedding from the local Ollama model."""
    async with httpx.AsyncClient(timeout=60.0) as client:
        r = await client.post(
            f"{settings.OLLAMA_URL}/api/embeddings",
            json={"model": settings.EMBED_MODEL, "prompt": text},
        )
        r.raise_for_status()
        return r.json()["embedding"]


async def create_missing_chunks(session: AsyncSession) -> int:
    """Create chunks for any messages that don't have chunks yet (backfill)."""
    has_chunk = select(Chunk.id).where(Chunk.message_id == Message.id).exists()
    msgs = (await session.execute(select(Message).where(~has_chunk))).scalars().all()
    created = 0
    for m in msgs:
        for i, ch in enumerate(chunk_text(m.content)):
            session.add(Chunk(message_id=m.id, content=ch, position=i))
            created += 1
    if created:
        await session.commit()
    return created


async def embed_pending(session: AsyncSession, limit: int = 64) -> int:
    """Embed up to `limit` chunks whose embedding is still NULL."""
    pending = (
        await session.execute(
            select(Chunk).where(Chunk.embedding.is_(None)).limit(limit)
        )
    ).scalars().all()
    done = 0
    for ch in pending:
        ch.embedding = await embed_text(ch.content)
        done += 1
    if done:
        await session.commit()
    return done


async def index_pending(session: AsyncSession) -> dict[str, int]:
    """Backfill missing chunks, then embed pending ones. Returns counts."""
    created = await create_missing_chunks(session)
    embedded = await embed_pending(session)
    remaining = (
        await session.execute(
            select(func.count()).select_from(Chunk).where(Chunk.embedding.is_(None))
        )
    ).scalar_one()
    return {"chunks_created": created, "embedded": embedded, "remaining": int(remaining)}
