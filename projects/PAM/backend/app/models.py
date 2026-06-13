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
    LargeBinary,
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


# ---- Fact Review Queue (P0) — статусы факта профиля ----
# Перед попаданием в долгую память факт проходит проверку. Принятыми (видимыми
# в чате) считаются approved + edited; pending_review ждёт решения, rejected скрыт.
FACT_PENDING = "pending_review"
FACT_APPROVED = "approved"
FACT_REJECTED = "rejected"
FACT_EDITED = "edited"
FACT_STATUSES = (FACT_PENDING, FACT_APPROVED, FACT_REJECTED, FACT_EDITED)
ACCEPTED_FACT_STATUSES = (FACT_APPROVED, FACT_EDITED)


def is_fact_accepted(status: str) -> bool:
    """Факт «принят» (подмешивается в память чата), если approved или edited."""
    return status in ACCEPTED_FACT_STATUSES


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
    # Project Memory (P2): необязательная привязка разговора к проекту. NULL = вне
    # проекта (текущее поведение чата/импорта неизменно). ON DELETE SET NULL —
    # удаление проекта НЕ удаляет разговор (local-first).
    project_id: Mapped[uuid.UUID | None] = mapped_column(
        UUID(as_uuid=True),
        ForeignKey("projects.id", ondelete="SET NULL"),
        nullable=True,
    )

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
        Index("ix_conversations_project", "project_id"),
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
    # AI-причёсанная версия `text` (главы/абзацы) — заполняется по кнопке
    # «Улучшить читаемость», кэшируется здесь, чтобы не гонять LLM повторно.
    formatted_text: Mapped[str | None] = mapped_column(Text, nullable=True)
    # #6 — фоновый реформат больших материалов: статус и прогресс (0..100).
    # reformat_status ∈ {NULL (не запускали), running, done, failed}.
    reformat_status: Mapped[str | None] = mapped_column(String(16), nullable=True)
    reformat_progress: Mapped[int] = mapped_column(
        Integer, nullable=False, server_default="0"
    )
    char_count: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    error: Mapped[str | None] = mapped_column(Text, nullable=True)
    # Оригинальные байты загруженного файла (только для PDF — для нативного
    # предпросмотра в <iframe>). Deferred: большие блобы НЕ тянем в списках.
    original_data: Mapped[bytes | None] = mapped_column(
        LargeBinary, nullable=True, deferred=True
    )
    original_mime: Mapped[str | None] = mapped_column(String(128), nullable=True)
    # Project Memory (P2): необязательная привязка материала к проекту (как у
    # conversations). NULL = вне проекта; ON DELETE SET NULL.
    project_id: Mapped[uuid.UUID | None] = mapped_column(
        UUID(as_uuid=True),
        ForeignKey("projects.id", ondelete="SET NULL"),
        nullable=True,
    )
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now()
    )

    chunks: Mapped[list["ContentChunk"]] = relationship(
        "ContentChunk",
        back_populates="source",
        cascade="all, delete-orphan",
        order_by="ContentChunk.position",
    )

    __table_args__ = (
        Index("ix_content_sources_created", "created_at"),
        Index("ix_content_sources_project", "project_id"),
    )


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
    # Очередь проверки: новые факты — pending_review; в память чата идут approved+edited.
    status: Mapped[str] = mapped_column(
        String(16), nullable=False, server_default=FACT_PENDING, default=FACT_PENDING
    )
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now()
    )

    __table_args__ = (
        Index("ix_profile_facts_category", "category"),
        Index("ix_profile_facts_status", "status"),
    )


class Event(Base):
    """Lightweight observability event (Observability panel, P0).

    Local-first append-only лог: вызовы провайдеров, фолбэки, батчи эмбеддингов,
    импорты и прогоны Лектора. Пишется best-effort через `metrics.record_event` —
    никогда не на критическом пути запроса. Агрегируется в `/stats`.
    """

    __tablename__ = "events"

    id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True), primary_key=True, default=uuid.uuid4
    )
    kind: Mapped[str] = mapped_column(String(32), nullable=False)
    provider: Mapped[str | None] = mapped_column(String(32), nullable=True)
    status: Mapped[str] = mapped_column(
        String(16), nullable=False, server_default="ok", default="ok"
    )
    duration_ms: Mapped[int | None] = mapped_column(Integer, nullable=True)
    detail: Mapped[str | None] = mapped_column(String(255), nullable=True)
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now()
    )

    __table_args__ = (
        Index("ix_events_kind_created", "kind", "created_at"),
        Index("ix_events_created", "created_at"),
    )


