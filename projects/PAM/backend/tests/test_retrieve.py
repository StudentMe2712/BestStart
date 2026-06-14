"""Unit tests — _retrieve short-circuits (empty query / all chips off) and returns
the unified (context, items) shape without touching the DB or embeddings."""
from app.routes.chat import _retrieve


async def test_empty_query_returns_empty_shape():
    out = await _retrieve(
        "   ", memory=True, materials=True, courses=True, saved=True
    )
    assert out.context == [] and out.items == []


async def test_all_sources_off_returns_empty_shape():
    out = await _retrieve(
        "привет", memory=False, materials=False, courses=False, saved=False
    )
    assert out.context == [] and out.items == []
