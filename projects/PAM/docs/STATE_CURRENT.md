# PAM — STATE_CURRENT (актуальная карта проекта)

> **Назначение.** Один файл, по которому любой агент через 3–6 месяцев понимает PAM
> целиком: что это, что реализовано, как устроено, какие сущности есть, что дальше.
> **Источник истины — код.** Документ собран аудитом репозитория (модели, миграции,
> роутеры, сервисы, extension, frontend). Где код и старые доки расходятся — здесь
> зафиксирован код. Где по коду нельзя определить — явно написано «не определено».
>
> Дата аудита: **2026-06-14**. Ветка: `main`. Что не выводится из кода (деплой,
> креды, состояние Neon/Docker) помечено как «состояние окружения», не как код.

---

## 1. Executive Summary

**PAM (Personal AI Memory)** — local-first персональное приложение-память. Два
основных продукта в одном:

1. **Чат с долгой памятью** — главный экран. RAG-чат (`POST /chat`, SSE-стриминг),
   который при ответе достаёт контекст из накопленной памяти (прошлые разговоры +
   материалы + избранное + курсы) и из «фактов о пользователе», поддерживает
   вложения (документы → текст, изображения → vision), и после каждого чата фоном
   извлекает новые факты о пользователе.
2. **Лектор** — из материала (статья / YouTube / PDF / файл / вставленный текст)
   генерируется персональный мини-курс (модули→уроки+квиз) под уровень пользователя,
   с трекингом прогресса.

Вспомогательное: **импорт истории** из ChatGPT/Claude браузерным расширением,
**избранное**, **полнотекстовый/семантический/гибридный поиск**, **диагностика**
(observability), **каталог** AI-экосистемы.

**Основные сценарии:** спросить с опорой на свою историю; добавить материал и
учиться по сгенерированному курсу; проверить/принять факты о себе; импортировать
прошлые разговоры в память; посмотреть здоровье памяти.

**Текущее состояние:** Фазы 1–5 завершены и в `main`. P0/P1-системы (Fact Review
Queue, Observability, Capture Reliability, Memory Health Score, Learning Progress,
Home Dashboard) отгружены. Объявлена **стабилизационная фаза** (см. §10). Следующий
крупный модуль — **Project Memory** — **только спроектирован** (`docs/project-memory-design.md`),
кода нет. **Состояние окружения:** хранилище — Postgres (по `handoff.md` сейчас
Neon-облако, Docker на паузе; это деплой, не код).

---

## 2. Product Map

Модули, существующие в коде (frontend-роут + backend).

| Модуль | Назначение | Статус | Ключевые сущности / точки |
|--------|-----------|--------|---------------------------|
| **Chat Memory** | RAG-чат с памятью, главный экран `/` | ✅ реализовано | `routes/chat.py`, `conversations`, `messages`, `chunks`, `profile_facts` |
| **Home Dashboard** | Пустой экран чата = точка входа в память | ✅ реализовано (переписан под UX-контракт 2026-06-14) | `web/app/home-dashboard.tsx`, читает `/stats` |
| **Fact Review Queue** | Проверка фактов до попадания в память | ✅ реализовано | `routes/facts.py`, `profile_facts.status`, `/me` |
| **Memory / Profile** | Факты о пользователе, профиль | ✅ реализовано | `extraction.py`, `profile_facts`, `/me` |
| **Lecturer (Лектор)** | Материал → курс + квиз | ✅ реализовано | `routes/learn.py`, `content.py`, `courses.py`, `content_sources`, `content_chunks`, `courses` |
| **Learning Progress** | Прогресс уроков + результат квиза | ✅ реализовано | `course_progress`, `routes/learn.py`, `/courses/[id]` |
| **Import History** | Импорт разговоров из ChatGPT/Claude | ✅ реализовано (capture-часть — extension) | extension, `/conversations`, `/history` |
| **Favorites (Избранное)** | Снимки сообщений ★ | ✅ реализовано | `saved_messages`, `routes/saved.py`, `/saved` |
| **Search** | Полнотекст / семантика / гибрид | ✅ реализовано | `routes/search.py` |
| **Diagnostics (Observability)** | Здоровье и метрики памяти | ✅ реализовано | `routes/stats.py`, `events`, `/diag` |
| **Catalog** | Витрина AI-экосистемы (статика) | ✅ реализовано | `web/lib/catalog.ts`, `/catalog`, `/catalog/[slug]` |
| **Projects (Project Memory)** | Память о проектах (группа над существующей памятью) | ✅ реализовано V1 (2026-06-14) | `routes/projects.py`, `projects` + nullable `project_id` на conversations/content_sources |
| **Memory Items** | Универсальный контейнер знаний (idea/note/article/tool/code/prompt/learning/decision) | ✅ реализовано V1 | `routes/memory.py`, `memory_items` (заменяет project_items из дизайн-дока) |
| **Telegram Capture** | Бот → сообщение в memory item (long polling) | ✅ реализовано V1 (бот отдельным процессом) | `app/telegram_bot.py` (aiogram) → `/memory/items` |
| **AI Tagger** | Авто summary/tags/item_type/importance после создания item | ✅ реализовано V1 | `app/tagging.py` (паттерн `extraction.py`, фон, event `tagger`) |
| **Memory Links** | Связи между объектами (полиморфно, без графовой БД) | ✅ реализовано V1 | `memory_links`, `routes/memory.py` |
| **Recall** | «что я сохранял по X / какие решения» — полнотекст+теги+scope+синтез | ✅ реализовано V1 | `POST /memory/recall` |