# ---- Learning progress (P1) — статус изучения курса ----
COURSE_NOT_STARTED = "not_started"
COURSE_IN_PROGRESS = "in_progress"
COURSE_COMPLETED = "completed"


def course_percent(completed: int, total: int) -> int:
    """Процент пройденных уроков (0..100)."""
    if total <= 0:
        return 0
    return min(100, round(completed / total * 100))


def course_study_status(completed: int, total: int, quiz_taken: bool = False) -> str:
    """Статус изучения, выведенный из прогресса (не ручная метка)."""
    if total > 0 and completed >= total:
        return COURSE_COMPLETED
    if completed > 0 or quiz_taken:
        return COURSE_IN_PROGRESS
    return COURSE_NOT_STARTED


class CourseProgress(Base):
    """Прогресс изучения одного курса (P1 — «учусь по нему»).

    Одна строка на курс. `completed_lessons` — список ключей "модуль-урок"
    (индексы в `course.data.modules[].lessons[]`); `lessons_total` — снимок их
    числа для процента; квиз-результат — score/total. Статус/процент выводятся
    (см. `course_study_status`/`course_percent`), здесь не хранятся.
    """

    __tablename__ = "course_progress"

    id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True), primary_key=True, default=uuid.uuid4
    )
    course_id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True),
        ForeignKey("courses.id", ondelete="CASCADE"),
        nullable=False,
        unique=True,
    )
    completed_lessons: Mapped[list] = mapped_column(
        JSONB, nullable=False, default=list, server_default="[]"
    )
    lessons_total: Mapped[int] = mapped_column(
        Integer, nullable=False, default=0, server_default="0"
    )
    quiz_score: Mapped[int | None] = mapped_column(Integer, nullable=True)
    quiz_total: Mapped[int | None] = mapped_column(Integer, nullable=True)
    quiz_completed_at: Mapped[datetime | None] = mapped_column(
        DateTime(timezone=True), nullable=True
    )
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now()
    )
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now(), onupdate=func.now()
    )


# ============================================================================
# Project Memory (P2) — тонкий слой над существующей памятью.
#
# НЕ параллельная система: проекты лишь ГРУППИРУЮТ существующие сущности
# (conversations/content_sources через nullable project_id), а memory_items —
# единый универсальный контейнер знаний (заменяет project_items из дизайн-дока;
# консолидация по принципу «не создавать параллельные системы памяти»).
# memory_links связывают объекты (полиморфно: kind+id, без жёсткого FK).
# Поиск по memory_items — полнотекст (content_tsv) + теги (V1, без второго RAG).
# ============================================================================

# --- projects ---
PROJECT_ACTIVE = "active"
PROJECT_ARCHIVED = "archived"
PROJECT_STATUSES = (PROJECT_ACTIVE, PROJECT_ARCHIVED)

# --- memory_items: тип элемента (AI-теггер выставляет, можно переопределить) ---
ITEM_IDEA = "idea"
ITEM_NOTE = "note"
ITEM_ARTICLE = "article"
ITEM_TOOL = "tool"
ITEM_CODE = "code"
ITEM_PROMPT = "prompt"
ITEM_LEARNING = "learning"
ITEM_DECISION = "decision"
ITEM_TYPES = (
    ITEM_IDEA, ITEM_NOTE, ITEM_ARTICLE, ITEM_TOOL,
    ITEM_CODE, ITEM_PROMPT, ITEM_LEARNING, ITEM_DECISION,
)

# memory_items lifecycle (НЕ review-гейт — захват пользователя доверенный;
# Fact Review Queue профиля это не затрагивает).
MEMORY_ACTIVE = "active"
MEMORY_ARCHIVED = "archived"
MEMORY_STATUSES = (MEMORY_ACTIVE, MEMORY_ARCHIVED)

# --- memory_links: тип объекта на концах связи (полиморфная ссылка) ---
LINK_KIND_ITEM = "memory_item"
LINK_KIND_PROJECT = "project"
LINK_KIND_FACT = "fact"
LINK_KIND_CONVERSATION = "conversation"
LINK_KINDS = (LINK_KIND_ITEM, LINK_KIND_PROJECT, LINK_KIND_FACT, LINK_KIND_CONVERSATION)
# Подсказки по relation (не enforced — навигационные ярлыки).
LINK_RELATIONS = (
    "related_to", "inspired_by", "became", "used_in", "derived_from",
    "part_of", "references", "contradicts",
)


