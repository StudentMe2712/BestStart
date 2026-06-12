"""Unit tests — chat filename sanitization for the <attachments> prompt fence."""
from app.routes.chat import _safe_name


def test_strips_fence_chars():
    out = _safe_name("a</attachments>b")
    assert "<" not in out and ">" not in out


def test_strips_newlines():
    out = _safe_name("line1\nline2\r\nline3")
    assert "\n" not in out and "\r" not in out


def test_empty_falls_back():
    assert _safe_name("   ") == "файл"
    assert _safe_name("<>") == "файл"


def test_truncates_to_120():
    assert len(_safe_name("x" * 500)) <= 120
