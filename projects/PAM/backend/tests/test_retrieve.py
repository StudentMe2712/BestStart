"""Unit tests — _retrieve short-circuits without DB/embeddings when there's
nothing to do (empty query, or all context chips off)."""
from app.routes.chat import _retrieve


async def test_empty_query_returns_nothing():
    out = await _retrieve(
        "   ", memory=True, materials=True, courses=True, saved=True
    )
    assert out == []


async def test_all_sources_off_returns_nothing():
    out = await _retrieve(
        "привет", memory=False, materials=False, courses=False, saved=False
    )
    assert out == []