**Frontend-роуты (`web/app/`):** `/` (чат+home), `/c/[id]` (детали разговора),
`/history`, `/saved`, `/me` (профиль+очередь фактов), `/learn`, `/courses/[id]`,
`/diag`, `/catalog`, `/catalog/[slug]`. Общие: `nav.tsx`, `chat-sidebar.tsx`,
`markdown.tsx`, `home-dashboard.tsx`, `refresh-button.tsx`.

**Наблюдение по навигации (код):** верхний нав-бар (`nav.tsx`) содержит вкладки
**Чат / История / Избранное / Лектор / Диагностика / Каталог**. Вкладки **«Профиль»
(`/me`) в верхнем баре НЕТ** — `/me` достижим из быстрых действий главного экрана
(«Проверить факты») и прямой ссылкой. Нав-бар унифицирован для всех страниц
(2026-06-14).

---

## 3. Architecture

### Стек (из кода)

- **Backend:** FastAPI (`backend/app/main.py`, версия app 0.2.0), SQLAlchemy Async,
  PostgreSQL, **pgvector** (`Vector(768)`), Alembic. Драйвер `asyncpg`
  (`DATABASE_URL=postgresql+asyncpg://…`).
- **Frontend:** Next.js (App Router, `web/app/`), React, Tailwind. UI на русском,
  тёмная тема, lime-акцент.
- **Extension:** Plasmo (MV3), content scripts + relay + background service worker.
- **AI:** Groq, OpenRouter (оба OpenAI-совместимые, cloud), Ollama (локально:
  эмбеддинги + опционально chat). Маршрутизация в `llm.py`.

### Потоки данных

**Capture (импорт истории):**
```
content script (world MAIN, contents/chatgpt.ts|claude.ts)
  патчит window.fetch → ловит JSON разговора → линеаризует → normalized
       │ window.postMessage (нельзя chrome.runtime в MAIN-мире)
       ▼
relay (isolated world, contents/relay.ts) → chrome.runtime.sendMessage
       ▼
background.ts: очередь + ретраи (4s×attempt, MAX_ATTEMPTS=5)
       │ POST http://localhost:8000/conversations  (UPSERT по (source, external_id))
       ▼
FastAPI → wipe+reinsert messages → content_tsv (UPDATE) → chunks (embedding NULL)
       │ при исчерпании ретраев → POST /stats/capture-failed (событие capture_failure)
```

**Индексация (фон, `main.py` lifespan, каждые 15с):**
```
index_pending: create_missing_chunks (бэкфилл) → embed_pending (≤64 чанков)
embed_pending_content: эмбеддинг content_chunks Лектора
       embed_text → Ollama /api/embeddings (nomic-embed-text, 768-dim)
```

**Чат (RAG, `POST /chat`, SSE):**
```
query → параллельно:
  _retrieve (по чипам: память/материалы — вектор; избранное/курсы — полнотекст) → top-6
  _profile_facts (только approved+edited, top-40 by confidence) → <profile>
  _recent_history (последние 10 сообщений чата)
→ собрать system+history+<profile>+<context>+<attachments>+«Запрос:»
→ LLM (Groq / OpenRouter / Ollama; hybrid выбирает провайдера по тексту)
→ SSE: meta(provider,model) → sources → token… → done
→ _persist (source='pam', +chunks) → _schedule_learn (фоновое извлечение фактов)
```

---

## 4. Database Inventory

13 таблиц (модели в `backend/app/models.py`). PK везде `UUID` (default uuid4).
Project Memory V1 реализован: добавлены `projects`, `memory_items`, `memory_links`
(+ nullable `project_id` на `conversations`/`content_sources`) — см. ниже.
Таблицы `project_items` НЕТ: дизайн-доковый `project_items` консолидирован в
`memory_items` (принцип «не создавать параллельные системы памяти»).

### conversations  *(migr 0001; pinned/archived — 68113919ba65)*
Разговор: импортированный из AI-сервиса или внутренний (`source='pam'`).
- `source` (str), `external_id` (str), `title`, `started_at`, `updated_at`
  (onupdate now), `pinned`/`archived` (bool), `raw_json` (JSONB).
- **Unique** `(source, external_id)` — контракт идемпотентного UPSERT.
- Связь: 1—N `messages` (CASCADE).

