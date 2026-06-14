"""Timeline (P2.3) — read-only производный поток событий памяти.

Линейная хронология ПОВЕРХ существующих сущностей (memory_items, conversations,
content_sources, courses, profile_facts, memory_links): union по веткам + сортировка
по времени. Не новая БД, не граф. Для memory_items/связей показываем связанные
элементы (related), чтобы видеть «idea → became → decision», без построения графа.

Фильтры: project_id, entity_type (csv), source, from/to (даты), limit/offset.
"""
from __future__ import annotations

import uuid
from collections import defaultdict
from datetime import datetime

from fastapi import APIRouter, Depends, Query
from sqlalchemy import and_, or_, select
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..models import (
    LINK_KIND_ITEM,
    MEMORY_ACTIVE,
    ContentSource,
    Conversation,
    Course,
    MemoryItem,
    MemoryLink,
    ProfileFact,
)
from ..schemas import TimelineItem, TimelineRelated

router = APIRouter(prefix="/timeline", tags=["timeline"])

ENTITY_TYPES = ("memory_item", "conversation", "material", "course", "fact", "link")
# Ветки без project_id — при фильтре по проекту их пропускаем.
NO_PROJECT = {"fact", "link"}


def _date(stmt, col, date_from, date_to):
    if date_from is not None:
        stmt = stmt.where(col >= date_from)
    if date_to is not None:
        stmt = stmt.where(col <= date_to)
    return stmt


@router.get("", response_model=list[TimelineItem])
async def get_timeline(
    project_id: uuid.UUID | None = Query(None),
    entity_type: str | None = Query(
        None, description="csv: memory_item,conversation,material,course,fact,link"
    ),
    source: str | None = Query(None, description="фильтр по полю source записи"),
    date_from: datetime | None = Query(None, alias="from"),
    date_to: datetime | None = Query(None, alias="to"),
    limit: int = Query(50, ge=1, le=200),
    offset: int = Query(0, ge=0),
    session: AsyncSession = Depends(get_session),
) -> list[TimelineItem]:
    want = (
        {e.strip() for e in entity_type.split(",") if e.strip()}
        if entity_type
        else set(ENTITY_TYPES)
    )
    if project_id is not None:
        want -= NO_PROJECT  # эти сущности не привязаны к проекту
    cap = offset + limit  # сколько максимум тянуть из каждой ветки
    rows: list[TimelineItem] = []

    # memory_items
    if "memory_item" in want:
        stmt = select(MemoryItem).where(MemoryItem.status == MEMORY_ACTIVE)
        if project_id is not None:
            stmt = stmt.where(MemoryItem.project_id == project_id)
        stmt = _date(stmt, MemoryItem.created_at, date_from, date_to)
        for it in (
            await session.execute(stmt.order_by(MemoryItem.created_at.desc()).limit(cap))
        ).scalars().all():
            rows.append(
                TimelineItem(
                    timestamp=it.created_at, entity_type="memory_item",
                    subtype=it.item_type,
                    title=it.title or (it.summary or (it.content or "")[:60]),
                    summary=it.summary, source=it.source, project_id=it.project_id,
                    ref_id=it.id,
                )
            )

    # conversations (не архивные)
    if "conversation" in want:
        stmt = select(Conversation).where(Conversation.archived.is_(False))
        if project_id is not None:
            stmt = stmt.where(Conversation.project_id == project_id)
        stmt = _date(stmt, Conversation.updated_at, date_from, date_to)
        for cv in (
            await session.execute(stmt.order_by(Conversation.updated_at.desc()).limit(cap))
        ).scalars().all():
            rows.append(
                TimelineItem(
                    timestamp=cv.updated_at, entity_type="conversation",
                    subtype=cv.source, title=cv.title or "Без названия",
                    source=cv.source, project_id=cv.project_id, ref_id=cv.id,
                )
            )

    # materials (content_sources)
    if "material" in want:
        stmt = select(ContentSource)
        if project_id is not None:
            stmt = stmt.where(ContentSource.project_id == project_id)
        stmt = _date(stmt, ContentSource.created_at, date_from, date_to)
        for s in (
            await session.execute(stmt.order_by(ContentSource.created_at.desc()).limit(cap))
        ).scalars().all():
            rows.append(
                TimelineItem(
                    timestamp=s.created_at, entity_type="material", subtype=s.kind,
                    title=s.title or "Материал", source=s.kind,
                    project_id=s.project_id, ref_id=s.id,
                )
            )

    # courses (project через source)
    if "course" in want:
        stmt = select(Course, ContentSource.project_id).join(
            ContentSource, ContentSource.id == Course.source_id
        )
        if project_id is not None:
            stmt = stmt.where(ContentSource.project_id == project_id)
        stmt = _date(stmt, Course.created_at, date_from, date_to)
        for crs, pid in (
            await session.execute(stmt.order_by(Course.created_at.desc()).limit(cap))
        ).all():
            rows.append(
                TimelineItem(
                    timestamp=crs.created_at, entity_type="course", subtype=crs.level,
                    title=crs.title or "Курс", source="course",
                    project_id=pid, ref_id=crs.id,
                )
            )

    # profile_facts
    if "fact" in want:
        stmt = _date(select(ProfileFact), ProfileFact.created_at, date_from, date_to)
        for f in (
            await session.execute(stmt.order_by(ProfileFact.created_at.desc()).limit(cap))
        ).scalars().all():
            rows.append(
                TimelineItem(
                    timestamp=f.created_at, entity_type="fact", subtype=f.category,
                    title=(f.content or "")[:80], summary=f.source_excerpt,
                    source="extraction", ref_id=f.id,
                )
            )

    # memory_links (момент связывания: idea → became → decision)
    link_rows: list[MemoryLink] = []
    if "link" in want:
        stmt = _date(select(MemoryLink), MemoryLink.created_at, date_from, date_to)
        link_rows = list(
            (
                await session.execute(stmt.order_by(MemoryLink.created_at.desc()).limit(cap))
            ).scalars().all()
        )
        for ln in link_rows:
            rows.append(
                TimelineItem(
                    timestamp=ln.created_at, entity_type="link", subtype=ln.relation,
                    title=ln.relation, source="link", ref_id=ln.id,
                )
            )

    # source-фильтр (единообразно по полю source) + сортировка + страница
    if source:
        rows = [r for r in rows if r.source == source]
    rows.sort(key=lambda r: r.timestamp, reverse=True)
    page = rows[offset : offset + limit]

    # related: связи для memory_item-записей страницы + концы для link-записей.
    await _attach_related(session, page, link_rows)
    return page


