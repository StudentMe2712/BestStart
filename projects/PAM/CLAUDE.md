# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> NOTE: a different `CLAUDE.md` lives at `C:\Users\Heart\CLAUDE.md` (it describes an unrelated project — QoldauFinance). **This file is the source of truth for PAM** — ignore the home-directory one when working in `Desktop\Pam\`.

## What this project is

**Personal AI Memory (PAM)** — local-first service that:
1. Captures the user's conversations from ChatGPT / Claude / Gemini via a browser extension.
2. Stores them in a local Postgres + pgvector DB.
3. Exposes search and (later phases) a RAG chat over them.

The project follows a **4-phase plan** documented in `implementation-plan.html` and `VIBE_PROMPT.md`:

| Phase | Goal | External APIs |
|---|---|---|
| 1 (current skeleton in place) | Capture + full-text search | none |
| 2 | RAG: chunks, embeddings via local Ollama, hybrid (text+vector) search | none |
| 3 | Memory layer: Gemini extracts structured `profile_facts` | Gemini Free Tier |
| 4 | Chat: SSE streaming `POST /chat`, RAG retrieval, Claude/Groq | Claude / Groq |

Each phase is shippable on its own. Don't pull Phase N+1 work into Phase N.

The Gemini content script is intentionally a **stub** (`extension/contents/gemini.ts`) — Claude.ai and ChatGPT capture is wired up; Gemini is left to be implemented after observing the real streaming format in DevTools.

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

No test harness exists yet. When adding tests, prefer `pytest` for the backend and Jest/Vitest only if/when justified for extension or web.

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

Each AI service returns very different JSON. Current design: **the extension normalizes** into the unified shape declared in `backend/app/schemas.py::IncomingConversation`, and the backend mostly trusts it. `backend/app/normalizers.py` contains parallel server-side normalizers that are **not currently called** from routes — they exist as a fallback / reference for when raw payloads need re-processing on the server. If you change one side's parser, decide whether the other needs to follow.

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

## When extension capture breaks

Symptoms: `parse error` in DevTools console of the AI site, or zero conversations arriving despite usage.

1. Open DevTools → Network on the relevant AI site.
2. Find the request that returns a full conversation in JSON.
3. Update `URL_RE` and/or the parser in `extension/contents/<site>.ts`.
4. The three sources are isolated — fixing one doesn't affect the others.

Manual import via the AI sites' official export feature is the documented fallback if a parser stays broken.

## File map (only the non-obvious bits)

```
backend/app/
  main.py            FastAPI app + CORS wildcard regex assembly
  normalizers.py     server-side fallback normalizers (NOT currently invoked from routes)
  routes/
    conversations.py UPSERT + wipe-and-reinsert messages + tsvector UPDATE
    search.py        websearch_to_tsquery, ts_headline snippets, ts_rank ordering

extension/
  background.ts      retry queue (4s × attempts, MAX_ATTEMPTS=5); stats in chrome.storage.local
  contents/*.ts      MAIN-world fetch patch → postMessage (claude.ts, chatgpt.ts; gemini.ts is a stub)
  contents/relay.ts  isolated-world bridge: window.postMessage → chrome.runtime.sendMessage
  contents/gemini.ts STUB — implement after observing real streaming format in DevTools

web/app/
  page.tsx           list + 300ms debounced search
  c/[id]/page.tsx    conversation detail with markdown

implementation-plan.html   The 4-phase plan (open in a browser, not Read)
VIBE_PROMPT.md             Session-starter prompt with full project context
docs/VIBE_PROMPT.md        Duplicate copy of the above
```

## Conventions

- **Russian language** in UI strings and many comments. Don't translate to English unless asked.
- **Local-first**: don't introduce cloud DBs, cloud auth, or telemetry without explicit ask. AI APIs are added per phase plan; conversations themselves stay local.
- **`source` enum** is `chatgpt | claude | gemini` everywhere. Don't invent new sources without updating the `Source` Literal in `backend/app/schemas.py`, the extension content scripts, and the Web UI filter chips.
- **Currency / locale**: no currency in this project (it's not a finance app — that's a different project on the same machine). Timestamps are timezone-aware (`DateTime(timezone=True)`).
