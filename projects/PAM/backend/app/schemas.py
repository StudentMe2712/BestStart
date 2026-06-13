"""Pydantic schemas for the REST API."""
from __future__ import annotations

import uuid
from datetime import datetime
from typing import Any, Literal

from pydantic import BaseModel, ConfigDict, Field

Source = Literal["chatgpt", "claude", "gemini"]
Role = Literal["user", "assistant", "system", "tool"]


# ---- Inbound (from extension) ----

class IncomingMessage(BaseModel):
    role: Role
    content: str
    position: int = 0
    sent_at: datetime | None = None


class IncomingConversation(BaseModel):
    """Payload the extension sends to the backend after capturing a conversation."""

    source: Source
    external_id: str
    title: str | None = None
    started_at: datetime | None = None
    messages: list[IncomingMessage] = Field(default_factory=list)
    raw: dict[str, Any] | None = None


# ---- Outbound (to the web UI) ----

class MessageOut(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    role: str
    content: str
    position: int
    sent_at: datetime | None


class ConversationSummary(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    source: str
    external_id: str
    title: str | None
    started_at: datetime | None
    updated_at: datetime
    pinned: bool = False
    archived: bool = False
    message_count: int = 0


class ConversationDetail(ConversationSummary):
    messages: list[MessageOut] = Field(default_factory=list)


class ConversationPatch(BaseModel):
    pinned: bool | None = None
    archived: bool | None = None


class SearchHit(BaseModel):
    conversation_id: uuid.UUID
    message_id: uuid.UUID
    source: str
    title: str | None
    role: str
    snippet: str
    sent_at: datetime | None
    rank: float | None = None


class IngestResult(BaseModel):
    conversation_id: uuid.UUID
    created: bool
    message_count: int


# ---- Saved ("Избранное") messages ----

class SavedMessageIn(BaseModel):
    conversation_id: uuid.UUID | None = None
    source: str
    title: str | None = None
    role: str
    content: str
    position: int | None = None
    note: str | None = None


class SavedMessageOut(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    conversation_id: uuid.UUID | None
    source: str
    title: str | None
    role: str
    content: str
    position: int | None
    note: str | None
    created_at: datetime


# ---- Profile facts (Phase 3 memory) ----

class ProfileFactOut(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    category: str
    content: str
    source_conversation_id: uuid.UUID | None
    source_excerpt: str | None
    confidence: float
    status: str
    created_at: datetime


class ProfileFactUpdate(BaseModel):
    """Правка факта в очереди проверки (перевод в статус edited)."""

    content: str | None = None
    category: str | None = None


# ---- Learning content (Phase 5 — личный лектор) ----

class ContentSourceOut(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    kind: str
    title: str | None
    url: str | None
    status: str
    char_count: int
    error: str | None
    created_at: datetime


class IngestArticleIn(BaseModel):
    url: str


class IngestTextIn(BaseModel):
    title: str | None = None
    text: str


class RememberIn(BaseModel):
    """«Запомнить файл» — распознанный текст вложения чата → ContentSource."""
    title: str | None = None
    text: str
    kind: str = "file"


class CourseOut(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    source_id: uuid.UUID
    title: str | None
    level: str | None
    data: dict[str, Any]
    created_at: datetime


# ---- Learning progress (P1) ----

class CourseProgressOut(BaseModel):
    course_id: uuid.UUID
    completed_lessons: list[str]
    completed_count: int
    lessons_total: int
    percent: int
    status: str
    quiz_score: int | None
    quiz_total: int | None
    quiz_completed_at: datetime | None


class LessonToggleIn(BaseModel):
    key: str
    completed: bool


class QuizResultIn(BaseModel):
    score: int = Field(ge=0)
    total: int = Field(ge=1)


class ProgressSummaryOut(BaseModel):
    """Лёгкая сводка прогресса по источнику (для сетки материалов / главной)."""

    source_id: uuid.UUID
    course_id: uuid.UUID
    percent: int
    status: str
    completed_count: int
    lessons_total: int


# ---- Observability ingest ----

class CaptureFailedIn(BaseModel):
    """Расширение репортит перманентный сброс захвата разговора."""

    source: str
    reason: str | None = None


# ---- Project Memory (P2) ----

class ProjectIn(BaseModel):
    name: str = Field(min_length=1, max_length=200)
    description: str | None = None


class ProjectPatch(BaseModel):
    name: str | None = Field(default=None, max_length=200)
    description: str | None = None
    status: str | None = None  # active | archived


class ProjectOut(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    name: str
    description: str | None
    status: str
    created_at: datetime
    updated_at: datetime
    # Производные счётчики (вычисляются в роуте, не хранятся).
    items_count: int = 0
    conversations_count: int = 0
    materials_count: int = 0


class MemoryItemIn(BaseModel):
    """Создание элемента памяти (из Telegram / чата / вручную).

    AI-теггер дозаполнит summary/tags/item_type/importance, если не заданы явно.
    """

    content: str = Field(min_length=1, max_length=200_000)
    title: str | None = Field(default=None, max_length=500)
    source: str = Field(default="manual", max_length=32)
    source_ref: str | None = Field(default=None, max_length=255)
    project_id: uuid.UUID | None = None
    item_type: str | None = None  # переопределить авто-классификацию
    tags: list[str] | None = None  # переопределить авто-теги


class MemoryItemPatch(BaseModel):
    title: str | None = None
    content: str | None = None
    summary: str | None = None
    item_type: str | None = None
    importance: int | None = Field(default=None, ge=1, le=5)
    tags: list[str] | None = None
    project_id: uuid.UUID | None = None
    status: str | None = None  # active | archived


class MemoryItemOut(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    project_id: uuid.UUID | None
    source: str
    source_ref: str | None
    title: str | None
    content: str
    summary: str | None
    item_type: str
    importance: int
    tags: list[str]
    status: str
    created_at: datetime
    updated_at: datetime


class MemoryLinkIn(BaseModel):
    source_id: uuid.UUID
    target_id: uuid.UUID
    relation: str = Field(min_length=1, max_length=32)
    source_kind: str = "memory_item"
    target_kind: str = "memory_item"
    confidence: float = Field(default=1.0, ge=0.0, le=1.0)


class MemoryLinkOut(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    source_kind: str
    source_id: uuid.UUID
    target_kind: str
    target_id: uuid.UUID
    relation: str
    confidence: float
    created_at: datetime


class RecallIn(BaseModel):
    query: str = Field(min_length=1, max_length=2_000)
    project_id: uuid.UUID | None = None
    limit: int = Field(default=8, ge=1, le=30)
    synthesize: bool = True  # собрать LLM-ответ поверх найденных элементов


class RecallOut(BaseModel):
    query: str
    answer: str | None  # синтез LLM (None, если synthesize=false или нет элементов)
    items: list[MemoryItemOut]
