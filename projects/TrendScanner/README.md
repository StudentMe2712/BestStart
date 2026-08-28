# TrendScanner — Local Analytical Intelligence Terminal

**TrendScanner** — автономный локальный аналитический терминал для непрерывного сбора, фильтрации, умной дедупликации и ИИ-оценки бизнес-трендов, микрониш и Micro-SaaS продуктов.

Система непрерывно агрегирует данные из 21 глобального источника (RSS, Reddit JSON, Playwright SPA, Telegram MTProto), проводит эвристическую санитизацию и fuzzy-дедупликацию, а затем классифицирует коммерческий потенциал через Groq Cloud LLM (*Llama-3.1-8b-instant*) со 100% переводом на русский язык.

---

## 📚 База знаний и Архитектура (Obsidian Vault)

Полная интерактивная архитектурная документация проекта развернута в формате **Obsidian Vault**:

👉 **[Главная карта знаний (MOC): TrendScanner_Vault/01_Architecture/Index.md](TrendScanner_Vault/01_Architecture/Index.md)**

### Основные разделы базы знаний:
1. **[Архитектура пайплайна и слияния](TrendScanner_Vault/01_Architecture/System_Pipeline.md)** (`System_Pipeline.md`) — сбор, санитизация, fuzzy-дедупликация ($\ge 85\%$), очередь Groq и фоновый планировщик APScheduler.
2. **[Схема базы данных и хранилище](TrendScanner_Vault/01_Architecture/Database_Schema.md)** (`Database_Schema.md`) — SQLite в режиме WAL, индексы, миграции и CLI-утилита безопасной очистки.
3. **[Экстракторы и 21 источник данных](TrendScanner_Vault/01_Architecture/Parsers_and_Extractors.md)** (`Parsers_and_Extractors.md`) — спецификация RSS, Reddit JSON API, Playwright SPA и Telethon MTProto / Web Preview.
4. **[ИИ-Ядро, Smart Translation & Deep Reports](TrendScanner_Vault/01_Architecture/AI_Engine_and_Translation.md)** (`AI_Engine_and_Translation.md`) — интеграция Groq, `langdetect`, промпт «Безжалостный аналитик», авто-retry и генератор венчурных отчетов.
5. **[Дизайн-система и интерфейс](TrendScanner_Vault/01_Architecture/Design_System_and_UI.md)** (`Design_System_and_UI.md`) — сине-серая палитра Tailwind, Soft UI семафоры, Topbar с таймером обратного отсчета, Sidebar с Inbox Zero.
6. **[Спецификация REST API](TrendScanner_Vault/01_Architecture/API_Reference.md)** (`API_Reference.md`) — документация всех эндпоинтов FastAPI (`/api/v1`), форматы запросов и ответов.

---

## 🛠️ Технологический стек

- **Backend:** FastAPI, Python 3.11+, SQLite 3 (WAL mode + Foreign Keys), APScheduler, Playwright (Chromium Headless), Telethon (MTProto), HTTPX, BeautifulSoup4, `langdetect`, Pydantic v2.
- **Frontend:** React 18, TypeScript, Vite, Tailwind CSS, Lucide Icons (высококонтрастная холодная палитра, Optimistic UI, концепция Inbox Zero).
- **ИИ-модель:** Groq Cloud API (*Llama-3.1-8b-instant*) со строгим классификатором, экспоненциальным бэкоффом при 429 ошибках и автоматической генерацией Deep Reports.
- **Изоляция:** Docker Compose с монтированием `./backend/data:/app/data` для надежного сохранения базы данных и сессий Telegram.

---

## 🚀 Быстрый старт

### 1. Настройка переменных окружения
Скопируйте пример файла конфигурации:
```bash
cp .env.example .env
```
Укажите ваш API-ключ Groq и опциональные настройки Telegram в `.env`:
```env
GROQ_API_KEY=gsk_your_groq_api_key_here
```

### 2. Запуск через Docker Compose (Рекомендуется)
```bash
docker compose up -d --build
```
- **Frontend:** http://localhost:3000
- **Backend API:** http://localhost:8000
- **Swagger UI:** http://localhost:8000/docs

### 3. Локальный запуск без Docker

#### Бэкенд:
```bash
cd backend
pip install -r requirements.txt
python -m uvicorn main:app --host 0.0.0.0 --port 8000 --reload
```

#### Фронтенд:
```bash
cd frontend
npm install
npm run dev
```

---

## 🧪 Тестирование

Запуск полного набора unit и integration тестов:
```bash
cd backend
python -m pytest tests/ -v
```
*(136 из 136 тестов со 100% прохождением)*

Сборка фронтенда:
```bash
cd frontend
npm run build
```
