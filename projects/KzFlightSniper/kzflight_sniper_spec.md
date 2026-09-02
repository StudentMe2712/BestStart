# KzFlightSniper — Technical Specification & Architectural Blueprint

**Version**: 2.0.0 | **Standard**: Spec-Kit Architecture Blueprint | **Updated**: 2026-09-02  
**Core Ingestion**: Aviasales / Travelpayouts v3 Flight Data REST API (`httpx`)  

---

## 1. Executive Summary & Overview

**KzFlightSniper** is an asynchronous flight tracking, price monitoring, and automated alerting engine tailored for the **Kazakhstan aviation market** (domestic routes like `ALA` $\leftrightarrow$ `NQZ`, `CIT`, `SCO`, `GUW`, `UKK`, `AKX`, `KSG`, and international connections such as `BKK`, `DXB`, `IST`, `HKT`, `TAS`, `FRU`, `TBS`, `AYT`).

Following **ADR-004**, the engine operates on an **API-first architecture**, querying the **Travelpayouts Aviasales v3 Flight Data REST API** via asynchronous `httpx` connection pools. It delivers sub-second response times (<300–500ms), 100% immunity against Cloudflare/Turnstile anti-bot blocking, and near-zero memory footprint (<60MB). When prices drop below user-defined target thresholds, rich HTML notifications with direct deep booking links are dispatched instantly to Telegram users.

---

## 2. System Architecture

```mermaid
graph TD
    User([Telegram User]) <-->|Natural Text, Commands & Callbacks| Bot[aiogram 3.x Bot Router & FSM]
    Bot -->|NLP Intent Parsing| Parser[NLP Parser (Groq LLM / Local Heuristic)]
    Parser -->|ParsedFlightIntent| Bot
    Bot <--> DB[(aiosqlite SQLite Database)]
    
    Scheduler[APScheduler AsyncIOScheduler (60s Tick)] -->|Triggers Due Checks| Worker[Sniper Worker Engine]
    Worker -->|Fetch Due Tasks| DB
    Worker -->|Execute Batched Route Search| ProviderAdapter[Provider Adapter Interface]
    ProviderAdapter -->|Async REST Client (httpx)| AviasalesAPI[Travelpayouts Aviasales v3 API]
    AviasalesAPI -->|Raw JSON Flight Payloads| AviasalesProvider[AviasalesProvider Adapter]
    AviasalesProvider -->|Normalized List of FlightOffer| Worker
    
    Worker -->|Check 60m Alert Window| Dedup[Alert Deduplication Engine]
    Dedup -->|Price <= Target & New Alert| AlertDispatcher[Alert Dispatcher]
    AlertDispatcher -->|Push HTML Alert Notification| Bot
    AlertDispatcher -->|Record Logged Alert| DB
    FastAPI[FastAPI Service] -->|Health & Webhook API| Bot
```

### Core Architecture Components:

1. **Telegram Bot Interface (`aiogram 3.x`)**:
   - Asynchronous Telegram bot with natural language processing text handler and commands (`/start`, `/help`, `/snipe`, `/list`, `/delete`, `/cancel`).
   - Two-stage FSM interactive wizard (`SniperStates.waiting_for_search_query` $\rightarrow$ `waiting_for_interval`) with live flight cards and custom interval selection.

2. **NLP Intent Parsing Engine (`Groq LLM` + Heuristic Fallback)**:
   - Structured JSON flight extraction powered by Groq Llama 3.1 (`llama-3.1-70b-versatile`).
   - Resilient zero-dependency rule-based heuristic parser with full Kazakhstan and international city name declensions, relative date resolution ("завтра", "через неделю", "15 октября"), currency conversion (USD, EUR, RUB to KZT), and custom interval detection.

3. **Asynchronous Task Scheduler (`APScheduler`) & Custom Intervals**:
   - `AsyncIOScheduler` executing checks at a configurable tick (default 60s), evaluating only tasks that are due based on their individual `interval_minutes` setting (e.g. 5m, 10m, 30m, 60m).

