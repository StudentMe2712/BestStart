"""Project Memory (P2.3) — авто-связи между memory_items.

После создания/обновления memory item в фоне (best-effort, как `tagging.py`) ищем
осмысленные связи к уже существующим элементам и записываем их в `memory_links`.

Принцип «1 шаг генерации + 1 шаг проверки», без LLM, без графовой БД, без рекурсии:
  1) КАНДИДАТЫ — переиспользуем `search_memory_items` (full-text + теги) по
     summary/тегам/заголовку элемента (т.е. кандидаты уже релевантны по содержимому);
  2) ПРОВЕРКА — `score_candidate`: связь по шаблонам типов (idea→became→decision и т.п.)
     либо `related_to` при достаточном пересечении тегов; confidence из сигналов
     (общие теги, один проект, совпадение шаблона). Гейт `MIN_CONFIDENCE`, кап
     `MAX_NEW_LINKS` на элемент, дубли отсекает UNIQUE в `memory_links`.

Сигналы — только существующие поля (project_id, item_type, tags, summary, content).
Сбой не ломает создание элемента (как и теггер).
"""
from __future__ import annotations

import asyncio
import logging
import uuid

from sqlalchemy import and_, or_, select
from sqlalchemy.exc import IntegrityError

from .db import AsyncSessionLocal
from .models import LINK_KIND_ITEM, MEMORY_ACTIVE, MemoryItem, MemoryLink
from .routes.memory import search_memory_items

log = logging.getLogger(__name__)

# Шаблоны связи по паре типов (source_type, target_type) -> relation (направление
# source -> target). relation — свободный текст (LINK_RELATIONS не enforced).
AUTO_RELATIONS: dict[tuple[str, str], str] = {
    ("idea", "decision"): "became",
    ("decision", "idea"): "derived_from",
    ("article", "idea"): "inspired_by",
    ("idea", "article"): "references",
    ("note", "idea"): "related_to",
    ("idea", "note"): "related_to",
    ("learning", "decision"): "resulted_in",
    ("learning", "idea"): "inspired_by",
    ("tool", "code"): "used_in",
    ("code", "tool"): "references",
    ("prompt", "tool"): "used_in",
    ("article", "decision"): "references",
}

CANDIDATE_POOL = 12   # сколько кандидатов отбираем до проверки
MAX_NEW_LINKS = 3     # не захламлять: максимум новых связей на элемент
MIN_CONFIDENCE = 0.6  # ниже — связь не создаём


def _shared_tags(a, b) -> int:
    ta = {t.lower() for t in (getattr(a, "tags", None) or [])}
    tb = {t.lower() for t in (getattr(b, "tags", None) or [])}
    return len(ta & tb)


def score_candidate(item, cand) -> tuple[str | None, float]:
    """Чистая функция: какую связь (если есть) создать item -> cand и насколько уверены.

    relation по шаблону типов, иначе `related_to` при ≥2 общих тегах, иначе (None, 0).
    confidence ∈ [0,1] из сигналов: общие теги, один проект, совпадение шаблона.
    """
    pair = (item.item_type, cand.item_type)
    relation = AUTO_RELATIONS.get(pair)
    shared = _shared_tags(item, cand)
    if relation is None:
        if shared >= 2:
            relation = "related_to"
        else:
            return None, 0.0
    same_project = item.project_id is not None and item.project_id == cand.project_id
    conf = (
        0.4
        + 0.15 * min(shared, 3)
        + (0.2 if same_project else 0.0)
        + (0.25 if pair in AUTO_RELATIONS else 0.0)
    )
    return relation, round(min(conf, 1.0), 2)


async def _existing_neighbors(session, item_id: uuid.UUID) -> set[uuid.UUID]:
    """id элементов, уже связанных с данным (любая сторона/relation) — чтобы не плодить."""
    links = (
        await session.execute(
            select(MemoryLink).where(
                or_(
                    and_(
                        MemoryLink.source_kind == LINK_KIND_ITEM,
                        MemoryLink.source_id == item_id,
                    ),
                    and_(
                        MemoryLink.target_kind == LINK_KIND_ITEM,
                        MemoryLink.target_id == item_id,
                    ),
                )
            )
        )
    ).scalars().all()
    ids: set[uuid.UUID] = set()
    for ln in links:
        if ln.source_id == item_id and ln.target_kind == LINK_KIND_ITEM:
            ids.add(ln.target_id)
        if ln.target_id == item_id and ln.source_kind == LINK_KIND_ITEM:
            ids.add(ln.source_id)
    return ids


async def generate_links(item_id: uuid.UUID) -> int:
    """Сгенерировать авто-связи для элемента. Возврат: сколько связей создано."""
    async with AsyncSessionLocal() as session:
        item = (
            await session.execute(select(MemoryItem).where(MemoryItem.id == item_id))
        ).scalar_one_or_none()
        if item is None or item.status != MEMORY_ACTIVE:
            return 0
        # 1) кандидаты — по содержательным сигналам элемента (reuse P2.2 поиска)
        query = " ".join(
            p for p in (item.title, item.summary, " ".join(item.tags or [])) if p
        ).strip() or (item.content or "")[:200]
        cands = await search_memory_items(session, query, limit=CANDIDATE_POOL)
        skip = await _existing_neighbors(session, item_id)
        # 2) проверка + scoring
        scored: list[tuple[float, MemoryItem, str]] = []
        for cand in cands:
            if cand.id == item_id or cand.id in skip or cand.status != MEMORY_ACTIVE:
                continue
            relation, conf = score_candidate(item, cand)
            if relation is None or conf < MIN_CONFIDENCE:
                continue
            scored.append((conf, cand, relation))
        # project-first, затем по убыванию confidence
        scored.sort(
            key=lambda t: (
                item.project_id is not None and t[1].project_id == item.project_id,
                t[0],
            ),
            reverse=True,
        )
        created = 0
        for conf, cand, relation in scored:
            if created >= MAX_NEW_LINKS:
                break
            session.add(
                MemoryLink(
                    source_kind=LINK_KIND_ITEM, source_id=item_id,
                    target_kind=LINK_KIND_ITEM, target_id=cand.id,
                    relation=relation, confidence=conf,
                )
            )
            try:
                await session.commit()
                created += 1
            except IntegrityError:
                await session.rollback()  # дубль (UNIQUE) — пропускаем
        if created:
            log.info("auto-link: +%d links for %s", created, item_id)
        return created


# Фоновые задачи — держим ссылки, чтобы их не собрал GC (как в tagging.py).
_bg_tasks: set = set()


def schedule_autolink(item_id: uuid.UUID) -> None:
    """Запустить генерацию авто-связей в фоне (fire-and-forget, best-effort)."""

    async def _run() -> None:
        try:
            await generate_links(item_id)
        except Exception as e:  # noqa: BLE001 — авто-связи не должны влиять на основной поток
            log.warning("auto-link failed for %s: %s", item_id, e)

    task = asyncio.create_task(_run())
    _bg_tasks.add(task)
    task.add_done_callback(_bg_tasks.discard)
