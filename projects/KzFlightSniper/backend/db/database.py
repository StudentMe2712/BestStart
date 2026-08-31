"""Async SQLite database connection manager and schema initialization."""

import os
from contextlib import asynccontextmanager
from typing import AsyncGenerator, Optional
import aiosqlite

from backend.core.config import get_settings


SCHEMA_SQL = """
PRAGMA foreign_keys = ON;

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
    last_price REAL NULL
);

CREATE TABLE IF NOT EXISTS alerts_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    task_id INTEGER NOT NULL,
    flight_number TEXT NOT NULL,
    price REAL NOT NULL,
    alert_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(task_id) REFERENCES tasks(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_tasks_active ON tasks (is_active);
CREATE INDEX IF NOT EXISTS idx_tasks_chat_id ON tasks (chat_id);
CREATE INDEX IF NOT EXISTS idx_alerts_task_id ON alerts_history (task_id);
"""


def _resolve_db_path(db_path: Optional[str] = None) -> str:
    """Resolve database path, ensuring parent directory exists."""
    path = db_path or get_settings().DATABASE_PATH
    abs_path = os.path.abspath(path)
    parent_dir = os.path.dirname(abs_path)
    if parent_dir and not os.path.exists(parent_dir):
        os.makedirs(parent_dir, exist_ok=True)
    return abs_path


@asynccontextmanager
async def get_db(db_path: Optional[str] = None) -> AsyncGenerator[aiosqlite.Connection, None]:
    """Async context manager providing an aiosqlite database connection."""
    resolved_path = _resolve_db_path(db_path)
    async with aiosqlite.connect(resolved_path) as conn:
        conn.row_factory = aiosqlite.Row
        await conn.execute("PRAGMA foreign_keys = ON;")
        yield conn


async def init_db(db_path: Optional[str] = None) -> None:
    """Initialize database schema, tables, and indices."""
    async with get_db(db_path) as conn:
        await conn.executescript(SCHEMA_SQL)
        await conn.commit()
