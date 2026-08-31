# KzFlightSniper — Technical Specification & Architectural Blueprint

## 1. Executive Summary & Overview

**KzFlightSniper** is an asynchronous flight tracking, price monitoring, and automated alerting engine tailored for the Kazakhstan aviation market (domestic routes like ALA $\leftrightarrow$ NQZ, CIT, SCO, GUW, UKK, etc., and international connections). It continuously tracks ticket prices through headless browser interception and stealth automation, alerting users via a feature-rich Telegram bot the instant prices drop below their configured target thresholds.

---

## 2. System Architecture

```mermaid
graph TD
    User([Telegram User]) <-->|Natural Text, Commands & Alerts| Bot[aiogram 3.x Bot Router]
    Bot -->|NLP Extraction| Parser[NLP Parser (Groq / Heuristic)]
    Parser -->|ParsedFlightIntent| Bot
    Bot <--> DB[(aiosqlite Database)]
    Scheduler[APScheduler AsyncIOScheduler (60s Tick)] -->|Triggers Due Checks| Worker[Sniper Worker Engine]
    Worker -->|Fetch Due Tasks| DB
    Worker -->|Execute Search| ProviderAdapter[Provider Adapter Interface]
    ProviderAdapter -->|Interception & Scrape| AviataProvider[Aviata.kz Provider (Playwright + Stealth)]
    AviataProvider -->|JSON Intercept & Parse| Worker
    Worker -->|Price <= Target & Deduplication OK| AlertDispatcher[Alert Dispatcher]
    AlertDispatcher -->|Push Notification| Bot
    AlertDispatcher -->|Record Alert Log| DB
    FastAPI[FastAPI Service] -->|Health & Webhook API| Bot
```

### Core Architecture Components:

1. **Telegram Bot Interface (`aiogram 3.x`)**:
   - Asynchronous Telegram bot with natural language processing text handler and commands (`/start`, `/help`, `/snipe`, `/list`, `/delete`, `/cancel`).
   - Inline interactive keyboards for confirming or cancelling parsed flight intents.

2. **NLP Intent Parsing Engine (`Groq LLM` + Heuristic Fallback)**:
   - Structured JSON flight extraction powered by Groq Llama 3.3 (`llama-3.3-70b-versatile`).
   - Resilient zero-dependency rule-based heuristic parser with full Kazakhstan and international city name declensions, relative date resolution ("завтра", "через неделю", "15 октября"), currency conversion (USD, EUR, RUB to KZT), and custom interval detection.

3. **Asynchronous Task Scheduler (`APScheduler`) & Custom Intervals**:
   - `AsyncIOScheduler` executing checks at a configurable tick (default 60s), evaluating only tasks that are due based on their individual `interval_minutes` setting (e.g. 5m, 10m, 30m, 60m).

4. **Browser Engine & Traffic Interception (`Playwright` + `playwright-stealth`)**:
   - Headless Chromium controlled via Playwright with stealth configurations (patching `navigator.webdriver`, webgl vendor, chrome runtime, randomized user agents, realistic viewport).
   - Network response interception (`page.on("response", ...)`) to directly extract raw JSON payloads from internal flight search endpoints with safe resource disposal in `try...finally` blocks.

5. **Provider Adapter Pattern (`FlightProvider` base class)**:
   - Modular architecture decoupling flight search sources. 
   - Initial implementation: `AviataProvider` (Aviata.kz aggregator).
   - Readily extensible for `KaspiTravelProvider`, `ChocotravelProvider`, `FlyArystanProvider`, and `AirAstanaProvider`.

6. **Persistence Layer (`aiosqlite`) & Automated Migrations**:
   - Lightweight, async SQLite database engine handling transactional operations for sniping tasks, flight snapshot history, and alert deduplication.
   - Built-in schema migration in `init_db()` ensuring `interval_minutes` and `max_transfers` columns are added to existing databases seamlessly.

7. **Web Layer (`FastAPI`)**:
   - Integrated REST & health-check server (`/health`, `/api/tasks`, `/api/check-now`).

---

## 3. Database Schema

### Table: `tasks`
Stores target flight monitoring criteria configured by users.

| Column | Type | Constraints | Description |
| :--- | :--- | :--- | :--- |
| `id` | `INTEGER` | `PRIMARY KEY AUTOINCREMENT` | Unique task ID |
| `chat_id` | `INTEGER` | `NOT NULL` | Telegram user ID / Chat ID |
| `origin` | `TEXT` | `NOT NULL` | 3-letter IATA origin code (e.g. `ALA`) |
| `destination` | `TEXT` | `NOT NULL` | 3-letter IATA destination code (e.g. `NQZ`) |
| `date` | `TEXT` | `NOT NULL` | Departure date (`YYYY-MM-DD`) |
| `flight_number` | `TEXT` | `NULL` | Optional specific flight number filter (e.g. `KC-871`) |
| `target_price` | `REAL` | `NOT NULL` | Max acceptable price in KZT |
| `is_active` | `INTEGER` | `DEFAULT 1` | 1 = active monitoring, 0 = paused/completed |
| `created_at` | `TIMESTAMP` | `DEFAULT CURRENT_TIMESTAMP` | Task creation timestamp |
| `last_checked_at` | `TIMESTAMP` | `NULL` | Timestamp of last inspection |
| `last_price` | `REAL` | `NULL` | Lowest price detected in last check |
| `interval_minutes`| `INTEGER` | `DEFAULT 5` | Custom monitoring check frequency in minutes |
| `max_transfers` | `INTEGER` | `DEFAULT 0` | 0 = direct flights only, 1+ = max transfers |

