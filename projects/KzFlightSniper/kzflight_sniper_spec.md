# KzFlightSniper — Technical Specification & Architectural Blueprint

## 1. Executive Summary & Overview

**KzFlightSniper** is an asynchronous flight tracking, price monitoring, and automated alerting engine tailored for the Kazakhstan aviation market (domestic routes like ALA $\leftrightarrow$ NQZ, CIT, SCO, GUW, UKK, etc., and international connections). It continuously tracks ticket prices through headless browser interception and stealth automation, alerting users via a feature-rich Telegram bot the instant prices drop below their configured target thresholds.

---

## 2. System Architecture

```mermaid
graph TD
    User([Telegram User]) <-->|Commands & Alerts| Bot[aiogram 3.x Bot Router]
    Bot <--> DB[(aiosqlite Database)]
    Scheduler[APScheduler AsyncIOScheduler] -->|Triggers Periodic Checks| Worker[Sniper Worker Engine]
    Worker -->|Fetch Active Tasks| DB
    Worker -->|Execute Search| ProviderAdapter[Provider Adapter Interface]
    ProviderAdapter -->|Interception & Scrape| AviataProvider[Aviata.kz Provider (Playwright + Stealth)]
    AviataProvider -->|JSON Intercept & Parse| Worker
    Worker -->|Price <= Target| AlertDispatcher[Alert Dispatcher]
    AlertDispatcher -->|Push Notification| Bot
    AlertDispatcher -->|Record Alert Log| DB
    FastAPI[FastAPI Service] -->|Health & Webhook API| Bot
```

### Core Architecture Components:

1. **Telegram Bot Interface (`aiogram 3.x`)**:
   - Asynchronous Telegram bot utilizing Routers, FSM (Finite State Machine) for multi-step task creation (`/new_snipe`), inline keyboard management, task deletion/toggle (`/my_snipes`), and system stats (`/stats`).

2. **Asynchronous Task Scheduler (`APScheduler`)**:
   - `AsyncIOScheduler` scheduling concurrent or rate-limited flight checks on user-defined intervals (e.g. default 300s / 5m), distributing scraping requests with randomized jitter to prevent rate-limiting.

3. **Browser Engine & Traffic Interception (`Playwright` + `playwright-stealth`)**:
   - Headless Chromium controlled via Playwright with stealth configurations (patching `navigator.webdriver`, webgl vendor, chrome runtime, randomized user agents, realistic viewport).
   - Network response interception (`page.on("response", ...)`) to directly extract raw JSON payloads from internal flight search endpoints rather than fragile DOM scraping.

4. **Provider Adapter Pattern (`FlightProvider` base class)**:
   - Modular architecture decoupling flight search sources. 
   - Initial implementation: `AviataProvider` (Aviata.kz aggregator).
   - Readily extensible for `KaspiTravelProvider`, `ChocotravelProvider`, `FlyArystanProvider`, and `AirAstanaProvider`.

5. **Persistence Layer (`aiosqlite`)**:
   - Lightweight, async SQLite database engine handling transactional operations for sniping tasks, flight snapshot history, and alert deduplication.

6. **Web Layer (`FastAPI`)**:
   - Optional lightweight REST & health-check server (`/health`, `/metrics`, and optional Telegram webhook mode).

---

## 3. Database Schema

### Table: `tasks`
Stores target flight monitoring criteria configured by users.

| Column | Type | Constraints | Description |
| :--- | :--- | :--- | :--- |
| `id` | `INTEGER` | `PRIMARY KEY AUTOINCREMENT` | Unique task ID |
| `user_id` | `INTEGER` | `NOT NULL` | Telegram user ID / Chat ID |
| `origin` | `TEXT` | `NOT NULL` | 3-letter IATA origin code (e.g. `ALA`) |
| `destination` | `TEXT` | `NOT NULL` | 3-letter IATA destination code (e.g. `NQZ`) |
| `date` | `TEXT` | `NOT NULL` | Departure date (`YYYY-MM-DD`) |
| `target_price` | `REAL` | `NOT NULL` | Max acceptable price in KZT |
| `max_transfers` | `INTEGER` | `DEFAULT 0` | 0 = direct flights only, 1+ = max transfers |
| `is_active` | `INTEGER` | `DEFAULT 1` | 1 = active monitoring, 0 = paused/completed |
| `created_at` | `TIMESTAMP` | `DEFAULT CURRENT_TIMESTAMP` | Task creation timestamp |
| `last_checked_at` | `TIMESTAMP` | `NULL` | Timestamp of last inspection |
| `last_price` | `REAL` | `NULL` | Lowest price detected in last check |

```sql
CREATE TABLE IF NOT EXISTS tasks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    origin TEXT NOT NULL,
    destination TEXT NOT NULL,
    date TEXT NOT NULL,
    target_price REAL NOT NULL,
    max_transfers INTEGER DEFAULT 0,
    is_active INTEGER DEFAULT 1,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_checked_at TIMESTAMP,
    last_price REAL
);

CREATE INDEX IF NOT EXISTS idx_tasks_active ON tasks (is_active);
CREATE INDEX IF NOT EXISTS idx_tasks_user ON tasks (user_id);
```

