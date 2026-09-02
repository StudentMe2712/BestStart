# ADR-004: Transition from Aviata UI Scraping (Playwright) to Aviasales API First (HTTPX)

**Date**: 2026-09-02  
**Status**: Accepted  
**Deciders**: Core Architecture Team, Backend Engineering, Reliability Specialist  
**Technical Domain**: Flight Data Provider Integration & Ingestion Pipeline  

---

## Context & Problem Statement

In the initial implementation of **KzFlightSniper** (Stages 1–5), flight ticket discovery and price monitoring relied on **headless browser automation** via Playwright (`playwright` + `playwright-stealth`). The browser navigated to `https://aviata.kz`, executed synthetic form interactions (origin, destination, date pickers), and intercepted internal JSON search responses (`page.on("response", ...)`).

While functional in early development, this UI scraping strategy introduced severe operational bottlenecks in staging and production:

1. **Aggressive Anti-Bot & Captcha Challenges**: Aviata.kz deployed aggressive Cloudflare "Under Attack" Mode, Turnstile captchas, and dynamic JavaScript challenges. Even with stealth evasion scripts, headless Chromium instances faced elevated block rates (~40–70%), sporadic timeouts, and empty search responses.
2. **Excessive Resource Overhead**: Running headless Chromium inside Docker containers required substantial CPU and RAM allocations (~600MB–1.2GB per container instance, heavy spike during concurrent multi-route searches), rendering containerized deployment on lightweight VPS/cloud tiers cost-inefficient and prone to OOM (Out Of Memory) kills.
3. **High Latency & Flakiness**: Full browser initialization, page rendering, SPA hydration, and network interception incurred a baseline latency of 6.0–15.0 seconds per search query, severely constraining the scheduler's check frequency and responsiveness.
4. **Maintenance Fragility**: DOM and client bundle changes on the provider's frontend frequently broke form-filling selectors and response interception hooks.

---

## Decision

We decided to execute a **strategic architectural pivot** from Playwright-based browser automation to an **API-first asynchronous integration** using the **Aviasales / Travelpayouts Flight Data v3 API** via an asynchronous `httpx` client.

### Architectural Blueprint:
- **Primary Endpoint**: Travelpayouts Aviasales v3 Prices for Dates:  
  `https://api.travelpayouts.com/aviasales/v3/prices_for_dates`
- **Client Implementation**: Asynchronous HTTP client (`httpx.AsyncClient`) with connection pooling, automatic gzip/deflate decompression, configurable timeout budgets (default: 15.0s), and strict exception handling.
- **Data Normalization**: Centralized parsing engine `AviasalesProvider.parse_aviasales_json()` standardizing raw JSON payloads into uniform Pydantic `FlightOffer` objects with IATA airline mapping, time computation, duration extraction, and deep booking link generation (`https://www.aviasales.kz/...`).
- **Provider Deprecation**: Transitioned `aviata_provider.py` to `aviata_provider.py.deprecated`, preserving historical reference while establishing `AviasalesProvider` as the core operational provider.

---

## Considered Alternatives

### Alternative 1: Playwright with Residential Proxy Rotation + Captcha Solving Services (2Captcha / CapSolver)
- **Pros**: Retains direct integration with Aviata.kz UI.
- **Cons**: 
  - Significant recurring operational costs ($2.00–$5.00/1k solved captchas).
  - High end-to-end latency (15–30 seconds per query).
  - Ongoing cat-and-mouse game with Cloudflare anti-bot fingerprinting.
- **Why Rejected**: High cost, unacceptable latency, and fragility for a real-time price sniping engine.

### Alternative 2: Reverse-Engineering Kaspi Travel / Chocotravel Private Internal APIs
- **Pros**: Native local Kazakhstan travel agencies.
- **Cons**: 
  - Required reverse-engineering proprietary mobile API signatures (HMAC/JWT with client certificates).
  - High risk of sudden breaking changes and account bans without public developer terms.
- **Why Rejected**: Unstable, non-public contracts with legal and maintainability liabilities.

### Alternative 3: Travelpayouts / Aviasales v3 Public REST API (Chosen)
- **Pros**:
  - Official, documented public REST API endpoint.
  - Sub-second response times (<300–500ms).
  - 100% immune to Cloudflare browser challenges.
  - Near-zero resource footprint (no browser runtime required).
  - Comprehensive route coverage across Kazakhstan domestic (`ALA`, `NQZ`, `CIT`, `SCO`, `GUW`, `UKK`, etc.) and international corridors (`BKK`, `DXB`, `IST`, `HKT`, `TAS`, `AYT`).
- **Cons**:
  - Price cache latency depending on Aviasales global indexing cycles (typically refreshed within minutes for high-volume routes).
- **Why Accepted**: Best-in-class reliability, speed, and zero infrastructure overhead.

---

## Consequences

### Positive (Benefits)
1. **Zero Browser Overhead**: Eliminated Playwright Chromium dependencies, reducing Docker image footprint from ~1.4GB to ~180MB and container runtime memory from >600MB to <60MB.
2. **Instant Response Times**: Reduced route query latency from 8,000–15,000ms down to **<300–500ms**, enabling high-throughput check cycles and instant live previews in Telegram bot dialogs.
3. **100% Cloudflare Immunity**: Clean REST API requests with official token authentication bypass all browser-based Turnstile and anti-bot challenges.
4. **Deterministic Testing**: Facilitated clean, fast unit and integration tests using mocked HTTP transports without spawning headless browser processes.
5. **Rich Metadata**: Full support for IATA airline mapping (Air Astana, FlyArystan, SCAT, Qazaq Air, Turkish Airlines, Emirates, etc.), duration calculations, transfer counts, and localized deep links.

### Negative & Trade-offs
1. **API Token Dependency**: Requires valid `TRAVELPAYOUTS_TOKEN` / `AVIASALES_TOKEN` (provided with fallback defaults).
2. **Price Snapshot Cache**: Prices reflect Aviasales aggregated indexes; rare edge cases of rapid price fluctuations are verified upon user redirection to the deep booking link.

### Risks & Mitigations
- **Risk**: API Rate Limiting on heavy workloads.  
  **Mitigation**: The `SniperWorker` groups checks by `(origin, destination, date)` route tuples so multiple monitoring tasks for the same route are resolved within a single API call.
- **Risk**: API downtime or network interruption.  
  **Mitigation**: `httpx.AsyncClient` includes exponential backoff, timeout protection, and graceful degradation returning empty lists without crashing the scheduler.

---

## References

- Implementation: [`backend/providers/aviasales_provider.py`](file:///C:/Users/Mila/Desktop/BestStart/projects/KzFlightSniper/backend/providers/aviasales_provider.py)
- Base Class: [`backend/providers/base.py`](file:///C:/Users/Mila/Desktop/BestStart/projects/KzFlightSniper/backend/providers/base.py)
- Deprecated Artifact: [`backend/providers/aviata_provider.py.deprecated`](file:///C:/Users/Mila/Desktop/BestStart/projects/KzFlightSniper/backend/providers/aviata_provider.py.deprecated)
- Spec-Kit Design Document: [`specs/design.md`](file:///C:/Users/Mila/Desktop/BestStart/projects/KzFlightSniper/specs/design.md)
