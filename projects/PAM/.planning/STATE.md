# STATE — быстрый указатель текущей позиции

> Короткий «где мы сейчас». История — в `../handoff.md`. План — в `ROADMAP.md`.
> Обновлять при каждом значимом сдвиге.

## 🟢 ТЕКУЩЕЕ (2026-06-04) — всё в `main`, запушено

**Фазы 1–5 завершены.** Продукт: чат с долгой памятью (главный экран) + лектор + импорт истории.
- **Чат:** RAG (топ-6) + профиль фактов + история; **гибридный LLM-роутер** (`LLM_PROVIDER=hybrid`): лёгкое→Groq `openai/gpt-oss-120b` (~4.5с), тяжёлое→OpenRouter `nvidia/nemotron-3-super-120b-a12b:free`. Под ответом плашка движка (⚡/🧠). Параллельный пре-LLM (retrieve+profile+history).
- **Авто-обучение:** после каждого чата фоном извлекаются факты о пользователе (`/me` растёт сам). Ручной триггер — кнопка на `/me`.
- **Сайдбар чатов** как у ChatGPT: секции Закреплённые/Недавнее, hover pin+⋯, контекст-меню (закрепить/архив/удалить, открывается вправо), pin/archive в БД (`conversations.pinned/archived`).
- **UI:** чат на всю ширину, минималистичный скроллбар, клиентский кэш для мгновенных переходов. Разделы: `/` чат, `/history`, `/saved`, `/me` профиль, `/learn` лектор (статья/PDF/YouTube→курс+квиз), `/catalog` (каталог AI-экосистемы + «Вайбкодинг для PAM»).
- **Провайдеры LLM:** `groq` | `openrouter` | `hybrid` | `ollama` (`backend/app/llm.py`, общий OpenAI-совместимый код). Эмбеддинги локально (Ollama `nomic-embed-text`, 768-dim).
- **Хранилище чатов:** Postgres (**сейчас Neon, облако** — Docker на паузе). Браузер только кэширует список (in-memory). Реальные данные → локальный Postgres когда вернётся Docker.
- **Ключи в `backend/.env`** (gitignored): `DATABASE_URL`(Neon), `GROQ_API_KEY`, `OPENROUTER_API_KEY`, `LLM_PROVIDER=hybrid`, `GROQ_MODEL`, `OPENROUTER_MODEL`. **OpenRouter-ключ светился в чате — ротировать.**
- **Промпт для новой машины:** `docs/NEW_MACHINE_PROMPT.md` (для переустановки Windows).

**▶ Первый шаг новой сессии:** см. `docs/NEW_MACHINE_PROMPT.md` (восстановление) или продолжай по бэклогу ниже.

---

- **🔀 РАЗВОРОТ ВИДЕНИЯ (2026-06-02):** главное в продукте — **чат с долгой памятью** (Phase 3+4, чат = главный экран) + **личный лектор** (новый Phase 5). Capture/поиск (Phase 1) переезжает во вспомогательный раздел «Импорт истории». Подробности — в `ROADMAP.md` → «Продуктовое видение».
- **★ Избранное ЗАШИПЖЕНО в `main`** (14/14 тестов: snapshot переживает ре-ингест, FK SET NULL, кириллица) + кнопка обновления на /history и /saved. Полнота перехвата подтверждена (24+24).
- **Ollama установлена и готова** (служба :11434, модель `nomic-embed-text`, 768-dim проверена). От пользователя для эмбеддингов больше ничего не нужно.
- **✅ Phase 2 (RAG) ЗАВЕРШЕНА** (ветка `phase-2-rag` → мерж в `main`): chunks+HNSW, чанкование при ingest, эмбеддинг-воркер (Ollama, фоновый + `/index/run`), `GET /search/semantic` и `GET /search/hybrid` (RRF), UI-переключатель режима. Семантика/гибрид проверены на реальных данных.
- **Фаза:** Phase 1 — capture ПОДТВЕРЖДЁН вживую (пользователь: «Сохранено:1»; мост MAIN→relay→background работает ✅). Открытые мелочи Phase 1: «Ошибок:2» (вероятно накопленные до сброса счётчиков) и «вытащило 86 сообщений — не вся переписка» (возможна пагинация/ленивая подгрузка на стороне сайта; разберём при доводке раздела «Импорт»).
- **Активный branch:** `main` — baseline-коммит `af61f83` запушен на GitHub (`https://github.com/StudentMe2712/PAM.git`). Фазовые ветки пока не созданы.
- **Последний шаг:** хардненинг манифеста extension перед браузерным тестом: убран неиспользуемый `webRequest` (код юзает только `runtime`+`storage`); `https://chat.openai.com/*` добавлен в `host_permissions` (его матчат `chatgpt.ts`/`relay.ts`, а dynamic `registerContentScripts` требует matches ⊆ host_permissions — иначе регистрация MAIN-скриптов падает). Поправлены README (Next 14→16, +relay.ts) и `.env.example` (Neon/asyncpg DSN + backend читает `backend/.env`). Prod build зелёный. Baseline + это — на GitHub.
- **До этого:** extension заскаффолжен (Plasmo 0.90 + React 19). Создан `tsconfig.json` + `assets/icon.png`. **Архитектурный фикс:** добавлен `contents/relay.ts` (isolated-world bridge), сломанные `chrome.runtime` слушатели убраны из MAIN-скриптов.
- **До этого:** связка web↔backend проверена вживую (CORS + ingest/list/search с `Origin: localhost:3000`, тестовая строка удалена). web на чистом Tailwind (Next 16 + Tailwind v4), 2 бага в `page.tsx` починены.

