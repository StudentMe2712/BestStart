"""Phase 5 — личный лектор: ingest learning material into `ContentSource`.

Extracts plain text from a source (article URL → HTML→text, PDF bytes → text),
then chunks + embeds it (reusing the Phase 2 local-embedding pipeline) so a
course can later be generated and lessons can retrieve the relevant part.

YouTube transcript ingest is intentionally deferred (needs an extra dependency
and a transcript fetch); the model already carries a `youtube` kind for it.

Security: extracted text is UNTRUSTED external content. It is never executed
and, when later fed to the LLM, must be wrapped as data (see course generation),
mirroring the anti-injection handling of chat <context>.
"""
from __future__ import annotations

import asyncio
import ipaddress
import logging
import socket
from urllib.parse import parse_qs, urlparse

import httpx
from bs4 import BeautifulSoup
from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from .indexing import chunk_text, embed_text
from .models import ContentChunk, ContentSource

log = logging.getLogger(__name__)


def _assert_public_url(url: str) -> None:
    """SSRF guard: only allow http(s) to a public host.

    Resolves the host and rejects loopback / private / link-local / reserved
    addresses so a user-supplied URL can't be used to reach internal services
    (defense-in-depth — the backend is local-first/single-user today, but this
    keeps article ingest safe if the API is ever exposed).
    """
    parsed = urlparse(url)
    if parsed.scheme not in ("http", "https") or not parsed.hostname:
        raise ValueError("url must be http(s) with a host")
    host = parsed.hostname
    try:
        infos = socket.getaddrinfo(host, None)
    except socket.gaierror as e:
        raise ValueError(f"cannot resolve host: {e}")
    for info in infos:
        ip = ipaddress.ip_address(info[4][0])
        if (
            ip.is_private
            or ip.is_loopback
            or ip.is_link_local
            or ip.is_reserved
            or ip.is_multicast
            or ip.is_unspecified
        ):
            raise ValueError("url resolves to a non-public address")

MAX_TEXT_CHARS = 200_000  # safety cap on stored extracted text
# Browser-like headers — some sites (e.g. Wikipedia) 403 minimal/bot user agents.
_FETCH_HEADERS = {
    "User-Agent": (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
        "(KHTML, like Gecko) Chrome/124.0 Safari/537.36"
    ),
    "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
    "Accept-Language": "en-US,en;q=0.9,ru;q=0.8",
}
# Tags whose text is navigation/boilerplate, not article content.
_STRIP_TAGS = ("script", "style", "nav", "header", "footer", "aside", "form", "noscript")


def html_to_text(html: str) -> tuple[str | None, str]:
    """Extract (title, readable_text) from an HTML document, dropping boilerplate."""
    soup = BeautifulSoup(html, "html.parser")
    title = soup.title.string.strip() if soup.title and soup.title.string else None
    for tag in soup(_STRIP_TAGS):
        tag.decompose()
    # Prefer <article> / <main> if present; else fall back to the whole body.
    root = soup.find("article") or soup.find("main") or soup.body or soup
    parts: list[str] = []
    for el in root.find_all(["h1", "h2", "h3", "h4", "li", "p", "blockquote", "pre"]):
        t = el.get_text(" ", strip=True)
        if t:
            parts.append(t)
    text = "\n\n".join(parts).strip()
    if not text:  # last resort: all text
        text = root.get_text("\n", strip=True)
    return title, text


async def _extract_article(url: str, max_redirects: int = 5) -> tuple[str | None, str]:
    """Fetch an article, following redirects manually so every hop is SSRF-checked."""
    async with httpx.AsyncClient(
        timeout=30.0, follow_redirects=False, headers=_FETCH_HEADERS
    ) as client:
        for _ in range(max_redirects + 1):
            _assert_public_url(url)  # validate BEFORE each request (incl. redirects)
            r = await client.get(url)
            if r.is_redirect and r.has_redirect_location:
                url = str(r.next_request.url)  # validated on next loop iteration
                continue
            r.raise_for_status()
            return html_to_text(r.text)
    raise ValueError("too many redirects")