---

### Table: `alerts_history`
Maintains log of triggered alerts to prevent duplicate spam notifications.

| Column | Type | Constraints | Description |
| :--- | :--- | :--- | :--- |
| `id` | `INTEGER` | `PRIMARY KEY AUTOINCREMENT` | Unique alert ID |
| `task_id` | `INTEGER` | `NOT NULL, FK -> tasks(id)` | Associated task ID |
| `user_id` | `INTEGER` | `NOT NULL` | Telegram user ID |
| `flight_number` | `TEXT` | `NOT NULL` | e.g. `KC-853`, `IQ-401`, `DV-713` |
| `airline` | `TEXT` | `NOT NULL` | e.g. `Air Astana`, `FlyArystan`, `Qazaq Air`, `SCAT` |
| `departure_time` | `TEXT` | `NOT NULL` | ISO or formatted departure string |
| `arrival_time` | `TEXT` | `NOT NULL` | ISO or formatted arrival string |
| `price` | `REAL` | `NOT NULL` | Found price in KZT |
| `deep_link` | `TEXT` | `NULL` | Direct booking URL |
| `sent_at` | `TIMESTAMP` | `DEFAULT CURRENT_TIMESTAMP` | Alert dispatch timestamp |

```sql
CREATE TABLE IF NOT EXISTS alerts_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    task_id INTEGER NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    user_id INTEGER NOT NULL,
    flight_number TEXT NOT NULL,
    airline TEXT NOT NULL,
    departure_time TEXT NOT NULL,
    arrival_time TEXT NOT NULL,
    price REAL NOT NULL,
    deep_link TEXT,
    sent_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_alerts_task ON alerts_history (task_id);
CREATE INDEX IF NOT EXISTS idx_alerts_user ON alerts_history (user_id);
```

---

## 4. Provider Adapter Pattern

```python
from abc import ABC, abstractmethod
from typing import List
from pydantic import BaseModel

class FlightOffer(BaseModel):
    provider: str
    airline: str
    flight_number: str
    origin: str
    destination: str
    departure_time: str
    arrival_time: str
    duration_minutes: int
    transfers_count: int
    price_kzt: float
    deep_link: str

class BaseFlightProvider(ABC):
    @abstractmethod
    async def search_flights(
        self, origin: str, destination: str, date: str, max_transfers: int = 0
    ) -> List[FlightOffer]:
        """Search flights for route and date, returning standardized flight offers."""
        pass
```

---

## 5. 5-Stage Implementation Roadmap

### Stage 1: Foundation, Spec, Scaffolding & Aviata PoC Interceptor
- [x] Create project specification `kzflight_sniper_spec.md` with full architecture, database schema, and roadmap.
- [x] Establish backend folder structure: `core/`, `providers/`, `db/`, `data/`.
- [x] Configure `requirements.txt` with FastAPI, aiogram, Playwright, playwright-stealth, APScheduler, aiosqlite, Pydantic.
- [x] Create production Playwright Dockerfile (`mcr.microsoft.com/playwright/python:v1.40.0-jammy`).
- [x] Create `docker-compose.yml` with persistent volumes and environment wiring.
- [x] Create `.env.example` with standard defaults.
- [x] Create robust `sync.sh` script.
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
- [ ] Implement `backend/providers/aviata.py` extending `BaseFlightProvider`.
- [ ] Add robust browser pool / context management with stealth and custom user-agent rotation.
- [ ] Implement resilience against anti-bot challenges and network timeouts.
- [ ] Add unit & integration tests for Aviata response parsing and edge cases (0 flights, sold out, direct vs multi-segment).

### Stage 4: Telegram Bot (aiogram 3.x) & Asynchronous Scheduler Engine
- [ ] Implement `backend/bot/` package with aiogram 3.x routers.
- [ ] Implement interactive FSM flow for `/new_snipe` (Origin $\rightarrow$ Destination $\rightarrow$ Date $\rightarrow$ Target Price $\rightarrow$ Direct/Transfer).
- [ ] Implement `/my_snipes` management inline keyboards (pause, resume, delete).
- [ ] Implement `/help`, `/start`, and `/status` handlers.
- [ ] Implement `backend/core/scheduler.py` running periodic checks via APScheduler and dispatching alerts to Telegram users with deep links.

### Stage 5: Integration, Docker Deployment, Testing & Hardening
- [ ] Implement `backend/main.py` entrypoint binding FastAPI, aiogram polling/webhook, and APScheduler lifecycle.
- [ ] Build and verify Docker container execution with headless Playwright.
- [ ] Implement health check endpoint (`/health`) and structured logging.
- [ ] Perform end-to-end integration test with live route monitoring and alert delivery verification.
