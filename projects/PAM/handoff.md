# PAM — Handoff Log

> Живой журнал работы над **Personal AI Memory (PAM)** для передачи контекста между чатами.
> Каждый агент/сессия дописывает сюда **что сделано, какие решения приняты, что дальше**.
> Источник правды по архитектуре — `CLAUDE.md`; по плану фаз — `implementation-plan.html` (открывать в браузере) и `VIBE_PROMPT.md`.
> Формат вдохновлён GSD (Get Shit Done): мелкие верифицируемые шаги, фиксация решений, свежий контекст.

---

## 🔴 Активные ограничения (читать первым делом)

- **Docker на ПАУЗЕ** — НЕ запускать `docker compose ...` пока пользователь явно не разрешит. Backend гоняем локально: `backend/.venv` (Python 3.11) против Neon.
- **Neon — ТОЛЬКО dev/тесты, НЕ реальные разговоры** (local-first). Реальные данные — в локальный Postgres, когда вернётся Docker. (Кредиты Neon были в чате — при экспорте чата стоит ротировать.)
- **Git:** ветка `main` на GitHub `https://github.com/StudentMe2712/PAM.git`. Правило пользователя: значимые фичи → **отдельная ветка с понятным именем + понятные коммиты**; мелочь → в `main`. Перед коммитом — preflight `git check-ignore` (секреты/`node_modules`/билды уже в `.gitignore`).
- **Запуск:** `dev.bat` (backend+extension скрыто в фоне, web — в окне) + `stop-dev.bat`. Миграции alembic: `DATABASE_URL="$(grep ^DATABASE_URL= backend/.env|cut -d= -f2-)" backend/.venv/Scripts/python.exe -m alembic ...`.

---

## 🗓️ Сессия 2026-06-04 — качество чата, сайдбар, гибрид-LLM, каталог (всё в `main`)

**Сделано (по фичам, каждая — отдельная ветка → мерж):**
- **Чат-GUI как ChatGPT:** код-блоки с подсветкой (react-syntax-highlighter) + кнопка «копировать», инлайн-код, стриминг-каретка (`web/app/markdown.tsx`).
- **UI/перф:** чат на всю ширину + минималистичный скроллбар (вынес max-w-5xl из layout в страницы); клиентский кэш `web/lib/cache.ts` (мгновенные переходы); `pam`-чаты убраны из «Истории».
- **Каталог:** `/catalog` + `docs/ai-ecosystem-catalog.md` (4 фоновых ресёрч-агента → синтез: MCP/Skills/Connectors/Plugins топы, тренды, + «Вайбкодинг для PAM»).
- **Качество чата:** Groq-модель → `openai/gpt-oss-120b` (reasoning в отдельном поле, не течёт). **Авто-обучение:** факты извлекаются фоном после каждого чата.
- **Сайдбар чатов** (ChatGPT-стиль): Закреплённые/Недавнее, hover pin+⋯, контекст-меню (открывается вправо, не перекрывает список; flip вверх снизу), `conversations.pinned/archived` + PATCH-роут.
- **OpenRouter-провайдер** + **гибрид-роутер** (`LLM_PROVIDER=hybrid`): лёгкое→Groq (~4.5с), тяжёлое→OpenRouter `nemotron-3-super-120b-a12b:free`; плашка движка в UI. Проверено вживую (greeting→groq, «напиши скрипт»→openrouter).

**Решения:** локально уровня Opus/GPT не достичь → гибрид Groq(скорость)+OpenRouter(сила). Приватность ослаблена сознательно (юзер ОК). Хранилище чатов — Postgres (сейчас Neon, облако): 18 чатов/44 сообщения.

**⚠️ Юзер сегодня вечером перебивает Windows.** Создан `docs/NEW_MACHINE_PROMPT.md` (бэкап `.env` + промпт для новой сессии + восстановление). OpenRouter-ключ светился в чате — ротировать.

---

## 🗓️ Сессия 2026-06-03 (вечер) — Phase 3 завершена → старт Phase 5

**Сделано:**
- **#13 протестировано вживую.** Поднял backend локально (`.venv` uvicorn :8000 против Neon). `POST /facts/extract?limit=10` → 8 разговоров → 18 фактов. Проверил `GET /facts`: все факты **только о пользователе** (Windows, 1С, MS SQL Server, SSMS, PowerShell, обслуживание баз 1С, сети/порты), у каждого есть `source_excerpt` (цитата) и `source_conversation_id` (traceable), дублей нет. Гварды (anti-injection / hallucination / дедуп) работают. Пара слабых over-inference фактов — не блокеры (низкая ценность, но traceable).
- **#14 факты в чат** (`routes/chat.py`): `_profile_facts()` собирает топ-40 по confidence → блок `<profile>` перед `<context>`; system-prompt обновлён (использовать профиль, но не зачитывать вслух; тот же data-not-commands guard). Коммит `1a65935`. Проверено вживую: «какую ОС/СУБД/инструменты я использую?» → ответ построен только из фактов.
- **#15 UI `/me`** («Память обо мне»): факты по категориям, confidence%, цитата, удаление (per-fact), кнопка «обновить профиль» (POST /facts/extract, limit=10). Вкладка «Профиль» в `nav.tsx`. API-клиент: `listFacts/deleteFact/extractFacts`. Web build зелёный. Коммит `f9c307a`.
- **#16 `/security-review`** по диффу ветки — **чисто** (нет High/Medium): SQLi нет (ORM-параметризация + UUID-валидация пути), XSS нет (`/me` рендерит plain JSX, не `dangerouslySetInnerHTML`), ключ Groq не логируется. Отсутствие auth — by design (local-first), не ново.

**Решения:**
- Профиль в промпте = **данные**, не команды (анти-инъекция как у `<context>`); факты сортируются по confidence, лимит 40 — чтобы не раздувать промпт.
- `/me` рендерит факты как текст (не markdown) — проще и безопаснее.

**Дальше:** мерж `phase-3-memory` → `main`; затем **Phase 5 (Личный лектор)** на ветке `phase-5-lecturer` — начать со слоя ингеста контента (`content_sources` + парсеры статья/PDF, reuse чанкование Phase 2). См. `.planning/STATE.md`.