# Office/PDF documents go through markitdown → Markdown (headings, tables and
# lists are preserved), which embeds and reads far better than a flat text dump.
# markitdown is imported lazily and the instance cached.
_MARKITDOWN_EXTS = {"pdf", "docx", "pptx", "xlsx", "xls"}
_md = None


def _markitdown():
    global _md
    if _md is None:
        from markitdown import MarkItDown

        _md = MarkItDown(enable_plugins=False)
    return _md


def document_to_text(filename: str, data: bytes) -> str:
    """Convert a pdf/docx/pptx/xlsx document to text via markitdown.

    markitdown reads from a path, so the upload bytes are written to a temp file
    (with the right extension, which markitdown uses to pick the converter).
    """
    import os
    import tempfile

    suffix = f".{_ext_of(filename)}" if "." in filename else ""
    fd, tmp_path = tempfile.mkstemp(suffix=suffix)
    try:
        with os.fdopen(fd, "wb") as f:
            f.write(data)
        result = _markitdown().convert(tmp_path)
        return (getattr(result, "text_content", "") or "").strip()
    finally:
        os.remove(tmp_path)


# Plain-text-ish extensions we can decode as UTF-8 directly (Markdown is stored
# raw — fine for embeddings). HTML/PDF/DOCX go through their own extractors.
_TEXT_EXTS = {"txt", "text", "md", "markdown", "log", "csv", "json", "rst"}


def _ext_of(filename: str) -> str:
    return filename.rsplit(".", 1)[-1].lower() if "." in filename else ""


# Документы, для которых храним ОРИГИНАЛ ради превью (помимо извлечённого текста):
# pdf — нативно в <iframe>; docx — рендер mammoth на клиенте; xlsx/xls — SheetJS.
# pptx сюда НЕ входит (нет лёгкого клиентского рендера) — остаётся только текст.
_PREVIEW_MIME = {
    "pdf": "application/pdf",
    "docx": "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    "xlsx": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    "xls": "application/vnd.ms-excel",
}


def extract_file_text(filename: str, data: bytes) -> tuple[str | None, str]:
    """Dispatch an uploaded file to the right extractor → (title, text).

    Office/PDF (pdf/docx/pptx/xlsx) → markitdown; html → readable text;
    plain-text family (txt/md/…) decoded as UTF-8. Raises ValueError on an
    unsupported type. The caller wraps the result as UNTRUSTED data
    (anti-injection) just like article/PDF ingest.
    """
    ext = _ext_of(filename)
    if ext in _MARKITDOWN_EXTS:
        return None, document_to_text(filename, data)
    if ext in ("html", "htm"):
        return html_to_text(data.decode("utf-8", errors="replace"))
    if ext in _TEXT_EXTS or ext == "":
        return None, data.decode("utf-8", errors="replace")
    raise ValueError(f"unsupported file type: .{ext}")


# Изображения распознаём vision-моделью; всё остальное — markitdown/текст.
# _IMAGE_EXTS выводится из _IMAGE_MIME — один источник правды.
_IMAGE_MIME = {
    "png": "image/png", "jpg": "image/jpeg", "jpeg": "image/jpeg",
    "webp": "image/webp", "gif": "image/gif", "bmp": "image/bmp",
}
_IMAGE_EXTS = set(_IMAGE_MIME)


