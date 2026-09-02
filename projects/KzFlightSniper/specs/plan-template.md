# Implementation Plan: [FEATURE / COMPONENT PIVOT]

**Branch**: `[###-feature-name]` | **Date**: [YYYY-MM-DD] | **Spec**: [specs/design.md](design.md)

**Input**: Feature specification from `/specs/[###-feature-name]/spec.md` or architectural decision record from `/specs/adr/`.

---

## 1. Summary

[Extract from architectural specification: primary requirement + technical approach + target provider interface]

---

## 2. Technical Context

**Language/Version**: Python 3.10+  
**Primary Frameworks**: FastAPI 0.110+, aiogram 3.4+, httpx 0.27+, APScheduler 3.10+, Pydantic 2.x  
**Storage**: aiosqlite (SQLite async WAL mode)  
**Testing**: pytest 7.4+, pytest-asyncio, unittest, TestClient  
**Target Platform**: Linux / Windows Server / Docker  
**Project Type**: Asynchronous Background Flight Price Monitoring Engine & Telegram Bot  
**Performance Goals**: <500ms p95 provider search latency, <100MB container memory, 60s scheduler tick  
**Constraints**: Zero Playwright/browser runtime, Cloudflare-immune REST API, reliable error handling and deduplication  

---

## 3. Constitution & Quality Gates Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **Gate 1: Async Non-Blocking**: All I/O operations (HTTP requests, DB queries, Telegram pushes) MUST use `async`/`await`.
- [x] **Gate 2: Provider Abstraction**: Providers MUST subclass `BaseFlightProvider` and return standardized `List[FlightOffer]`.
- [x] **Gate 3: Deduplication & Spam Protection**: Alerts MUST pass through 60-minute window deduplication check in SQLite.
- [x] **Gate 4: Schema Integrity**: Database alterations MUST include automated forward migrations in `init_db()`.
- [x] **Gate 5: Comprehensive Testing**: Core logic MUST have corresponding passing unit/integration tests with mocks.

---

## 4. Project Structure & Documentation

```text
projects/KzFlightSniper/
├── specs/
│   ├── design.md                  # Comprehensive technical specification & architecture
│   ├── plan-template.md           # This implementation plan template
│   └── adr/
│       ├── README.md              # ADR index and lineage
│       └── 0004-transition-from-playwright-to-aviasales-httpx.md
├── backend/
│   ├── bot/                       # aiogram 3.x routers, FSM states, NLP parser
│   ├── core/                      # Config, settings, Pydantic data models
│   ├── db/                        # Database connection, DAO layer, migrations
│   ├── engine/                    # Scheduler (APScheduler), SniperWorker engine
│   ├── providers/                 # Base provider, AviasalesProvider (HTTPX)
│   ├── tests/                     # Unit and integration test suites
│   └── main.py                    # Application entrypoint & FastAPI service
├── kzflight_sniper_spec.md        # Master technical specification
├── README.md                      # Project documentation & quickstart
└── docker-compose.yml             # Containerized deployment manifest
```

---

## 5. Phase-by-Phase Execution Workflow

### Phase 0: Research & Provider Contract Definition
- Analyze upstream API specifications, rate limits, currency formats, and parameters.
- Verify payload variations and edge cases (missing fields, nested objects, direct arrays).
- Document findings in `specs/design.md` or ADR.

### Phase 1: Provider Implementation & Normalization
- Implement provider class inheriting `BaseFlightProvider`.
- Build resilient JSON response parser mapping airline IATA codes and computing duration.
- Implement deep booking URL generator.
- Write unit tests against mocked API payloads.

### Phase 2: Engine & Bot Integration
- Wire provider into `SniperWorker` and dependency injection.
- Update bot live search handlers and 2-stage FSM workflow.
- Ensure route-based batch grouping minimizes outbound HTTP calls.

### Phase 3: Verification & Regression Testing
- Execute full test suite (`pytest backend/tests/ -v`).
- Run interactive manual test simulator (`python backend/tests/manual_test_simulator.py`).
- Validate container build and memory footprints.

---

## 6. Complexity Tracking & Trade-off Record

| Decision / Trade-off | Why Needed | Rejected Alternative & Rationale |
| :--- | :--- | :--- |
| **API-First HTTPX over Playwright** | Overcome aggressive Cloudflare blocking and reduce 1.2GB RAM consumption to <60MB. | Browser stealth scraping: Unstable, high latency (12s), 50% block rate. |
| **Batch Route Grouping** | Prevents redundant API requests when multiple users monitor identical routes. | Independent task execution: Multiplied API traffic and triggered rate limits. |
| **Hybrid NLP Parsing** | Combines Llama 3.1 contextual accuracy with offline regex speed and 100% uptime. | Pure LLM: Latency and external API dependency failure points. |
