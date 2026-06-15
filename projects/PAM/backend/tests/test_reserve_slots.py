"""Unit tests — retrieval priority: reserve solution slots (чистая логика, без БД).

Проверяет инвариант _reserve_solution_slots: только совпавшие решения, бюджет
MEM_ITEM_K не превышается, при отсутствии инъекции выдача идентична прежней.
"""
from types import SimpleNamespace

from app.routes.chat import MEM_ITEM_K, SOLUTION_SLOTS, _reserve_solution_slots


def _row(key, item_type="note"):
    return SimpleNamespace(key=key, item_type=item_type)


def test_nothing_to_inject_returns_primary_unchanged():
    primary = [_row(i) for i in range(MEM_ITEM_K)]
    assert _reserve_solution_slots(primary, []) == primary[:MEM_ITEM_K]


def test_injects_missing_solutions_evicting_lowest_non_solutions():
    primary = [_row(i) for i in range(MEM_ITEM_K)]  # 5 НЕ-решений (по убыванию релевантности)
    sols = [_row("s1", "solution"), _row("s2", "solution")]
    out = _reserve_solution_slots(primary, sols)
    assert len(out) == MEM_ITEM_K
    assert sum(1 for r in out if r.item_type == "solution") == SOLUTION_SLOTS
    # вытеснены наименее релевантные (хвост) НЕ-решения, верх сохранён
    assert {r.key for r in out if r.item_type != "solution"} == {0, 1, 2}


def test_present_solutions_count_as_reserved():
    primary = [_row("s1", "solution"), _row("s2", "solution")] + [_row(i) for i in range(3)]
    # уже 2 решения = SOLUTION_SLOTS → ничего не добавляем
    assert _reserve_solution_slots(primary, [_row("s3", "solution")]) == primary[:MEM_ITEM_K]


def test_dedup_solution_already_in_primary():
    primary = [_row("s1", "solution")] + [_row(i) for i in range(4)]
    # подвыборка вернула s1 (уже есть) + s2 → инжектим только s2
    out = _reserve_solution_slots(
        primary, [_row("s1", "solution"), _row("s2", "solution")]
    )
    keys = [r.key for r in out]
    assert len(out) == MEM_ITEM_K
    assert keys.count("s1") == 1 and "s2" in keys


def test_never_exceeds_budget():
    primary = [_row(i) for i in range(MEM_ITEM_K)]
    sols = [_row(f"s{i}", "solution") for i in range(SOLUTION_SLOTS + 3)]
    assert len(_reserve_solution_slots(primary, sols)) <= MEM_ITEM_K


def test_short_primary_is_not_padded():
    primary = [_row(0), _row(1)]  # 2 НЕ-решения
    out = _reserve_solution_slots(primary, [_row("s1", "solution")])
    # 2 + 1 инъекция = 3, без искусственного добивания до total
    assert len(out) == 3
    assert sum(1 for r in out if r.item_type == "solution") == 1
