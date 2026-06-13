# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> NOTE: a different `CLAUDE.md` lives at `C:\Users\Heart\CLAUDE.md` (it describes an unrelated project — QoldauFinance). **This file is the source of truth for PAM** — ignore the home-directory one when working in `Desktop\Pam\`.

> **Working mode:** when a task is given inside this project, operate as the **PAM Development Orchestrator** — see [`ORCHESTRATOR.md`](ORCHESTRATOR.md) (3-tier orchestrator/executor/verifier model, product priorities P0–P2, and the "simpler/more stable/clearer/more useful after every change" success criterion). This file (`CLAUDE.md`) stays the source of truth for code & architecture; `ORCHESTRATOR.md` governs *how* work is planned and verified.

## What this project is

**Personal AI Memory (PAM)** — a local-first personal-knowledge app. It started as a
capture-and-search tool for AI conversations and has grown into two connected products:

1. **Memory + chat** — capture conversations from ChatGPT / Claude via a
   browser extension into local Postgres+pgvector; a RAG **chat with memory**
   (`POST /chat`, SSE streaming) that retrieves across past conversations **and** learning
   material, supports **file/image attachments** (documents → markitdown, images → Groq
   vision; `POST /chat/attachment`), and auto-extracts `profile_facts` about the user.
2. **Лектор (learning)** — ingest material (article URL / YouTube transcript / uploaded
   file / pasted text) → extract text → generate a structured **mini-course**
   (modules→lessons + quiz) tailored to the user's level. The reader has YouTube/PDF
   preview and an AI "улучшить читаемость" reformat of the raw source text.

Everything stays **local-first**: conversations and material live in the local DB; only the
LLM calls (Groq / OpenRouter) and embeddings (local Ollama) reach out.

> **Historical phase plan (context only):** the build followed a 4-phase plan
> (`implementation-plan.html`, `VIBE_PROMPT.md`): (1) capture + full-text search, (2) RAG
> chunks/embeddings, (3) profile-fact extraction, (4) streaming chat. A later "Phase 5 —
> личный лектор" added the learning/course features. Those docs are historical and partly
> inaccurate (e.g. extraction/chat run on Groq/OpenRouter, not Gemini/Claude as the old
> table said) — **this file is the current source of truth.**