```sql
CREATE TABLE IF NOT EXISTS tasks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    chat_id INTEGER NOT NULL,
    origin TEXT NOT NULL,
    destination TEXT NOT NULL,
    date TEXT NOT NULL,
    flight_number TEXT NULL,
    target_price REAL NOT NULL,
    is_active INTEGER DEFAULT 1,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_checked_at TIMESTAMP NULL,
    last_price REAL NULL,
    interval_minutes INTEGER DEFAULT 5,
    max_transfers INTEGER DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_tasks_active ON tasks (is_active);
CREATE INDEX IF NOT EXISTS idx_tasks_chat_id ON tasks (chat_id);
```

---

### Table: `alerts_history`
Maintains log of triggered alerts to prevent duplicate spam notifications.

| Column | Type | Constraints | Description |
| :--- | :--- | :--- | :--- |
| `id` | `INTEGER` | `PRIMARY KEY AUTOINCREMENT` | Unique alert ID |
| `task_id` | `INTEGER` | `NOT NULL, FK -> tasks(id)` | Associated task ID |
| `flight_number` | `TEXT` | `NOT NULL` | e.g. `KC-853`, `IQ-401`, `DV-713` |
| `price` | `REAL` | `NOT NULL` | Found price in KZT |
| `alert_time` | `TIMESTAMP` | `DEFAULT CURRENT_TIMESTAMP` | Alert dispatch timestamp |

```sql
CREATE TABLE IF NOT EXISTS alerts_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    task_id INTEGER NOT NULL,
    flight_number TEXT NOT NULL,
    price REAL NOT NULL,
    alert_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(task_id) REFERENCES tasks(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_alerts_task_id ON alerts_history (task_id);
```

---

## 4. Provider Adapter Pattern

```python
from abc import ABC, abstractmethod
from typing import List, Optional
from pydantic import BaseModel

class FlightOffer(BaseModel):
    provider: str = "aviata"
    airline: str
    flight_number: str
    origin: str
    destination: str
    departure_time: str
    arrival_time: str
    price_kzt: float
    transfers_count: int = 0
    duration_minutes: Optional[int] = None
    deep_link: Optional[str] = None

class BaseFlightProvider(ABC):
    @abstractmethod
    async def search_flights(
        self, origin: str, destination: str, date: str, max_transfers: int = 0
    ) -> List[FlightOffer]:
        """Search flights for route and date, returning standardized flight offers."""
        pass
```

---

## 5. 6-Stage Implementation Roadmap

### Stage 1: Foundation, Spec, Scaffolding & Aviata PoC Interceptor
- [x] Create project specification `kzflight_sniper_spec.md` with full architecture, database schema, and roadmap.
- [x] Establish backend folder structure: `core/`, `providers/`, `db/`, `data/`.
- [x] Configure `requirements.txt` with FastAPI, aiogram, Playwright, playwright-stealth, APScheduler, aiosqlite, Pydantic.
- [x] Create production Playwright Dockerfile (`mcr.microsoft.com/playwright/python:v1.40.0-jammy`).
- [x] Create `docker-compose.yml` with persistent volumes and environment wiring.
- [x] Create `.env.example` with standard defaults.
- [x] Implement `poc_aviata.py` standalone asynchronous Playwright interceptor with stealth and structured console output.

### Stage 2: Database Layer, Models, and Provider Adapter Architecture
- [x] Implement `backend/core/config.py` using `pydantic-settings`.
- [x] Implement `backend/db/database.py` with `aiosqlite` connection manager, schema initialization, and async context managers.
- [x] Implement `backend/db/dao.py` for task creation, retrieval, updates, status toggling, and alert recording.
- [x] Implement `backend/core/models.py` with standard Pydantic models (`FlightOffer`, `TaskCreate`, `TaskRead`, `AlertRead`).
- [x] Implement `backend/providers/base.py` defining `BaseFlightProvider`.
- [x] Implement `backend/bot/` with `aiogram 3.x` handlers (`/start`, `/help`, `/snipe`, `/list`, `/delete`, `/cancel`).
- [x] Implement `backend/main.py` with FastAPI endpoints (`/`, `/health`, `/api/tasks`) and lifespan management.
- [x] Implement `backend/tests/test_stage2.py` with 100% passing unit & integration tests.

