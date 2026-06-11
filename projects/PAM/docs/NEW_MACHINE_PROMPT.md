# PAM — промпт и чек-лист для новой машины (после переустановки Windows)

> Цель: за 10 минут поднять PAM на чистой Windows и продолжить работу с тем же контекстом.

---

## 🔴 ШАГ 0 — СДЕЛАТЬ ДО ПЕРЕУСТАНОВКИ (иначе потеряешь доступы)

**Скопируй файл `backend/.env`** в облако / на флешку / в менеджер паролей. В нём:
- `DATABASE_URL` — строка подключения к Neon (можно восстановить в дашборде Neon → Connection string, но проще сохранить).
- `GROQ_API_KEY` — перевыпускается на console.groq.com.
- `OPENROUTER_API_KEY` — перевыпускается на openrouter.ai/keys. **Этот ключ светился в чате — всё равно ротируй.**
- `LLM_PROVIDER=hybrid`, `GROQ_MODEL=openai/gpt-oss-120b`, `OPENROUTER_MODEL=nvidia/nemotron-3-super-120b-a12b:free`.

Код, миграции и доки — на GitHub (переживут переустановку). Данные (чаты, факты, эмбеддинги) — в Neon (облако, переживут). Локально сотрётся только `.env`, `.venv`, `node_modules`, билды и модель Ollama — всё пересоздаётся (см. ниже).

---

## 📋 ПРОМПТ ДЛЯ НОВОЙ СЕССИИ CLAUDE CODE (скопируй целиком)

```
Продолжаем проект PAM (Personal AI Memory). Это локальный AI-сервис с долгой памятью:
чат с RAG по моей истории + личный лектор (PDF/YouTube/статья → курс с тестами) +
импорт истории из ChatGPT/Claude через расширение.

Репозиторий: https://github.com/StudentMe2712/PAM.git  (ветка main — там всё свежее)
Стек: FastAPI + SQLAlchemy async + Alembic + Postgres/pgvector (сейчас Neon, облако,
т.к. Docker на паузе) | Next.js 16 + Tailwind v4 | расширение Plasmo | эмбеддинги
локально (Ollama nomic-embed-text) | чат через гибридный роутер Groq+OpenRouter.

Статус: фазы 1–5 готовы. Подробности — в .planning/STATE.md (блок «ТЕКУЩЕЕ»),
.planning/ROADMAP.md, handoff.md.

ПРАВИЛА: Docker на паузе (не запускать без моего слова). Backend локально через
backend/.venv (py3.11) против Neon; запуск dev.bat / stop-dev.bat. Значимые фичи →
отдельная ветка + понятные коммиты; перед коммитом git check-ignore (backend/.env
НЕ коммитить). UI/комментарии на русском. Тест чата/ингеста — через python+httpx
(не urllib и не curl: urllib ловит Cloudflare-1010, curl бьёт кириллицу). GSD:
мелкие верифицируемые шаги, обновляй STATE/handoff (есть команда /handoff).

ПЕРВЫЙ ШАГ: подними проект по docs/NEW_MACHINE_PROMPT.md (раздел «Восстановление»),
дождись зелёного билда и живого ответа чата на localhost:3000, потом спроси меня
что делаем дальше (бэклог — внизу STATE.md).
```

---

## ⚙️ Восстановление (по шагам)

```bash
# 1. Инструменты: Git, Node.js 20+, Python 3.11, Ollama  (Docker Desktop — опц., на потом)

# 2. Клон
git clone https://github.com/StudentMe2712/PAM.git
cd PAM

# 3. Секреты: верни сохранённый backend/.env в backend/
#    (или: cp .env.example backend/.env  и впиши DATABASE_URL + GROQ_API_KEY + OPENROUTER_API_KEY)

# 4. Backend (venv против Neon)
py -3.11 -m venv backend\.venv
backend\.venv\Scripts\python -m pip install -e backend
#    схема в Neon уже есть; проверить миграции (PowerShell):
#    $env:DATABASE_URL = (нужный DSN); backend\.venv\Scripts\python -m alembic -c backend\alembic.ini upgrade head

# 5. Ollama (локальные эмбеддинги — обязательно)
ollama pull nomic-embed-text

# 6. Web
cd web && npm install && cd ..

# 7. Extension (опционально, для импорта истории)
cd extension && npm install && npm run build && cd ..

# 8. Запуск
dev.bat            # backend (фон) + extension (фон) + web (окно).  Стоп: stop-dev.bat
#    Открой http://localhost:3000 (чат) и http://localhost:8000/docs (API).
```

**Проверка, что всё ОК:** на http://localhost:3000 задай чат-вопрос — должен прийти
стримящийся ответ с плашкой движка (⚡ groq / 🧠 openrouter) и чипами «память».

---

## 🧠 Что уже умеет PAM (чтобы не открывать заново)

| Раздел | URL | Что |
|---|---|---|
| Чат | `/` | RAG-память + профиль фактов; гибрид Groq(быстро)/OpenRouter(мощно); сайдбар как ChatGPT (pin/архив/удаление); авто-обучение фактов после каждого чата |
| История | `/history` | импортированные разговоры + полнотекстовый/семантический/гибридный поиск |
| Избранное | `/saved` | ★ снимки сообщений (переживают ре-захват) |
| Профиль | `/me` | факты обо мне по категориям + удаление + «обновить профиль» |
| Лектор | `/learn` | статья/PDF/YouTube → мини-курс (модули+уроки+квиз) под мой уровень |
| Каталог | `/catalog` | каталог AI-экосистемы (MCP/Skills/…) + раздел «Вайбкодинг для PAM» |

## 🔧 Тонкости конфигурации
- `LLM_PROVIDER`: `hybrid` (рекоменд., нужны оба ключа) | `groq` | `openrouter` | `ollama`.
- Сменить «мощную» модель — `OPENROUTER_MODEL` в `.env` (напр. `moonshotai/kimi-k2.6:free` — сильнее, но ~16с).
- Что считать «тяжёлым» запросом — регэксп `_HARD_RE` в `backend/app/llm.py`.
- Возврат Docker → локальный Postgres: docs/SETUP.md; после — ротировать креды Neon.

## 🗒️ Бэклог (кандидаты на «дальше»)
- Ускорить лёгкий путь чата (пропускать RAG-ретрив для совсем коротких реплик → ~2с).
- Ручной тумблер «всегда мощно» в интерфейсе чата.
- Сохранение результатов квиза / прогресс по урокам лектора.
- YouTube для лектора уже есть; авто-«популярное»/mindmap тем; vision-вложения (Groq).
- `contents/gemini.ts` (нужен DevTools на gemini.google.com).