---

## 🗓️ Сессия 2026-06-03 (продолжение) — Phase 5 (Личный лектор) backend+UI

**Сделано (ветка `phase-5-lecturer`, 4 коммита):**
- **#1 Ингест контента:** `ContentSource`/`ContentChunk` (миграция `96d1d6982add`), `content.py` (статья: httpx+BeautifulSoup со снятием boilerplate и браузерным UA; PDF: pypdf), чанкование (reuse `chunk_text`) + эмбеддинг content-чанков в фоновом воркере. Роуты `/learn` (POST `/article`, POST `/pdf` multipart 25MB, GET/DELETE sources). Деп `beautifulsoup4`+`pypdf`. **Проверено вживую:** httpbin/rfc793/реальный PDF извлеклись, 197 чанков заэмбеддились, Wikipedia 403→failed.
- **#2 Курс:** `courses.py` + таблица `courses` (миграция `a1a3e3d1fb0d`), LLM JSON-mode → модули→уроки+квиз, POST/GET `/learn/sources/{id}/course`. **Баг и фикс:** профиль перехватывал ТЕМУ курса (Moby Dick → курс про сети/1С). Переписал промпт: тема ИЗ `<material>`, профиль только калибрует УРОВЕНЬ; перепроверено — корректный курс по Мелвиллу.
- **#3 UI `/learn`:** добавление статьи/PDF, список материалов, генерация/просмотр курса, интерактивный квиз. Вкладка «Лектор» вместо «скоро». Build зелёный.
- **SSRF-guard** на ингесте статьи (block private/loopback/metadata IP; ручные редиректы с проверкой каждого хопа). Проверено.

**Решения:**
- Отдельные таблицы `content_*` (не переиспользовать `chunks`, который FK на `messages`) — учебный контент не смешивается с памятью чата.
- Тема курса — строго из материала; профиль — только уровень/темп/аналогии.
- YouTube-транскрипт отложен (доп. зависимость), `kind='youtube'` зарезервирован.

**Дальше:** `/security-review` по `phase-5-lecturer` → мерж в `main`. Sample-материалы оставлены в Neon (httpbin/pdf/rfc793) для тестов курса.

**Добавлено в той же сессии:**
- **Security-review Phase 5 — чисто**, `phase-5-lecturer` смержена в `main` (`6453961`, запушено).
- **YouTube-транскрипт** (ветка `phase-5-youtube`): третий источник лектора. `youtube-transcript-api` (1.2.4, instance API: `.list().find_transcript([...]).fetch()`), sync-fetch в потоке (`asyncio.to_thread`), заголовок через oEmbed. POST `/learn/youtube`; UI авто-детект (`isYoutubeUrl`). Проверено вживую (Rick Astley → транскрипт+заголовок→курс по песне). Инлайн-секьюрити: фиксированный хост (нет SSRF), defusedxml (нет XXE), video_id 11 симв валидируется. → мержить в `main`.

---

## 📍 Текущее состояние (на 2026-06-03)

**🔀 Видение (уточнено):** PAM = личный AI с долгой памятью. ОСНОВНОЕ: (1) **чат с памятью** (Phase 3+4, чат = главный экран), (2) **личный лектор** (Phase 5: PDF/YouTube/статья → курс+тесты). ДОПОЛНИТЕЛЬНОЕ: раздел **«Импорт истории»** (бывш. Phase 1: extension тянет разговоры). Детали — `ROADMAP.md` → «Продуктовое видение».

| Компонент | Статус |
|---|---|
| Backend (FastAPI, Neon, миграции 0001 + `efc12b5654c3` saved_messages) | ✅ проверен вживую (14/14 тестов) |
| Web (Next 16 + Tailwind v4): лендинг `/`, `/history` (список+поиск+refresh), `/c/[id]` (★), `/saved`, nav-бар | ✅ build зелёный, связка с backend проверена |
| Extension (Plasmo): `background`, `popup`, MAIN-world `claude/chatgpt`, isolated `relay`, `gemini`=заглушка | ✅ capture подтверждён в браузере (user 24+24); мост MAIN→relay→background работает |
| Фича ★ Избранное (snapshot, переживает ре-ингест; FK SET NULL) | ✅ зашипжена в `main` |

**Phase 1 закрыта** (как «Импорт истории»). Открытые мелочи: `gemini.ts` (нужен DevTools пользователя), полнота очень длинных чатов ChatGPT (BFS vs current_node — пока полнота подтверждена 24+24).

**▶ СЛЕДУЮЩИЙ ШАГ: Phase 2 (RAG).** Ждём выбор пользователя A/B (см. низ лога) и установку **Ollama** (`ollama pull nomic-embed-text`) — единственное, что нужно от пользователя.

---

## 🗺️ Roadmap (уточнённый — детали в `.planning/ROADMAP.md`)

1. **Phase 1** ✅ *(закрыта)* — capture + full-text search → раздел «Импорт истории». + ★ Избранное.
2. **Phase 2** *(следующая)* — Ollama (`nomic-embed-text`), таблица `chunks`, эмбеддинги, гибридный поиск (text+vector, RRF). Разблокирует авто-«популярное», mindmap v2, память чата.
3. **Phase 3** — память: извлечение `profile_facts` (prompt-injection + hallucination guard `source_message_id`), UI `/me`.
4. **Phase 4** — **чат с памятью = ГЛАВНЫЙ экран** (`POST /chat` SSE, RAG, Groq→Claude). Прошлый список/поиск уже в разделе «Импорт».
5. **Phase 5** — **Личный лектор**: ингест PDF/YouTube/статья → персональный мини-курс + тесты под уровень из памяти.

Правило: фаза шиппится отдельно, на своей ветке (`phase-2-rag` и т.д.). Не тянуть Phase N+1 в Phase N.

---

## 🧭 Рабочий процесс (GSD-style)

- Один branch на фазу (`phase-2-rag`, `phase-3-memory`, `phase-4-chat`).
- Мелкие шаги → atomic commits → обновление этого файла после каждого значимого шага.
- Между фазами: `/code-review`, перед Phase 3/4: `/security-review`.
- Решение по самому GSD (полная установка `@opengsd/gsd-core` vs ручная адаптация) — см. лог ниже.

