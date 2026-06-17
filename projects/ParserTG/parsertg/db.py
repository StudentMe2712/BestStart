"""SQLite persistence. Single-user, low volume -> stdlib sqlite3 with one short-lived
connection per call (no pool, no server). Two tables: channels (with the parse cursor
last_message_id) and messages (channel, date, text, link)."""
from __future__ import annotations

import sqlite3
from contextlib import closing
from dataclasses import dataclass
from pathlib import Path

SCHEMA = """
CREATE TABLE IF NOT EXISTS channels (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    username        TEXT    NOT NULL UNIQUE,
    title           TEXT,
    last_message_id INTEGER NOT NULL DEFAULT 0,
    added_at        TEXT    NOT NULL DEFAULT (datetime('now'))
);
CREATE TABLE IF NOT EXISTS messages (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    channel_id  INTEGER NOT NULL REFERENCES channels(id) ON DELETE CASCADE,
    message_id  INTEGER NOT NULL,
    date        TEXT    NOT NULL,
    text        TEXT    NOT NULL DEFAULT '',
    link        TEXT    NOT NULL DEFAULT '',
    saved_at    TEXT    NOT NULL DEFAULT (datetime('now')),
    UNIQUE(channel_id, message_id)
);
"""


@dataclass(frozen=True)
class Stats:
    channel_count: int
    message_count: int
    last: dict | None  # most recently saved message, or None: {channel, message_id, date, link}


def connect(db_path: str) -> sqlite3.Connection:
    Path(db_path).expanduser().parent.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA foreign_keys = ON")
    return conn


def init_db(db_path: str) -> None:
    with closing(connect(db_path)) as conn:
        conn.executescript(SCHEMA)
        conn.commit()


def sync_channels(db_path: str, usernames: list[str]) -> None:
    """Make sure every configured channel exists. Existing rows (and their cursor) are kept."""
    with closing(connect(db_path)) as conn:
        for username in usernames:
            conn.execute(
                "INSERT INTO channels(username) VALUES(?) ON CONFLICT(username) DO NOTHING",
                (username,),
            )
        conn.commit()


def get_channels(db_path: str) -> list[sqlite3.Row]:
    with closing(connect(db_path)) as conn:
        return conn.execute(
            "SELECT id, username, title, last_message_id FROM channels ORDER BY id"
        ).fetchall()


def update_channel(
    db_path: str,
    channel_id: int,
    *,
    title: str | None = None,
    last_message_id: int | None = None,
) -> None:
    sets: list[str] = []
    params: list[object] = []
    if title is not None:
        sets.append("title = ?")
        params.append(title)
    if last_message_id is not None:
        sets.append("last_message_id = ?")
        params.append(last_message_id)
    if not sets:
        return
    params.append(channel_id)
    with closing(connect(db_path)) as conn:
        conn.execute(f"UPDATE channels SET {', '.join(sets)} WHERE id = ?", params)
        conn.commit()


def save_messages(db_path: str, channel_id: int, rows: list[tuple]) -> tuple[int, int]:
    """Insert rows of (message_id, date, text, link). Duplicates are ignored.

    Returns (number actually inserted, highest message_id seen)."""
    saved = 0
    max_id = 0
    with closing(connect(db_path)) as conn:
        for message_id, date, text, link in rows:
            cur = conn.execute(
                "INSERT OR IGNORE INTO messages(channel_id, message_id, date, text, link) "
                "VALUES(?, ?, ?, ?, ?)",
                (channel_id, message_id, date, text, link),
            )
            if cur.rowcount:
                saved += 1
            max_id = max(max_id, message_id)
        conn.commit()
    return saved, max_id


def get_recent_messages(db_path: str, limit: int) -> list[sqlite3.Row]:
    with closing(connect(db_path)) as conn:
        return conn.execute(
            "SELECT m.message_id, m.date, m.text, m.link, "
            "       COALESCE(c.title, c.username) AS channel "
            "FROM messages m JOIN channels c ON c.id = m.channel_id "
            "ORDER BY m.id DESC LIMIT ?",
            (limit,),
        ).fetchall()


def get_stats(db_path: str) -> Stats:
    with closing(connect(db_path)) as conn:
        channel_count = conn.execute("SELECT COUNT(*) FROM channels").fetchone()[0]
        message_count = conn.execute("SELECT COUNT(*) FROM messages").fetchone()[0]
        last_row = conn.execute(
            "SELECT m.message_id, m.date, m.link, "
            "       COALESCE(c.title, c.username) AS channel "
            "FROM messages m JOIN channels c ON c.id = m.channel_id "
            "ORDER BY m.id DESC LIMIT 1"
        ).fetchone()
    return Stats(channel_count, message_count, dict(last_row) if last_row else None)
