"""Unit tests — _project_first: проектные записи впереди, затем глобальная добивка,
дедуп по `.key`, обрезка до k. Чистая логика (fake fetch, без БД)."""
from types import SimpleNamespace

from app.routes.chat import _project_first


def _row(key):
    return SimpleNamespace(key=key)


async def test_no_project_passes_through():
    async def fetch(pid, lim):
        assert pid is None
        return [_row(1), _row(2)]

    out = await _project_first(fetch, 5, None)
    assert [r.key for r in out] == [1, 2]


async def test_project_rows_first_then_global_fill_deduped():
    async def fetch(pid, lim):
        if pid == "P":
            return [_row(1), _row(2)]          # проектные
        return [_row(2), _row(3), _row(4)]     # глобальные (2 — дубль проектного)

    out = await _project_first(fetch, 3, "P")
    # проект впереди, дубль key=2 не повторяется, добивка глобальными до k=3
    assert [r.key for r in out] == [1, 2, 3]


async def test_project_full_skips_global_call():
    calls: list = []

    async def fetch(pid, lim):
        calls.append(pid)
        if pid == "P":
            return [_row(1), _row(2), _row(3)]
        return [_row(9)]

    out = await _project_first(fetch, 2, "P")
    assert [r.key for r in out] == [1, 2]      # проект даёт >= k
    assert calls == ["P"]                       # глобальный fetch не звали


async def test_caps_to_k():
    async def fetch(pid, lim):
        if pid == "P":
            return [_row(1)]
        return [_row(2), _row(3), _row(4), _row(5)]

    out = await _project_first(fetch, 3, "P")
    assert [r.key for r in out] == [1, 2, 3]
