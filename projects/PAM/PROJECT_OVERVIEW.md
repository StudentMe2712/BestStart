# PAM — Personal AI Memory · полный обзор проекта

> Сгенерировано как единый детальный справочник по всему, что есть в проекте `projects/PAM`.
> Источник истины по правилам разработки — `CLAUDE.md`; текущая позиция — `.planning/STATE.md`;
> план фаз — `.planning/ROADMAP.md`. Этот файл сводит всё это вместе + разбор кода.

---

## 1. Что это за продукт

**Personal AI Memory (PAM)** — локально-ориентированное (local-first) приложение «персональный AI
с долгой памятью о тебе». Изначально — инструмент захвата и поиска по разговорам с AI, вырос в
**два связанных продукта** на общей инфраструктуре:

1. **Чат с долгой памятью** (главный экран) — пишешь прямо в PAM; ассистент отвечает, доставая
   релевантный контекст из накопленной памяти (прошлые разговоры + факты о тебе) через RAG.
   Поддерживает вложения файлов/изображений и авто-извлечение фактов о пользователе.
2. **Лектор (обучение)** — кидаешь материал (URL статьи / YouTube-ссылку / загруженный файл /
   вставленный текст) → извлекается текст → генерируется структурированный **мини-курс**
   (модули → уроки + квиз) под уровень пользователя. У читалки есть превью YouTube/PDF/Word/Excel
   и AI-функция «улучшить читаемость».

**Вспомогательный раздел** — «История / Импорт»: браузерное расширение тянет разговоры из
ChatGPT/Claude, чтобы засеять память прошлым. Просмотр + полнотекстовый/семантический поиск.

**Local-first:** разговоры и материалы живут в локальной БД; наружу ходят только вызовы LLM
(Groq / OpenRouter) и эмбеддинги (локальный Ollama).

### Целевой пользователь (из профиля памяти)
Системный администратор / спец по компьютерным сетям и 1С (Windows-окружение). Системный промпт
чата и калибровка курсов это учитывают.

---

## 2. Технологический стек

| Слой | Технологии |
|------|-----------|
| **Backend** | Python 3.11+, FastAPI, SQLAlchemy 2.0 (async), Alembic, Pydantic v2 / pydantic-settings, httpx, uvicorn |
| **БД** | PostgreSQL 17 + расширение **pgvector** (вектор 768-dim, индекс по cosine), полнотекст через `tsvector`/`websearch_to_tsquery` (словарь `simple`) |
| **Эмбеддинги** | Локальный **Ollama**, модель `nomic-embed-text` (768-dim) |
| **LLM (чат/извлечение)** | Провайдер-агностично: **Groq** (free, быстро), **OpenRouter** (мощно), **hybrid** (роутер), **Ollama** (локально). Всё через OpenAI-совместимый API |
| **Vision** | Groq `meta-llama/llama-4-scout-17b-16e-instruct` (или OpenRouter) для распознавания изображений |
| **Извлечение текста** | `markitdown` (pdf/docx/pptx/xlsx), BeautifulSoup (статьи), `youtube-transcript-api` (YouTube) |
| **Web UI** | Next.js 16, React 19, Tailwind v4, react-markdown + remark-gfm, react-syntax-highlighter; `mammoth` (Word→HTML на клиенте), `xlsx`/SheetJS (Excel на клиенте) |
| **Расширение** | Plasmo 0.90, React 19, TypeScript, Manifest V3 |
| **Прототип UI** | v0.dev-проект (`v0dev/`) — Next.js + shadcn/ui (полный набор Radix-компонентов) |
| **Инфраструктура** | Docker Compose (Postgres + backend + web + extension), `.bat`-лаунчеры для Windows-dev без Docker |

---

## 3. Структура репозитория