### messages  *(migr 0001)*
Сообщение разговора.
- `conversation_id` (FK→conversations, CASCADE), `role`, `content`, `position`,
  `sent_at`, **`content_tsv`** (TSVECTOR, **GIN-индекс**, словарь `simple`).
- `content_tsv` заполняется app-side через `UPDATE … to_tsvector('simple', content)`
  после ingest/persist (не триггером).

### saved_messages  *(migr efc12b5654c3)*
«Избранное» как **снимок** (контент копируется), чтобы переживать wipe+reinsert.
- `conversation_id` (FK→conversations, **SET NULL**), `source`, `title`, `role`,
  `content`, `position`, `note`, `created_at`.

### chunks  *(migr 2ec708645017)*
Чанк сообщения + эмбеддинг (RAG памяти).
- `message_id` (FK→messages, CASCADE), `content`, `position`,
  **`embedding` Vector(768)** (NULL пока воркер не заполнит).
- **HNSW-индекс** `ix_chunks_embedding_hnsw` (`vector_cosine_ops`) — ANN-поиск.

### content_sources  *(migr 96d1d6982add; preview/formatted — b1f2c3d4e5a6; reformat — c2d3e4f5a6b7)*
Учебный материал Лектора.
- `kind` (article|pdf|youtube|file…), `title`, `url`, `status` (pending|extracted|failed),
  `text`, `formatted_text` (AI-причёсанный), `reformat_status`(running|done|failed)/
  `reformat_progress`(0..100), `char_count`, `error`, `original_data`(LargeBinary,
  deferred — байты PDF/docx/xlsx для превью), `original_mime`, `created_at`.
- Связь: 1—N `content_chunks` (CASCADE).

### content_chunks  *(migr 96d1d6982add)*
Чанк материала + эмбеддинг (зеркало `chunks`).
- `source_id` (FK→content_sources, CASCADE), `content`, `position`,
  **`embedding` Vector(768)**.
- ⚠️ **Векторного (HNSW/IVFFlat) индекса НЕТ** — только `ix_content_chunks_source`
  (по `source_id`). Семантический поиск по материалам = full scan (см. §12).

### courses  *(migr a1a3e3d1fb0d)*
Сгенерированный мини-курс.
- `source_id` (FK→content_sources, CASCADE), `title`, `level`, **`data` JSONB**
  (level/summary/modules→lessons/quiz), `created_at`.
- Несколько курсов на источник (регенерация) — в UI «новейший побеждает».

### profile_facts  *(migr 4b747af609d8; status — d3e4f5a6b7c8)*
Устойчивый факт **о пользователе**.
- `category`, `content`, `source_conversation_id` (FK→conversations, **SET NULL**),
  `source_excerpt` (цитата-основание, hallucination-guard), `confidence` (float),
  **`status`** (pending_review|approved|rejected|edited; индекс), `created_at`.
- Константы в `models.py`: `FACT_STATUSES`, `ACCEPTED_FACT_STATUSES=(approved,edited)`,
  `is_fact_accepted()`. **Гейт памяти:** в чат идут только `approved+edited`.

### events  *(migr e4f5a6b7c8d9)*
Лёгкий append-only лог observability (best-effort, `metrics.record_event`).
- `kind` (chat|extraction|lecturer|reformat|embed|vision|vision_fallback|import|
  capture_failure|**tagger**), `provider`, `status` (ok|error|fallback), `duration_ms`,
  `detail`(≤255), `created_at`. Индексы `(kind,created_at)`, `(created_at)`.

### course_progress  *(migr f5a6b7c8d9e0)*
Прогресс изучения курса — **1 строка/курс**.
- `course_id` (FK→courses, CASCADE, **unique**), `completed_lessons` (JSONB, ключи
  `"<модуль>-<урок>"`), `lessons_total` (снимок), `quiz_score`/`quiz_total`/
  `quiz_completed_at`, `created_at`/`updated_at`.
- **Статус/процент не хранятся, а выводятся** (`course_study_status`/`course_percent`
  в `models.py`).

### projects  *(migr a6b7c8d9e0f1 — Project Memory P2)*
Группа-зонтик. `name`, `description`, `status` (active|archived; индекс),
`created_at`/`updated_at`. К ней привязываются разговоры/материалы (nullable
`project_id`, ON DELETE SET NULL) и `memory_items`. Счётчики выводятся, не хранятся.

### memory_items  *(migr b7c8d9e0f1a2 — Project Memory P2)*
Универсальный контейнер знаний (заменяет project_items из дизайн-дока).
- `project_id` (FK→projects, SET NULL), `source` (telegram|chat|manual|…),
  `source_ref`, `title`, `content`, `summary` (AI), `item_type`
  (idea|note|article|tool|code|prompt|learning|decision; AI), `importance` 1..5 (AI),
  `tags` (JSONB, AI; **GIN**), `status` (active|archived), `content_tsv` (TSVECTOR,
  **GIN**, словарь simple — для recall), `created_at`/`updated_at`.