## 🔴 Блокеры / ограничения
- Docker на паузе (Windows). Backend гоняем локально: `backend/.venv` (Python 3.11) против Neon.
- **Neon — ТОЛЬКО dev/тесты, НЕ реальные разговоры** (local-first). Реальные данные — в локальный Postgres, когда вернётся Docker.

## ✅ Phase 4 (чат с памятью) — ЗАВЕРШЕНА, в `main`
**LLM:** гибрид Groq (по умолчанию, ключ в `backend/.env`) + Ollama опцией; эмбеддинги локально. Gemini не используем.
- [x] #7 `llm.py` (Groq SSE / Ollama стриминг) + config. #8 `POST /chat` (RAG топ-6 + anti-injection + SSE + персист в `source='pam'`, мультитёрн). #9 чаты = память. #10 чат-UI на `/` (ChatGPT-стиль: бабблы, аватар, typing-dots, авто-рост ввод, чипы памяти). #11 security-review (чисто) + мерж.
- **Проверено вживую с ключом Groq:** «Что я спрашивал про 1С?» → ответ по реальной истории пользователя; стриминг, дедуп источников, чат сохраняется. ✅
- Запущен backend на :8000 с кодом main (можно пользоваться). PAM-чаты пишутся как `source=pam` (видны и в «Истории» — позже можно отфильтровать).

## ✅ Phase 3 (память/факты) — ЗАВЕРШЕНА (ветка `phase-3-memory` → мерж в `main`)
**Решения:** память = RAG + факты + персона-промпт (без «дообучения модели»). LLM-извлечение через Groq JSON-mode.
- [x] #12 Таблица `profile_facts` + миграция `4b747af609d8` на Neon.
- [x] #13 **ПРОТЕСТИРОВАНО вживую:** `llm.complete(json_mode)` + `extraction.py` (`extract_pending`: строгий JSON только о ПОЛЬЗОВАТЕЛЕ; anti-injection — текст в `<conversation>` = данные; hallucination-guard — факт без `source_excerpt` отбрасывается; дедуп по `content`) + `routes/facts.py` (`POST /facts/extract`, `GET /facts`, `DELETE /facts/{id}`). **Тест: 8 разговоров → 18 фактов, все о пользователе (Windows/1С/SQL Server/SSMS/PowerShell/сети), у каждого цитата + traceable conv_id, без дублей.**
- [x] #14 **Факты подмешаны в чат** (`routes/chat.py`): блок `<profile>` (топ-40 по confidence) перед `<context>`, system-prompt учит использовать его, тот же anti-injection guard. **Проверено: «какую ОС/СУБД/инструменты я использую?» → ответ только из фактов.**
- [x] #15 UI `/me` («Память обо мне»): факты по категориям + confidence% + цитата + удаление + кнопка «обновить профиль» (POST /facts/extract). Вкладка «Профиль» в nav. Web build зелёный.
- [x] #16 `/security-review` — **чисто** (нет High/Medium): ORM-параметризация, UUID-валидация, React-эскейпинг, ключ Groq не логируется. → мерж в `main`.

