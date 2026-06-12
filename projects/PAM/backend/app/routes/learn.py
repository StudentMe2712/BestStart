"""Phase 5 — личный лектор: ingest + list learning material.

POST /learn/article  {url}            → fetch + extract an article
POST /learn/pdf      (multipart file) → extract text from an uploaded PDF
GET  /learn/sources                   → list ingested sources
GET  /learn/sources/{id}              → one source (with extracted text)
DELETE /learn/sources/{id}            → remove a source (+ its chunks)

Course generation lives in a later step; this slice just gets material in,
extracted, chunked and (via the background worker) embedded.
"""
from __future__ import annotations

import uuid

from fastapi import APIRouter, Depends, File, HTTPException, Response, UploadFile
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from ..content import (
    ingest_article,
    ingest_file,
    ingest_pdf,
    ingest_recognized,
    ingest_text,
    ingest_youtube,
)
from ..courses import generate_course
from ..db import get_session
from ..formatting import reformat_source_text
from ..models import ContentSource, Course
from ..schemas import (
    ContentSourceOut,
    CourseOut,
    IngestArticleIn,
    IngestTextIn,
    RememberIn,
)

router = APIRouter(prefix="/learn", tags=["learn"])

MAX_PDF_BYTES = 25 * 1024 * 1024  # 25 MB
MAX_FILE_BYTES = 25 * 1024 * 1024  # 25 MB
# Universal file inbox — extensions we know how to extract text from.
ALLOWED_FILE_EXTS = {
    "txt", "text", "md", "markdown", "log", "csv", "json", "rst",
    "html", "htm", "pdf", "docx", "pptx", "xlsx", "xls",
}


@router.post("/article", response_model=ContentSourceOut)
async def add_article(
    payload: IngestArticleIn,
    session: AsyncSession = Depends(get_session),
) -> ContentSource:
    url = payload.url.strip()
    if not (url.startswith("http://") or url.startswith("https://")):
        raise HTTPException(status_code=400, detail="url must be http(s)")
    src = await ingest_article(session, url)
    if src.status == "failed":
        raise HTTPException(status_code=422, detail=src.error or "extraction failed")
    return src


@router.post("/youtube", response_model=ContentSourceOut)
async def add_youtube(
    payload: IngestArticleIn,
    session: AsyncSession = Depends(get_session),
) -> ContentSource:
    url = payload.url.strip()
    if not (url.startswith("http://") or url.startswith("https://")):
        raise HTTPException(status_code=400, detail="url must be http(s)")
    src = await ingest_youtube(session, url)
    if src.status == "failed":
        raise HTTPException(status_code=422, detail=src.error or "transcript failed")
    return src


@router.post("/pdf", response_model=ContentSourceOut)
async def add_pdf(
    file: UploadFile = File(...),
    session: AsyncSession = Depends(get_session),
) -> ContentSource:
    data = await file.read()
    if not data:
        raise HTTPException(status_code=400, detail="empty file")
    if len(data) > MAX_PDF_BYTES:
        raise HTTPException(status_code=413, detail="pdf too large (max 25 MB)")
    src = await ingest_pdf(session, file.filename or "document.pdf", data)
    if src.status == "failed":
        raise HTTPException(status_code=422, detail=src.error or "extraction failed")
    return src


@router.post("/file", response_model=ContentSourceOut)
async def add_file(
    file: UploadFile = File(...),
    session: AsyncSession = Depends(get_session),
) -> ContentSource:
    """Universal file inbox: txt/md/html/docx/pdf → extracted into memory."""
    name = file.filename or "file.txt"
    ext = name.rsplit(".", 1)[-1].lower() if "." in name else ""
    if ext and ext not in ALLOWED_FILE_EXTS:
        raise HTTPException(
            status_code=415,
            detail=f"unsupported type .{ext}; allowed: {sorted(ALLOWED_FILE_EXTS)}",
        )
    data = await file.read()
    if not data:
        raise HTTPException(status_code=400, detail="empty file")
    if len(data) > MAX_FILE_BYTES:
        raise HTTPException(status_code=413, detail="file too large (max 25 MB)")
    src = await ingest_file(session, name, data)
    if src.status == "failed":
        raise HTTPException(status_code=422, detail=src.error or "extraction failed")
    return src


@router.post("/text", response_model=ContentSourceOut)
async def add_text(
    payload: IngestTextIn,
    session: AsyncSession = Depends(get_session),
) -> ContentSource:
    """Paste raw text straight into memory."""
    text = (payload.text or "").strip()
    if not text:
        raise HTTPException(status_code=400, detail="text is empty")
    title = (payload.title or "").strip() or None
    src = await ingest_text(session, title, text)
    if src.status == "failed":
        raise HTTPException(status_code=422, detail=src.error or "no text")
    return src