- Константы `ITEM_TYPES`, `MEMORY_STATUSES`. **Векторного индекса нет** — recall по
  полнотексту + тегам (решение P2). НЕ проходит Fact-Review-гейт (захват доверенный).

### memory_links  *(migr c8d9e0f1a2b3 — Project Memory P2)*
Полиморфные связи (без жёсткого FK): `source_kind`/`source_id`,
`target_kind`/`target_id` (kind ∈ memory_item|project|fact|conversation), `relation`,
`confidence`, `created_at`. Unique `(source_kind,source_id,target_kind,target_id,relation)`.
Только PostgreSQL — никакой графовой БД.

**Цепочка миграций:** линейная, **один head `c8d9e0f1a2b3`** (P2: f5a6b7c8d9e0 →
a6b7c8d9e0f1 → b7c8d9e0f1a2 → c8d9e0f1a2b3), без веток. `0001` делает
`CREATE EXTENSION IF NOT EXISTS vector`.

---

## 5. RAG Inventory — «что именно уже является RAG»

**Чанкинг** (`indexing.py::chunk_text`, `MAX_CHARS=1000`): по границам абзацев
(`\n\n`), длинный абзац режется жёстко. Создаётся при ingest (`conversations.py`),
при persist чата (`chat.py::_persist`) и для материалов (`content.py`).

**Эмбеддинги** (`indexing.py::embed_text`): локально через Ollama
`POST /api/embeddings`, модель `nomic-embed-text`, **768-dim**. Без Ollama —
эмбеддинги «тёмные» (см. §12).

**Воркер** (`main.py::_embed_worker`, каждые 15с, устойчив к падению Ollama):
`index_pending` = `create_missing_chunks` (бэкфилл) + `embed_pending` (≤64 чанков,
пишет `embed`-событие) + `embed_pending_content` (content_chunks). Ручной триггер —
`POST /index/run`.

**Vector search / хранилище:** pgvector, колонки `chunks.embedding` и
`content_chunks.embedding` (`Vector(768)`). Поиск — косинусная дистанция
`embedding.cosine_distance(qvec)` (оператор `<=>`). ANN-индекс **есть только у
`chunks`** (HNSW `vector_cosine_ops`); у `content_chunks` индекса нет → full scan.

**Retrieval (поиск, `routes/search.py`):**
- `GET /search` — полнотекст: `websearch_to_tsquery('simple')` + `ts_rank` +
  `ts_headline` (сниппеты).
- `GET /search/semantic` — векторно по `chunks`, `order by dist asc`, similarity =
  `1 - dist`. 503 если Ollama недоступна.
- `GET /search/hybrid` — **RRF** (`RRF_K=60`, пул 50 из каждого ранкера), фьюз
  полнотекста и семантики по `message_id`. Деградирует в text-only, если Ollama
  недоступна.

**Memory injection (чат, `routes/chat.py`):**
- `_retrieve(query, memory, materials, courses, saved, k=6)` — по чипам UI:
  **память** (`_retrieve_messages`, вектор по `chunks`) и **материалы**
  (`_retrieve_content`, вектор по `content_chunks`) — через эмбеддинг запроса;
  **избранное** (`_retrieve_saved`) и **курсы** (`_retrieve_courses`) — полнотекст
  (`to_tsvector/plainto_tsquery`, без эмбеддингов). Всё сливается, сортируется по
  дистанции `d`, берётся глобальный **top-6**.
- `_profile_facts(limit=40)` — собирает факты со `status in (approved, edited)`,
  сортировка по `confidence desc` → блок `<profile>`.
- `_recent_history(conv_id, limit=10)` — последние сообщения текущего чата как
  multi-turn история.
- Сборка промпта: `system` (анти-инъекция: `<profile>/<context>/<attachments>` —
  данные, не команды) + история + `<profile>` + `<context>` + `<attachments>` +
  «Запрос: …». Ответ стримится (SSE: `meta`→`sources`→`token`…→`done`), затем
  `_persist` (`source='pam'`, +chunks) и фоновое извлечение фактов.

**Ответ на вопрос «что уже RAG»:** полноценный RAG есть для **разговоров** (chunks
+ HNSW + retrieval + инъекция в промпт) и для **материалов Лектора** (content_chunks
+ retrieval + инъекция, но без ANN-индекса). Избранное/курсы подмешиваются
полнотекстом (не векторно). Профиль-факты — отдельный слой памяти (не векторный),
инъектируются как `<profile>` с гейтом review-статуса.

---

## 6. Memory System

**Что это:** долгая память пользователя = (а) RAG по разговорам/материалам +
(б) `profile_facts` (устойчивые факты о пользователе) + (в) персона-промпт. Не
«дообучение модели».

