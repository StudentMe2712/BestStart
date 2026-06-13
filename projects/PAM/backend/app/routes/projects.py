"""Project Memory (P2) — projects CRUD + scoping existing entities.

Проект ГРУППИРУЕТ существующие сущности (разговоры/материалы через nullable
project_id и memory_items), своего хранилища не заводит. Счётчики выводятся, не
денормализуются.
"""
from __future__ import annotations

import uuid

from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy import func, select, update
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..models import (
    PROJECT_STATUSES,
    ContentSource,
    Conversation,
    MemoryItem,
    Message,
    Project,
)
from ..schemas import (
    ContentSourceOut,
    ConversationSummary,
    MemoryItemOut,
    ProjectIn,
    ProjectOut,
    ProjectPatch,
)

router = APIRouter(prefix="/projects", tags=["projects"])


async def _count(session: AsyncSession, stmt) -> int:
    return int((await session.execute(stmt)).scalar_one())


async def _project_out(session: AsyncSession, p: Project) -> ProjectOut:
    out = ProjectOut.model_validate(p)
    out.items_count = await _count(
        session,
        select(func.count()).select_from(MemoryItem).where(MemoryItem.project_id == p.id),
    )
    out.conversations_count = await _count(
        session,
        select(func.count()).select_from(Conversation).where(Conversation.project_id == p.id),
    )
    out.materials_count = await _count(
        session,
        select(func.count()).select_from(ContentSource).where(ContentSource.project_id == p.id),
    )
    return out


async def _get_project(session: AsyncSession, project_id: uuid.UUID) -> Project:
    p = (
        await session.execute(select(Project).where(Project.id == project_id))
    ).scalar_one_or_none()
    if p is None:
        raise HTTPException(status_code=404, detail="Project not found")
    return p


@router.post("", response_model=ProjectOut, status_code=201)
async def create_project(
    payload: ProjectIn, session: AsyncSession = Depends(get_session)
) -> ProjectOut:
    p = Project(name=payload.name.strip(), description=payload.description)
    session.add(p)
    await session.commit()
    await session.refresh(p)
    return await _project_out(session, p)


@router.get("", response_model=list[ProjectOut])
async def list_projects(
    status: str | None = Query(None, description="active | archived"),
    session: AsyncSession = Depends(get_session),
) -> list[ProjectOut]:
    q = select(Project).order_by(Project.updated_at.desc())
    if status:
        q = q.where(Project.status == status)
    rows = (await session.execute(q)).scalars().all()
    return [await _project_out(session, p) for p in rows]


@router.get("/{project_id}", response_model=ProjectOut)
async def get_project(
    project_id: uuid.UUID, session: AsyncSession = Depends(get_session)
) -> ProjectOut:
    p = await _get_project(session, project_id)
    return await _project_out(session, p)


@router.patch("/{project_id}", response_model=ProjectOut)
async def patch_project(
    project_id: uuid.UUID,
    payload: ProjectPatch,
    session: AsyncSession = Depends(get_session),
) -> ProjectOut:
    p = await _get_project(session, project_id)
    if payload.name is not None:
        name = payload.name.strip()
        if not name:
            raise HTTPException(status_code=422, detail="name cannot be empty")
        p.name = name
    if payload.description is not None:
        p.description = payload.description
    if payload.status is not None:
        if payload.status not in PROJECT_STATUSES:
            raise HTTPException(status_code=422, detail="Unknown status")
        p.status = payload.status
    await session.commit()
    await session.refresh(p)
    return await _project_out(session, p)


@router.delete("/{project_id}", status_code=204)
async def delete_project(
    project_id: uuid.UUID, session: AsyncSession = Depends(get_session)
) -> None:
    """Удалить проект. Контент (разговоры/материалы/memory_items) НЕ удаляется —
    их `project_id` обнуляется (FK ON DELETE SET NULL), local-first."""
    p = await _get_project(session, project_id)
    await session.delete(p)
    await session.commit()


# --- содержимое проекта ---

@router.get("/{project_id}/items", response_model=list[MemoryItemOut])
async def project_items(
    project_id: uuid.UUID,
    item_type: str | None = Query(None),
    session: AsyncSession = Depends(get_session),
) -> list[MemoryItemOut]:
    await _get_project(session, project_id)
    q = (
        select(MemoryItem)
        .where(MemoryItem.project_id == project_id)
        .order_by(MemoryItem.created_at.desc())
    )
    if item_type:
        q = q.where(MemoryItem.item_type == item_type)
    rows = (await session.execute(q)).scalars().all()
    return [MemoryItemOut.model_validate(r) for r in rows]


@router.get("/{project_id}/conversations", response_model=list[ConversationSummary])
async def project_conversations(
    project_id: uuid.UUID, session: AsyncSession = Depends(get_session)
) -> list[ConversationSummary]:
    await _get_project(session, project_id)
    mcount = (
        select(func.count(Message.id))
        .where(Message.conversation_id == Conversation.id)
        .scalar_subquery()
    )
    rows = (
        await session.execute(
            select(Conversation, mcount.label("mc"))
            .where(Conversation.project_id == project_id)
            .order_by(Conversation.updated_at.desc())
        )
    ).all()
    out: list[ConversationSummary] = []
    for conv, mc in rows:
        s = ConversationSummary.model_validate(conv)
        s.message_count = int(mc or 0)
        out.append(s)
    return out


@router.get("/{project_id}/materials", response_model=list[ContentSourceOut])
async def project_materials(
    project_id: uuid.UUID, session: AsyncSession = Depends(get_session)
) -> list[ContentSourceOut]:
    await _get_project(session, project_id)
    rows = (
        await session.execute(
            select(ContentSource)
            .where(ContentSource.project_id == project_id)
            .order_by(ContentSource.created_at.desc())
        )
    ).scalars().all()
    return [ContentSourceOut.model_validate(r) for r in rows]


@router.post("/{project_id}/attach", status_code=204)
async def attach_to_project(
    project_id: uuid.UUID,
    kind: str = Query(..., description="conversation | material"),
    ref_id: uuid.UUID = Query(...),
    session: AsyncSession = Depends(get_session),
) -> None:
    """Привязать существующий разговор/материал к проекту (выставить project_id)."""
    await _get_project(session, project_id)
    if kind == "conversation":
        model = Conversation
    elif kind == "material":
        model = ContentSource
    else:
        raise HTTPException(status_code=422, detail="kind must be conversation|material")
    res = await session.execute(
        update(model).where(model.id == ref_id).values(project_id=project_id)
    )
    if res.rowcount == 0:
        raise HTTPException(status_code=404, detail=f"{kind} not found")
    await session.commit()


@router.post("/{project_id}/detach", status_code=204)
async def detach_from_project(
    project_id: uuid.UUID,
    kind: str = Query(..., description="conversation | material"),
    ref_id: uuid.UUID = Query(...),
    session: AsyncSession = Depends(get_session),
) -> None:
    """Отвязать разговор/материал от проекта (project_id → NULL)."""
    model = Conversation if kind == "conversation" else ContentSource if kind == "material" else None
    if model is None:
        raise HTTPException(status_code=422, detail="kind must be conversation|material")
    await session.execute(
        update(model)
        .where(model.id == ref_id, model.project_id == project_id)
        .values(project_id=None)
    )
    await session.commit()