Only **Claude.ai and ChatGPT** capture is wired up. A Gemini content script was **removed**
(TODO #11): its non-standard stream format was never implemented, so the stub was dropped to
avoid implying Gemini capture works. `gemini` stays a valid `source` value (it could be
imported manually), but nothing captures it automatically. To add it later, write a new
`extension/contents/gemini.ts` after observing the real stream format in DevTools and re-add
the `gemini.google.com` host permission + relay match.

## Common commands

All commands assume CWD = `Desktop\Pam\` unless noted.

### Backend + DB (Docker)

```bash
docker compose up -d                                # starts Postgres + backend (alembic upgrade head runs automatically)
docker compose logs -f backend                      # tail backend logs
docker compose down                                 # stop
docker compose exec db psql -U pam pam              # psql shell
docker compose exec backend bash                    # bash inside backend container
```

Backend lives at `http://localhost:8000` (Swagger at `/docs`). Postgres on `:5432` (`pam` / `pam` / `pam`).

### Migrations (Alembic)

```bash
docker compose exec backend alembic revision --autogenerate -m "description"
docker compose exec backend alembic upgrade head
docker compose exec backend alembic downgrade -1
```

**Always migrate via Alembic** — no manual `ALTER`s. Each schema change is a new revision; never edit a merged one.

### Web UI

```bash
cd web
# one-time: see web/INSTALL.md (next-app scaffold + react-markdown)
npm run dev      # http://localhost:3000
npm run build
```

### Extension (Plasmo)

```bash
cd extension
# one-time: see extension/INSTALL.md (npm init + plasmo + manifest block)
npm run dev      # writes to extension/build/chrome-mv3-dev
npm run build
```

Then in Chrome: `chrome://extensions` → Developer mode → Load unpacked → `extension/build/chrome-mv3-dev`.

### Tests

A minimal **pytest** suite lives in `backend/tests/` — pure-function units (attachment
recognition dispatch, reformat chunking, filename sanitization, SSRF guard). Run it:

```bash
docker compose exec backend pytest -q
```

pytest deps live in `pyproject.toml` `[project.optional-dependencies] dev` (baked into the
image; `backend/tests` is also bind-mounted for live editing). There's no DB-backed/route
test yet and no web test runner — `npm run build` (in `web/`) is the type/compile gate for
the frontend. See `TODO.md` #12 for the planned expansion.

## Architecture — what requires reading multiple files to grasp

### Three-process data flow

```
content script (page world)  --postMessage-->  content script (isolated world)
       │                                              │
       │ patches window.fetch on AI site              │ chrome.runtime.sendMessage
       ▼                                              ▼
   reads response JSON                       background service worker
                                                      │
                                                      │ queue + retry (4s × attempt, max 5)
                                                      ▼
                                              POST http://localhost:8000/conversations
                                                      │
                                                      ▼
                                              FastAPI → normalize → UPSERT → Postgres
```

The split between **page world** and **isolated content-script world** matters: `window.fetch` patching must happen in `world: "MAIN"` (page world) — see `extension/contents/claude.ts` and `chatgpt.ts`. The page-world script can't call `chrome.runtime.sendMessage` directly (`chrome.runtime` is not exposed in `world: "MAIN"`), so it bridges via `window.postMessage` to an **isolated-world relay** (`extension/contents/relay.ts`, which has no `world` field and therefore runs in the default isolated world), which then forwards to the background worker. Plasmo registers the MAIN-world scripts dynamically via `chrome.scripting` (hence the auto-added `scripting` permission), while the isolated relay is declared as a normal `content_scripts` entry in the manifest.

### Normalization is two-sided

Each AI service returns very different JSON. Current design: **the extension normalizes** into the unified shape declared in `backend/app/schemas.py::IncomingConversation`, and the backend trusts it. There is **no** server-side normalizer — that dead fallback layer (`backend/app/normalizers.py`) was removed (TODO #9). If raw payloads ever need re-processing on the server, reintroduce a normalizer module deliberately rather than relying on a stub.

### Idempotency contract

`POST /conversations` is **UPSERT on `(source, external_id)`** — the same conversation will be re-sent every time the user reopens it. The route's current strategy on update is wipe-and-reinsert all messages of that conversation (see `routes/conversations.py::ingest_conversation`). This is intentional simplicity for Phase 1; a smarter diff is a Phase 2+ concern.

### Full-text search column is updated app-side, not by a trigger

`messages.content_tsv` is a `TSVECTOR` that is **filled via an explicit `UPDATE ... to_tsvector('simple', content)` after every ingest** in `routes/conversations.py`. If you add a new code path that inserts messages, you must update `content_tsv` too, or the message won't show up in `/search`. The dictionary is `"simple"` (no stemming) — chosen so Russian content also works.

### pgvector is enabled on day one

`alembic/versions/0001_initial.py` does `CREATE EXTENSION IF NOT EXISTS vector` even though Phase 1 doesn't use vectors. Phase 2 migrations should add the `chunks` table and `embedding vector(768)` column without re-enabling the extension.

### CORS handling

`backend/app/main.py` splits `CORS_ORIGINS` into explicit origins and wildcard patterns; wildcards become a single regex passed to `allow_origin_regex` because Chrome extensions have the form `chrome-extension://<random-id>`. If you add a new origin pattern to `.env`, it just works — don't touch the splitting logic.

## Pitfalls baked into the design

These are documented in `VIBE_PROMPT.md` and `implementation-plan.html`; restating the load-bearing ones:

- **Don't parse DOM** in extension content scripts. Patch `window.fetch` instead. DOM scraping breaks on every UI tweak; fetch shapes change much less often.
- **Manifest V3** disallows persistent background scripts — keep using the service-worker model in `background.ts`. For long-running work (Phase 2+ embedding pipeline lives in the backend, **not** in the extension).
- **`host_permissions`** for `http://localhost:8000/*` must stay in the extension manifest (see `extension/INSTALL.md`) or the extension can't reach the backend.
- **Russian-language UI**: code identifiers and comments are mixed Russian/English, the UI is Russian. Keep UI strings in Russian unless changing a whole page.
- **Phase 3 prompt-injection awareness**: conversations may contain `"ignore previous instructions"`. When you build the extraction prompt, wrap user content in clearly delimited blocks and don't trust nested instructions.
- **Phase 3 hallucination guard**: every extracted fact must store `source_message_id`. Never persist an LLM-extracted fact you can't trace back to a message.
- **Fact Review Queue gate (P0)**: a `profile_facts.status` (`pending_review|approved|rejected|edited`) governs what reaches chat memory. Extraction writes `pending_review`; **only `approved`+`edited` are retrieved** (`chat._profile_facts`, single source of truth `ACCEPTED_FACT_STATUSES`/`is_fact_accepted` in `models.py`). If you add a new path that surfaces facts into a prompt, gate it the same way — don't read raw `profile_facts` without filtering status.
- **Observability is best-effort, off the critical path**: instrument boundaries with `await record_event(...)` from `metrics.py` only — it opens its own session and swallows all errors. Never let a metric write change behavior or block a response. Do **not** record per-tick errors in the 15s embed worker (floods on no-Ollama machines); the DB snapshot in `/stats` already shows the embed backlog (`chunks_pending`). The `/diag` panel reads `/stats`; live counts come from the data tables, capture-heavy metrics from the `events` log. **Capture reliability** = `import` events vs `capture_failure` events — the extension reports a permanent drop via `POST /stats/capture-failed` after exhausting its retry queue (best-effort: if the backend is fully down the drop can't be reported). **Memory Health Score** is a weighted composite (capture .35 / indexing .25 / review .20 / stability .20) computed in `stats.py` (pure `compute_health`/`health_label`); the panel shows the score + per-component breakdown. On a no-Ollama machine `indexing` is 0 (embeddings dark) so the score caps ~75 — that's the honest signal, not a bug.
- **Learning progress is server-derived, one row per course**: `course_progress` (unique `course_id`) stores `completed_lessons` (keys `"<moduleIdx>-<lessonIdx>"`), a `lessons_total` snapshot, and the quiz result. **Status and percent are computed, not stored** (`course_study_status`/`course_percent` in `models.py`). Regenerating a course = new `course_id` = fresh progress (by design). This replaced the old localStorage `course-status.ts` (removed) — don't reintroduce a parallel client-side study status.
- **Provider paths are a closed set (don't add branches)**: the only sanctioned LLM paths are **Groq** (fast default), **OpenRouter** (heavy/hybrid via `route_provider`), **Ollama** (local chat + the local embeddings in `indexing.py`), and **vision** (Groq-vision or OpenRouter in `describe_image`/`stream_vision`). There is **no provider-to-provider failure fallback** — the only real degradation is vision→text pre-pass in `chat.py`. Add a new provider/branch only with a measured benefit (see iteration brief #6).
- **New App Router routes 404 until `docker compose restart web`**: the dockerized `next dev` (Turbopack over a bind mount with polling) does **not** reliably hot-register a brand-new route *folder* (`web/app/<new>/page.tsx`) added while it's running — the route returns 404 even though `tsc` is clean. Restart the web container to re-scan routes. Editing an existing route hot-reloads fine; only brand-new route segments need the restart. (Verifying a new page over HTTP, not just `tsc`, catches this.)

## When extension capture breaks

Symptoms: `parse error` in DevTools console of the AI site, or zero conversations arriving despite usage.

1. Open DevTools → Network on the relevant AI site.
2. Find the request that returns a full conversation in JSON.
3. Update `URL_RE` and/or the parser in `extension/contents/<site>.ts`.
4. The per-site scripts are isolated — fixing one doesn't affect the other.

Manual import via the AI sites' official export feature is the documented fallback if a parser stays broken.

## File map (only the non-obvious bits)

```
backend/app/
  main.py            FastAPI app + CORS wildcard regex assembly
  config.py          settings: LLM provider/models (incl. GROQ_VISION_MODEL), Ollama, DB
  llm.py             provider-agnostic chat (Groq/OpenRouter/Ollama): stream/complete,
                     hybrid router, describe_image (vision for attachments)
  indexing.py        chunk_text + local Ollama embeddings (nomic-embed-text, 768-dim)
  content.py         Лектор ingest + extraction (markitdown pdf/docx/xlsx/pptx), article/
                     YouTube fetch, SSRF guard, recognize_attachment (image→vision/doc→md)
  courses.py         generate a mini-course (modules/lessons/quiz) from a ContentSource
  formatting.py      AI «улучшить читаемость» reformat of raw source text (chunked)
  extraction.py      extract profile_facts ABOUT THE USER from conversations
  models.py          SQLAlchemy models (Conversation/Message/Chunk, ContentSource/
                     ContentChunk, Course, CourseProgress, ProfileFact, SavedMessage, Event)
  routes/
    conversations.py UPSERT + wipe-and-reinsert messages + tsvector UPDATE
    search.py        websearch_to_tsquery, ts_headline snippets, ts_rank ordering
    chat.py          POST /chat (SSE RAG chat) + POST /chat/attachment (recognition)
    learn.py         Лектор: ingest sources, course gen, PDF file preview, reformat,
                     learning progress (sources/{id}/progress|lesson|quiz, /learn/progress)
    facts.py         profile_facts review queue: extract + list(?status=) + counts
                     + approve/reject + PATCH(edit→edited) + delete
    stats.py         GET /stats — observability snapshot (DB counts + events aggregates
                     + capture reliability + Memory Health Score); POST /stats/capture-failed
  metrics.py         record_event(...) — best-effort append to `events` (never raises)
    saved.py         starred («Избранное») messages
  tests/             pytest units (recognize dispatch, reformat split, _safe_name, SSRF)

extension/
  background.ts      retry queue (4s × attempts, MAX_ATTEMPTS=5); stats in chrome.storage.local
  contents/*.ts      MAIN-world fetch patch → postMessage (claude.ts, chatgpt.ts)
  contents/relay.ts  isolated-world bridge: window.postMessage → chrome.runtime.sendMessage

web/app/
  page.tsx           CHAT page (RAG chat + attachments, glass UI, центр-glow);
                     empty state (no messages) renders <HomeDashboard/>
  home-dashboard.tsx Home dashboard shown on the empty chat landing (continue last
                     chat, recent materials, active courses %, pending facts, actions)
  chat-sidebar.tsx   chat sidebar (date-grouped «Недавнее», search modal, glass «Новый чат»)
  c/[id]/page.tsx    conversation detail with markdown (opened from History)
  learn/page.tsx     Лектор: add material + materials grid/list
  courses/[id]/page.tsx  course reader (содержание/конспект/тест, YouTube/PDF hero, reformat)
  markdown.tsx       ReactMarkdown (code blocks, reader variant, empty-img guard)
web/lib/
  api.ts             backend client (fetch wrappers + streamChat SSE)
  material-ui.tsx    shared helpers: Kind/KIND_LABEL/kindOf/youtubeId/fmtDate/PlayIcon/cleanSourceMarkdown

implementation-plan.html   The 4-phase plan (open in a browser, not Read) — historical
VIBE_PROMPT.md             Session-starter prompt — historical
TODO.md                    prioritized roadmap / backlog
```

## Conventions

- **Russian language** in UI strings and many comments. Don't translate to English unless asked.
- **Local-first**: don't introduce cloud DBs, cloud auth, or telemetry without explicit ask. AI APIs are added per phase plan; conversations themselves stay local.
- **`source` enum** is `chatgpt | claude | gemini` everywhere. Don't invent new sources without updating the `Source` Literal in `backend/app/schemas.py`, the extension content scripts, and the Web UI filter chips.
- **Currency / locale**: no currency in this project (it's not a finance app — that's a different project on the same machine). Timestamps are timezone-aware (`DateTime(timezone=True)`).