```
projects/PAM/
├── CLAUDE.md                  источник истины: что за проект, архитектура, правила, pitfalls
├── README.md                  быстрый старт (исторически Phase 1)
├── PROJECT_OVERVIEW.md        ← этот файл
├── TODO.md                    приоритизированный бэклог
├── VIBE_PROMPT.md             исторический промпт-стартер сессий
├── SECOND_PC_PROMPT.md        бутстрап на второй машине
├── handoff.md                 история работы (хронология)
├── implementation-plan.html   4-фазный план (открывать в браузере) — исторический
├── docker-compose.yml         Postgres + backend + web + extension
├── dev.bat / stop-dev.bat     Windows-лаунчеры dev без Docker (backend+extension скрыто, web в окне)
├── .env.example               образец переменных (копировать в backend/.env)
├── .gitignore
├── .claude/commands/handoff.md  кастомная команда обновления handoff
├── .planning/
│   ├── STATE.md               «где мы сейчас» (фазы 1–5 завершены)
│   └── ROADMAP.md             план фаз + продуктовое видение
│
├── backend/                   FastAPI + SQLAlchemy + Alembic
│   ├── Dockerfile
│   ├── pyproject.toml          зависимости + pytest config
│   ├── alembic.ini, alembic/   миграции (env.py + versions/)
│   ├── app/
│   │   ├── main.py            точка входа FastAPI + CORS + фоновый embed-воркер
│   │   ├── config.py          настройки (LLM-провайдеры, модели, Ollama, DB, CORS)
│   │   ├── db.py              async-движок + сессии
│   │   ├── models.py          SQLAlchemy-модели (8 таблиц)
│   │   ├── schemas.py         Pydantic-схемы API
│   │   ├── llm.py             провайдер-агностичный LLM (stream/complete/vision + hybrid-роутер)
│   │   ├── indexing.py        chunk_text + эмбеддинги (Ollama) + воркер индексации
│   │   ├── content.py         Лектор: ингест статьи/PDF/YouTube/файла + SSRF-guard + распознавание вложений
│   │   ├── courses.py         генерация мини-курса из материала
│   │   ├── formatting.py      AI «улучшить читаемость» (фоновый реформат с прогрессом)
│   │   ├── extraction.py      извлечение profile_facts о пользователе
│   │   └── routes/
│   │       ├── conversations.py  ingest (UPSERT) / list / detail / patch / delete
│   │       ├── search.py         /search (full-text) /semantic /hybrid (RRF)
│   │       ├── chat.py           POST /chat (RAG+SSE) + /chat/attachment (распознавание)
│   │       ├── learn.py          Лектор: ингест источников, курсы, превью файла, реформат
│   │       ├── facts.py          profile_facts: extract / list / delete
│   │       ├── saved.py          избранные сообщения (снимки)
│   │       └── indexing.py       POST /index/run (ручной триггер индексации)
│   └── tests/                  pytest-юниты (recognize, formatting split, _safe_name, SSRF, retrieve)
│
├── extension/                 Plasmo Chrome MV3 extension
│   ├── package.json           манифест (host_permissions, permissions: storage)
│   ├── background.ts          service worker: очередь + ретраи (4с×попытка, max 5), статистика
│   ├── popup.tsx              popup со статистикой (Сохранено / В очереди / Ошибок)
│   ├── contents/
│   │   ├── claude.ts          MAIN-world: патч window.fetch на claude.ai → postMessage
│   │   ├── chatgpt.ts         MAIN-world: патч window.fetch на chatgpt.com (linearize mapping-дерева)
│   │   └── relay.ts           isolated-world мост: window.postMessage → chrome.runtime.sendMessage
│   ├── lib/api.ts             клиент backend (sendConversation)
│   ├── assets/icon.png
│   ├── tsconfig.json
│   └── INSTALL.md
│
├── web/                       Next.js 16 UI (основной интерфейс)
│   ├── package.json
│   ├── app/
│   │   ├── layout.tsx, globals.css, icon.svg, nav.tsx
│   │   ├── page.tsx           ГЛАВНАЯ: чат (RAG + вложения + multimodal, glass-UI)   [654 строки]
│   │   ├── chat-sidebar.tsx   сайдбар чатов (Закреплённые/Недавнее, поиск, контекст-меню) [521]
│   │   ├── markdown.tsx       ReactMarkdown (код-блоки, reader-вариант, guard пустых img)
│   │   ├── c/[id]/page.tsx    детальный просмотр разговора (из Истории)               [136]
│   │   ├── history/page.tsx   История/Импорт: список + поиск (text/semantic/hybrid)   [200]
│   │   ├── saved/page.tsx     Избранное                                                [97]
│   │   ├── me/page.tsx        «Память обо мне» — профиль фактов по категориям          [148]
│   │   ├── learn/page.tsx     Лектор: добавить материал + сетка/список материалов      [749]
│   │   ├── courses/[id]/page.tsx  читалка курса (содержание/конспект/тест, превью)     [872]
│   │   ├── catalog/page.tsx       каталог AI-экосистемы (витрина)                      [707]
│   │   ├── catalog/[slug]/page.tsx деталь инструмента каталога                          [251]
│   │   └── refresh-button.tsx
│   ├── lib/
│   │   ├── api.ts             клиент backend (fetch-обёртки + streamChat SSE)
│   │   ├── material-ui.tsx    хелперы материалов (Kind/KIND_LABEL/youtubeId/cleanSourceMarkdown/PlayIcon)
│   │   ├── catalog.ts         данные витрины каталога (TOOLS, STARS, категории)
│   │   ├── course-export.ts   экспорт курса в Markdown / PDF (печать)
│   │   ├── course-status.ts   личный статус изучения курса (localStorage)
│   │   └── cache.ts           клиентский кэш списков (мгновенные переходы)
│   ├── types/mammoth-browser.d.ts
│   └── public/pam-logo.png
│
├── v0dev/                     v0.dev-прототип чат-UI (Next + полный shadcn/ui)
│   ├── app/, components/pam/, components/ui/ (~50 Radix-компонентов), hooks/, lib/, public/
│   └── package.json, pnpm-lock.yaml, ...
│
└── docs/
    ├── SETUP.md
    ├── NEW_MACHINE_PROMPT.md       восстановление на новой машине
    ├── VIBE_PROMPT.md              исторический
    ├── v0-chat-ui-prompt.md        промпт для v0.dev
    ├── ai-ecosystem-catalog.md     источник данных для витрины каталога
    └── claude-code-best-practices.md
```