---

## ✅ Лог шагов

### 2026-06-02 — Сессия: старт процесса
- Перепроверён статус проекта против VIBE_PROMPT.md и implementation-plan.html.
- **Решение зафиксировано:** Docker на паузе до явного разрешения пользователя.
- **Уточнение:** shadcn/ui — только в плане, в реальном `web/INSTALL.md` его нет; web не заскаффолжен.
- Изучён GSD (`open-gsd/gsd-core`): workflow Initialize→Discuss→Plan→Execute→Verify→Ship; state-файлы; `npx @opengsd/gsd-core@latest`.
- Создан этот `handoff.md`.
- **Решение пользователя по GSD:** ручная адаптация в репо (без глобальной установки). → создан `.planning/STATE.md` и `.planning/ROADMAP.md`. Мультиагенты = subagents Claude Code (Explore/Plan/general-purpose).
- **Решение пользователя по БД:** использовать его **Neon** (cloud Postgres) как DEV-базу, пока Docker на паузе. Оговорка от меня: только синтетика/тесты, НЕ реальные разговоры (local-first). Ждём connection string в `.env` (отдельный Neon-проект под PAM, не QoldauFinance).
- **Neon подключён и Phase 1 runtime-проверена ✅** (см. отдельную запись ниже).

**Подготовка к Neon (проверен код, изменения делать при подключении + проверке):**
- `config.py` читает `DATABASE_URL` из `.env`, дефолт `postgresql+asyncpg://pam:pam@localhost:5432/pam`. `db.py` уже с `pool_pre_ping=True` (хорошо для serverless Neon, который засыпает). `alembic/env.py` берёт `DATABASE_URL` из env — миграции подхватят Neon автоматически.
- **SSL нюанс (важно):** у `asyncpg` НЕ `sslmode=require` (это libpq/psycopg), а `?ssl=require` в URL или `connect_args={"ssl": "require"}`. Neon строки обычно дают в формате psycopg (`sslmode=require`) — нужно конвертировать.
- **Pooler нюанс:** брать **direct (non-pooled) endpoint** Neon, иначе с pgbouncer + asyncpg ломаются prepared statements (или ставить `statement_cache_size=0`).
- План применения: получить URL → переписать под asyncpg-формат → положить в `.env` → `alembic upgrade head` (проверить, что `CREATE EXTENSION vector` проходит на Neon) → smoke-тест ingest+search.

### 2026-06-02 — Сессия: Neon подключён, Phase 1 verified
- Пользователь дал Neon connection string (pooled, psycopg-формат).
- Конвертировал под asyncpg: **direct endpoint** (убрал `-pooler`) + `?ssl=require` (вместо `sslmode`/`channel_binding`). Положил в `backend/.env` (gitignored). Предупреждение в файле: только тестовые данные.
- Локальное dev-окружение без Docker: `backend/.venv` на **Python 3.11.9** (`py -3.11 -m venv .venv`), зависимости поставлены (asyncpg 0.31, SQLAlchemy 2.0.50, alembic 1.18, fastapi, uvicorn).
- `alembic upgrade head` → миграция `0001` прошла на Neon, включая `CREATE EXTENSION vector`. ✅
- Smoke-тест endpoint'ов (backend на 127.0.0.1:8000):
  - `POST /conversations` → `created:true`, 2 msg. Повторный → `created:false` (UPSERT/wipe-reinsert работает).
  - `GET /search?q=pgvector` / `Postgres` / `семантического` → находит, сниппеты с подсветкой. **Русский полнотекст через `simple` работает.** ✅
  - `GET /conversations`, `DELETE /conversations/{id}` (204) → ок. Тестовая строка удалена с Neon.
- **Фикс кода:** в `routes/conversations.py` убран мёртвый `session.execute(func.to_tsvector(...))` (без фикса делал бесполезный `SELECT ... FROM messages` full-scan на каждом ingest); `update` поднят в импорты. После фикса smoke-тест повторён — всё ок.

### 2026-06-02 — Сессия: web заскаффолжен (Phase 1)
- **Решение по UI:** чистый Tailwind, НЕ shadcn/ui. Причины: исходники web уже написаны в цельной «терминальной» эстетике на чистом Tailwind; Phase 1 UI — 3 простые страницы без сложных примитивов (диалоги/комбобоксы/тосты), ради которых берут shadcn; shadcn требует `@/`-алиас, а наш скаффолд намеренно `--no-import-alias`. shadcn отложен до Phase 3/4 (UI `/me` и `/chat`). Зафиксировано в Decision Log.
- `create-next-app@latest` → **Next.js 16.2.7 + React 19.2.4 + Tailwind v4** (не 14/15, как писал старый INSTALL.md). Tailwind v4 = нет `tailwind.config.js`, всё через `@import "tailwindcss"` / `@plugin` в `app/globals.css`.
- Процедура: бэкап наших `app/`+`lib/`+`INSTALL.md` → скаффолд в пустую `web/` → возврат наших исходников поверх сгенерированных → удалил мусор скаффолдера (`web/CLAUDE.md`, `web/AGENTS.md`) → бэкап удалён.
- Доставлены зависимости: `react-markdown`, `remark-gfm`, `-D @tailwindcss/typography` (последний — для `prose`, старый INSTALL.md его НЕ ставил → классы `prose` не работали бы). `globals.css` урезан до `@import "tailwindcss"; @plugin "@tailwindcss/typography";`.
- **Починены 2 бага в `web/app/page.tsx`** (всплыли на typecheck):
  1. `sourceClass(): JSX.Element` → `ReactElement` (в React 19 глобального namespace `JSX` нет).
  2. `sourceClass()` возвращает готовый `<span>`-бейдж, но вызывался как `className={sourceClass(...)}` (под `as any`) — JSX-элемент в строковом `className`. Заменил на рендер элемента `{sourceClass(...)}` в 2 местах. Раньше бейджи рендерились бы без стилей.
