"""SQLite persistence (single-user companion → SQLite keeps it dead simple).

Four tables mirror the spec's data model (§15): users (profile + settings),
runtime_state (mutable counters), role_state (preference weights), messages (log).
One connection is reused; everything runs in a single asyncio thread so that is safe.
"""
from __future__ import annotations

import os
import sqlite3
from datetime import datetime
from typing import Any

from . import quality
from .config import Settings
from .roles import ROLES

_conn: sqlite3.Connection | None = None
_owner_id: int = 0

SCHEMA = """
CREATE TABLE IF NOT EXISTS users (
    user_id INTEGER PRIMARY KEY,
    timezone TEXT NOT NULL,
    paused INTEGER NOT NULL DEFAULT 0,
    quiet_start INTEGER NOT NULL,
    quiet_end INTEGER NOT NULL,
    max_per_day INTEGER NOT NULL,
    min_gap_minutes INTEGER NOT NULL,
    created_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS runtime_state (
    user_id INTEGER PRIMARY KEY,
    last_sent_at TEXT,
    sent_today INTEGER NOT NULL DEFAULT 0,
    today_date TEXT,
    ignore_streak INTEGER NOT NULL DEFAULT 0,
    silence_until TEXT,
    last_role TEXT
);
CREATE TABLE IF NOT EXISTS role_state (
    user_id INTEGER NOT NULL,
    role TEXT NOT NULL,
    enabled INTEGER NOT NULL DEFAULT 1,
    weight REAL NOT NULL DEFAULT 1.0,
    PRIMARY KEY (user_id, role)
);
CREATE TABLE IF NOT EXISTS messages (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    ts TEXT NOT NULL,
    role TEXT NOT NULL,
    source TEXT NOT NULL,
    content TEXT NOT NULL,
    tg_message_id INTEGER,
    reaction TEXT,
    reply TEXT,
    replied_at TEXT
);
CREATE TABLE IF NOT EXISTS facts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    text TEXT NOT NULL,
    created_at TEXT NOT NULL
);
"""


def _db() -> sqlite3.Connection:
    if _conn is None:
        raise RuntimeError("db.init() must be called first")
    return _conn


def init(db_path: str) -> None:
    global _conn
    parent = os.path.dirname(db_path)
    if parent:
        os.makedirs(parent, exist_ok=True)
    _conn = sqlite3.connect(db_path, check_same_thread=False)
    _conn.row_factory = sqlite3.Row
    _conn.executescript(SCHEMA)
    _conn.commit()


def close() -> None:
    global _conn
    if _conn is not None:
        _conn.close()
        _conn = None


def ensure_user(settings: Settings, now_iso: str) -> None:
    """Create the owner profile, runtime row and role weights if they don't exist yet."""
    global _owner_id
    _owner_id = settings.owner_id
    conn = _db()
    conn.execute(
        """INSERT OR IGNORE INTO users
           (user_id, timezone, paused, quiet_start, quiet_end, max_per_day, min_gap_minutes, created_at)
           VALUES (?, ?, 0, ?, ?, ?, ?, ?)""",
        (settings.owner_id, settings.timezone, settings.quiet_start, settings.quiet_end,
         settings.max_per_day, settings.min_gap_minutes, now_iso),
    )
    conn.execute("INSERT OR IGNORE INTO runtime_state (user_id) VALUES (?)", (settings.owner_id,))
    for key in ROLES:
        enabled = 1 if key in settings.enabled_roles else 0
        conn.execute(
            "INSERT OR IGNORE INTO role_state (user_id, role, enabled, weight) VALUES (?, ?, ?, ?)",
            (settings.owner_id, key, enabled, ROLES[key].default_weight),
        )
    # Drop weights for roles that no longer exist (e.g. removed inspirer/observer/...),
    # so get_roles() never hands the scheduler a key that isn't in ROLES.
    if ROLES:
        placeholders = ",".join("?" for _ in ROLES)
        conn.execute(
            f"DELETE FROM role_state WHERE user_id = ? AND role NOT IN ({placeholders})",
            (settings.owner_id, *ROLES.keys()),
        )
    conn.commit()