---

## 4. Архитектура и ключевые потоки данных

### 4.1. Захват разговоров расширением (три процесса)

```
content script (page world, world:"MAIN")  --window.postMessage-->  relay (isolated world)
       │ патчит window.fetch на AI-сайте                                 │ chrome.runtime.sendMessage
       ▼                                                                 ▼
   читает JSON ответа                                          background service worker
                                                                         │ очередь + ретрай (4с×попытка, max 5)
                                                                         ▼
                                                       POST http://localhost:8000/conversations
                                                                         ▼
                                                   FastAPI → нормализация → UPSERT → Postgres
```

**Почему три процесса.** Патчить `window.fetch` можно только в page-world (`world:"MAIN"`), но там
нет `chrome.runtime`. Поэтому MAIN-скрипт шлёт данные через `window.postMessage` в **isolated-world
relay** (`relay.ts`, без `world` → дефолтный изолированный мир, где `chrome.runtime` доступен),
который форвардит в background. Plasmo регистрирует MAIN-скрипты динамически через `chrome.scripting`
(отсюда авто-разрешение `scripting`), а relay объявлен обычным `content_scripts`.

**Нормализация — на стороне расширения.** Каждый AI-сервис отдаёт свой JSON; расширение приводит его
к единой форме `schemas.py::IncomingConversation`, backend ей доверяет. **Серверного нормализатора нет**
(мёртвый `normalizers.py` удалён, TODO #9).

- `claude.ts` — ловит `GET /api/organizations/{org}/chat_conversations/{id}`, берёт `chat_messages`.
- `chatgpt.ts` — ловит `/backend-api/conversation/{id}`, **линеаризует дерево `mapping`** (BFS от корня,
  parts могут быть строками или объектами — собираются обе формы).
- **Правило:** не парсить DOM, патчить `fetch` (формы ответов меняются реже, чем вёрстка).

### 4.2. Идемпотентность ingest
`POST /conversations` — **UPSERT по `(source, external_id)`**. При повторном открытии разговора он
присылается снова; стратегия на апдейт — **wipe-and-reinsert** всех сообщений (просто и безопасно для
Phase 1). Чанки сообщений каскадно удаляются (FK ON DELETE CASCADE) и пересоздаются.

### 4.3. Full-text колонка — обновляется приложением, не триггером
`messages.content_tsv` (TSVECTOR, GIN-индекс) заполняется явным `UPDATE ... to_tsvector('simple', content)`
после каждого ingest. Любой новый путь вставки сообщений обязан тоже обновить `content_tsv`. Словарь
**`simple`** (без стемминга) — чтобы работал и русский текст.

### 4.4. RAG-поиск (Phase 2)
- **Чанкование** (`indexing.py::chunk_text`): ≤1000 символов по границам абзацев.
- **Эмбеддинги**: локальный Ollama `nomic-embed-text` (768-dim), `Chunk.embedding`/`ContentChunk.embedding`.
- **Фоновый воркер** (`main.py` lifespan): каждые 15с добивает чанки и эмбеддинги (chunks + content_chunks),
  устойчив к падению Ollama. Ручной триггер — `POST /index/run`.
- **Поиск**: `/search` (full-text, `websearch_to_tsquery` + `ts_headline` сниппеты + `ts_rank`),
  `/search/semantic` (cosine `<=>`), `/search/hybrid` (**RRF**, K=60 — фьюз полнотекста и семантики;
  деградирует в text-only, если Ollama недоступна).

### 4.5. Чат с памятью (Phase 4) — `POST /chat`, SSE
Параллельно (`asyncio.gather`) готовятся: **ретрив** по выбранным контекст-чипам, **факты профиля**
(топ-40 по confidence), **история чата** (последние сообщения). Затем собирается промпт:
`<profile>` + `<context>` + `<attachments>` + «Запрос: …», стримится ответ, и тёрн сохраняется как
разговор `source='pam'` (становится частью памяти). После ответа — **фоновое авто-обучение**
(извлечение новых фактов о пользователе).

**Контекст-чипы (что подмешивать в ретрив):**
- `use_memory` — прошлые разговоры (векторно) + факты профиля
- `use_materials` — материалы Лектора `content_chunks` (векторно)
- `use_courses` — сгенерированные курсы (полнотекст по названию+summary)
- `use_saved` — избранное (полнотекст)

Все ретриверы возвращают `SimpleNamespace(content,title,source,d)`, сливаются по `d` (меньше=релевантнее),
берётся глобальный top-6. Каждый ретривер открывает свою сессию (конкурентность).

**Гибридный LLM-роутер** (`llm.py::route_provider`): при `LLM_PROVIDER=hybrid` «тяжёлые» запросы
(длина >280, наличие ```, или regex-триггеры: код/sql/debug/оптимизация/«объясни подробно»/…) →
**OpenRouter** (мощная модель); остальное → **Groq** (быстро). Под ответом UI показывает плашку движка.

### 4.6. True-multimodal вложения
- `POST /chat/attachment` — разовое распознавание загруженного файла: изображение → vision-модель
  (транскрипция + описание), документ → markitdown/текст. Сам файл нигде не хранится.
- В `/chat` вложения уходят как данные. Режим `multimodal=true`: картинка (`image_url`, data:-URL)
  идёт **прямо в vision-модель** и стримится; при сбое vision до первого токена — деградация на
  текстовый pre-pass. Документы и текст всегда идут текстом в `<attachments>`.
- Кнопка «Запомнить файл» (`POST /learn/remember`) — распознанный текст вложения → `ContentSource`
  (попадает в память Лектора, индексируется).

### 4.7. Лектор (Phase 5)
Ингест → извлечение текста → чанкование+эмбеддинги (переиспользуется пайплайн Phase 2) → генерация курса.
**Главное правило генерации:** ТЕМА строго из `<material>`, профиль ученика влияет ТОЛЬКО на сложность/
темп/глубину/аналогии (анти-подмена темы). AI «улучшить читаемость» причёсывает сырой текст по кускам
в фоне с прогрессом.

### 4.8. CORS
`main.py` делит `CORS_ORIGINS` на явные origin'ы и wildcard-паттерны; wildcard'ы (`chrome-extension://*`)
собираются в один regex для `allow_origin_regex` (у расширений origin вида `chrome-extension://<random-id>`).

---

## 5. Backend — детально

### 5.1. Настройки (`config.py`)

| Переменная | Дефолт | Назначение |
|-----------|--------|-----------|
| `DATABASE_URL` | `postgresql+asyncpg://pam:pam@localhost:5432/pam` | async DSN (asyncpg) |
| `CORS_ORIGINS` | `chrome-extension://*,http://localhost:3000` | список + wildcard'ы |
| `LOG_LEVEL` | `INFO` | |
| `OLLAMA_URL` / `EMBED_MODEL` | `http://localhost:11434` / `nomic-embed-text` | локальные эмбеддинги |
| `LLM_PROVIDER` | `groq` (в `.env.example` рекомендуется `hybrid`) | `groq`/`openrouter`/`hybrid`/`ollama` |
| `GROQ_API_KEY` / `GROQ_MODEL` | — / `openai/gpt-oss-120b` | чат (reasoning в отд. поле, не течёт в content) |
| `GROQ_JSON_MODEL` | `llama-3.3-70b-versatile` | JSON-mode (курсы/факты) — НЕ reasoning-модель, иначе JSON обрезается |
| `GROQ_VISION_MODEL` | `meta-llama/llama-4-scout-17b-16e-instruct` | распознавание изображений |
| `OPENROUTER_API_KEY` / `OPENROUTER_MODEL` | — / `nvidia/nemotron-3-super-120b-a12b:free` | мощная ветка hybrid |
| `OLLAMA_CHAT_MODEL` | `llama3.2:3b` | локальный чат-fallback |

### 5.2. Модель данных (`models.py`) — 8 таблиц

| Таблица | Ключевые поля | Назначение |
|---------|---------------|-----------|
| **conversations** | `source`, `external_id`, `title`, `started_at`, `updated_at`, `pinned`, `archived`, `raw_json` (JSONB). UNIQUE(`source`,`external_id`) | разговор (импортированный или `source='pam'`) |
| **messages** | `conversation_id`→conv (CASCADE), `role`, `content`, `position`, `sent_at`, `content_tsv` (TSVECTOR, GIN) | сообщение |
| **saved_messages** | снимок: `conversation_id`→conv (SET NULL), `source`, `title`, `role`, `content`, `position`, `note` | избранное — снимок переживает re-ingest (нельзя флаг на messages, их вайпает UPSERT) |
| **chunks** | `message_id`→msg (CASCADE), `content`, `position`, `embedding vector(768)` (NULL пока не заэмбеддено) | RAG-чанк сообщения |
| **content_sources** | `kind` (article/pdf/youtube/file/text), `title`, `url`, `status` (pending/extracted/failed), `text`, `formatted_text`, `reformat_status`, `reformat_progress`, `char_count`, `error`, `original_data` (LargeBinary, deferred), `original_mime` | материал Лектора |
| **content_chunks** | `source_id`→source (CASCADE), `content`, `position`, `embedding vector(768)` | RAG-чанк материала |
| **courses** | `source_id`→source (CASCADE), `title`, `level`, `data` (JSONB: modules→lessons + quiz) | сгенерированный мини-курс (новейший выигрывает в UI) |
| **profile_facts** | `category`, `content`, `source_conversation_id`→conv (SET NULL), `source_excerpt` (цитата), `confidence` (0..1) | устойчивый факт о пользователе (с трассируемым источником) |

### 5.3. Миграции Alembic (`alembic/versions/`)
Применяются автоматически (`alembic upgrade head` в Docker-команде backend). Хронология:
1. `0001_initial` — базовые таблицы + `CREATE EXTENSION vector` (включается «в день один», даже без векторов в Phase 1).
2. `2ec708645017_chunks_embeddings` — таблица `chunks` + embedding (Phase 2 RAG).
3. `4b747af609d8_profile_facts` — `profile_facts` (Phase 3).
4. `efc12b5654c3_saved_messages` — `saved_messages` (избранное).
5. `68113919ba65_conversations_pinned_archived_sidebar` — `pinned`/`archived` (сайдбар чатов).
6. `96d1d6982add_content_sources_content_chunks_phase5_` — `content_sources` + `content_chunks` (Phase 5 Лектор).
7. `a1a3e3d1fb0d_courses_table_phase5_lecturer` — `courses`.
8. `b1f2c3d4e5a6_content_source_preview_formatted` — `formatted_text` + `original_data`/`original_mime` (превью + реформат).
9. `c2d3e4f5a6b7_content_source_reformat_status` — `reformat_status` + `reformat_progress` (фоновый реформат).

> Правило: всегда через Alembic, без ручных `ALTER`. Новая правка схемы = новая ревизия.

### 5.4. Полный список HTTP-эндпоинтов

**Сервис**
- `GET /` — мета сервиса; `GET /health` — `{status: ok}`.

**conversations** (`/conversations`)
- `POST /conversations` — UPSERT разговора (идемпотентно по source+external_id) → `IngestResult`.
- `GET /conversations` — список (закреплённые первыми, затем новейшие); по умолчанию **скрывает `source='pam'`**; `?source=`, `?archived=`, `?limit`, `?offset`.
- `PATCH /conversations/{id}` — переключить `pinned`/`archived`.
- `GET /conversations/{id}` — детально с сообщениями.
- `DELETE /conversations/{id}`.

**search** (`/search`)
- `GET /search` — full-text (`websearch_to_tsquery` + `ts_headline` + `ts_rank`).
- `GET /search/semantic` — cosine `<=>` по эмбеддингам (503, если Ollama недоступна).
- `GET /search/hybrid` — RRF-фьюз text+semantic (деградирует в text-only).

**chat** (`/chat`)
- `POST /chat` — RAG-чат, SSE-стрим (`meta`→`sources`→`token`…→`done`). Тело: `message`, `conversation_id`, `attachments[]`, `use_memory/use_materials/use_courses/use_saved`, `multimodal`.
- `POST /chat/attachment` — распознать загруженный файл (image→vision, doc→markitdown). Лимиты: ≤25 МБ, текст обрезается до 12k.

**learn** (`/learn`) — Лектор
- `POST /learn/article` `{url}` — статья (SSRF-guard, browser-UA, чистка boilerplate).
- `POST /learn/youtube` `{url}` — транскрипт (ru/en→любой) + заголовок через oEmbed.
- `POST /learn/pdf` (multipart, ≤25 МБ) — PDF через markitdown + сохранение оригинала для превью.
- `POST /learn/file` (multipart, ≤25 МБ) — универсальный инбокс (txt/md/html/docx/pdf/pptx/xlsx).
- `POST /learn/text` `{title,text}` — вставленный текст.
- `POST /learn/remember` `{title,text,kind}` — распознанный текст вложения чата → материал.
- `GET /learn/sources` — список; `GET /learn/sources/{id}` — деталь (+text/formatted_text/reformat_status/has_file); `DELETE /learn/sources/{id}`.
- `GET /learn/sources/{id}/file` — отдать оригинал (PDF) inline (заголовки `nosniff`, `SAMEORIGIN`).
- `POST /learn/sources/{id}/reformat` — запустить фоновый AI-реформат (готово→кэш, идёт→прогресс).
- `POST /learn/sources/{id}/course` — сгенерировать курс; `GET .../course` — новейший курс.

**facts** (`/facts`)
- `POST /facts/extract?limit=N` — извлечь факты из ещё не обработанных разговоров.
- `GET /facts?category=` — список; `DELETE /facts/{id}`.

**saved** (`/saved`)
- `POST /saved` (201) — сохранить снимок; `GET /saved?source=` — список; `DELETE /saved/{id}`.

**index** (`/index`)
- `POST /index/run` — ручной прогон индексации → `{chunks_created, embedded, remaining}`.

### 5.5. Сервисные модули

- **`llm.py`** — провайдер-агностичный слой. `stream_chat` (SSE-токены), `complete` (non-stream, JSON-mode
  для курсов/фактов на не-reasoning модели), `describe_image`/`stream_vision`/`vision_target` (мультимодал),
  `route_provider`/`model_for` (hybrid-роутинг). Один `_stream_openai_compatible`/`_complete_openai_compatible`
  на Groq и OpenRouter (читается `delta.content`, reasoning остаётся в `delta.reasoning` — стрим чистый).
- **`indexing.py`** — `chunk_text` (≤1000 симв по абзацам), `embed_text` (Ollama), `create_missing_chunks`
  (backfill), `embed_pending` (по 64/тик), `index_pending` (backfill+embed, для воркера и `/index/run`).
- **`content.py`** — Лектор-ингест: `_assert_public_url` (**SSRF-guard**), `html_to_text`/`_extract_article`
  (ручные редиректы, каждый хоп проверяется), `document_to_text`/`extract_file_text` (markitdown/текст),
  `recognize_attachment` (image→vision / doc→md), `youtube_video_id`+`ingest_youtube`, `ingest_article/pdf/file/text/recognized`,
  `_finalize` (обрезка ≤200k, чанкование), `embed_pending_content`.
- **`courses.py`** — `generate_course`: материал ≤9k (head), профиль топ-30, JSON-mode; анти-инъекция
  (`<material>` = данные); тема строго из материала.
- **`formatting.py`** — фоновый реформат: `_split_for_reformat` (по абзацам ≤6k), `_reformat_one`
  (graceful degradation — при сбое кусок как есть), `schedule_reformat`/`_reformat_background`
  (прогресс коммитится по кускам для поллинга, пауза 0.8с между вызовами).
- **`extraction.py`** — `extract_facts_for_conversation`/`extract_pending`: строгий JSON только о ПОЛЬЗОВАТЕЛЕ,
  дедуп по content, **hallucination-guard** (факт без `source_excerpt` отбрасывается), **anti-injection**
  (`<conversation>` = данные).

### 5.6. Фоновый воркер
`main.py::_embed_worker` — бесконечный цикл (15с): `index_pending` (чанки+эмбеддинги сообщений) +
`embed_pending_content` (чанки материалов). Никогда не падает (ловит все исключения, ретрай на след. тике).
Запускается/останавливается через FastAPI `lifespan`.

### 5.7. Безопасность (встроенные гарды)
- **Anti-injection** — везде, где внешний/исторический текст идёт в LLM (`<conversation>`/`<context>`/
  `<material>`/`<attachments>`), системный промпт явно объявляет это ДАННЫМИ, а не командами.
- **Hallucination-guard** — каждый факт обязан иметь `source_excerpt` + по возможности `source_conversation_id`.
- **SSRF-guard** — ингест статьи резолвит хост и блокирует private/loopback/link-local/reserved/multicast/
  unspecified; редиректы вручную, каждый хоп проверяется. YouTube/oEmbed — фиксированный хост (нет SSRF).
- **Параметризация ORM**, UDID-валидация, React-эскейпинг; ключи LLM не логируются. Превью-файл отдаётся
  с `X-Content-Type-Options: nosniff` и `X-Frame-Options: SAMEORIGIN`.

### 5.8. Тесты (`backend/tests/`, pytest)
Чистые юниты (без БД/сети): `test_recognize.py` (диспетч распознавания вложения), `test_formatting.py`
(разбивка реформата), `test_chat_utils.py` (`_safe_name`), `test_ssrf.py` (SSRF-guard), `test_retrieve.py`.
Запуск: `docker compose exec backend pytest -q`. DB-/route-тестов и web-раннера пока нет; фронт-гейт —
`npm run build`.

### 5.9. Зависимости backend (`pyproject.toml`)
`fastapi`, `uvicorn[standard]`, `sqlalchemy[asyncio]`, `asyncpg`, `alembic`, `pydantic`, `pydantic-settings`,
`python-multipart`, `pgvector`, `httpx`, `beautifulsoup4`, `markitdown[pdf,docx,pptx,xlsx]`,
`youtube-transcript-api`. Dev: `pytest`, `pytest-asyncio`.

---

## 6. Frontend (web) — разделы и хелперы

**Навигация (`nav.tsx`):** Чат `/` · История `/history` · Избранное `/saved` · Лектор `/learn` · Каталог `/catalog`.
(Раздел «Память обо мне» `/me` доступен, но не в верхнем nav.)

| Маршрут | Файл | Что делает |
|---------|------|-----------|
| `/` | `app/page.tsx` (654) | главный чат: RAG + вложения + multimodal, glass-UI, центр-glow; контекст-чипы; плашка движка |
| — | `app/chat-sidebar.tsx` (521) | сайдбар: Закреплённые/Недавнее, hover pin+⋯, контекст-меню (закрепить/архив/удалить), поиск чатов |
| `/c/[id]` | `app/c/[id]/page.tsx` (136) | детальный разговор (markdown), открывается из Истории |
| `/history` | `app/history/page.tsx` (200) | История/Импорт: список + поиск (text/semantic/hybrid), кнопка обновления |
| `/saved` | `app/saved/page.tsx` (97) | Избранное |
| `/me` | `app/me/page.tsx` (148) | «Память обо мне»: факты по категориям + confidence% + цитата + удаление + «обновить профиль» |
| `/learn` | `app/learn/page.tsx` (749) | Лектор: добавить статью/PDF/YouTube/файл/текст, сетка/список материалов со статусами |
| `/courses/[id]` | `app/courses/[id]/page.tsx` (872) | читалка курса: содержание/конспект/тест (интерактивный квиз), YouTube/PDF/Word/Excel-превью, реформат, экспорт |
| `/catalog` | `app/catalog/page.tsx` (707) | витрина AI-экосистемы (live GitHub-звёзды, infinite scroll) |
| `/catalog/[slug]` | `app/catalog/[slug]/page.tsx` (251) | карточка инструмента |

**`lib/api.ts`** — полный клиент backend: типы (`ConversationSummary`, `Course`, `ContentSource`, …),
обёртки над всеми эндпоинтами + `streamChat` (парсинг SSE: meta/sources/token/error/done).
**`lib/material-ui.tsx`** — `Kind`/`KIND_LABEL`/`kindOf`/`youtubeId`/`fmtDate`/`cleanSourceMarkdown`/`PlayIcon`.
**`lib/catalog.ts`** — статические данные витрины (TOOLS, STARS, ACCENTS, категории) + хелперы сортировки/иконок.
**`lib/course-export.ts`** — `courseToMarkdown`, `downloadText`, `safeCourseFilename` (PDF через печать).
**`lib/course-status.ts`** — личный статус курса (new/learning/done/favorite/archive) в localStorage.
**Превью на клиенте:** Word — `mammoth`, Excel — `xlsx`/SheetJS, PDF — нативно через `<iframe>` (байты с backend).

---

## 7. Браузерное расширение (Plasmo MV3)

- **Манифест** (`package.json` → `manifest`): `host_permissions` = chatgpt.com, chat.openai.com, claude.ai,
  localhost:8000; `permissions` = `storage` (Plasmo сам добавляет `scripting` для динамической регистрации MAIN-скриптов).
- **`background.ts`** — очередь `QueueItem` с ретраями (`RETRY_DELAY_MS=4000 × attempts`, `MAX_ATTEMPTS=5`),
  статистика (`total/success/failed`) в `chrome.storage.local`; сообщения `CAPTURE_CONVERSATION`/`GET_STATS`/`RESET_STATS`.
- **`popup.tsx`** — статус «PAM // active» + счётчики (Сохранено/В очереди/Ошибок) + сброс + ссылки на backend/web.
- **`contents/claude.ts`**, **`chatgpt.ts`** — MAIN-world патч `window.fetch`, нормализация, `postMessage`.
- **`contents/relay.ts`** — isolated-world мост в background.
- **`lib/api.ts`** — `sendConversation` → `POST /conversations`.
- Только Claude и ChatGPT подключены. Gemini-скрипт **удалён** (TODO #11): `gemini` остаётся валидным
  значением `source` (можно импортировать вручную), но авто-захвата нет.

**Когда захват ломается:** DevTools→Network на сайте → найти запрос с полным разговором в JSON → обновить
`URL_RE`/парсер в `contents/<site>.ts`. Per-site скрипты изолированы. Фолбэк — официальный экспорт сайта.

---

## 8. v0dev — прототип UI

`v0dev/` — отдельный Next.js-проект из v0.dev: ранний дизайн чат-интерфейса PAM. Содержит
`components/pam/` (chat-app, chat-input, chat-sidebar, message-list, navbar) и полный набор
shadcn/ui (`components/ui/`, ~50 Radix-компонентов). Промпт-источник — `docs/v0-chat-ui-prompt.md`.
Это референс-прототип, не основной фронт (основной — `web/`).

---

## 9. Инфраструктура и запуск

### 9.1. Docker Compose (`docker-compose.yml`) — 4 сервиса
- **db** — `pgvector/pgvector:pg17`, порт 5432 (pam/pam/pam), volume `./data/postgres`, healthcheck.
- **backend** — собирается из `./backend/Dockerfile`, порт 8000; ключи из `backend/.env`; `environment`
  переопределяет `DATABASE_URL`→`db:5432` и `OLLAMA_URL`→`host.docker.internal:11434`; команда
  `alembic upgrade head && uvicorn ... --reload`; bind-mount кода для hot-reload.
- **web** — `node:22-bookworm-slim`, порт 3000; `npm install && next dev`; polling для file-watch на Windows.
- **extension** — `node:22-bookworm-slim`; **prod**-build Plasmo (`build/chrome-mv3-prod`, без HMR-клиента → чистая консоль).

Запуск: `docker compose up -d`. Swagger: `http://localhost:8000/docs`. UI: `http://localhost:3000`.
Chrome: `chrome://extensions` → Developer mode → Load unpacked → `extension/build/chrome-mv3-prod` (или `-dev`).

### 9.2. Dev без Docker (Windows) — `dev.bat` / `stop-dev.bat`
`dev.bat` поднимает backend (`backend/.venv`, uvicorn :8000, скрыто, лог в файл) + extension (`npm run dev`,
скрыто) и открывает web в отдельном окне. Требует заранее созданный `backend/.venv` (Python 3.11) и
`pip install -e backend`. `stop-dev.bat` гасит фоновые сервисы.

### 9.3. Переменные окружения (`.env.example` → `backend/.env`, gitignored)
`DATABASE_URL`, `CORS_ORIGINS`, `LOG_LEVEL`, `OLLAMA_URL`/`EMBED_MODEL`, `LLM_PROVIDER` (реком. `hybrid`),
`GROQ_API_KEY`/`GROQ_MODEL`, `OPENROUTER_API_KEY`/`OPENROUTER_MODEL`, `OLLAMA_CHAT_MODEL`.
Заметки asyncpg для Neon: DIRECT-эндпоинт (без `-pooler`), `?ssl=require` (не `sslmode`), префикс `postgresql+asyncpg://`.

---

## 10. Текущее состояние (из `.planning/STATE.md`)

- **Фазы 1–5 завершены**, всё в `main`, запушено (`github.com/StudentMe2712/PAM.git`).
- **Чат**: RAG (top-6) + профиль фактов + история; **hybrid** Groq↔OpenRouter; авто-обучение фактам; плашка движка.
- **Сайдбар** как у ChatGPT (Закреплённые/Недавнее, pin/archive в БД).
- **Лектор**: статья/PDF/YouTube/файл/текст → курс + квиз; реформат; превью PDF/Word/Excel; экспорт MD/PDF.
- **Память/факты** (`/me`), **Избранное** (снимки, 14/14 тестов), **Каталог**.
- **Эмбеддинги** локально (Ollama `nomic-embed-text`, 768-dim).

### Блокеры / нюансы среды
- **Docker на паузе** (Windows) на основной машине — backend гоняли локально (`backend/.venv`) против БД.
- **Хранилище**: исторически дев-БД на **Neon** (облако) — задумано как dev; реальные разговоры → локальный
  Postgres при возврате Docker (local-first). В STATE отмечено: **ротировать ключи** (OpenRouter-ключ светился).
- **Эта (вторая) машина**: мало RAM → поднимать PAM **без Ollama** (семантический ретрив тогда «тёмный»,
  чат деградирует на ответ без векторного контекста; полнотекст/курсы/избранное работают).

### Известные мелочи / бэклог (`TODO.md`, ROADMAP)
- Полнота захвата ChatGPT: BFS по всему `mapping` vs путь по `current_node` (видимая нить) — решить.
- Сохранение результатов квиза / прогресс по урокам; map-reduce курса для длинных материалов (сейчас head ~9–12k).
- Авто-«популярное»/mindmap тем (на эмбеддингах); аналитика частоты тем.
- `contents/gemini.ts` — реализовать после наблюдения реального стрима.

---

## 11. Конвенции
- **Язык**: UI и многие комментарии — на русском; не переводить без запроса.
- **Local-first**: без облачных БД/auth/телеметрии без явного запроса; наружу — только LLM/эмбеддинги.
- **`source` enum** = `chatgpt | claude | gemini` (+ внутренний `pam`); менять в трёх местах сразу
  (`schemas.py`, content-скрипты, фильтры web).
- **Время** — timezone-aware (`DateTime(timezone=True)`). Валюты в проекте нет.
- **Миграции** — только через Alembic, новая ревизия на каждое изменение схемы.
- **Фазовые ветки** + atomic-коммиты + обновление `handoff.md`; значимые фичи — отдельная ветка.
```
