"""SQLAlchemy models for the Personal AI Memory project."""
from __future__ import annotations

import uuid
from datetime import datetime
from typing import Any

from sqlalchemy import (
    JSON,
    Boolean,
    DateTime,
    Float,
    ForeignKey,
    Index,
    Integer,
    String,
    Text,
    UniqueConstraint,
    func,
)
from sqlalchemy.dialects.postgresql import JSONB, TSVECTOR, UUID
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column, relationship
from pgvector.sqlalchemy import Vector


class Base(DeclarativeBase):
    pass


class Conversation(Base):
    """A conversation imported from one of the AI services."""

    __tablename__ = "conversations"

    id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True), primary_key=True, default=uuid.uuid4
    )
    source: Mapped[str] = mapped_column(String(32), nullable=False)
    external_id: Mapped[str] = mapped_column(String(255), nullable=False)
    title: Mapped[str | None] = mapped_column(Text, nullable=True)
    started_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now(), onupdate=func.now()
    )
    # Phase: sidebar — закрепление/архивация чатов (в основном для source='pam').
    pinned: Mapped[bool] = mapped_column(Boolean, nullable=False, server_default="false")
    archived: Mapped[bool] = mapped_column(Boolean, nullable=False, server_default="false")
    raw_json: Mapped[dict[str, Any] | None] = mapped_column(JSONB, nullable=True)

    messages: Mapped[list["Message"]] = relationship(
        "Message",
        back_populates="conversation",
        cascade="all, delete-orphan",
        order_by="Message.position",
    )

    __table_args__ = (
        UniqueConstraint("source", "external_id", name="uq_source_external"),
        Index("ix_conversations_source", "source"),
        Index("ix_conversations_updated", "updated_at"),
    )


class Message(Base):
    """A single message inside a conversation."""

    __tablename__ = "messages"

    id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True), primary_key=True, default=uuid.uuid4
    )
    conversation_id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True),
        ForeignKey("conversations.id", ondelete="CASCADE"),
        nullable=False,
    )
    role: Mapped[str] = mapped_column(String(32), nullable=False)
    content: Mapped[str] = mapped_column(Text, nullable=False)
    position: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    sent_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))

    # GIN-indexed tsvector for fast full-text search (computed via trigger or app-side)
    content_tsv: Mapped[Any | None] = mapped_column(TSVECTOR, nullable=True)

    conversation: Mapped[Conversation] = relationship(
        "Conversation", back_populates="messages"
    )

    __table_args__ = (
        Index("ix_messages_conv", "conversation_id"),
        Index("ix_messages_tsv", "content_tsv", postgresql_using="gin"),
    )


class SavedMessage(Base):
    """A message the user starred ("Избранное").

    Stored as a SNAPSHOT (content copied here) rather than a flag on `messages`,
    because messages are wiped-and-reinserted on every re-capture (UPSERT). The
    snapshot survives re-ingest. `conversation_id` links back if the conversation
    still exists (SET NULL on delete).
    """

    __tablename__ = "saved_messages"

    id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True), primary_key=True, default=uuid.uuid4
    )
    conversation_id: Mapped[uuid.UUID | None] = mapped_column(
        UUID(as_uuid=True),
        ForeignKey("conversations.id", ondelete="SET NULL"),
        nullable=True,
    )
    source: Mapped[str] = mapped_column(String(32), nullable=False)
    title: Mapped[str | None] = mapped_column(Text, nullable=True)
    role: Mapped[str] = mapped_column(String(32), nullable=False)
    content: Mapped[str] = mapped_column(Text, nullable=False)
    position: Mapped[int | None] = mapped_column(Integer, nullable=True)
    note: Mapped[str | None] = mapped_column(Text, nullable=True)
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now()
    )

    __table_args__ = (Index("ix_saved_messages_created", "created_at"),)


class Chunk(Base):
    """A chunk of a message + its embedding (Phase 2 RAG).

    Messages are split into chunks; each chunk gets a 768-dim embedding from the
    local Ollama model `nomic-embed-text`. Used for semantic / hybrid search.
    Wiped with its message (CASCADE) — re-embedded after re-ingest.
    """

    __tablename__ = "chunks"

    id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True), primary_key=True, default=uuid.uuid4
    )
    message_id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True),
        ForeignKey("messages.id", ondelete="CASCADE"),
        nullable=False,
    )
    content: Mapped[str] = mapped_column(Text, nullable=False)
    position: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    # NULL until the background worker embeds it.
    embedding: Mapped[list[float] | None] = mapped_column(Vector(768), nullable=True)
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now()
    )

    __table_args__ = (Index("ix_chunks_message", "message_id"),)