async def recognize_attachment(filename: str, data: bytes) -> tuple[str, str]:
    """Распознать вложение чата → (kind, text).

    Изображение (png/jpg/webp/…) → vision-модель (транскрипция + описание);
    документ (pdf/docx/xlsx/txt/…) → markitdown/текст. kind ∈ {image, document}.
    """
    ext = _ext_of(filename)
    if ext in _IMAGE_EXTS:
        import time

        from .llm import describe_image, vision_target
        from .metrics import record_event

        provider = vision_target()[0]
        t0 = time.monotonic()
        try:
            text = (await describe_image(data, _IMAGE_MIME.get(ext, "image/png"))).strip()
        except Exception as e:  # noqa: BLE001 — атрибутируем сбой vision и пробрасываем
            await record_event(
                "vision", provider=provider, status="error",
                duration_ms=int((time.monotonic() - t0) * 1000), detail=str(e),
            )
            raise
        await record_event(
            "vision", provider=provider, status="ok",
            duration_ms=int((time.monotonic() - t0) * 1000),
        )
        return "image", text
    _title, text = extract_file_text(filename, data)
    return "document", (text or "").strip()


def youtube_video_id(url: str) -> str | None:
    """Extract the 11-char video id from common YouTube URL shapes."""
    u = urlparse(url.strip())
    host = (u.hostname or "").lower().removeprefix("www.")
    if host in ("youtu.be",):
        vid = u.path.lstrip("/").split("/")[0]
    elif host in ("youtube.com", "m.youtube.com", "music.youtube.com"):
        if u.path == "/watch":
            vid = (parse_qs(u.query).get("v") or [""])[0]
        elif u.path.startswith(("/shorts/", "/embed/", "/v/", "/live/")):
            vid = u.path.split("/")[2]
        else:
            vid = ""
    else:
        return None
    vid = vid.strip()
    return vid if len(vid) == 11 else None


def _extract_youtube_sync(video_id: str) -> str:
    """Fetch a transcript (sync; runs in a thread). Prefers ru/en, else any."""
    from youtube_transcript_api import NoTranscriptFound, YouTubeTranscriptApi

    api = YouTubeTranscriptApi()
    tlist = api.list(video_id)
    try:
        transcript = tlist.find_transcript(["ru", "en"])
    except NoTranscriptFound:
        transcript = next(iter(tlist))  # first available language
    fetched = transcript.fetch()
    return "\n".join(s.text for s in fetched if getattr(s, "text", "").strip()).strip()


async def _youtube_title(video_id: str) -> str | None:
    """Best-effort video title via YouTube's public oEmbed (fixed host, no SSRF)."""
    try:
        async with httpx.AsyncClient(timeout=10.0, headers=_FETCH_HEADERS) as client:
            r = await client.get(
                "https://www.youtube.com/oembed",
                params={"url": f"https://www.youtube.com/watch?v={video_id}", "format": "json"},
            )
            r.raise_for_status()
            return r.json().get("title")
    except Exception:  # noqa: BLE001 — title is optional
        return None


async def ingest_youtube(session: AsyncSession, url: str) -> ContentSource:
    """Fetch a YouTube transcript into a ContentSource."""
    src = ContentSource(kind="youtube", url=url, status="pending")
    session.add(src)
    await session.flush()
    video_id = youtube_video_id(url)
    if not video_id:
        await _fail(session, src, "not a recognizable YouTube video URL")
        await session.commit()
        return src
    try:
        text = await asyncio.to_thread(_extract_youtube_sync, video_id)
        title = await _youtube_title(video_id) or f"YouTube {video_id}"
        await _finalize(session, src, title=title, text=text)
    except Exception as e:  # noqa: BLE001
        await _fail(session, src, f"youtube transcript failed: {type(e).__name__}: {e}")
    await session.commit()
    return src


async def ingest_article(session: AsyncSession, url: str) -> ContentSource:
    """Fetch + extract an article URL into a ContentSource (status set accordingly)."""
    src = ContentSource(kind="article", url=url, status="pending")
    session.add(src)
    await session.flush()
    try:
        title, text = await _extract_article(url)
        await _finalize(session, src, title=title, text=text)
    except Exception as e:  # noqa: BLE001
        await _fail(session, src, f"article extract failed: {e}")
    await session.commit()
    return src