### Stage 3: Playwright-Stealth Aviata Engine & Parser Pipeline
- [x] Implement `backend/providers/aviata_provider.py` extending `BaseFlightProvider`.
- [x] Add robust browser context management with stealth scripts and user-agent emulation.
- [x] Implement resilience against anti-bot challenges and network timeouts.
- [x] Implement `backend/engine/sniper_worker.py` monitoring engine with alert deduplication and HTML notifications.
- [x] Implement `backend/engine/scheduler.py` APScheduler integration and `POST /api/check-now` endpoint.
- [x] Add unit & integration tests (`backend/tests/test_stage3.py`) with 100% pass rate.

### Stage 4: Telegram Bot (aiogram 3.x) & Asynchronous Scheduler Engine
- [x] Implement `backend/bot/` package with aiogram 3.x routers.
- [x] Implement interactive command and parser flow for `/snipe` (Origin, Destination, Date, Target Price, Flight filter).
- [x] Implement `/list` and `/delete` / `/cancel` task management handlers.
- [x] Implement `/help` and `/start` handlers with Kazakhstan IATA airport reference.
- [x] Implement `backend/engine/scheduler.py` running periodic checks via APScheduler and dispatching alerts to Telegram users with deep links.

### Stage 5: Integration, Docker Deployment, Testing & Hardening
- [x] Implement `backend/main.py` entrypoint binding FastAPI, aiogram polling, and APScheduler lifecycle.
- [x] Refine and verify Docker container execution (`Dockerfile` and `docker-compose.yml`) with headless Playwright.
- [x] Implement health check endpoint (`/health`), REST endpoints, and structured logging.
- [x] Perform end-to-end integration test suite (`backend/tests/test_integration.py`) with 100% pass rate.

### Stage 6: NLP Evolution, Custom Intervals & Manual Test Simulator
- [x] Implement `backend/bot/nlp_parser.py` with Groq Llama 3.3 LLM integration and rule-based heuristic fallback.
- [x] Support Russian/Kazakh city names declension mapping and dynamic currency conversion (USD, EUR, RUB $\rightarrow$ KZT).
- [x] Update `backend/db/database.py` with automated migration for `interval_minutes` column in SQLite.
- [x] Update `backend/db/dao.py` with `get_due_tasks()` and custom interval persistence.
- [x] Implement natural language message handler with inline confirmation/cancellation buttons in `backend/bot/handlers.py`.
- [x] Update `backend/engine/sniper_worker.py` and `backend/engine/scheduler.py` for due tasks evaluation.
- [x] Implement standalone executable test runner and simulator `backend/tests/manual_test_simulator.py`.
- [x] Create comprehensive test suite `backend/tests/test_nlp_and_intervals.py` (35/35 passing tests).

### Дополнительный этап: UX-рефакторинг, FSM и Live Preview
- [x] FSM стейт-машина (`SniperStates.waiting_for_flight_text`) и кнопка «🎯 Создать мониторинг» в `/start`.
- [x] Защита от спама: обработка текста строго в активном FSM стейте.
- [x] Опциональная целевая цена (`target_price: Optional[float] = None`) и расширение маппинга азиатских городов (Чэнду=CTU, Пекин=PEK, Сеул=ICN, Пхукет=HKT, Гуанчжоу=CAN, Шанхай=PVG).
- [x] Live Preview: предварительный поиск реальных билетов через `AviataProvider` перед подтверждением задачи.
- [x] Автоматическая установка `target_price` по минимальной найденной на рынке цене, если пользователь не указал цену.
- [x] Защита от утечек контекстов Playwright (`try...finally` с `context.close()`).

---

## 6. UX-рефакторинг: Двухэтапный FSM и Интерактивный Live Preview

- [x] **Шаг 1: Расширенная стейт-машина (`backend/bot/handlers.py`)**: `SniperStates` (`waiting_for_search_query`, `waiting_for_interval`) с `FSMContext` (`update_data()`, `get_data()`) и типизированными `CallbackData`.
- [x] **Шаг 2: Этап 1 — Парсинг направления и Live-список рейсов**: кнопка «🎯 Создать мониторинг» в `/start`, парсинг направления и даты через Groq LLM/Heuristic, вызов `AviataProvider.search(...)` и генерация Inline-клавиатуры со списком рейсов (`[✈️ Airline Flight - Price ₸]`).
- [x] **Шаг 3: Детализация рейса и кнопка «🎯 Мониторить этот рейс»**: обработка CallbackQuery выбора конкретного рейса, вывод карточки с деталями рейса и Inline-кнопкой перехода ко 2-му этапу.
- [x] **Шаг 4: Этап 2 — Настройка интервала и Подтверждение**: переход в `waiting_for_interval`, парсинг интервала через Groq LLM, установка `target_price = flight.price_kzt`, формирование итоговой карточки с кнопками `[ ⬅️ Назад ]`, `[ ✅ Подтвердить ]`, `[ ❌ Отменить ]`.
- [x] **Шаг 5: Очистка ресурсов браузера**: проверка `AviataProvider` с надежным закрытием `context.close()` и `browser.close()` в блоках `finally`.
- [x] **Шаг 6: Комплексное тестирование и валидация**: обновление тестовых наборов `backend/tests/` для 100% покрытия нового 2-этапного FSM флоу (51/51 passing tests).


