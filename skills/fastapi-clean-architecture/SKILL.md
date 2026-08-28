---
name: fastapi-clean-architecture
description: Clean architecture and design standards for developing robust, typed FastAPI web services and endpoints.
---

# FastAPI Clean Architecture Standards

This skill defines the architectural guidelines and best practices for developing FastAPI backends in analytical and data-driven applications.

## Key Principles

1. **Layered Separation of Concerns:**
   - `api/`: REST routing, query/path parameters, request validation, HTTP status codes.
   - `core/`: Application settings, environment configuration, constants.
   - `db/`: Database session/connection lifecycle, schema definitions, migrations, DAO (Data Access Object) queries.
   - `models/`: Pydantic schemas for request validation, data serialization, and response types.
   - `services/`: Business logic, orchestrators, external API integrations, background pipelines.
   - `workers/`: Background scheduling (APScheduler), crons, asynchronous workers.

2. **Asynchronous Execution:**
   - Always declare route handlers as `async def` unless wrapping purely synchronous blocking operations.
   - For blocking operations, delegate to worker threads via `run_in_threadpool` or background tasks.

3. **Pydantic v2 Best Practices:**
   - Use `ConfigDict(from_attributes=True)` instead of deprecated class-based configs.
   - Validate and coerce types cleanly; use `field_validator(..., mode="before")` when normalizing external unstructured inputs.

4. **Global Exception Handling & Resilience:**
   - Catch known exceptions and raise structured `HTTPException` with informative error details.
   - Implement lifecycle hooks using `@asynccontextmanager` with FastAPI `lifespan`.

5. **API Documentation:**
   - Provide summary, tags, and response models on all routers for automatic OpenAPI / Swagger documentation.