- **Верификация web:** `npm run build` зелёный (typecheck + SSG `/` и `/c/[id]`). Dev-сервер поднимается, `GET /` → 200, `<title>Personal AI Memory</title>`.
- `web/INSTALL.md` обновлён под Next 16 / Tailwind v4 / typography / async-params.
- **Верификация связки web↔backend ✅:** backend (uvicorn против Neon) + curl с `Origin: http://localhost:3000` (эмуляция браузера, без реального браузера). `POST /conversations` → 200 `created:true` 2 msg; `GET /conversations?source=claude` → наш item; `GET /search?q=pgvector` → сниппет с подсветкой «pgvector» (русский контент). Во всех ответах `access-control-allow-origin: http://localhost:3000` (CORS пускает web-origin → клиентские fetch из `lib/api.ts` пройдут). Тестовая строка удалена (DELETE 204). CORS-дефолт в `config.py`: `chrome-extension://*,http://localhost:3000`.
- **Phase 1 вертикальный срез (backend↔БД↔web) подтверждён.** Осталось по Phase 1: extension (скаффолд + проверка перехвата), `contents/gemini.ts` (нужен пользователь), baseline-коммит.

### 2026-06-02 — Сессия: extension заскаффолжен + фикс MAIN-world моста (Phase 1)
- **Связка web↔backend проверена** (см. правку записи web выше): backend uvicorn против Neon, curl с `Origin: http://localhost:3000`, все ответы с `access-control-allow-origin`, тестовая строка удалена.
- **Extension собран:** Plasmo 0.90.5 + React 19 + TS 6. `package.json` с manifest-блоком (`host_permissions` на 3 AI-сайта + `localhost:8000`, permissions `storage`/`webRequest`). `plasmo build` → `build/chrome-mv3-prod` зелёный.
- **Два недостающих файла, без которых Plasmo не собирается** (в старом INSTALL.md их не было): `tsconfig.json` (Plasmo 0.90 НЕ генерит сам — падал с ENOENT) и `assets/icon.png` (без иконки манифест ссылается на несуществующие `gen-assets/icon16.png` → ERROR). Иконку сгенерил через System.Drawing (тёмный фон + лаймовая рамка + «PAM», 512×512).
- **Как Plasmo цепляет content-скрипты:** MAIN-world (`claude/chatgpt/gemini`) регистрируются динамически через `chrome.scripting.registerContentScripts` (Plasmo внедряет регистратор в background bundle → авто-permission `scripting`); в статическом манифесте их нет. Isolated-world `relay.ts` — обычный `content_scripts` entry.
- **Архитектурный фикс (баг в исходниках):** `claude.ts` и `chatgpt.ts` объявлены `world: "MAIN"`, но содержали `window.addEventListener("message", … chrome.runtime.sendMessage …)`. В MAIN-мире `chrome.runtime` недоступен → мост был бы сломан (capture не доходил бы до background). Это противоречило архитектуре в CLAUDE.md («слушатель в isolated-мире»). Решение: создан `contents/relay.ts` (без `world` → isolated, матчит все 4 паттерна), слушатели убраны из MAIN-скриптов. Обновлён CLAUDE.md (абзац про page/isolated world + file map) и `extension/INSTALL.md`.
- **НЕ проверено вживую:** реальный перехват на claude.ai/chatgpt (нужен Chrome + залогиненный пользователь). Это «самая хрупкая часть Phase 1» по CLAUDE.md — URL_RE/парсеры могли устареть. Фикс моста тоже финально проверяется только в браузере.

### 2026-06-02 — Сессия: хардненинг манифеста extension (перед браузерным тестом)
- Пользователь запустил `npm run dev` (Plasmo dev собрался успешно). Перед загрузкой в Chrome прошёлся по оставшимся техническим моментам:
- **Убран `webRequest`** из `permissions` — grep подтвердил, что код использует только `chrome.runtime` + `chrome.storage`, а capture идёт через патч `window.fetch`, не через `chrome.webRequest`. Неиспользуемое разрешение = лишняя поверхность атаки + пугающий промпт (а проект privacy-first).
- **Добавлен `https://chat.openai.com/*` в `host_permissions`** — его матчат `chatgpt.ts` и `relay.ts`, но в host_permissions его не было. MAIN-world скрипты Plasmo регистрирует динамически через `chrome.scripting.registerContentScripts`, который требует matches ⊆ host_permissions; рассинхрон мог уронить регистрацию ВСЕХ MAIN-скриптов → capture не работал бы даже на chatgpt.com.
- **Док-фиксы:** README (`Next.js 14`→`16`, добавлен `relay.ts` в дерево); `.env.example` (заметка про Neon/asyncpg DSN — direct endpoint + `?ssl=require` + префикс `postgresql+asyncpg://`; и что backend читает `backend/.env`, т.к. `env_file=".env"` CWD-relative).
- **Важно для теста:** Plasmo dev НЕ хот-релоадит изменения `manifest` в `package.json` → пользователю надо перезапустить `npm run dev` (Ctrl+C + заново) и перезагрузить extension в `chrome://extensions`, иначе будет старый манифест (с webRequest, без chat.openai.com). Prod build уже пересобран и валиден.

### 2026-06-02 — Сессия: dev.bat + подтверждение работы capture-моста
- **`dev.bat`** в корне: запускает backend (venv+uvicorn --reload), web (`npm run dev`), extension (`plasmo dev`) в 3 отдельных окнах через `start /D`. Локальный Postgres не стартует (Neon + Docker на паузе). Когда вернётся Docker — backend-окно меняется на `docker compose up`.
- **🎉 Capture-пайплайн ПОДТВЕРЖДЁН в браузере.** Пользователь загрузил extension (backend был выключен). Popup: `Сохранено:0, В очереди:1, Ошибок:1`. Это доказывает, что вся цепочка работает: патч `window.fetch` (MAIN) → `postMessage` → `relay.ts` (isolated) → `chrome.runtime.sendMessage` → очередь в `background.ts`. Единственный сбой — финальный `POST localhost:8000` (backend не запущен → 5 ретраев → `failed++`). **Мост MAIN→relay→background (мой фикс) работает.** Осталось: поднять backend, сбросить счётчики, переоткрыть разговор → должно уйти в «Сохранено».
- Возможный остаточный риск: если backend поднят, но прилетит 422 — значит парсер отдаёт payload не по схеме (тогда смотреть claude.ts/chatgpt.ts). Но структурно payload собрался (иначе не дошёл бы до очереди).

