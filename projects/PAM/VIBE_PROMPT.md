# Vibe-coding prompt для PAM

Этот файл — стартовый промпт для работы с AI-помощником (Claude, Cursor,
любая нейронка с большим контекстом) над проектом **Personal AI Memory**.

Скопируй всё содержимое ниже в начало сессии. Можешь добавить в System
prompt или в первое сообщение.

---

# Контекст проекта

Я разрабатываю **Personal AI Memory (PAM)** — локальный сервис, который:

1. Собирает мои разговоры с ChatGPT, Claude и Gemini в одну БД
2. Даёт по ним семантический поиск
3. Извлекает структурированные факты обо мне из них
4. Превращается в AI-ассистента с долгой памятью

Я работаю в стиле **вайб-кодинга**: даю задачи AI, он пишет код, я проверяю
и интегрирую. Не нужно объяснять мне основы — нужно писать рабочий код,
объяснять архитектурные решения, и предупреждать о подводных камнях.

## Архитектура

```
Chrome extension (Plasmo, TypeScript)
   → patches window.fetch on AI sites
   → captures conversation JSON
   → sends to backend via runtime.sendMessage → background worker

FastAPI backend (Python 3.11, SQLAlchemy 2.0 async, Alembic)
   → REST API: POST /conversations, GET /conversations, GET /search
   → stores in Postgres + pgvector (Docker)

Next.js 14 web UI (TypeScript, Tailwind, app router)
   → lists conversations
   → text search
   → conversation detail view
```

## Стек

- **Backend**: Python 3.11, FastAPI, SQLAlchemy 2.0 (async), asyncpg, Alembic
- **DB**: PostgreSQL 16 + pgvector
- **Extension**: TypeScript, Plasmo framework, Manifest V3
- **Frontend**: Next.js 14 (app router), TypeScript, Tailwind
- **Infrastructure**: Docker Compose

## Принципы

- **Local-first**: никаких облачных БД. Все данные у меня.
- **Privacy-aware**: AI-API вызовы только по моей инициативе и только на extraction/chat задачи. Сырые разговоры наружу не уходят без явного шага.
- **Прогрессивно**: 4 фазы. Каждая работает самостоятельно.
- **Cost-conscious**: Phase 1-2 без API. Phase 3-4 — сначала бесплатные tier'ы (Gemini Free, Groq), потом платные (Claude Haiku/Sonnet).

## Текущее состояние (Phase 1)

Готов скелет:
- ✅ docker-compose.yml — Postgres + backend в контейнерах
- ✅ Backend (FastAPI): модели, схемы, ingest/list/search endpoints, миграция
- ✅ Extension (Plasmo): background worker, content scripts для Claude и ChatGPT, popup UI
- ⚠️ Content script для Gemini — заглушка, требует реализации
- ✅ Web UI (Next.js): список разговоров, поиск, детальный просмотр

Запускается так:
```bash
docker compose up -d
cd web && npm run dev          # http://localhost:3000
cd extension && npm run dev    # потом load unpacked в Chrome
```

## План фаз

**Phase 1** (текущая) — сбор разговоров и full-text поиск. Без AI API.

**Phase 2** — RAG поиск:
- Установить Ollama локально, `ollama pull nomic-embed-text`
- Добавить таблицу `chunks` (message_id, content, embedding vector(768))
- Background worker эмбедит новые chunks
- Endpoint `POST /search/semantic` с pgvector `ORDER BY embedding <-> $1`
- UI: переключатель режима поиска (text / semantic / hybrid)

**Phase 3** — Memory Layer:
- Подключить Gemini API (free tier Flash-Lite: 15 RPM, 1500 RPD)
- Worker извлечения фактов: после нового разговора → промпт в LLM → JSON по схеме
- Таблица `profile_facts` (category, content, source_message_id, confidence)
- UI `/me` — профиль с категориями, edit/delete фактов