async def _attach_related(
    session: AsyncSession, page: list[TimelineItem], link_rows: list[MemoryLink]
) -> None:
    """Подтянуть связанные элементы (одним запросом титулов), без N+1 и без графа."""
    item_ids = [r.ref_id for r in page if r.entity_type == "memory_item"]
    page_link_ids = {r.ref_id for r in page if r.entity_type == "link"}
    page_links = [ln for ln in link_rows if ln.id in page_link_ids]

    links_for_items: list[MemoryLink] = []
    if item_ids:
        links_for_items = list(
            (
                await session.execute(
                    select(MemoryLink).where(
                        or_(
                            and_(
                                MemoryLink.source_kind == LINK_KIND_ITEM,
                                MemoryLink.source_id.in_(item_ids),
                            ),
                            and_(
                                MemoryLink.target_kind == LINK_KIND_ITEM,
                                MemoryLink.target_id.in_(item_ids),
                            ),
                        )
                    )
                )
            ).scalars().all()
        )

    # собрать id всех нужных memory_item-титулов одним запросом
    need: set[uuid.UUID] = set()
    for ln in links_for_items + page_links:
        if ln.source_kind == LINK_KIND_ITEM:
            need.add(ln.source_id)
        if ln.target_kind == LINK_KIND_ITEM:
            need.add(ln.target_id)
    titles: dict[uuid.UUID, str | None] = {}
    if need:
        for iid, title in (
            await session.execute(
                select(MemoryItem.id, MemoryItem.title).where(MemoryItem.id.in_(need))
            )
        ).all():
            titles[iid] = title

    # related для memory_item-записей (сосед на другом конце связи)
    item_rel: dict[uuid.UUID, list[TimelineRelated]] = defaultdict(list)
    page_item_ids = set(item_ids)
    for ln in links_for_items:
        if ln.source_id in page_item_ids and ln.target_kind == LINK_KIND_ITEM:
            item_rel[ln.source_id].append(
                TimelineRelated(
                    relation=ln.relation, kind="memory_item",
                    id=ln.target_id, title=titles.get(ln.target_id),
                )
            )
        if ln.target_id in page_item_ids and ln.source_kind == LINK_KIND_ITEM:
            item_rel[ln.target_id].append(
                TimelineRelated(
                    relation=ln.relation, kind="memory_item",
                    id=ln.source_id, title=titles.get(ln.source_id),
                )
            )

    # related для link-записей: оба конца связи
    link_rel: dict[uuid.UUID, list[TimelineRelated]] = {}
    for ln in page_links:
        link_rel[ln.id] = [
            TimelineRelated(
                relation="from", kind=ln.source_kind,
                id=ln.source_id, title=titles.get(ln.source_id),
            ),
            TimelineRelated(
                relation="to", kind=ln.target_kind,
                id=ln.target_id, title=titles.get(ln.target_id),
            ),
        ]

    for r in page:
        if r.entity_type == "memory_item":
            r.related = item_rel.get(r.ref_id, [])
        elif r.entity_type == "link":
            r.related = link_rel.get(r.ref_id, [])