@router.post("/remember", response_model=ContentSourceOut)
async def remember(
    payload: RememberIn,
    session: AsyncSession = Depends(get_session),
) -> ContentSource:
    """«Запомнить файл»: распознанный текст вложения чата → ContentSource."""
    text = (payload.text or "").strip()
    if not text:
        raise HTTPException(status_code=400, detail="text is empty")
    title = (payload.title or "").strip() or None
    src = await ingest_recognized(session, title, text, kind=payload.kind)
    if src.status == "failed":
        raise HTTPException(status_code=422, detail=src.error or "no text")
    return src


@router.get("/sources", response_model=list[ContentSourceOut])
async def list_sources(
    session: AsyncSession = Depends(get_session),
) -> list[ContentSource]:
    rows = (
        await session.execute(
            select(ContentSource).order_by(ContentSource.created_at.desc())
        )
    ).scalars().all()
    return list(rows)


@router.get("/sources/{source_id}")
async def get_source(
    source_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
) -> dict:
    src = (
        await session.execute(
            select(ContentSource).where(ContentSource.id == source_id)
        )
    ).scalar_one_or_none()
    if not src:
        raise HTTPException(status_code=404, detail="source not found")
    return {
        "id": str(src.id),
        "kind": src.kind,
        "title": src.title,
        "url": src.url,
        "status": src.status,
        "char_count": src.char_count,
        "error": src.error,
        "text": src.text,
        "formatted_text": src.formatted_text,
        # has_file/mime — флаг нативного предпросмотра (сейчас только PDF).
        # original_mime НЕ deferred, так что байты файла мы тут не тянем.
        "has_file": src.original_mime is not None,
        "mime": src.original_mime,
        "created_at": src.created_at.isoformat() if src.created_at else None,
    }


@router.get("/sources/{source_id}/file")
async def get_source_file(
    source_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
) -> Response:
    """Serve the stored original file bytes (PDF) for inline preview.

    Selects the (deferred) blob column directly so it's never pulled elsewhere.
    """
    row = (
        await session.execute(
            select(ContentSource.original_data, ContentSource.original_mime).where(
                ContentSource.id == source_id
            )
        )
    ).first()
    if not row or row.original_data is None:
        raise HTTPException(status_code=404, detail="no original file for this source")
    return Response(
        content=row.original_data,
        media_type=row.original_mime or "application/octet-stream",
        headers={
            "Content-Disposition": "inline",
            "X-Content-Type-Options": "nosniff",
            "X-Frame-Options": "SAMEORIGIN",
        },
    )


@router.post("/sources/{source_id}/reformat")
async def reformat_source(
    source_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
) -> dict:
    """AI-«причесать» исходный текст в читаемый markdown (кэшируется)."""
    src = (
        await session.execute(
            select(ContentSource).where(ContentSource.id == source_id)
        )
    ).scalar_one_or_none()
    if not src:
        raise HTTPException(status_code=404, detail="source not found")
    if not src.text:
        raise HTTPException(status_code=409, detail="source has no text")
    if src.formatted_text:  # уже причёсано — отдаём кэш, не гоняем LLM повторно
        return {"formatted_text": src.formatted_text}
    try:
        formatted = await reformat_source_text(session, src)
    except ValueError as e:
        raise HTTPException(status_code=422, detail=str(e))
    except Exception as e:  # noqa: BLE001
        raise HTTPException(status_code=502, detail=f"reformat failed: {e}")
    return {"formatted_text": formatted}


@router.delete("/sources/{source_id}", status_code=204)
async def delete_source(
    source_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
) -> None:
    src = (
        await session.execute(
            select(ContentSource).where(ContentSource.id == source_id)
        )
    ).scalar_one_or_none()
    if not src:
        raise HTTPException(status_code=404, detail="source not found")
    await session.delete(src)
    await session.commit()


@router.post("/sources/{source_id}/course", response_model=CourseOut)
async def make_course(
    source_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
) -> Course:
    """Generate a mini-course from the source, tailored to the user's level."""
    src = (
        await session.execute(
            select(ContentSource).where(ContentSource.id == source_id)
        )
    ).scalar_one_or_none()
    if not src:
        raise HTTPException(status_code=404, detail="source not found")
    if src.status != "extracted" or not src.text:
        raise HTTPException(status_code=409, detail="source has no extracted text")
    try:
        return await generate_course(session, src)
    except ValueError as e:
        raise HTTPException(status_code=422, detail=str(e))
    except Exception as e:  # noqa: BLE001
        raise HTTPException(status_code=502, detail=f"course generation failed: {e}")


@router.get("/sources/{source_id}/course", response_model=CourseOut | None)
async def latest_course(
    source_id: uuid.UUID,
    session: AsyncSession = Depends(get_session),
) -> Course | None:
    """Return the most recently generated course for the source (or null)."""
    return (
        await session.execute(
            select(Course)
            .where(Course.source_id == source_id)
            .order_by(Course.created_at.desc())
            .limit(1)
        )
    ).scalar_one_or_none()
