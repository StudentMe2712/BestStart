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