# --- users -----------------------------------------------------------------

def get_user() -> sqlite3.Row:
    row = _db().execute("SELECT * FROM users WHERE user_id = ?", (_owner_id,)).fetchone()
    if row is None:
        raise RuntimeError("owner user not initialised")
    return row


_USER_COLUMNS = frozenset(
    {"timezone", "paused", "quiet_start", "quiet_end", "max_per_day", "min_gap_minutes"}
)


def update_user(**fields: Any) -> None:
    if not fields:
        return
    unknown = set(fields) - _USER_COLUMNS
    if unknown:
        raise ValueError(f"unknown user columns: {unknown}")
    cols = ", ".join(f"{k} = ?" for k in fields)
    _db().execute(f"UPDATE users SET {cols} WHERE user_id = ?", (*fields.values(), _owner_id))
    _db().commit()


# --- runtime ---------------------------------------------------------------

def get_runtime() -> sqlite3.Row:
    row = _db().execute("SELECT * FROM runtime_state WHERE user_id = ?", (_owner_id,)).fetchone()
    if row is None:
        raise RuntimeError("runtime state not initialised")
    return row


_RUNTIME_COLUMNS = frozenset(
    {"last_sent_at", "sent_today", "today_date", "ignore_streak", "silence_until", "last_role"}
)


def update_runtime(**fields: Any) -> None:
    if not fields:
        return
    unknown = set(fields) - _RUNTIME_COLUMNS
    if unknown:
        raise ValueError(f"unknown runtime columns: {unknown}")
    cols = ", ".join(f"{k} = ?" for k in fields)
    _db().execute(f"UPDATE runtime_state SET {cols} WHERE user_id = ?", (*fields.values(), _owner_id))
    _db().commit()


# --- roles -----------------------------------------------------------------

def get_roles() -> dict[str, dict[str, float]]:
    rows = _db().execute(
        "SELECT role, enabled, weight FROM role_state WHERE user_id = ?", (_owner_id,)
    ).fetchall()
    return {r["role"]: {"enabled": bool(r["enabled"]), "weight": float(r["weight"])} for r in rows}


def set_role_enabled(role: str, enabled: bool) -> None:
    _db().execute(
        "UPDATE role_state SET enabled = ? WHERE user_id = ? AND role = ?",
        (1 if enabled else 0, _owner_id, role),
    )
    _db().commit()


def adjust_weight(role: str, factor: float, floor: float = 0.2, cap: float = 3.0) -> None:
    row = _db().execute(
        "SELECT weight FROM role_state WHERE user_id = ? AND role = ?", (_owner_id, role)
    ).fetchone()
    if row is None:
        return
    new_weight = max(floor, min(cap, float(row["weight"]) * factor))
    _db().execute(
        "UPDATE role_state SET weight = ? WHERE user_id = ? AND role = ?",
        (new_weight, _owner_id, role),
    )
    _db().commit()


# --- messages --------------------------------------------------------------

def log_message(ts_iso: str, role: str, source: str, content: str, tg_message_id: int | None) -> int:
    cur = _db().execute(
        """INSERT INTO messages (user_id, ts, role, source, content, tg_message_id)
           VALUES (?, ?, ?, ?, ?, ?)""",
        (_owner_id, ts_iso, role, source, content, tg_message_id),
    )
    _db().commit()
    return int(cur.lastrowid)


def recent_contents(limit: int = 8) -> list[str]:
    rows = _db().execute(
        "SELECT content FROM messages WHERE user_id = ? ORDER BY id DESC LIMIT ?", (_owner_id, limit)
    ).fetchall()
    return [r["content"] for r in rows]


def last_message() -> sqlite3.Row | None:
    return _db().execute(
        "SELECT * FROM messages WHERE user_id = ? ORDER BY id DESC LIMIT 1", (_owner_id,)
    ).fetchone()