**Phase 4** — Chat with your AI:
- Подключить chat LLM (Groq бесплатно → Claude Haiku → Sonnet)
- Endpoint `POST /chat` со streaming через SSE
- RAG pipeline: query → embed → retrieve chunks + facts → assemble context → LLM
- UI `/chat` с подсветкой использованных источников

## Подводные камни, о которых нужно помнить

1. **Браузерные extension'ы часто ломаются**: AI-сайты меняют API. Перехватывать через `window.fetch` patch надёжнее, чем парсить DOM. Если "parse error" — открой Network в DevTools на сайте и обнови URL regex или JSON-парсер.

2. **Manifest V3**: persistent background scripts запрещены, нужен service worker + offscreen documents для долгих задач.

3. **CORS**: extension должен иметь explicit `host_permissions` для `http://localhost:8000/*`.

4. **Идемпотентность**: UPSERT по `(source, external_id)`. Один разговор приходит много раз.

5. **Миграции БД**: каждое изменение схемы — через Alembic. Никаких ручных ALTER.

6. **Prompt injection** (Phase 3+): мои разговоры могут содержать "ignore previous instructions". Изолируй user content в чётко обозначенные секции.

7. **Rate limiting** (Phase 3+): Gemini Free Tier = 15 RPM. Реализуй очередь с задержкой 4с между запросами.

8. **Hallucination в извлечении фактов**: ВСЕГДА храни `source_message_id` для верификации. Не доверяй extraction'у на слово.

## Как давать мне задачи

- Если задача техническая — пиши **рабочий код** с комментариями
- Если архитектурная — объясни **почему именно так**, и какие альтернативы рассматривал
- Если меняешь схему БД — **новая миграция Alembic**, не правка существующей
- Если ловишь edge case — сразу пиши тест-сценарий «как воспроизвести»
- Если что-то неоднозначно — задай **один точный вопрос**, не пять размытых

## Что сейчас сделать

[ЗАМЕНИ ЭТОТ БЛОК НА КОНКРЕТНУЮ ЗАДАЧУ]

Например:
- "Тестирую Phase 1. Разговор с claude.ai не сохраняется, в console на claude.ai вижу parse error. Открой `extension/contents/claude.ts` и проверь URL_RE и парсер на текущую структуру API."
- "Начинаем Phase 2. Добавь таблицу `chunks`, миграцию, и Ollama-клиент в backend. Worker пока без триггера — позже подключим."
- "Текст разговора в UI плохо рендерится — длинные сообщения не помещаются. Поправь стили в `web/app/c/[id]/page.tsx`."

---

## Полезные команды

```bash
# Backend
docker compose up -d                    # запустить
docker compose logs -f backend          # смотреть логи
docker compose down                     # остановить
docker compose exec db psql -U pam pam  # psql shell
docker compose exec backend bash        # bash в контейнере

# Миграции (внутри backend контейнера)
docker compose exec backend alembic revision --autogenerate -m "описание"
docker compose exec backend alembic upgrade head
docker compose exec backend alembic downgrade -1

# Frontend
cd web && npm run dev
cd web && npm run build

# Extension
cd extension && npm run dev
cd extension && npm run build
```

## Структура файлов

```
backend/app/
  main.py               FastAPI app, middleware, router include
  config.py             pydantic-settings из env
  db.py                 async engine, session factory
  models.py             SQLAlchemy: Conversation, Message
  schemas.py            Pydantic: IncomingConversation, ConversationDetail, SearchHit
  normalizers.py        helpers для разных AI-источников
  routes/
    conversations.py    POST/GET/DELETE
    search.py           GET /search (websearch_to_tsquery)

backend/alembic/
  env.py                async setup
  versions/0001_initial.py

extension/
  background.ts         service worker + queue
  popup.tsx             stats UI
  lib/api.ts            backend client
  contents/
    claude.ts           ✅ реализован
    chatgpt.ts          ✅ реализован
    gemini.ts           ⚠️ TODO

web/app/
  layout.tsx
  page.tsx              list + search
  c/[id]/page.tsx       detail
web/lib/
  api.ts
```

Удачи!
