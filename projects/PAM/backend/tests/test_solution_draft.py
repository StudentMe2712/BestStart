"""Unit tests — Save Conversation As Solution draft parsing/normalization.

Чистые функции из routes/memory.py (без БД/LLM): устойчивый разбор JSON-ответа
модели и нормализация полей черновика.
"""
from app.routes.memory import _normalize_solution_draft, _parse_json_obj


def test_parse_plain_json():
    assert _parse_json_obj('{"title": "x"}') == {"title": "x"}


def test_parse_with_wrapper_text():
    d = _parse_json_obj('вот JSON: {"title": "x", "problem": "y"} — конец')
    assert d["title"] == "x" and d["problem"] == "y"


def test_parse_garbage_returns_none():
    assert _parse_json_obj("no json here") is None


def test_normalize_fills_missing_with_empty():
    out = _normalize_solution_draft({"title": "T"})
    assert out["title"] == "T"
    assert out["problem"] == out["cause"] == out["solution"] == out["notes"] == ""


def test_normalize_strips_and_caps():
    out = _normalize_solution_draft({"title": "  hi  ", "solution": "x" * 9000})
    assert out["title"] == "hi"
    assert len(out["solution"]) == 8000


def test_normalize_non_dict_safe():
    assert _normalize_solution_draft(None) == {
        "title": "", "problem": "", "cause": "", "solution": "", "notes": ""
    }


def test_normalize_ignores_non_string_values():
    out = _normalize_solution_draft({"title": 123, "problem": ["a"]})
    assert out["title"] == "" and out["problem"] == ""