def minutes_since_role(role: str, now_utc: datetime) -> float | None:
    """Minutes since Echo last sent in `role`, or None if it never has (per-role cooldown)."""
    row = _db().execute(
        "SELECT MAX(ts) AS m FROM messages WHERE user_id = ? AND role = ?", (_owner_id, role)
    ).fetchone()
    if row is None or row["m"] is None:
        return None
    try:
        last = datetime.fromisoformat(row["m"])
    except ValueError:
        return None
    return (now_utc - last).total_seconds() / 60


def trailing_unanswered(limit: int = 12) -> int:
    """Count the most recent consecutive messages that got neither a reply nor a reaction."""
    rows = _db().execute(
        "SELECT reaction, reply FROM messages WHERE user_id = ? ORDER BY id DESC LIMIT ?",
        (_owner_id, limit),
    ).fetchall()
    streak = 0
    for r in rows:
        if r["reaction"] is None and r["reply"] is None:
            streak += 1
        else:
            break
    return streak


def message_by_tg(tg_message_id: int) -> sqlite3.Row | None:
    return _db().execute(
        "SELECT * FROM messages WHERE user_id = ? AND tg_message_id = ? ORDER BY id DESC LIMIT 1",
        (_owner_id, tg_message_id),
    ).fetchone()


def set_reaction(message_id: int, reaction: str) -> None:
    _db().execute("UPDATE messages SET reaction = ? WHERE id = ?", (reaction, message_id))
    _db().commit()


def set_reply(message_id: int, reply: str, replied_at: str) -> None:
    _db().execute(
        "UPDATE messages SET reply = ?, replied_at = ? WHERE id = ?", (reply, replied_at, message_id)
    )
    _db().commit()


def stats() -> dict[str, Any]:
    conn = _db()
    total = conn.execute("SELECT COUNT(*) c FROM messages WHERE user_id = ?", (_owner_id,)).fetchone()["c"]
    replied = conn.execute(
        "SELECT COUNT(*) c FROM messages WHERE user_id = ? AND reply IS NOT NULL", (_owner_id,)
    ).fetchone()["c"]
    liked = conn.execute(
        "SELECT COUNT(*) c FROM messages WHERE user_id = ? AND reaction = 'like'", (_owner_id,)
    ).fetchone()["c"]
    return {"total": total, "replied": replied, "liked": liked}


# --- facts (life-memory the Friend register references) --------------------

# Keep at most this many auto-learned facts; the oldest are pruned so the store can't grow
# without bound for a long-running single user.
MAX_FACTS = 40


def add_fact(text: str, created_at: str) -> int | None:
    """Store a life-fact, skipping blanks and near-duplicates. Returns the new id or None."""
    cleaned = text.strip()
    if not cleaned:
        return None
    if quality.is_too_similar(cleaned, fact_texts()):
        return None
    cur = _db().execute(
        "INSERT INTO facts (user_id, text, created_at) VALUES (?, ?, ?)",
        (_owner_id, cleaned, created_at),
    )
    _db().execute(
        """DELETE FROM facts WHERE user_id = ? AND id NOT IN (
               SELECT id FROM facts WHERE user_id = ? ORDER BY id DESC LIMIT ?
           )""",
        (_owner_id, _owner_id, MAX_FACTS),
    )
    _db().commit()
    return int(cur.lastrowid)


def list_facts() -> list[sqlite3.Row]:
    return _db().execute(
        "SELECT id, text FROM facts WHERE user_id = ? ORDER BY id", (_owner_id,)
    ).fetchall()


def delete_fact(fact_id: int) -> bool:
    cur = _db().execute("DELETE FROM facts WHERE user_id = ? AND id = ?", (_owner_id, fact_id))
    _db().commit()
    return cur.rowcount > 0


def fact_texts(limit: int | None = None) -> list[str]:
    """Stored facts, most recent first (for dedupe and for injecting into the Friend prompt)."""
    sql = "SELECT text FROM facts WHERE user_id = ? ORDER BY id DESC"
    params: list[Any] = [_owner_id]
    if limit is not None:
        sql += " LIMIT ?"
        params.append(limit)
    return [r["text"] for r in _db().execute(sql, params).fetchall()]