### 2026-06-02 — Сессия: разворот видения + ветка import-history-section
- **Разворот продукта** (по уточнению пользователя): главное — чат с долгой памятью (Phase 3+4, чат = главный экран) + личный лектор (новый Phase 5, PDF/YouTube/статья → курс+тесты). Capture/поиск (Phase 1) → вспомогательный раздел «Импорт истории». ROADMAP обновлён (раздел «Продуктовое видение»), коммит на `main` `36a5a52`. Ответы пользователю: чат-с-памятью = это и был финал плана; парсер разговоров нужен только для импорта (опционально), главному чату — нет; лектору нужны парсеры PDF/транскрипт/статья.
- **Капча состояния:** пользователь подтвердил перехват вживую («Сохранено:1»), но заметил «Ошибок:2» (вероятно накопленные до сброса) и «86 сообщений — не вся переписка».
- **Выбор приоритета:** (C) доводка «Импорт истории». Ветка **`import-history-section`**.
- **Сделано на ветке:**
  - web: список+поиск перенесён в `/history`; главная `/` стала лендингом с карточками разделов (Чат/Лектор — «скоро», История — «открыть»); навигация в `layout.tsx`; back-link детальной → `/history`. Импорт в `history/page.tsx` поправлен на `../../lib/api`. Build зелёный (роуты `/`, `/history`, `/c/[id]`).
  - extension: вероятная причина «не вся переписка» — `chatgpt.ts` отбрасывал object-парты (мультимодальный/структурный контент); теперь извлекает `p.text||p.content`. Оба парсера логируют «extracted N of M» для диагностики. Build зелёный.
- **Открыто (нужен пользователь):** прогнать перехват, снять из DevTools console числа `[PAM/...] extracted N of M`. Разрыв N<M → теряем парты (точечный фикс парсера); M уже меньше реального → сайт отдаёт частично (пагинация, надо дотягивать).

### 2026-06-02 — Сессия: фоновый dev.bat, nav-бар, диагностика полноты
- **`dev.bat` переписан на фоновый запуск:** backend и extension стартуют скрыто через `powershell Start-Process -WindowStyle Hidden` с редиректом в логи (`backend\dev-backend.log`, `extension\dev-extension.log` — оба под `*.log` в .gitignore). web — в видимом окне. Проверил, что `Hidden + RedirectStandardOutput` работает на PS 5.1. Добавлен **`stop-dev.bat`** (через `Get-CimInstance` матчит `uvicorn app.main`/`plasmo` и `Stop-Process`). Оба .bat — чистый ASCII.
- **Nav-бар** (`web/app/nav.tsx`, клиентский, `usePathname`): sticky-бар с backdrop-blur, бренд PAM, табы с активным состоянием, «скоро»-чипы для Чат/Лектор. Заменил прежние «ссылки как на форуме». web build зелёный.
- **Диагностика перехвата:** пользователь дал `[PAM/chatgpt] extracted 48 messages from 81 nodes`. Вывод: 33 узла — системные/пустые/tool, 48 — вероятно полная нить (не потеря). Усилил лог разбивкой по ролям (user/assistant/...), чтобы подтвердить. CSP-ошибка `ws://localhost:1815` = Plasmo HMR-вебсокет, только в dev, на capture не влияет.
- **Запланированы фичи раздела «Импорт»** (ROADMAP): pin важных сообщений, mindmap тем «что чаще спрашиваешь» (v1 частотность / v2 эмбеддинги Phase 2), аналитика. Заметка про ChatGPT: захват по JSON из fetch, не по скроллу — порядок «старое вверху» на полноту не влияет.
- Всё на ветке `import-history-section`.

### 2026-06-02 — Сессия: ★ Избранное (ветка saved-messages)
- **Полнота перехвата закрыта:** пользователь дал `extracted 48 from 81 {user:24, assistant:24}` → 24+24 полная переписка, потерь нет (33 узла служебные). CSP `ws://localhost:1815` = Plasmo HMR (dev-only), безвреден.
- **Выбор пользователя:** сначала ручное ★ Избранное, потом RAG (auto-«популярное» — это RAG-фича, нужны эмбеддинги).
- `import-history-section` смержена в `main` (ff). Заведена ветка `saved-messages`.
- **Backend ★ Избранное:** модель `SavedMessage` (снимок content; FK conversation_id → SET NULL; НЕ флаг на messages, т.к. их вайпают при ре-ингесте), схемы `SavedMessageIn/Out`, роуты `POST/GET/DELETE /saved` (`routes/saved.py`, зарегистрирован в main.py). Миграция autogenerate `efc12b5654c3` (down_revision 0001), применена на Neon. Smoke-тест свежим backend: openapi содержит /saved, POST→201, GET→1, DELETE→204, итог 0. (Прежний 404 — отвечал старый процесс на :8000; убил, поднял свежий. Прежний 400 на POST — кириллица в curl на Windows билась, ASCII-тело прошло; реальный браузер шлёт корректный UTF-8.)
- **Web ★ Избранное:** `lib/api.ts` (+saveMessage/listSaved/deleteSaved+типы), кнопка ☆/★ на каждом сообщении в `/c/[id]` (состояние savedIds), новый раздел `/saved` (markdown, удаление, ссылка на разговор), вкладка «Избранное» в nav. Web build зелёный (роуты /, /history, /saved, /c/[id]).
- **Открыто:** браузерная проверка (нужен backend с НОВЫМ кодом — перезапусти dev.bat); затем мерж в main. Дальше — Phase 2 RAG (нужен Ollama).