4. **API-First Ingestion Engine (`AviasalesProvider` + `httpx`)**:
   - Pure asynchronous HTTP client (`httpx.AsyncClient`) querying Travelpayouts Aviasales v3 endpoints (`/aviasales/v3/prices_for_dates`) with token authorization.
   - High-throughput response normalization pipeline resolving IATA airline codes, calculating flight durations and arrival times, and formatting localized deep booking links.
   - Zero browser overhead and complete resilience against Cloudflare Turnstile captchas.

5. **Provider Adapter Pattern (`BaseFlightProvider` base class)**:
   - Modular interface decoupling flight data ingestion from business logic.
   - Active implementation: `AviasalesProvider` (Travelpayouts v3 API).
   - Deprecated legacy implementation: `aviata_provider.py.deprecated` (Playwright browser scraper).
   - Extensible for `KaspiTravelProvider`, `FlyArystanProvider`, and `AirAstanaProvider`.

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
Maintains log of triggered alerts to prevent duplicate spam notifications within a 60-minute window.

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

## 4. Provider Adapter Specification

### 4.1 Interface Contract (`BaseFlightProvider`)

```python
from abc import ABC, abstractmethod
from typing import List, Optional
from pydantic import BaseModel

class FlightOffer(BaseModel):
    provider: str = "aviasales"
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
    @property
    @abstractmethod
    def provider_name(self) -> str:
        """Return provider identifier name."""
        pass

    @abstractmethod
    async def search_flights(
        self, origin: str, destination: str, date: str, max_transfers: int = 0
    ) -> List[FlightOffer]:
        """Search flights for route and date, returning standardized flight offers."""
        pass

    async def search(
        self,
        origin: str,
        destination: str,
        date: str,
        direct_only: bool = True,
        flight_number: Optional[str] = None,
        max_transfers: Optional[int] = None,
    ) -> List[FlightOffer]:
        """Convenience search method with multi-parameter filtering."""
        pass
```

### 4.2 Aviasales REST Implementation (`AviasalesProvider`)

- **Class**: `backend.providers.aviasales_provider.AviasalesProvider`
- **Primary Endpoint**: `https://api.travelpayouts.com/aviasales/v3/prices_for_dates`
- **Authentication**: Token configured via `TRAVELPAYOUTS_TOKEN` / `AVIASALES_TOKEN` env variables.
- **Normalization Pipeline**:
  1. `parse_aviasales_json(raw_data, origin, destination, base_url)` parses JSON array or dictionary payloads.
  2. Resolves airline IATA codes (`KC` $\rightarrow$ Air Astana, `FS` $\rightarrow$ FlyArystan, `DV` $\rightarrow$ SCAT, `IQ` $\rightarrow$ Qazaq Air, `TK` $\rightarrow$ Turkish Airlines, `FZ` $\rightarrow$ Flydubai, `EK` $\rightarrow$ Emirates, `PC` $\rightarrow$ Pegasus, `HY` $\rightarrow$ Uzbekistan Airways).
  3. Formats flight codes (`FS-7051`, `KC-853`).
  4. Parses departure/arrival timestamps and computes arrival times when durations are provided.
  5. Formats Aviasales booking deep links (`https://www.aviasales.kz/search/ALANQZ15101`).

---

## 5. Implementation Roadmap & Milestones

### Stage 1: Foundation, Spec, Scaffolding & PoC Interceptor
- [x] Create project specification `kzflight_sniper_spec.md` with architecture, database schema, and roadmap.
- [x] Establish backend folder structure: `core/`, `providers/`, `db/`, `engine/`, `bot/`, `data/`.
- [x] Configure `requirements.txt` with FastAPI, aiogram, APScheduler, aiosqlite, httpx, Pydantic.
- [x] Create Dockerfile and `docker-compose.yml` with persistent volume mounting.
- [x] Create `.env.example` with configuration template.

### Stage 2: Database Layer, Models, and Provider Adapter Architecture
- [x] Implement `backend/core/config.py` using `pydantic-settings`.
- [x] Implement `backend/db/database.py` with `aiosqlite` connection manager, schema initialization, and async context managers.
- [x] Implement `backend/db/dao.py` for task creation, retrieval, updates, status toggling, and alert recording.
- [x] Implement `backend/core/models.py` with standard Pydantic models (`FlightOffer`, `TaskCreate`, `TaskRead`, `AlertRead`).
- [x] Implement `backend/providers/base.py` defining `BaseFlightProvider`.
- [x] Implement `backend/main.py` with FastAPI endpoints (`/`, `/health`, `/api/tasks`) and lifespan management.

