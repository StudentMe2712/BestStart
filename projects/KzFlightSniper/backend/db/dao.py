"""Data Access Object (DAO) for flight sniping tasks and alert history."""

from contextlib import asynccontextmanager
from datetime import datetime, timezone
from typing import Any, AsyncGenerator, Dict, List, Optional
import aiosqlite

from backend.db.database import get_db


class FlightSniperDAO:
    """Data Access Object providing transactional operations on tasks and alerts."""

    def __init__(self, db_path: Optional[str] = None) -> None:
        """Initialize DAO with an optional database file path override."""
        self.db_path = db_path

    def _get_connection(self) -> Any:
        """Obtain an async database connection context manager."""
        return get_db(self.db_path)

    async def add_task(
        self,
        chat_id: int,
        origin: str,
        destination: str,
        date: str,
        target_price: float,
        flight_number: Optional[str] = None,
    ) -> int:
        """Insert a new sniping task and return its generated ID.

        Args:
            chat_id: Telegram chat ID / user ID.
            origin: 3-letter IATA origin code (e.g. 'ALA').
            destination: 3-letter IATA destination code (e.g. 'NQZ').
            date: Flight date in YYYY-MM-DD format.
            target_price: Maximum acceptable price in KZT.
            flight_number: Optional specific flight number filter (e.g. 'KC-853').

        Returns:
            The integer primary key ID of the inserted task.
        """
        clean_origin = origin.strip().upper()
        clean_dest = destination.strip().upper()
        clean_flight = flight_number.strip().upper() if flight_number and flight_number.strip() else None

        async with self._get_connection() as conn:
            cursor = await conn.execute(
                """
                INSERT INTO tasks (
                    chat_id, origin, destination, date, flight_number, target_price, is_active
                ) VALUES (?, ?, ?, ?, ?, ?, 1)
                """,
                (chat_id, clean_origin, clean_dest, date.strip(), clean_flight, float(target_price)),
            )
            await conn.commit()
            return int(cursor.lastrowid)

    async def get_active_tasks(self) -> List[Dict[str, Any]]:
        """Fetch all active monitoring tasks (is_active = 1).

        Returns:
            List of task dictionaries.
        """
        async with self._get_connection() as conn:
            cursor = await conn.execute(
                """
                SELECT id, chat_id, origin, destination, date, flight_number,
                       target_price, is_active, created_at, last_checked_at, last_price
                FROM tasks
                WHERE is_active = 1
                ORDER BY created_at ASC
                """
            )
            rows = await cursor.fetchall()
            return [dict(row) for row in rows]

    async def get_user_tasks(self, chat_id: int, active_only: bool = True) -> List[Dict[str, Any]]:
        """Fetch tasks belonging to a specific Telegram user.

        Args:
            chat_id: Telegram chat ID / user ID.
            active_only: If True, returns only active tasks (is_active = 1).

        Returns:
            List of user task dictionaries ordered by creation ID descending.
        """
        query = """
            SELECT id, chat_id, origin, destination, date, flight_number,
                   target_price, is_active, created_at, last_checked_at, last_price
            FROM tasks
            WHERE chat_id = ?
        """
        params: List[Any] = [chat_id]
        if active_only:
            query += " AND is_active = 1"
        query += " ORDER BY id DESC"

        async with self._get_connection() as conn:
            cursor = await conn.execute(query, params)
            rows = await cursor.fetchall()
            return [dict(row) for row in rows]

    async def get_task_by_id(self, task_id: int) -> Optional[Dict[str, Any]]:
        """Fetch a single task by its unique ID.

        Args:
            task_id: Task primary key.

        Returns:
            Task dictionary if found, else None.
        """
        async with self._get_connection() as conn:
            cursor = await conn.execute(
                """
                SELECT id, chat_id, origin, destination, date, flight_number,
                       target_price, is_active, created_at, last_checked_at, last_price
                FROM tasks
                WHERE id = ?
                """,
                (task_id,),
            )
            row = await cursor.fetchone()
            return dict(row) if row else None

    async def delete_task(self, task_id: int, chat_id: int) -> bool:
        """Delete a task owned by the specified chat_id.

        Args:
            task_id: Task primary key ID to delete.
            chat_id: Telegram chat ID of the owner.

        Returns:
            True if task was found and deleted, False otherwise.
        """
        async with self._get_connection() as conn:
            cursor = await conn.execute(
                "DELETE FROM tasks WHERE id = ? AND chat_id = ?",
                (task_id, chat_id),
            )
            await conn.commit()
            return cursor.rowcount > 0

    async def update_task_last_check(
        self,
        task_id: int,
        last_price: Optional[float] = None,
    ) -> bool:
        """Update last_checked_at timestamp and optional last_price for a task.

        Args:
            task_id: Task primary key ID.
            last_price: Lowest observed price in KZT during the check.

        Returns:
            True if updated successfully, False if task was not found.
        """
        async with self._get_connection() as conn:
            cursor = await conn.execute(
                """
                UPDATE tasks
                SET last_checked_at = CURRENT_TIMESTAMP,
                    last_price = COALESCE(?, last_price)
                WHERE id = ?
                """,
                (last_price, task_id),
            )
            await conn.commit()
            return cursor.rowcount > 0

    async def log_alert(
        self,
        task_id: int,
        flight_number: str,
        price: float,
        alert_time: Optional[str] = None,
    ) -> int:
        """Record a triggered price alert entry in alerts_history.

        Args:
            task_id: Associated task ID.
            flight_number: Flight code triggered (e.g. 'KC-853').
            price: Found ticket price in KZT.
            alert_time: Optional explicit timestamp string ('YYYY-MM-DD HH:MM:SS').

        Returns:
            The integer primary key ID of the inserted alert log.
        """
        clean_flight = flight_number.strip().upper()
        async with self._get_connection() as conn:
            if alert_time:
                # Normalize ISO 'T' to space for consistent SQLite datetime queries
                formatted_time = alert_time.replace("T", " ").split("+")[0].split("Z")[0].strip()
                cursor = await conn.execute(
                    """
                    INSERT INTO alerts_history (task_id, flight_number, price, alert_time)
                    VALUES (?, ?, ?, ?)
                    """,
                    (task_id, clean_flight, float(price), formatted_time),
                )
            else:
                cursor = await conn.execute(
                    """
                    INSERT INTO alerts_history (task_id, flight_number, price)
                    VALUES (?, ?, ?)
                    """,
                    (task_id, clean_flight, float(price)),
                )
            await conn.commit()
            return int(cursor.lastrowid)

    async def check_recent_alert(
        self,
        task_id: int,
        flight_number: str,
        price: float,
        window_minutes: int = 60,
    ) -> bool:
        """Return True if an alert for this task + flight_number + price was sent within window_minutes.

        Prevents duplicate alert spam notifications when price has not decreased further.

        Args:
            task_id: Target task ID.
            flight_number: Flight number to check.
            price: Current observed price.
            window_minutes: Suppression window in minutes (default: 60).

        Returns:
            True if matching alert was recorded within the window, False otherwise.
        """
        clean_flight = flight_number.strip().upper()
        async with self._get_connection() as conn:
            cursor = await conn.execute(
                """
                SELECT id FROM alerts_history
                WHERE task_id = ?
                  AND UPPER(flight_number) = ?
                  AND price <= ?
                  AND alert_time >= datetime('now', '-' || ? || ' minutes')
                LIMIT 1
                """,
                (task_id, clean_flight, float(price), int(window_minutes)),
            )
            row = await cursor.fetchone()
            return row is not None

    async def get_active_tasks_count(self) -> int:
        """Return the count of currently active monitoring tasks."""
        async with self._get_connection() as conn:
            cursor = await conn.execute("SELECT COUNT(*) AS count FROM tasks WHERE is_active = 1")
            row = await cursor.fetchone()
            return int(row["count"]) if row else 0
