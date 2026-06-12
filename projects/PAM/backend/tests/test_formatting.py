"""Unit tests — reformat chunking: respects size limit, preserves content."""
from app.formatting import _split_for_reformat


def test_respects_limit():
    text = "\n\n".join("a" * 200 for _ in range(20))
    chunks = _split_for_reformat(text, limit=500)
    assert chunks
    assert all(len(c) <= 500 for c in chunks)


def test_keeps_all_paragraphs():
    text = "\n\n".join(f"Параграф{i} " + "x" * 80 for i in range(40))
    chunks = _split_for_reformat(text, limit=1000)
    joined = "\n\n".join(chunks)
    for i in range(40):
        assert f"Параграф{i}" in joined


def test_hard_splits_long_single_paragraph():
    text = "z" * 2500
    chunks = _split_for_reformat(text, limit=1000)
    assert all(len(c) <= 1000 for c in chunks)
    # одиночный длинный абзац режется жёстко без вставки разделителей
    assert "".join(chunks) == text


def test_empty_returns_single():
    assert _split_for_reformat("") == [""]
