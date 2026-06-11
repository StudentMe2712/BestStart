# PAM — Personal AI Memory

> Phase 1 skeleton: collect your ChatGPT / Claude / Gemini conversations
> in one local database with full-text search. Local-first, no external APIs.

## Что это

Браузерный extension перехватывает твои разговоры с AI-сервисами и сохраняет
их в локальную PostgreSQL. Веб-интерфейс показывает все разговоры в одном
месте и даёт по ним искать.

Это **Phase 1** из 4-фазного плана. Дальше — RAG-поиск (Phase 2),
извлечение фактов о тебе (Phase 3), персональный AI-чат (Phase 4).

## Архитектура

```
Chrome extension  ─POST→  FastAPI backend  ─SQL→  Postgres + pgvector
   (Plasmo, TS)              (Python)                  (Docker)

                                  ↑
                                  │ GET conversations, search
                                  │
                            Next.js web UI
                              (TypeScript)
```

## Перед началом

Установить:
- **Docker Desktop** — https://www.docker.com/products/docker-desktop
- **Node.js 20+** — https://nodejs.org
- (опционально, для разработки backend без Docker) Python 3.11+

## Шаг 1. Запустить backend + БД

```bash
# из корня проекта
docker compose up -d
```

Это поднимет:
- Postgres с расширением pgvector на :5432
- FastAPI backend на :8000 (с авто-миграциями и hot-reload)

Проверь:
- http://localhost:8000 — должно вернуть JSON `{"service": "Personal AI Memory", ...}`
- http://localhost:8000/docs — Swagger UI с API

Логи:
```bash
docker compose logs -f backend
```

Остановить:
```bash
docker compose down
```

## Шаг 2. Запустить web UI

```bash
cd web
# см. web/INSTALL.md — это однократная настройка
# после установки:
npm run dev
```

Открой http://localhost:3000

## Шаг 3. Поставить extension

```bash
cd extension
# см. extension/INSTALL.md
npm run dev
```

Затем в Chrome:
1. Открой `chrome://extensions`
2. Включи **Developer mode** (правый верхний угол)
3. **Load unpacked** → выбери папку `extension/build/chrome-mv3-dev`

Кликни на иконку расширения — увидишь popup со статистикой.

## Шаг 4. Использование

1. Открой [claude.ai](https://claude.ai) или [chatgpt.com](https://chatgpt.com)
2. Открой существующий разговор или начни новый
3. В popup'е расширения цифра «Сохранено» должна вырасти
4. Открой http://localhost:3000 — увидишь свой разговор

## Что внутри

```
pam/
├── docker-compose.yml      Postgres + backend
├── backend/                FastAPI + SQLAlchemy + Alembic
│   ├── app/
│   │   ├── main.py         entry point
│   │   ├── models.py       SQLAlchemy schema
│   │   ├── schemas.py      Pydantic
│   │   ├── normalizers.py  AI-specific JSON → unified format
│   │   └── routes/         REST endpoints
│   └── alembic/            DB migrations
├── extension/              Plasmo Chrome extension
│   ├── background.ts       service worker (queue, retries)
│   ├── popup.tsx           extension popup UI
│   ├── contents/           per-site interceptors (claude/chatgpt/gemini, MAIN world)
│   │   └── relay.ts        isolated-world bridge → background worker
│   └── lib/                shared utils
├── web/                    Next.js 16 UI
│   ├── app/
│   │   ├── page.tsx        list & search
│   │   └── c/[id]/         conversation detail
│   └── lib/                API client
└── docs/
    └── VIBE_PROMPT.md      promt for vibe-coding next phases
```

## Возможные проблемы

**`docker compose up` падает**
→ убедись что Docker Desktop запущен. На macOS — иконка в menu bar.

**Backend ответил 500 на ingest**
→ `docker compose logs backend`. Самые частые причины: миграция не применилась
(подожди 5-10 сек после запуска) или Postgres ещё не готов.

**Extension не появляется в Chrome**
→ убедись что выбрал папку `build/chrome-mv3-dev`, а не корень `extension/`.

**Разговоры с Claude/ChatGPT не сохраняются**
→ сайты периодически меняют свои API. Открой DevTools на claude.ai/chatgpt.com,
посмотри Console — там сообщения от content script. Если "parse error" —
обнови `URL_RE` или парсер в соответствующем `contents/*.ts` файле.

**Gemini не работает**
→ ожидаемо. Заглушка в `contents/gemini.ts`. См. там TODO.

## Что дальше

См. `docs/VIBE_PROMPT.md` — промпт для продолжения работы с AI-помощником.
Там полный контекст проекта, текущее состояние и задачи для следующих фаз.