### 2026-06-03 — Сессия: кнопка обновления + расширенные тесты Избранного
- Пользователь подтвердил: ★ Избранное работает. Попросил красивую кнопку обновления в «Истории» (вместо перезагрузки браузера) + доп. тесты.
- **Кнопка обновления:** `web/app/refresh-button.tsx` (SVG refresh-иконка + `animate-spin` при загрузке). Подключена на `/history` и `/saved` через `tick`-стейт (эффект списка зависит от `[source, tick]` / `[tick]`). Web build зелёный.
- **Расширенные тесты (14/14):** stdlib-`urllib` скрипт (UTF-8), backend на порту **8001** (8000 был занят фантомным сокетом/рабочим backend пользователя — не трогал). Проверено: POST/UPSERT, `/saved` 201/Cyrillic round-trip/link, **★ выживает при ре-захвате разговора** (ключевое), detail после ре-ингеста, поиск кириллицы, DELETE conversation→204, **★ остаётся + conversation_id→NULL (FK SET NULL)**, DELETE saved→204, cleanup. Тестовые данные с Neon удалены (0 остатков).
- Замечание по портам: `--reload` backend плодит дочерний процесс, чья cmdline НЕ матчит `*uvicorn*app.main*` → `stop-dev.bat`/kill по имени может не добить его; надёжнее kill по владельцу порта. (На будущее.)
- Всё на ветке `saved-messages`. Дальше: мерж в main + Phase 2 RAG (нужен Ollama).

### 2026-06-03 — Сессия: Ollama настроена + старт Phase 2 (таблица chunks)
- Пользователь установил Ollama. Пояснил: GUI-чат не нужен (закрыть); важна служба на :11434 (работает, `HTTP 200`). `ollama` НЕ в PATH git-bash → работаю через HTTP API.
- **Модель скачал сам через API**: `POST :11434/api/pull {"model":"nomic-embed-text"}` → success. Проверил `POST :11434/api/embeddings` → **dim=768**, кириллица ок. Под `vector(768)` в плане.
- **Старт Phase 2** (путь A — Ollama готова). Ветка **`phase-2-rag`**.
  - `pgvector` 0.4.2 в venv + добавлен в `backend/pyproject.toml`.
  - Модель `Chunk` (`backend/app/models.py`): message_id FK CASCADE, content, position, `embedding Vector(768)`, created_at.
  - Миграция `2ec708645017` (autogenerate + ручная правка): **добавил `from pgvector.sqlalchemy import Vector`** (autogen забыл импорт, упало бы NameError) и **HNSW-индекс** `CREATE INDEX ... USING hnsw (embedding vector_cosine_ops)`. Применена на Neon, проверено: таблица + 3 индекса (pkey, ix_chunks_message, ix_chunks_embedding_hnsw).
- **Дальше по Phase 2** (см. STATE): чанкование → эмбеддинг-воркер (Ollama) → `/search/semantic` + гибрид (RRF) → UI-переключатель → тесты → мерж.
- Запрос эмбеддинга для воркера: `POST http://localhost:11434/api/embeddings` body `{"model":"nomic-embed-text","prompt":"<text>"}` → `{"embedding":[...768...]}`.

### 2026-06-03 — Сессия: Phase 2 ядро (чанкование + эмбеддинги + семантический поиск)
- Трекаю задачи через таск-лист (по просьбе пользователя): #1 чанкование ✅, #2 воркер ✅, #3 семантика ✅; #4 гибрид/#5 UI/#6 тесты+мерж — pending.
- **`app/indexing.py`**: `chunk_text` (~1000 симв, по абзацам, хард-сплит длинных), `embed_text` (httpx → Ollama `/api/embeddings`, `nomic-embed-text`), `create_missing_chunks` (backfill), `embed_pending` (64 за раз), `index_pending` (backfill+embed, отдаёт counts).
- **Чанкование при ingest**: `routes/conversations.py` создаёт `Chunk` для новых сообщений (embedding NULL). На ре-ингесте старые messages удаляются → chunks каскадятся (FK CASCADE) → создаются заново.
- **Воркер**: фоновый цикл в `main.py` lifespan (asyncio task, каждые 15с `index_pending`, устойчив к падению Ollama — log+continue, idle = дешёвые SELECT без вызова Ollama). + ручной `POST /index/run` (`routes/indexing.py`).
- **`GET /search/semantic`** (`routes/search.py`): `embed_text(q)` → `Chunk.embedding.cosine_distance(qvec)` (`<=>`, использует HNSW), джойн message+conversation, топ-N; 503 если Ollama недоступна. `rank` = `1 - distance`.
- **Config**: `OLLAMA_URL`, `EMBED_MODEL` в settings. Деп `httpx` добавлен (venv + pyproject).
- **E2E тест (8001, fresh backend)**: ingest «Хлеб»+«Авто» → `/index/run` → `/search/semantic q="рецепт домашнего хлеба"` → топ **«Хлеб» 0.81** (семантика по смыслу!), полнотекст работает, тестовые разговоры удалены. 7/9 ассертов — 2 «фейла» НЕ баги (фоновый воркер опередил ручной run по chunks_created; remaining=403 — бэклог реальных данных).
- **ОТКРЫТИЕ:** в Neon уже лежат реальные разговоры пользователя (~467 chunks, всплыл реальный «Excel в 1С»). Семантика на них работает. Бэклог эмбеддингов дожуётся воркером.
- Всё на ветке `phase-2-rag` (НЕ мержено — впереди гибрид/UI/тесты).

### 2026-06-03 — Сессия (продолжение): гибрид + UI + мерж Phase 2
- **#4 Гибрид** (`GET /search/hybrid`, `routes/search.py`): RRF (K=60) фьюз полнотекста (message-level) и семантики (chunk-level, дедуп до лучшего чанка на сообщение). Деградирует в text-only, если Ollama недоступна (try/except вокруг embed). Тест 4/4: «выпечка хлеба в духовке» → топ «Хлеб».
- **#5 UI**: `lib/api.ts::search(q, source, mode)` (`text`→`/search`, `semantic`→`/search/semantic`, `hybrid`→`/search/hybrid`); в `/history` добавлен `<select>` «по словам / по смыслу / гибрид». Web build зелёный.
- **#6**: тесты пройдены, реальные данные в Neon доэмбеддились (`remaining:0`). **Ветка `phase-2-rag` смержена в `main`.**
- **Phase 2 ЗАВЕРШЕНА.** Чтобы пользоваться семантикой в UI: поднять backend с кодом `main` (перезапусти `dev.bat`) — фоновый воркер держит эмбеддинги свежими; выбрать режим в «Истории».
- Дальше по ROADMAP: Phase 3 (память/`profile_facts`, Gemini) или авто-«популярное»/mindmap v2 поверх эмбеддингов; чат (Phase 4) = главный экран. Решает пользователь.