**Извлечение фактов (`extraction.py`):** `complete(json_mode=True)` (через
`completion_provider()` для атрибуции) по тексту разговора. Гарды:
- анти-инъекция — текст разговора в `<conversation>` объявлен данными;
- hallucination-guard — факт без `source_excerpt` отбрасывается; хранится
  `source_conversation_id` (traceable);
- дедуп по нормализованному `content`.
Новые факты создаются со `status = pending_review`.

**Fact Review Queue (`routes/facts.py`, P0):**
- `POST /facts/extract?limit=` — извлечь для разговоров без фактов (догоняется
  повторными вызовами).
- `GET /facts?category=&status=`, `GET /facts/counts` (сводка по статусам).
- `POST /facts/{id}/approve` → `approved`; `POST /facts/{id}/reject` → `rejected`;
  `PATCH /facts/{id}` (правка content/category) → `edited`; `DELETE /facts/{id}`.
- **Гейт ретрива:** `_profile_facts` фильтрует `status in (approved, edited)`,
  и только при `use_memory=true`. Единый источник правды — `ACCEPTED_FACT_STATUSES`.

**Авто-обучение:** после каждого чата `_schedule_learn(conv_id)` фоном извлекает
факты по этому разговору (попадают в очередь `pending_review`).

**Что реализовано:** извлечение, очередь проверки, статусы approved/rejected/edited,
provenance, гейт памяти, авто-извлечение, UI `/me` (профиль + очередь).

**Чего ещё НЕТ (ограничения текущего подхода):**
- Нет мультипользовательности/аутентификации — память **глобальная, один
  пользователь** (by design, local-first).
- Факты не версионируются (правка перезаписывает; история изменений не хранится).
- Возможен over-inference (слабые, но traceable факты — отмечалось в `handoff.md`).
- Извлечение зависит от доступности LLM (Groq/OpenRouter).
- Память о пользователе не привязана к проектам/темам (это Project Memory — §11).

---

## 7. Lecturer (Лектор)

**Ingest (`content.py`, роуты `/learn/*`):**
- `POST /learn/article` — URL → HTML→текст (httpx + BeautifulSoup, чистка
  boilerplate, браузерный UA) + **SSRF-guard** (резолв host, блок
  private/loopback/link-local/reserved/multicast, ручные редиректы с проверкой хопа).
- `POST /learn/youtube` — транскрипт (`youtube-transcript-api`), `video_id` парсится
  из watch/youtu.be/shorts/embed/live; заголовок через oEmbed.
- `POST /learn/pdf` (multipart, 25MB) — pypdf/markitdown.
- `POST /learn/file` — markitdown (docx/xlsx/pptx/…); изображение → vision
  (`recognize_attachment` → `describe_image`, событие `vision`).
- `POST /learn/text` — вставленный текст. `POST /learn/remember` — распознанный
  текст вложения чата → ContentSource (`kind=file`).
- Все источники чанкуются (`chunk_text`) и эмбеддятся фоновым воркером
  (`embed_pending_content`).

**Курсогенерация (`courses.py`, `POST /learn/sources/{id}/course`):**
`complete(json_mode=True)` → JSON: модули→уроки + квиз. **Тема строго из `<material>`,
профиль только калибрует УРОВЕНЬ** (не подменяет тему). Анти-инъекция (внешний
текст = данные). `MAX_MATERIAL_CHARS` ~head материала. Несколько курсов на источник
(новейший в UI). Событие `lecturer`.

**Уроки/тесты:** структура курса в `courses.data` (modules→lessons + quiz). UI
`/courses/[id]`: содержание/конспект/тест, интерактивный квиз (выбор→подсветка
верного + пояснение), Hero-превью (PDF iframe из `original_data`; docx/xlsx —
клиентский mammoth/SheetJS), кнопка «улучшить читаемость» (reformat).

**Reformat (`formatting.py`):** фон, режет текст ~6000 симв, причёсывает ВЕСЬ текст
по кускам, прогресс 0..100 коммитится по куску, пауза 0.8с между вызовами; per-chunk
сбой → сырой кусок, общий → `failed`. Событие `reformat`. Запуск
`POST /learn/sources/{id}/reformat`, поллинг через `GET /learn/sources/{id}`.

**Progress tracking (`course_progress`, P1):** `GET/POST
/learn/sources/{id}/progress|lesson|quiz`, сводка `GET /learn/progress`. 1 строка на
курс; `completed_lessons` (ключи `"модуль-урок"`), результат квиза; статус/процент
**вычисляются** (`course_study_status`/`course_percent`); при изменении структуры
курса `lessons_total` пересчитывается.

---

## 8. Observability

**`/diag` (frontend) читает `/stats` (backend `routes/stats.py`).** Два источника:
живые счётчики из таблиц данных + агрегаты по логу `events`. Только чтение.
`record_event` (`metrics.py`) — best-effort, своя сессия, глотает все ошибки,
никогда не на критическом пути.