### Stage 3: SniperWorker Engine & Periodic Scheduler
- [x] Implement `backend/engine/sniper_worker.py` monitoring engine with alert deduplication and HTML notifications.
- [x] Implement `backend/engine/scheduler.py` APScheduler integration and `POST /api/check-now` endpoint.
- [x] Add unit & integration tests (`backend/tests/test_stage3.py`).

### Stage 4: Telegram Bot (aiogram 3.x) & Command Handlers
- [x] Implement `backend/bot/` package with aiogram 3.x routers.
- [x] Implement `/snipe`, `/list`, `/delete`, `/cancel`, `/help`, and `/start` handlers with Kazakhstan IATA airport reference.
- [x] Dispatch alerts to Telegram users with deep booking links.

### Stage 5: NLP Evolution, Custom Intervals & Simulator
- [x] Implement `backend/bot/nlp_parser.py` with Groq Llama 3.1 LLM integration and rule-based heuristic fallback.
- [x] Support Russian/Kazakh city names declension mapping and dynamic currency conversion (USD, EUR, RUB $\rightarrow$ KZT).
- [x] Update `backend/db/database.py` with automated migration for `interval_minutes` column in SQLite.
- [x] Update `backend/db/dao.py` with `get_due_tasks()` and custom interval persistence.
- [x] Implement standalone executable test runner and simulator `backend/tests/manual_test_simulator.py`.

### Stage 6: UX-Refactoring, 2-Stage FSM Wizard & Live Preview
- [x] 2-stage FSM state machine (`SniperStates.waiting_for_search_query` $\rightarrow$ `waiting_for_interval`).
- [x] Live flight search results preview with interactive inline selection buttons (`[✈️ Airline Flight - Price ₸]`).
- [x] Quick preset interval selection (`[5 мин]`, `[10 мин]`, `[30 мин]`, `[1 час]`) and custom natural text parsing.
- [x] Auto-setting target price to lowest current market price when unspecified by user.

### Stage 7: API-First Architecture Pivot & Aviasales v3 Integration (Current)
- [x] Implement `backend/providers/aviasales_provider.py` interfacing with Travelpayouts Aviasales v3 REST API via async `httpx`.
- [x] Deprecate `aviata_provider.py` to `aviata_provider.py.deprecated`.
- [x] Establish backward compatibility aliases across provider package (`AviataProvider = AviasalesProvider`).
- [x] Document ADR-004 in `specs/adr/` and update master architectural blueprint.
- [x] Eliminate Playwright runtime overhead and achieve sub-500ms query latency.

---

## 6. Architecture Decision Records (ADRs)

### ADR-004: Transition from Aviata UI Scraping (Playwright) to Aviasales API First (HTTPX)

- **Date**: 2026-09-02
- **Status**: **Accepted**
- **Deciders**: Core Architecture Team, Backend Engineering, Reliability Specialist
- **Context & Problem Statement**:  
  Aviata.kz deployed aggressive Cloudflare "Under Attack" Mode, Turnstile captchas, and dynamic JavaScript challenges that blocked headless browser automation (causing 40–70% block rates and timeouts) while consuming 600MB–1.2GB of RAM per instance.
- **Decision**:  
  Transition from Playwright-based UI automation to an API-first integration using Aviasales (Travelpayouts v3 Flight Data API: `https://api.travelpayouts.com/aviasales/v3/prices_for_dates`) via asynchronous `httpx.AsyncClient`.
- **Consequences**:
  1. *Zero Playwright Overhead*: Eliminates headless Chromium dependencies; container runtime memory dropped from >600MB to <60MB.
  2. *Sub-Second Latency*: Search query latency reduced from 8–15s to **<300–500ms**.
  3. *100% Cloudflare Immunity*: Official REST API token authentication bypasses all anti-bot browser challenges.
  4. *Clean Deprecation*: Preserved `aviata_provider.py.deprecated` while establishing `AviasalesProvider` as the primary engine.
  5. *Batching Efficiency*: SniperWorker groups tasks by route tuples `(origin, dest, date)` to minimize outbound HTTP calls.