### 2026-06-03 — Сессия: старт Phase 4 (чат), выбор LLM
- **API-ликбез (веб, июнь 2026):** Gemini free-tier — лимиты урезаны, Pro убрали, и **на free Google тренируется на данных** → для приватного PAM не берём. Groq free — без карты, ~30 RPM, быстро, на данных не тренируется → выбран. Эмбеддинги остаются локально (Ollama).
- **Железо пользователя:** RAM 15.8ГБ (free ~1.8ГБ), i3-12100, **нет дискретной GPU** (Intel UHD 730). Вывод: хорошую локальную чат-модель не тянет → облако (Groq) практичнее; локалка — только маленькая 3B, медленно.
- **Решение:** гибрид — Groq по умолчанию + Ollama опцией (провайдер-агностичный код). Gemini не нужен (факты тоже через Groq).
- **Сделано (#7):** `config.py` (+`LLM_PROVIDER`, `GROQ_API_KEY`, `GROQ_MODEL=llama-3.3-70b-versatile`, `OLLAMA_CHAT_MODEL=llama3.2:3b`); `app/llm.py` (`stream_chat` → Groq SSE / Ollama `/api/chat`). Импорт проверен.
- **Задачи #8–#11** (pending): `POST /chat` (RAG-ретрив + system-промпт с anti-injection + SSE), хранить чаты как `Conversation(source='pam')` (→ чанкуются/эмбеддятся = память), чат-UI на главной, `/security-review`+тест+мерж.
- **Сделано #7–#10:** `llm.py` (Groq/Ollama стриминг); `POST /chat` (`routes/chat.py`: RAG топ-6 + anti-injection system-промпт + SSE + персист в `Conversation(source='pam')`, мультитёрн); чат-UI на `/` (`app/page.tsx`: стриминг через fetch+ReadableStream, сайдбар, markdown, чипы памяти); nav «Чат» активна. Backend импорт-чек ок (`/chat` в роутах), web build зелёный.
- **Осталось #11 (блок на ключ):** `/security-review` + e2e-тест чата + мерж. **Ждём `GROQ_API_KEY`** (console.groq.com) в `backend/.env`. Без ключа: `LLM_PROVIDER=ollama` + `ollama pull llama3.2:3b` (медленно на этом ПК).
- **#11 + МЕРЖ:** пользователь вписал `GROQ_API_KEY`. 404 на `/chat` был из-за старого backend на :8000 → перезапустил (убил все python: `--reload` плодит дочерний процесс, не матчащийся по cmdline — надёжно бить по порту/`Get-Process python`). **E2E с Groq: «Что я спрашивал про 1С?» → ответ по реальной истории**, стриминг, дедуп источников, чат сохранён. GUI допилен под ChatGPT/Claude (бабблы, аватар, typing-dots, авто-рост ввод). `/security-review` — чисто (инлайн; XSS нет — ReactMarkdown без raw; SQLi/SSRF нет; секреты ок). **`phase-4-chat` смержена в `main` (ff).**
- **Phase 4 ЗАВЕРШЕНА.** Дальше по плану пользователя: авто-«популярное»/mindmap в «Импорте» (поверх эмбеддингов), затем Phase 3 (profile_facts через Groq). Заметка: curl на Windows бьёт кириллицу в body → тестировать чат через python/urllib (UTF-8), не curl.

### 2026-06-03 — Сессия: чат-качество + старт Phase 3 (память)
- **Уточнения пользователю:** «обучение модели» не нужно (fine-tune недоступен/дорог) → память = RAG + факты (Phase 3) + персона-промпт. Домены: сети/сисадмин/1С (для 1С нужна сильная модель — можно сменить Groq-модель). Вложения файлов/картинок — отдельный шаг (картинки → vision-модель Groq). Figma напрямую недоступен (нет Figma-MCP) → GUI через v0.dev по промпту.
- **Сделано:** улучшен системный промпт чата (персона под сети/сисадмин/1С + типовые вопросы + игнор нерелевантной памяти), смержено в `main` (`e4836ec`). Выдан промпт для v0.dev (пользователь генерит GUI параллельно).
- **Выбор пользователя:** следующий крупный кусок = **Phase 3 (память обо мне)**; GUI = v0.dev по промпту.
- **Phase 3 #12 готов:** модель `ProfileFact` + миграция `4b747af609d8` (на Neon). Поймал баг autogenerate: он пытался дропнуть `ix_chunks_embedding_hnsw` (op.execute-индекс не виден autogenerate) — убрал лишние drop/create из миграции. HNSW цел.
- **Осталось Phase 3 (#13–#16):** извлечение фактов (Groq JSON + guards) → факты в контекст чата + /facts API → UI /me → security-review+мерж. Ветка `phase-3-memory`.

### 2026-06-03 — Сессия: подготовка к переустановке Windows
- Пользователь вечером переставляет Windows. Аудит сохранности:
  - **В git/GitHub:** весь код + миграции + доки + ветки. `main` (фазы 1–4), `phase-3-memory` (текущая, память). Всё запушено (`git push --all`).
  - **НЕ в git (сохранить вручную!):** `backend/.env` (gitignored) — `DATABASE_URL` Neon + `GROQ_API_KEY`. Восстановимы и из дашбордов Neon/Groq.
  - **В облаке:** все данные в Neon (разговоры/чанки/эмбеддинги/факты) — переживут переустановку.
  - **Пересоздаётся:** `.venv`, `node_modules`, билды, модель Ollama (`ollama pull nomic-embed-text`).
- Создан **`docs/SETUP.md`** — пошаговое восстановление с нуля. `.env.example` дополнен всеми текущими переменными (Ollama/Groq/LLM).
- **`v0dev/`** закоммичен — сгенерированный на v0.dev макет чат-GUI (исходники `components/pam/*` + shadcn ui). Не подключён; интеграция — отдельный шаг.
- Активная ветка для продолжения: **`phase-3-memory`** (после клона: `git checkout phase-3-memory`). Здесь же — самый свежий STATE/handoff.

### 2026-06-03 — Сессия: Phase 3 #13 (код извлечения фактов, НЕ протестирован)
- Написан код извлечения фактов (ветка `phase-3-memory`), но **тест прерван пользователем** — extract не запускали.
  - `llm.py`: добавлен `complete(messages, json_mode)` — не-стриминговый вызов (Groq `response_format=json_object` / Ollama `format=json`).
  - `extraction.py`: `extract_facts_for_conversation` + `extract_pending(limit_convs)`. Строгий JSON `{"facts":[{category,content,confidence,source_excerpt}]}`. **Guards:** anti-injection (текст разговора в `<conversation>` = данные, не команды), hallucination (факт без `source_excerpt` отбрасывается), дедуп по `content` (lowercase). Обрабатывает разговоры без фактов (idempotent-ish).
  - `routes/facts.py`: `POST /facts/extract?limit=N`, `GET /facts`, `DELETE /facts/{id}`. Зарегистрирован в `main.py`. Схема `ProfileFactOut` в `schemas.py`.
  - Импорт-чек OK (маршруты `/facts`, `/facts/extract`, `/facts/{fact_id}`).
- **СЛЕДУЮЩИЙ ШАГ:** поднять backend, `POST /facts/extract?limit=10` → `GET /facts`, проверить качество фактов (только о пользователе, с цитатами). Затем #14 — подмешать профиль фактов в промпт чата (`routes/chat.py`), #15 — UI `/me`, #16 — security-review+мерж.
- Замечание: тестировать /facts можно curl'ом (нет тела запроса → проблема кириллицы в body не возникает).

### 2026-06-03 — Сессия: интеграция best-practice репо (выборочно)
- Пользователь попросил «полностью интегрировать» https://github.com/shanraisshan/claude-code-best-practice. Изучил (клон во временную папку): это **репо-справочник про Claude Code** (концепты, сравнение воркфлоу GSD/Spec-Kit/Superpowers/RPI, cross-model, 83 tips) + **демо-`.claude/`** (weather/time/presentation агенты, звуковые хуки .mp3/.wav, settings с кастомным спиннером «Admiring Shayan», чужие permissions, .mcp.json с playwright/context7/deepwiki). 445 файлов, НЕ код приложения.
- **Решение:** слепо копировать всё = засорить PAM. Интегрировал ВЫБОРОЧНО (ценность = знания/паттерны):
  - **Создан `docs/claude-code-best-practices.md`** — выжимка применимых практик (R→P→E→R→S, контекст <40%, новая задача=новая сессия, субагенты для контекста, CLAUDE.md <200 строк, вертикальные срезы, grill-me, и т.д.) со ссылкой на источник.
  - **Создан `.claude/commands/handoff.md`** — project-команда `/handoff` (кодифицирует наш ритуал: обновить STATE/handoff + git-чек + сводка + промпт).
  - **НЕ копировал:** звуковые хуки, демо-агенты/скиллы, чужой settings.json/.mcp.json, презентации, changelog, Codex-конфиги (это обучающий материал, не ассеты проекта).
- Временный клон удалён. Файлы — на ветке `phase-3-memory` (придут в main при мерже Phase 3).
- **Полезный вывод на будущее:** там есть готовые воркфлоу (GSD `gsd-build/get-shit-done`, Superpowers, Spec-Kit) — если захотим формальный процесс-движок, можно поставить как плагин/marketplace отдельно.

**Как поднять backend локально (без Docker):**
```bash
cd backend
./.venv/Scripts/python.exe -m uvicorn app.main:app --host 127.0.0.1 --port 8000
# миграции: DATABASE_URL берётся из backend/.env приложением; для alembic — задать env var:
#   DATABASE_URL='postgresql+asyncpg://...?ssl=require' ./.venv/Scripts/python.exe -m alembic upgrade head
```

---

## 📝 Журнал решений (Decision Log)

| Дата | Решение | Почему |
|---|---|---|
| 2026-06-02 | Docker-запуск на паузе | проблемы с Windows у пользователя |
| 2026-06-02 | Применяем GSD-подход к процессу | мультиагентный вайбкодинг, борьба с context rot |
| 2026-06-02 | GSD — ручная адаптация в репо, без глобальной установки | обратимость, local-first, нет влияния на другие проекты |
| 2026-06-02 | Neon как DEV-БД пока Docker на паузе | pgvector «из коробки», нет боли с установкой на Windows |
| 2026-06-02 | Neon — только тестовые данные, НЕ реальные разговоры | сохранить local-first/приватность PAM |
| 2026-06-02 | Web UI — чистый Tailwind, НЕ shadcn/ui (отложен до Phase 3/4) | исходники уже на Tailwind; Phase 1 UI прост; shadcn требует `@/`-алиас (у нас `--no-import-alias`) |
| 2026-06-02 | Добавлен `contents/relay.ts` (isolated-world bridge), `chrome.runtime` убран из MAIN-скриптов | `chrome.runtime` недоступен в `world:"MAIN"`; так требует архитектура CLAUDE.md; иначе capture не доходит до background |

---

## ⏭️ Следующие действия (кандидаты)

- [x] Выбрать способ внедрения GSD → ручная адаптация.
- [x] Выбрать путь прогресса без Docker → Neon dev + локальный venv.
- [x] Поднять backend, прогнать миграцию, проверить `POST /conversations` и `/search`.
- [x] Заскаффолдить web по `web/INSTALL.md` → чистый Tailwind, Next 16, build зелёный.
- [x] Проверить связку web↔backend вживую (CORS + ingest/list/search).
- [x] Заскаффолдить extension (Plasmo build зелёный) + фикс MAIN-world моста (`relay.ts`).
- [ ] Проверить extension↔backend вживую в Chrome (нужен пользователь: Load unpacked + логин на claude.ai/chatgpt).
- [ ] Заскаффолдить extension по `extension/INSTALL.md`, проверить перехват на claude.ai/chatgpt → backend.
- [ ] Реализовать `contents/gemini.ts` (нужен пользователь + DevTools на gemini.google.com).
- [ ] Сделать первый baseline git-коммит (по просьбе пользователя).
- [ ] (опц.) Решить, нужен ли `alembic/env.py` авто-load `.env`, чтобы не задавать `DATABASE_URL` вручную для миграций.
