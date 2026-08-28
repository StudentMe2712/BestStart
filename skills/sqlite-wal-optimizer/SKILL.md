---
name: sqlite-wal-optimizer
description: Best practices and configuration rules for high-concurrency, crash-resilient SQLite in Python async applications.
---

# SQLite WAL Optimizer & Concurrency Best Practices

This skill outlines mandatory SQLite configurations and locking avoidance strategies for multi-threaded and asynchronous Python services.

## Mandatory PRAGMA Directives

Every SQLite connection must execute:

```sql
PRAGMA journal_mode = WAL;          -- Enables Write-Ahead Logging for concurrent readers & writer
PRAGMA foreign_keys = ON;           -- Enforces referential integrity and ON DELETE CASCADE
PRAGMA busy_timeout = 5000;         -- 5-second wait before raising "database is locked" error
PRAGMA synchronous = NORMAL;        -- High performance while maintaining WAL safety
```

## Implementation Rules

1. **Context Managed Connections:**
   Always manage SQLite connections via context managers that handle `commit()` on success and `rollback()` on exceptions:
   ```python
   @contextmanager
   def get_db_connection():
       conn = sqlite3.connect(get_db_path(), timeout=10.0)
       conn.row_factory = sqlite3.Row
       conn.execute("PRAGMA foreign_keys = ON;")
       conn.execute("PRAGMA busy_timeout = 5000;")
       try:
           yield conn
           conn.commit()
       except Exception:
           conn.rollback()
           raise
       finally:
           conn.close()
   ```

2. **Deduplication Strategy:**
   - Store normalized SHA-256 hashes (`content_hash`) with `UNIQUE` index.
   - Use `TrendsDAO.get_existing_hashes()` to batch check existing content prior to ingestion, saving database writes and LLM token costs.

3. **Index Optimization:**
   - Index all foreign keys (`source_id`), search status flags (`is_reviewed`, `is_trend`), and timestamp columns (`parsed_date`).