**Реальные формулы (`stats.py`):**
- `_pct(part, whole)` = `round(part/whole*100)`, **100 если whole==0**.
- `_reliability(ok, failed)` = `_pct(ok, ok+failed)`.
- **Memory Health Score** = `compute_health` = `round(Σ component[k]*weight[k])`,
  веса: `capture .35 / indexing .25 / review .20 / stability .20` (Σ=1.0).
  Компоненты:
  - `capture` = capture_reliability = `_reliability(import-события, capture_failure-события)`;
  - `indexing` = `_pct(chunks_embedded + content_chunks_embedded, chunks_total + content_chunks_total)`;
  - `review` = `_pct(approved+rejected+edited, всего фактов)`;
  - `stability` = `100 - error_rate`, где `error_rate = round((errors+fallbacks)/total_events*100)` (0 если событий нет).
  - `health_label`: good ≥80, ok ≥50, иначе poor.
- **Capture reliability** = `_reliability(captures_ok, captures_failed)`, где
  `captures_ok` = события `kind='import'`, `captures_failed` = `kind='capture_failure'`
  (окно `days`, по умолчанию 7).
- **Provider metrics:** события сгруппированы по `(provider, status)` за окно.
  **Атрибуция закрыта** — `provider` всегда реальный (`groq`/`openrouter`/`ollama`),
  не литерал `hybrid` (chat → `prov_used`; non-streaming → `completion_provider()`;
  vision → `vision_target()`).
- **Timing:** по `kind` — `count`, `avg(duration_ms)`, `percentile_cont(0.95)` (p95).
- **fallbacks/errors:** счётчики статусов за окно. **recent_errors:** последние 10
  событий со статусом error|fallback.

**Состав `/stats`:** `days`, `memory` (разговоры/сообщения/чанки total/embedded/
pending), `lecturer` (источники/курсы/content-чанки), `facts` (по статусам + total),
`events` (providers/fallbacks/errors/timing/recent_errors/capture), `health`
(score/label/components).

**Замечание:** на машине без Ollama `indexing`=0 (эмбеддинги тёмные) → health
упирается ~75 — это честный сигнал, не баг.

---

## 9. Extension (Plasmo, MV3)

**Площадки:** только **ChatGPT и Claude** (Gemini удалён — стаб снят, host снят).

- **fetch patch** (`contents/chatgpt.ts`, `contents/claude.ts`, `world: "MAIN"`,
  `run_at: document_start`): патчат `window.fetch`, ловят ответ API разговора по
  `URL_RE` (ChatGPT: `/backend-api/conversation/{id}`), парсят/линеаризуют дерево
  `mapping` в хронологический список сообщений (BFS от root по `children`),
  нормализуют в форму `IncomingConversation` и шлют `window.postMessage` (в MAIN-мире
  `chrome.runtime` недоступен). **DOM не парсится** — только JSON из fetch.
- **relay** (`contents/relay.ts`, изолированный мир, без `world`): слушает
  `window.message` с маркером `__PAM__`, форвардит `chrome.runtime.sendMessage`
  в background.
- **background** (`background.ts`, service worker): очередь `QueueItem` + ретраи
  (`RETRY_DELAY_MS=4000` × attempt, `MAX_ATTEMPTS=5`), `sendConversation` →
  `POST /conversations`. Статистика (`total/success/failed`) в
  `chrome.storage.local`. Сообщения `CAPTURE_CONVERSATION / GET_STATS / RESET_STATS`.
- **capture failures:** при исчерпании ретраев → `stats.failed++` +
  `reportCaptureFailure(source, err)` → `POST /stats/capture-failed` (событие
  `capture_failure`). Best-effort: если бэкенд полностью лёг — сброс зафиксировать
  негде.
- **Манифест (`package.json`):** `host_permissions` = chatgpt.com, chat.openai.com,
  claude.ai, localhost:8000; `permissions` = `storage` (плюс `scripting` добавляется
  Plasmo для динамической регистрации MAIN-скриптов).

**Идемпотентность:** `POST /conversations` — UPSERT по `(source, external_id)`; при
обновлении сообщения **wipe+reinsert** (чанки каскадно удаляются и переэмбеддятся).

---

## 10. Current Roadmap

### Stabilized (завершено, в `main`)
- **Фазы 1–5:** capture + full-text (Phase 1), RAG hybrid (Phase 2), память/факты
  (Phase 3), чат с памятью (Phase 4), Лектор (Phase 5).
- **P0:** Fact Review Queue; Observability Dashboard `/diag`.
- **P0+:** Capture Reliability + Memory Health Score.
- **P1:** Learning Progress (server-persisted); Home Dashboard.
- **Стабилизация (2026-06-14):** closed provider attribution; единый нав-бар;
  главный экран переписан под Home Screen UX Contract; финальный аудит новых систем
  (чисто).
