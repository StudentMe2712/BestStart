"""Conversation routes — ingest, list, detail."""
from __future__ import annotations

import uuid

from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy import func, select, update
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm import selectinload

from ..db import get_session
from ..indexing import chunk_text
from ..models import Chunk, Conversation, Message
from ..schemas import (
    ConversationDetail,
    ConversationPatch,
    ConversationSummary,
    IncomingConversation,
    IngestResult,
    MessageOut,
)

router = APIRouter(prefix="/conversations", tags=["conversations"])


@router.post("", response_model=IngestResult)
async def ingest_conversation(
    payload: IncomingConversation,
    session: AsyncSession = Depends(get_session),
) -> IngestResult:
    """UPSERT a conversation captured by the browser extension.

    Idempotent on (source, external_id).
    """
    # find existing
    q = select(Conversation).where(
        Conversation.source == payload.source,
        Conversation.external_id == payload.external_id,
    )
    existing = (await session.execute(q)).scalar_one_or_none()

    created = existing is None

    if existing is None:
        existing = Conversation(
            source=payload.source,
            external_id=payload.external_id,
            title=payload.title,
            started_at=payload.started_at,
            raw_json=payload.raw,
        )
        session.add(existing)
        await session.flush()
    else:
        # update mutable fields
        if payload.title:
            existing.title = payload.title
        if payload.raw is not None:
            existing.raw_json = payload.raw
        # Wipe and reinsert messages — simple but safe.
        # For Phase 1 this is fine. Smarter diff comes later.
        await session.execute(
            Message.__table__.delete().where(Message.conversation_id == existing.id)
        )

    new_messages: list[Message] = []
    for m in payload.messages:
        obj = Message(
            conversation_id=existing.id,
            role=m.role,
            content=m.content,
            position=m.position,
            sent_at=m.sent_at,
        )
        session.add(obj)
        new_messages.append(obj)

    await session.flush()

    # Fill content_tsv for this conversation's messages in one UPDATE.
    # Dictionary is "simple" (no stemming) so Russian content also matches.
    await session.execute(
        update(Message)
        .where(Message.conversation_id == existing.id)
        .values(content_tsv=func.to_tsvector("simple", Message.content))
    )

    # Phase 2: create chunks for the new messages. Embeddings are filled later
    # by the indexing worker (chunk.embedding stays NULL here). On re-ingest the
    # old messages were deleted above, and their chunks cascaded (FK ON DELETE CASCADE).
    for obj in new_messages:
        for i, ch in enumerate(chunk_text(obj.content)):
            session.add(Chunk(message_id=obj.id, content=ch, position=i))

    await session.commit()

    return IngestResult(
        conversation_id=existing.id,
        created=created,
        message_count=len(payload.messages),
    )


@router.get("", response_model=list[ConversationSummary])
async def list_conversations(
    source: str | None = Query(None, description="filter: chatgpt|claude|gemini|pam"),
    limit: int = Query(50, ge=1, le=200),
    offset: int = Query(0, ge=0),
    archived: bool = Query(False, description="show archived instead of active"),
    session: AsyncSession = Depends(get_session),
) -> list[ConversationSummary]:
    """List conversations — pinned first, then newest. Archived hidden by default."""
    q = (
        select(
            Conversation,
            func.count(Message.id).label("msg_count"),
        )
        .outerjoin(Message)
        .group_by(Conversation.id)
        .where(Conversation.archived.is_(archived))
        .order_by(Conversation.pinned.desc(), Conversation.updated_at.desc())
        .limit(limit)
        .offset(offset)
    )
    if source:
        q = q.where(Conversation.source == source)
    else:
        # Hide PAM chat conversations from the default "История" list — they are
        # the user's own chats with PAM (kept as memory + searchable), not
        # imported history. An explicit ?source=pam still returns them.
        q = q.where(Conversation.source != "pam")

    rows = (await session.execute(q)).all()
    out: list[ConversationSummary] = []
    for conv, cnt in rows:
        s = ConversationSummary.model_validate(conv)
        s.message_count = int(cnt or 0)
        out.append(s)
    return out


@router.patch("/{conv_id}", response_model=ConversationSummary)
async def patch_conversation(
    conv_id: uuid.UUID,
    patch: ConversationPatch,
    session: AsyncSession = Depends(get_session),
) -> ConversationSummary:
    """Toggle pinned / archived on a conversation (sidebar actions)."""
    conv = (
        await session.execute(select(Conversation).where(Conversation.id == conv_id))
    ).scalar_one_or_none()
    if not conv:
        raise HTTPException(status_code=404, detail="Conversation not found")
    if patch.pinned is not None:
        conv.pinned = patch.pinned
    if patch.archived is not None:
        conv.archived = patch.archived
    await session.commit()
    await session.refresh(conv)
    return ConversationSummary.model_validate(conv)


@router.get("/{conv_id}", response_model=ConversationDetail)
async def get_conversation(
    conv_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
) -> ConversationDetail:
    q = (
        select(Conversation)
        .where(Conversation.id == conv_id)
        .options(selectinload(Conversation.messages))
    )
    conv = (await session.execute(q)).scalar_one_or_none()
    if not conv:
        raise HTTPException(status_code=404, detail="Conversation not found")

    detail = ConversationDetail.model_validate(conv)
    detail.message_count = len(conv.messages)
    detail.messages = [MessageOut.model_validate(m) for m in conv.messages]
    return detail


@router.delete("/{conv_id}", status_code=204)
async def delete_conversation(
    conv_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
) -> None:
    q = select(Conversation).where(Conversation.id == conv_id)
    conv = (await session.execute(q)).scalar_one_or_none()
    if not conv:
        raise HTTPException(status_code=404, detail="Conversation not found")
    await session.delete(conv)
    await session.commit()