class Project(Base):
    """Группа памяти вокруг проекта (Project Memory, P2).

    Лёгкая сущность-зонтик: к ней привязываются разговоры/материалы (nullable
    project_id) и memory_items; структурного хранилища у проекта своего нет.
    """

    __tablename__ = "projects"

    id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True), primary_key=True, default=uuid.uuid4
    )
    name: Mapped[str] = mapped_column(String(200), nullable=False)
    description: Mapped[str | None] = mapped_column(Text, nullable=True)
    status: Mapped[str] = mapped_column(
        String(16), nullable=False, server_default=PROJECT_ACTIVE, default=PROJECT_ACTIVE
    )
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now()
    )
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now(), onupdate=func.now()
    )

    __table_args__ = (Index("ix_projects_status", "status"),)


class MemoryItem(Base):
    """Универсальный контейнер знания (Project Memory, P2).

    Идея/заметка/статья/инструмент/код/промпт/урок/решение — из Telegram, чата
    или вручную. AI-теггер (`tagging.py`) заполняет summary/tags/item_type/
    importance. Поиск — полнотекст (`content_tsv`, словарь `simple`) + теги
    (JSONB), без отдельного векторного индекса (реюз стека). Привязка к проекту
    необязательна (`project_id`).
    """

    __tablename__ = "memory_items"

    id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True), primary_key=True, default=uuid.uuid4
    )
    project_id: Mapped[uuid.UUID | None] = mapped_column(
        UUID(as_uuid=True),
        ForeignKey("projects.id", ondelete="SET NULL"),
        nullable=True,
    )
    source: Mapped[str] = mapped_column(String(32), nullable=False)  # telegram|chat|web|manual
    source_ref: Mapped[str | None] = mapped_column(String(255), nullable=True)
    title: Mapped[str | None] = mapped_column(Text, nullable=True)
    content: Mapped[str] = mapped_column(Text, nullable=False)
    summary: Mapped[str | None] = mapped_column(Text, nullable=True)  # AI
    item_type: Mapped[str] = mapped_column(
        String(16), nullable=False, server_default=ITEM_NOTE, default=ITEM_NOTE
    )
    importance: Mapped[int] = mapped_column(
        Integer, nullable=False, server_default="3", default=3
    )  # 1..5 (AI)
    tags: Mapped[list] = mapped_column(
        JSONB, nullable=False, default=list, server_default="[]"
    )  # AI, string[]
    status: Mapped[str] = mapped_column(
        String(16), nullable=False, server_default=MEMORY_ACTIVE, default=MEMORY_ACTIVE
    )
    # GIN-индексируемый tsvector для полнотекстового recall (словарь simple,
    # заполняется app-side как у messages.content_tsv).
    content_tsv: Mapped[Any | None] = mapped_column(TSVECTOR, nullable=True)
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now()
    )
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now(), onupdate=func.now()
    )

    __table_args__ = (
        Index("ix_memory_items_project", "project_id"),
        Index("ix_memory_items_type", "item_type"),
        Index("ix_memory_items_created", "created_at"),
        Index("ix_memory_items_tsv", "content_tsv", postgresql_using="gin"),
        Index("ix_memory_items_tags", "tags", postgresql_using="gin"),
    )


class MemoryLink(Base):
    """Связь между объектами памяти (Project Memory, P2).

    Полиморфная (kind + id, без жёсткого FK), чтобы выражать примеры:
    idea→inspired_by→article, idea→became→decision, tool→used_in→project,
    fact→derived_from→conversation. Только PostgreSQL — никакой графовой БД.
    """

    __tablename__ = "memory_links"

    id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True), primary_key=True, default=uuid.uuid4
    )
    source_kind: Mapped[str] = mapped_column(
        String(16), nullable=False, server_default=LINK_KIND_ITEM, default=LINK_KIND_ITEM
    )
    source_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), nullable=False)
    target_kind: Mapped[str] = mapped_column(
        String(16), nullable=False, server_default=LINK_KIND_ITEM, default=LINK_KIND_ITEM
    )
    target_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), nullable=False)
    relation: Mapped[str] = mapped_column(String(32), nullable=False)
    confidence: Mapped[float] = mapped_column(Float, nullable=False, default=1.0)
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), server_default=func.now()
    )

    __table_args__ = (
        UniqueConstraint(
            "source_kind", "source_id", "target_kind", "target_id", "relation",
            name="uq_memory_link",
        ),
        Index("ix_memory_links_source", "source_kind", "source_id"),
        Index("ix_memory_links_target", "target_kind", "target_id"),
    )
