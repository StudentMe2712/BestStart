# Architecture Decision Records (ADRs)

This directory documents key architectural decisions made throughout the lifecycle of **KzFlightSniper**.

## Index of Architectural Decisions

| ADR ID | Title | Status | Date | Decision Summary |
| :--- | :--- | :--- | :--- | :--- |
| **ADR-001** | Asynchronous SQLite Persistence with `aiosqlite` | Accepted | 2026-08-20 | Adopted lightweight async SQLite with transactional DAO layer and automated migrations. |
| **ADR-002** | Provider Adapter Pattern for Multi-Source Scalability | Accepted | 2026-08-24 | Established `BaseFlightProvider` abstract base class to decouple flight search engines. |
| **ADR-003** | Hybrid NLP Flight Intent Parser (Groq LLM + Local Heuristics) | Accepted | 2026-08-28 | Combined Groq Llama 3.1 LLM extraction with offline regex heuristic fallback parser. |
| **[ADR-004](0004-transition-from-playwright-to-aviasales-httpx.md)** | Transition from Aviata UI Scraping (Playwright) to Aviasales API First (HTTPX) | **Accepted** | 2026-09-02 | Replaced Playwright headless Chromium scraping with Travelpayouts v3 REST API via `httpx`. |

---

## ADR Guidelines

- Decisions are captured according to the Spec-Kit and Michael Nygard lightweight ADR standards.
- New ADRs must be numbered sequentially (`0005-...`, etc.) and indexed here.
- Status values: `proposed` | `accepted` | `deprecated` | `superseded by ADR-NNNN`.