- **P2 — Project Memory + Telegram Capture V1 (2026-06-14):** `projects` +
  `memory_items` + `memory_links` + nullable `project_id`; AI-теггер (`tagging.py`);
  recall (`/memory/recall`); Telegram-бот (`telegram_bot.py`, aiogram long polling).
  Миграции a6b7c8d9e0f1/b7c8d9e0f1a2/c8d9e0f1a2b3 применены; pytest 35/35; E2E живьём
  (проект→item→теггер→link→recall) — зелёно. **Бот запускается отдельно (не авто).**
- Прочее: избранное, поиск (text/semantic/hybrid), вложения (документы+vision),
  true-multimodal по тумблеру, превью docx/xlsx, экспорт курса (md/print), каталог.

### In Progress
- **Стабилизационная фаза** (объявлена 2026-06-14, `.planning/STATE.md`): не новые
  фичи, а приёмка отгруженного на реальных данных. Чеклист: `pytest` в Docker,
  проверка `/diag` на реальных провайдерах, `web build`, сквозной E2E-сценарий.

### Planned (только спроектировано / частично)
- **Project Memory V2 (поверх V1):** UI-раздел `/projects` во фронте (V1 — только
  API + Telegram); векторные эмбеддинги `memory_items` (V1 — full-text+теги);
  голосовые в Telegram (V1 — пропущены, Whisper позже); recall-обход графа по
  `memory_links` (V1 — плоский recall).
- Бэклог (`TODO.md`/`ROADMAP.md`): map-reduce курса по длинным материалам;
  авто-«популярное»/mindmap тем; настоящие эмбеддинги для избранного/курсов;
  route-уровневые тесты; реализация `contents/gemini.ts`.

---

## 11. Project Memory Readiness

> **ОБНОВЛЕНО 2026-06-14:** Project Memory, Telegram Capture, Memory Items, AI
> Tagger, Memory Links — **реализованы V1** (см. §10 и `routes/projects.py`,
> `routes/memory.py`, `tagging.py`, `telegram_bot.py`). Ниже — исходный анализ
> готовности; для реализованных пунктов он показывает, ИЗ чего они собраны, а
> «нужно добавить» в основном уже сделано. Knowledge Graph Lite — пока не делали.

Для каждого кандидата — что **уже есть в коде**, что **нужно добавить**, какие
**таблицы можно переиспользовать**.

### Project Memory
- **Есть:** полный субстрат памяти — `content_sources`/`content_chunks` (документы),
  `conversations`/`messages`/`chunks` (обсуждения), `profile_facts` + Fact Review
  Queue (паттерн извлечения с гейтом и provenance), RAG-ретрив. **Готовый дизайн** —
  `docs/project-memory-design.md`.
- **Нужно добавить:** таблицы `projects` + `project_items`; nullable `project_id`
  (FK) на `conversations` и `content_sources`; скоуп-фильтр в ретриве; проектный
  экстрактор; роуты/UI `/projects`.
- **Переиспользовать:** весь ингест/эмбеддинг/RAG; паттерн review-queue
  (`ACCEPTED_FACT_STATUSES`); анти-инъекция/hallucination-guard `extraction.py`.

### Telegram Capture
- **Есть:** контракт ingest `POST /conversations` (`IncomingConversation`),
  идемпотентный UPSERT, паттерн очереди/ретраев в extension. **Самого Telegram-кода
  НЕТ.**
- **Нужно добавить:** внешний ингестер (бот/userbot) → `POST /conversations`;
  расширить `Source`-литерал (сейчас `chatgpt|claude|gemini` в `schemas.py`) новым
  значением и согласовать индексы/фильтры.
- **Переиспользовать:** `/conversations` UPSERT, `chunks`/`messages`,
  `content_tsv`-пайплайн.

### Memory Items
- **Есть:** ближайший аналог — `profile_facts` (атомарный элемент памяти со
  статусом-гейтом и provenance). В дизайне Project Memory заложен `project_items`
  (универсальный элемент: decision/task/question/note).
- **Нужно добавить:** обобщённой таблицы «memory item» в коде нет; если нужна вне
  проектов — новая таблица либо обобщение `profile_facts`.
- **Переиспользовать:** схему `profile_facts` (status, source_excerpt, confidence)
  как образец.

### AI Tagger
- **Есть:** паттерн LLM-извлечения (`extraction.py`: json_mode, анти-инъекция,
  provenance, дедуп); у фактов есть свободное поле `category` (де-факто тег).
  **Отдельной сущности тегов НЕТ.**
- **Нужно добавить:** таблицу тегов/связей либо нормализованное поле тегов;
  функцию-теггер (по образцу `extract_facts_for_conversation`).
- **Переиспользовать:** `extraction.py`, `record_event` для атрибуции, `category`.

### Memory Links
- **Есть:** единственная «связь» в данных — `profile_facts.source_conversation_id`
  (факт → разговор-первоисточник) и FK-связи моделей. **Таблицы рёбер/ссылок между
  произвольными сущностями НЕТ.**
- **Нужно добавить:** таблицу связей (from_id/to_id/type) при необходимости.
- **Переиспользовать:** паттерн provenance-ссылки (`source_*`), `position`-упорядочивание.