## ⏭️ Текущее — Phase 5 (Личный лектор), ветка `phase-5-lecturer`
**Видение:** кидаешь PDF / YouTube-ссылку / статью → персональный мини-курс с тестами под твой уровень (уровень берётся из памяти/фактов). См. `ROADMAP.md` → Phase 5.
**Прогресс (GSD):**
- [x] **#1 Слой ингеста** (`content_sources`+`content_chunks`, миграция `96d1d6982add`): `content.py` извлекает текст — статья (httpx+BeautifulSoup, чистка boilerplate, browser-UA) / PDF (pypdf); текст капается, чанкуется (reuse `chunk_text`), эмбеддится фоновым воркером (расширен на content-чанки). API `/learn`: POST `/article`, POST `/pdf` (multipart, 25MB), GET/DELETE sources. **Проверено: httpbin (3595), rfc793 (170k), реальный PDF; 197 чанков заэмбеддились; Wikipedia 403→status=failed.** Деп: `beautifulsoup4`,`pypdf` в `.venv`+pyproject.
- [x] **#2 Генерация курса** (`courses.py`, таблица `courses`, миграция `a1a3e3d1fb0d`): LLM JSON → модули→уроки+квиз; POST/GET `/learn/sources/{id}/course`. **Важный фикс:** профиль не должен подменять ТЕМУ — тема из `<material>`, профиль только калибрует УРОВЕНЬ (перепроверено на Moby Dick → корректный курс по Мелвиллу). Внешний текст в `<material>` как данные (anti-injection).
- [x] **#3 UI `/learn`**: добавить статью URL / загрузить PDF, список материалов со статусами+удаление, генерация/открытие курса, рендер модулей/уроков + интерактивный квиз (выбор→подсветка верного+пояснение). Вкладка «Лектор» (была заглушка «скоро»). Web build зелёный.
- [x] **SSRF-guard** на ингесте статьи (резолв host → блок private/loopback/link-local/reserved/multicast; редиректы вручную, каждый хоп проверяется). Проверено: httpbin ок; 127.0.0.1/192.168.x/169.254.169.254 заблокированы.
- [x] **#4 `/security-review`** по `phase-5-lecturer` — **чисто** (SSRF-guard добавлен, не дыра; ORM/UUID; React-эскейп; внешний текст=данные) → **смержено в `main`** (`6453961`).
- [x] **YouTube-транскрипт** (ветка `phase-5-youtube`): `youtube_video_id()` парсит watch/youtu.be/shorts/embed/live; `ingest_youtube()` через `youtube-transcript-api` (ru/en→любой), sync-fetch в потоке, реальный заголовок через YouTube oEmbed. POST `/learn/youtube`; UI авто-детектит YouTube-ссылки. **Проверено: Rick Astley → транскрипт 2089 симв + заголовок; курс по тексту песни (уровень базовый); парсер юнит-протестирован.** Безопасность (inline): фиксированный хост youtube.com (нет SSRF), defusedxml в либе (нет XXE), video_id валидируется.

**▶ ПЕРВЫЙ ШАГ В НОВОЙ СЕССИИ:** смержить `phase-5-youtube` в `main` (если ещё не смержено). Затем кандидаты: сохранение результатов квиза/прогресс по урокам; убрать `pam`-чаты из «Истории»; авто-«популярное»/mindmap; vision-вложения. Backend локально на :8000; sample-материалы в Neon (httpbin/pdf/rfc793/youtube).

Бэклог: авто-«популярное»/mindmap (Phase 2 готов); вложения файлов/картинок (vision-модель Groq); убрать `pam`-чаты из «Истории». Фоновые: `contents/gemini.ts`; Docker — по слову.

---
### Phase 2 (RAG) — ЗАВЕРШЕНА, в `main`:
- [x] **Чанкование** (`indexing.py::chunk_text`, ~1000 симв по абзацам) — создаётся при ingest + backfill `create_missing_chunks`.
- [x] **Эмбеддинг-воркер**: `embed_text` → Ollama `/api/embeddings`; `embed_pending` пишет вектор. Фоновый цикл в `main.py` lifespan (каждые 15с, устойчив к падению Ollama) + ручной `POST /index/run`.
- [x] **`GET /search/semantic`**: эмбеддит запрос → `Chunk.embedding.cosine_distance` (`<=>`) → топ-N. **Проверено: запрос «рецепт домашнего хлеба» → топ «Хлеб» 0.81 (по смыслу).**
- [x] **Гибрид** (RRF, K=60) — `GET /search/hybrid` (фьюз полнотекста + семантики, деградирует в text-only если Ollama недоступна). Тест 4/4: «выпечка хлеба в духовке» → топ «Хлеб».
- [x] **UI** переключатель «по словам / по смыслу / гибрид» в `/history` (`lib/api.ts::search(q,source,mode)`). Web build зелёный.
- [x] Тесты пройдены. **Phase 2 смержена в `main`.** Реальные данные в Neon доэмбеддились (remaining 0).

**⚠️ Реальные данные в Neon:** пользователь уже накопил реальные разговоры (~467 chunks). Семантика на них работает. Бэклог эмбеддингов (~403 на момент теста) фоновый воркер дожуёт по 64/15с, когда поднимется backend с Phase 2 кодом. (Напоминание: Neon задумывался как dev; реальные данные → при возврате Docker в локальный Postgres; креды ротировать.)
Ollama готова (:11434, `nomic-embed-text` 768-dim). Фоновые: `contents/gemini.ts`; Docker — по слову пользователя.

## 🧭 Мультиагенты (GSD-style, через Agent-инструмент Claude Code)
- **Explore** — широкий поиск по коду перед планированием шага.
- **Plan** — проектирование плана шага/фазы (architecture trade-offs).
- **general-purpose / claude** — исполнение многошаговых задач.
- Ревью: `/code-review` между шагами, `/security-review` перед Phase 3 и 4.
- Принцип: каждый агент стартует со свежим контекстом и узкой задачей; результат фиксируется в `handoff.md`.