async def ingest_pdf(session: AsyncSession, filename: str, data: bytes) -> ContentSource:
    """Extract text from an uploaded PDF into a ContentSource."""
    src = ContentSource(kind="pdf", title=filename, status="pending")
    session.add(src)
    await session.flush()
    try:
        text = document_to_text(filename, data)
        await _finalize(session, src, title=filename, text=text)
        if src.status == "extracted":  # keep the original bytes for native preview
            src.original_data = data
            src.original_mime = "application/pdf"
    except Exception as e:  # noqa: BLE001
        await _fail(session, src, f"pdf extract failed: {e}")
    await session.commit()
    return src


async def ingest_file(session: AsyncSession, filename: str, data: bytes) -> ContentSource:
    """Extract text from an uploaded document (txt/md/html/docx/pdf)."""
    src = ContentSource(kind="file", title=filename, status="pending")
    session.add(src)
    await session.flush()
    try:
        title, text = extract_file_text(filename, data)
        await _finalize(session, src, title=title or filename, text=text)
        # Документы с превью (pdf нативно, docx/xlsx — на клиенте) — храним оригинал.
        ext = _ext_of(filename)
        if src.status == "extracted" and ext in _PREVIEW_MIME:
            src.original_data = data
            src.original_mime = _PREVIEW_MIME[ext]
    except Exception as e:  # noqa: BLE001
        await _fail(session, src, f"file extract failed: {type(e).__name__}: {e}")
    await session.commit()
    return src


async def ingest_text(
    session: AsyncSession, title: str | None, text: str
) -> ContentSource:
    """Store raw pasted text as a learning material (kind='text')."""
    src = ContentSource(kind="text", title=title, status="pending")
    session.add(src)
    await session.flush()
    await _finalize(session, src, title=title or "Текст", text=text)
    await session.commit()
    return src


async def ingest_recognized(
    session: AsyncSession, title: str | None, text: str, *, kind: str = "file"
) -> ContentSource:
    """Store an already-recognized chat attachment as a learning ContentSource.

    Текст вложения уже распознан (vision/markitdown) на стороне чата — здесь его
    только кладём в память: chunked + (фоном) embedded → ищется и доступен Лектору.
    """
    if kind not in ("file", "text"):
        kind = "file"
    src = ContentSource(kind=kind, title=title, status="pending")
    session.add(src)
    await session.flush()
    await _finalize(session, src, title=title or "Файл", text=text)
    await session.commit()
    return src


async def _finalize(
    session: AsyncSession, src: ContentSource, *, title: str | None, text: str
) -> None:
    text = (text or "").strip()[:MAX_TEXT_CHARS]
    if not text:
        src.status = "failed"
        src.error = "no text extracted"
        return
    if title and not src.title:
        src.title = title[:500]
    src.text = text
    src.char_count = len(text)
    src.status = "extracted"
    for i, ch in enumerate(chunk_text(text)):
        session.add(ContentChunk(source_id=src.id, content=ch, position=i))


async def _fail(session: AsyncSession, src: ContentSource, msg: str) -> None:
    log.warning("content ingest %s: %s", src.id, msg)
    src.status = "failed"
    src.error = msg[:1000]


async def embed_pending_content(session: AsyncSession, limit: int = 64) -> int:
    """Embed up to `limit` content chunks whose embedding is still NULL."""
    pending = (
        await session.execute(
            select(ContentChunk).where(ContentChunk.embedding.is_(None)).limit(limit)
        )
    ).scalars().all()
    done = 0
    for ch in pending:
        ch.embedding = await embed_text(ch.content)
        done += 1
    if done:
        await session.commit()
    return done


async def content_remaining(session: AsyncSession) -> int:
    return int(
        (
            await session.execute(
                select(func.count())
                .select_from(ContentChunk)
                .where(ContentChunk.embedding.is_(None))
            )
        ).scalar_one()
    )