### Knowledge Graph Lite
- **Есть:** **не реализовано.** Нет узлов/рёбер графа, нет кластеризации. Есть
  «сырьё»: эмбеддинги (`chunks`/`content_chunks`) для похожести и `profile_facts`
  как кандидаты-узлы.
- **Нужно добавить:** сущности узлов и рёбер, построение/обход графа.
- **Переиспользовать:** pgvector-эмбеддинги (похожесть/кластеры), `profile_facts`,
  `events` (если нужна телеметрия построения).

---

## 12. Technical Debt

- **Ollama может отсутствовать.** Без неё эмбеддинги «тёмные»: семантический/гибридный
  поиск деградирует (hybrid → text-only; `/chat` материалы/память без вектора —
  пустой контекст), `indexing`-компонент health = 0, health упирается ~75.
- **Часть проверок завязана на Docker.** `pytest`-сьют (`backend/tests/`) гоняется в
  Docker-образе. На dev-машине Docker на паузе, venv нет, `pgvector` в системном
  Python отсутствует → сьют локально не запускается; `web/node_modules` фактически
  пуст → `npm run build`/`tsc` локально не гоняется (см. `handoff.md`).
- **`content_chunks` без векторного индекса** (только messages-`chunks` имеют HNSW).
  Семантика по материалам — full scan; ок на малых объёмах, деградирует при росте.
- **Хранилище — Neon (облако)** по `handoff.md`, что расходится с local-first
  принципом; реальные данные планируется вернуть в локальный Postgres при возврате
  Docker. Креды Neon/OpenRouter ранее светились — рекомендована ротация.
- **Тесты — только pure-function units** (recognize dispatch, reformat split,
  `_safe_name`, SSRF, health, facts review, course progress, retrieve). **Нет
  DB-backed/route-тестов**, нет web-test-раннера, нет CI-шага. Реального E2E
  (extension↔backend в браузере; chat SSE на реальных данных; reformat/vision вживую)
  по коду гарантировать нельзя — нужна ручная приёмка.
- **Re-ingest = wipe+reinsert** сообщений → полный перечанкинг/переэмбеддинг при
  каждом перезахвате разговора (стоимость растёт с длиной).
- **`reformat` duration_ms** включает паузы между чанками (0.8с) — грубый тайминг
  для фон-джоба.
- **Историчные `events`** могли писаться со старыми/`hybrid`-провайдерами до фикса —
  выветрятся из окна `days`, миграцию лога не делали (best-effort).
- **Навигация:** `/me` (Профиль) отсутствует в верхнем нав-баре (достижим только
  через быстрые действия/прямую ссылку) — возможно, недосмотр.
- **Capture-полнота:** для очень длинных чатов возможна ленивая подгрузка/пагинация
  на стороне сайта; текущий BFS берёт весь полученный `mapping` (см. §13).

---

## 13. Open Questions

Только реальные незакрытые вопросы (из кода, `TODO.md`/`ROADMAP.md`,
`docs/project-memory-design.md`):

1. **ChatGPT capture completeness** (`ROADMAP.md`): BFS по всему дереву `mapping`
   (надмножество, включая правки/ветки) **vs** путь по `current_node` (точная видимая
   нить, верный порядок). Что выбрать? Влияет на полноту/порядок импорта.
2. **Local-first vs Neon:** когда и как реальные разговоры переезжают из облачного
   Neon в локальный Postgres (возврат Docker)? До тех пор — нарушение local-first.
3. **Векторный индекс для `content_chunks`:** добавлять HNSW (как у `chunks`) или
   принять full scan? Порог объёма не определён.
4. **Project Memory (из дизайн-дока), открытые вопросы:**
   - привязка существующих чатов к проекту (массовый перенос) — v1 или стретч?
   - извлечение `project_items` — по кнопке или авто-фоном после проектного чата?
   - достаточность видов `kind ∈ {decision, task, question, note}`?
   - политика удаления проекта: `SET NULL` для контента vs каскад?
5. **Профиль в навигации:** должна ли быть вкладка `/me` в верхнем баре?
6. **LLM_PROVIDER по умолчанию:** в коде `config.py` default `groq`; по `handoff.md`
   деплой использует `hybrid` (через `.env`, не в репозитории). Каноничный режим —
   не определён по коду (зависит от не-версионируемого `.env`).
7. **Тестовая стратегия:** вводить ли DB-backed/route-тесты и CI-шаг (сейчас отсутствуют)?

---

> **Как поддерживать актуальность:** при значимых изменениях кода обновлять
> соответствующий раздел здесь. Этот файл — главная карта; детальные решения — в
> `CLAUDE.md` (код/архитектура), `ORCHESTRATOR.md` (процесс), `.planning/STATE.md`
> (короткий «где мы»), `handoff.md` (журнал), `docs/project-memory-design.md` (P2).