class ContentSource(Base):
    """A piece of learning material the user fed in (Phase 5 — личный лектор).

    A `kind` of article/pdf/youtube. The raw text is extracted on ingest and
    stored in `text`; it is then chunked into `ContentChunk`s and embedded
    (reusing the Phase 2 local-embedding pipeline) so a course can be generated
    and lessons can retrieve the relevant part of long material.
    """

    __tablename__ = "content_sources"

    id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True), primary_key=True, default=uuid.uuid4
    )
    kind: Mapped[str] = mapped_column(String(16), nullable=False)  # article|pdf|youtube
    title: Mapped[str | None] = mapped_column(Text, nullable=True)
    url: Mapped[str | None] = mapped_column(Text, nullable=True)  # article/youtube source
    status: Mapped[str] = mapped_column(
        String(16), nullable=False, default="pending"
    )  # pending|extracted|failed
    text: Mapped[str | None] = mapped_column(Text, nullable=True)
    char_count: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    error: Mapped[str | None] = mapped_column(Text, nullable=True)
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now()
    )

    chunks: Mapped[list["ContentChunk"]] = relationship(
        "ContentChunk",
        back_populates="source",
        cascade="all, delete-orphan",
        order_by="ContentChunk.position",
    )

    __table_args__ = (Index("ix_content_sources_created", "created_at"),)


class ContentChunk(Base):
    """A chunk of a `ContentSource` + its embedding (Phase 5, mirrors `Chunk`)."""

    __tablename__ = "content_chunks"

    id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True), primary_key=True, default=uuid.uuid4
    )
    source_id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True),
        ForeignKey("content_sources.id", ondelete="CASCADE"),
        nullable=False,
    )
    content: Mapped[str] = mapped_column(Text, nullable=False)
    position: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    embedding: Mapped[list[float] | None] = mapped_column(Vector(768), nullable=True)
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now()
    )

    source: Mapped[ContentSource] = relationship(
        "ContentSource", back_populates="chunks"
    )

    __table_args__ = (Index("ix_content_chunks_source", "source_id"),)


class Course(Base):
    """A generated mini-course for a `ContentSource` (Phase 5 — личный лектор).

    The structured course (level, summary, modules→lessons, quiz) is stored as
    JSON in `data`. Tailored to the user's level inferred from `profile_facts`.
    Multiple courses can exist per source (regeneration); newest wins in the UI.
    """

    __tablename__ = "courses"

    id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True), primary_key=True, default=uuid.uuid4
    )
    source_id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True),
        ForeignKey("content_sources.id", ondelete="CASCADE"),
        nullable=False,
    )
    title: Mapped[str | None] = mapped_column(Text, nullable=True)
    level: Mapped[str | None] = mapped_column(String(64), nullable=True)
    data: Mapped[dict[str, Any]] = mapped_column(JSONB, nullable=False)
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now()
    )

    __table_args__ = (Index("ix_courses_source", "source_id"),)


class ProfileFact(Base):
    """A durable fact about the USER, extracted from conversations (Phase 3).

    Hallucination guard: each fact keeps a traceable source — `source_excerpt`
    (snapshot of the supporting text, survives re-ingest) and best-effort
    `source_conversation_id` (SET NULL if the conversation is deleted).
    """

    __tablename__ = "profile_facts"

    id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True), primary_key=True, default=uuid.uuid4
    )
    category: Mapped[str] = mapped_column(String(64), nullable=False)
    content: Mapped[str] = mapped_column(Text, nullable=False)
    source_conversation_id: Mapped[uuid.UUID | None] = mapped_column(
        UUID(as_uuid=True),
        ForeignKey("conversations.id", ondelete="SET NULL"),
        nullable=True,
    )
    source_excerpt: Mapped[str | None] = mapped_column(Text, nullable=True)
    confidence: Mapped[float] = mapped_column(Float, nullable=False, default=0.5)
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now()
    )

    __table_args__ = (Index("ix_profile_facts_category", "category"),)
