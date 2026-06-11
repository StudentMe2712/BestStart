# PAM — установка / восстановление с нуля

Гайд для подъёма проекта после чистой переустановки Windows (или на новой машине).

## 0. Что переживает переустановку, а что нет

| Где | Что | После переустановки |
|---|---|---|
| **GitHub** (`github.com/StudentMe2712/PAM`) | весь код, миграции, доки, ветки | ✅ `git clone` вернёт всё |
| **Neon** (облако) | все данные: разговоры, чанки, эмбеддинги, факты | ✅ на сервере, нужен лишь доступ к аккаунту Neon |
| **Локальный диск** | `backend/.env` (секреты), `.venv`, `node_modules`, билды, модель Ollama | ❌ сотрётся → пересоздать (см. ниже) |

### 🔴 ПЕРЕД переустановкой — сохрани вручную (НЕ в git):
- **`backend/.env`** — скопируй файл в безопасное место (облако/USB/менеджер паролей). В нём:
  - `DATABASE_URL` (Neon) — также есть в дашборде Neon (Connection string → конвертировать под asyncpg: direct endpoint, `?ssl=require`).
  - `GROQ_API_KEY` — также перевыпускается на console.groq.com (не критично потерять).

## 1. Поставить инструменты
- **Git**, **Node.js 20+** (nodejs.org), **Python 3.11** (python.org), **Ollama** (ollama.com).
- (Docker Desktop — опционально, на будущее для локального Postgres.)

## 2. Клонировать репозиторий
```bash
git clone https://github.com/StudentMe2712/PAM.git
cd PAM
```

## 3. Восстановить секреты
- Положи сохранённый `backend/.env` обратно в `backend/`.
- Если не сохранил — создай по шаблону: `cp .env.example backend/.env`, затем впиши `DATABASE_URL` (из Neon) и `GROQ_API_KEY` (из Groq).

## 4. Backend (Python venv, без Docker)
```bash
py -3.11 -m venv backend\.venv
backend\.venv\Scripts\python -m pip install -e backend
# схема в Neon уже есть; для проверки/наката миграций:
#   set DATABASE_URL=...  &&  backend\.venv\Scripts\python -m alembic -c backend\alembic.ini upgrade head
```

## 5. Ollama (локальные эмбеддинги)
```bash
ollama pull nomic-embed-text
# (опц. локальная чат-модель вместо Groq:)  ollama pull llama3.2:3b
```

## 6. Web
```bash
cd web
npm install
```

## 7. Extension (Plasmo)
```bash
cd extension
npm install
npm run build   # затем chrome://extensions → Load unpacked → extension\build\chrome-mv3-dev (после npm run dev)
```

## 8. Запуск
```bash
dev.bat          # backend (фон) + extension (фон) + web (окно)
# стоп фоновых:  stop-dev.bat
```
Открой http://localhost:3000 (Чат / История / Избранное). Backend: http://localhost:8000/docs.

## Заметки
- `v0dev/` — сгенерированный на v0.dev макет чат-GUI (исходники, для интеграции). Не подключён к проекту.
- Текущее состояние/план — `.planning/STATE.md`, `.planning/ROADMAP.md`, журнал — `handoff.md`.
- Ветка незавершённой работы: `phase-3-memory` (память/факты, в процессе).
