"""Personal AI Memory — FastAPI entry point."""
from __future__ import annotations

import asyncio
import logging
import re
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from .config import settings
from .content import embed_pending_content
from .db import AsyncSessionLocal
from .indexing import index_pending
from .routes import (
    chat,
    conversations,
    facts,
    indexing as index_routes,
    learn,
    saved,
    search,
)

logging.basicConfig(
    level=getattr(logging, settings.LOG_LEVEL.upper(), logging.INFO),
    format="%(asctime)s %(levelname)s %(name)s: %(message)s",
)

_worker_log = logging.getLogger("pam.worker")


async def _embed_worker() -> None:
    """Background loop: backfill chunks + embed pending ones every 15s.

    Resilient to Ollama being down — logs and retries on the next tick. Idle
    when there's nothing to do (no Ollama call when no chunks are pending).
    """
    while True:
        try:
            async with AsyncSessionLocal() as session:
                result = await index_pending(session)
                if result["embedded"] or result["chunks_created"]:
                    _worker_log.info("indexed %s", result)
                # Phase 5: embed pending learning-content chunks on the same tick.
                content_embedded = await embed_pending_content(session)
                if content_embedded:
                    _worker_log.info("indexed content chunks: %s", content_embedded)
        except Exception as e:  # noqa: BLE001 — worker must never die
            _worker_log.warning("embed worker tick failed: %s", e)
        await asyncio.sleep(15)


@asynccontextmanager
async def lifespan(app: FastAPI):
    task = asyncio.create_task(_embed_worker())
    try:
        yield
    finally:
        task.cancel()
        try:
            await task
        except asyncio.CancelledError:
            pass


app = FastAPI(
    title="Personal AI Memory",
    version="0.2.0",
    description="Phase 2 — collect conversations, full-text + semantic search.",
    lifespan=lifespan,
)

# CORS: allow extension and local web UI
# We expand "chrome-extension://*" to a regex because Chrome's origins are
# chrome-extension://<random-id>, and FastAPI's CORS middleware needs allow_origin_regex
# for wildcards.
_explicit_origins: list[str] = []
_origin_patterns: list[str] = []
for o in settings.cors_origins_list:
    if "*" in o:
        _origin_patterns.append(re.escape(o).replace(r"\*", ".*"))
    else:
        _explicit_origins.append(o)

app.add_middleware(
    CORSMiddleware,
    allow_origins=_explicit_origins or ["*"],
    allow_origin_regex="|".join(_origin_patterns) if _origin_patterns else None,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(conversations.router)
app.include_router(saved.router)
app.include_router(search.router)
app.include_router(index_routes.router)
app.include_router(chat.router)
app.include_router(facts.router)
app.include_router(learn.router)


@app.get("/")
async def root() -> dict[str, str]:
    return {
        "service": "Personal AI Memory",
        "version": "0.1.0",
        "phase": "1 — collection & basic search",
    }


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok"}
