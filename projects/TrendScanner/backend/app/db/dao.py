"""Data Access Object (DAO) for Sources and Trends SQLite operations."""

import hashlib
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional, Set
from app.db.database import get_db_connection


def calculate_text_hash(text: str) -> str:
    """Compute SHA-256 hash of normalized text for deduplication."""
    normalized = " ".join(text.strip().lower().split())
    return hashlib.sha256(normalized.encode("utf-8")).hexdigest()


class SourcesDAO:
    """DAO for interacting with the `sources` table."""

    @staticmethod
    def get_all(active_only: bool = False) -> List[Dict[str, Any]]:
        """Retrieve all sources, optionally filtering active only."""
        with get_db_connection() as conn:
            if active_only:
                cursor = conn.execute(
                    "SELECT * FROM sources WHERE is_active = 1 ORDER BY id ASC"
                )
            else:
                cursor = conn.execute("SELECT * FROM sources ORDER BY id ASC")
            return [dict(row) for row in cursor.fetchall()]

    @staticmethod
    def get_by_id(source_id: int) -> Optional[Dict[str, Any]]:
        """Get source by its ID."""
        with get_db_connection() as conn:
            cursor = conn.execute("SELECT * FROM sources WHERE id = ?", (source_id,))
            row = cursor.fetchone()
            return dict(row) if row else None

    @staticmethod
    def get_by_url(url: str) -> Optional[Dict[str, Any]]:
        """Get source by its URL."""
        with get_db_connection() as conn:
            cursor = conn.execute("SELECT * FROM sources WHERE url = ?", (url,))
            row = cursor.fetchone()
            return dict(row) if row else None

    @staticmethod
    def exists_by_url(url: str) -> bool:
        """Check if source with matching URL exists."""
        with get_db_connection() as conn:
            cursor = conn.execute("SELECT 1 FROM sources WHERE url = ? LIMIT 1", (url,))
            return cursor.fetchone() is not None

    @staticmethod
    def create(name: str, url: str, source_type: str, is_active: bool = True) -> int:
        """Create a new source record and return its ID."""
        with get_db_connection() as conn:
            cursor = conn.execute(
                """
                INSERT INTO sources (name, url, source_type, is_active)
                VALUES (?, ?, ?, ?)
                """,
                (name, url, source_type, 1 if is_active else 0),
            )
            return cursor.lastrowid

    @staticmethod
    def update(
        source_id: int,
        name: Optional[str] = None,
        url: Optional[str] = None,
        source_type: Optional[str] = None,
        is_active: Optional[bool] = None,
    ) -> bool:
        """Update existing source fields."""
        fields: List[str] = []
        params: List[Any] = []

        if name is not None:
            fields.append("name = ?")
            params.append(name)
        if url is not None:
            fields.append("url = ?")
            params.append(url)
        if source_type is not None:
            fields.append("source_type = ?")
            params.append(source_type)
        if is_active is not None:
            fields.append("is_active = ?")
            params.append(1 if is_active else 0)

        if not fields:
            return False

        params.append(source_id)
        query = f"UPDATE sources SET {', '.join(fields)} WHERE id = ?"
        with get_db_connection() as conn:
            cursor = conn.execute(query, params)
            return cursor.rowcount > 0

    @staticmethod
    def update_last_scanned(
        source_id: int, last_scanned: Optional[datetime] = None
    ) -> bool:
        """Update last_scanned timestamp for a source."""
        target_dt = last_scanned or datetime.now(timezone.utc)
        if target_dt.tzinfo is not None:
            target_dt = target_dt.astimezone(timezone.utc).replace(tzinfo=None)
        ts = target_dt.strftime("%Y-%m-%d %H:%M:%S")
        with get_db_connection() as conn:
            cursor = conn.execute(
                "UPDATE sources SET last_scanned = ? WHERE id = ?",
                (ts, source_id),
            )
            return cursor.rowcount > 0

    @staticmethod
    def get_last_scan_time() -> Optional[str]:
        """Retrieve the most recent last_scanned timestamp across sources."""
        with get_db_connection() as conn:
            cursor = conn.execute("SELECT MAX(last_scanned) as max_scanned FROM sources WHERE last_scanned IS NOT NULL")
            row = cursor.fetchone()
            return str(row["max_scanned"]) if row and row["max_scanned"] else None

    @staticmethod
    def delete(source_id: int) -> bool:
        """Delete source by ID."""
        with get_db_connection() as conn:
            cursor = conn.execute("DELETE FROM sources WHERE id = ?", (source_id,))
            return cursor.rowcount > 0


class TrendsDAO:
    """DAO for interacting with the `trends` table."""

    @staticmethod
    def create(
        source_id: int,
        original_text: str,
        content_hash: Optional[str] = None,
        is_trend: bool = False,
        trend_name: Optional[str] = None,
        ai_score: Optional[int] = None,
        scam_probability: Optional[int] = None,
        ai_summary: Optional[str] = None,
        source_url: Optional[str] = None,
        is_reviewed: bool = False,
        ai_status: str = "pending",
        mention_count: int = 1,
        is_liked: bool = False,
        is_new: bool = True,
    ) -> int:
        """Insert a single trend record and return its ID."""
        h = content_hash or calculate_text_hash(original_text)
        with get_db_connection() as conn:
            cursor = conn.execute(
                """
                INSERT INTO trends (
                    source_id, original_text, content_hash, is_trend,
                    trend_name, ai_score, scam_probability, ai_summary,
                    source_url, is_reviewed, ai_status, mention_count, is_liked, is_new
                )
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    source_id,
                    original_text,
                    h,
                    1 if is_trend else 0,
                    trend_name,
                    ai_score,
                    scam_probability,
                    ai_summary,
                    source_url,
                    1 if is_reviewed else 0,
                    ai_status,
                    max(1, mention_count),
                    1 if is_liked else 0,
                    1 if is_new else 0,
                ),
            )
            return cursor.lastrowid

    @staticmethod
    def create_batch(trends_data: List[Dict[str, Any]]) -> int:
        """Batch insert trends, ignoring duplicate content hashes."""
        if not trends_data:
            return 0

        inserted_count = 0
        with get_db_connection() as conn:
            for item in trends_data:
                original_text = item["original_text"]
                content_hash = item.get("content_hash") or calculate_text_hash(
                    original_text
                )
                ai_status = item.get("ai_status", "pending")
                mention_count = item.get("mention_count", 1)
                is_liked = item.get("is_liked", False)
                is_new = item.get("is_new", True)
                try:
                    conn.execute(
                        """
                        INSERT INTO trends (
                            source_id, original_text, content_hash, is_trend,
                            trend_name, ai_score, scam_probability, ai_summary,
                            source_url, is_reviewed, ai_status, mention_count, is_liked, is_new
                        )
                        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                        """,
                        (
                            item["source_id"],
                            original_text,
                            content_hash,
                            1 if item.get("is_trend") else 0,
                            item.get("trend_name"),
                            item.get("ai_score"),
                            item.get("scam_probability"),
                            item.get("ai_summary"),
                            item.get("source_url"),
                            1 if item.get("is_reviewed") else 0,
                            ai_status,
                            mention_count,
                            1 if is_liked else 0,
                            1 if is_new else 0,
                        ),
                    )
                    inserted_count += 1
                except Exception:
                    # Ignore unique constraint violations on content_hash
                    continue
        return inserted_count

    @staticmethod
    def archive_previous_inbox() -> int:
        """Archive unliked items from previous inbox runs by setting is_new = 0."""
        with get_db_connection() as conn:
            cursor = conn.execute(
                "UPDATE trends SET is_new = 0 WHERE is_new = 1 AND is_liked = 0"
            )
            return cursor.rowcount

    @staticmethod
    def increment_mention(
        trend_id: int,
        additional_text: Optional[str] = None,
        source_name: Optional[str] = None,
    ) -> bool:
        """Increment mention_count and optionally enrich original_text with context from new source."""
        with get_db_connection() as conn:
            cursor = conn.execute("SELECT original_text FROM trends WHERE id = ?", (trend_id,))
            row = cursor.fetchone()
            if not row:
                return False

            existing_text = row["original_text"]
            if additional_text and source_name:
                enriched_text = f"{existing_text}\n\n[Дополнительное упоминание ({source_name})]:\n{additional_text}"
                cursor = conn.execute(
                    """
                    UPDATE trends
                    SET mention_count = mention_count + 1,
                        original_text = ?
                    WHERE id = ?
                    """,
                    (enriched_text, trend_id),
                )
            else:
                cursor = conn.execute(
                    "UPDATE trends SET mention_count = mention_count + 1 WHERE id = ?",
                    (trend_id,),
                )
            return cursor.rowcount > 0

    @staticmethod
    def get_recent_candidates(limit: int = 150) -> List[Dict[str, Any]]:
        """Retrieve recent trends for similarity and fuzzy matching."""
        with get_db_connection() as conn:
            cursor = conn.execute(
                """
                SELECT id, trend_name, original_text, source_url, content_hash, mention_count, ai_status, is_liked, is_new
                FROM trends
                ORDER BY parsed_date DESC, id DESC
                LIMIT ?
                """,
                (limit,),
            )
            return [dict(row) for row in cursor.fetchall()]

    @staticmethod
    def get_pending_trends(limit: int = 3) -> List[Dict[str, Any]]:
        """Retrieve items in the queue awaiting Groq AI classification, prioritizing viral multi-mentions."""
        with get_db_connection() as conn:
            cursor = conn.execute(
                """
                SELECT t.*, s.name as source_name, s.source_type
                FROM trends t
                LEFT JOIN sources s ON t.source_id = s.id
                WHERE t.ai_status = 'pending'
                ORDER BY t.mention_count DESC, t.id ASC
                LIMIT ?
                """,
                (limit,),
            )
            return [dict(row) for row in cursor.fetchall()]

    @staticmethod
    def count_pending() -> int:
        """Count items currently queued for AI classification."""
        with get_db_connection() as conn:
            cursor = conn.execute("SELECT COUNT(*) as cnt FROM trends WHERE ai_status = 'pending'")
            row = cursor.fetchone()
            return row["cnt"] if row else 0

    @staticmethod
    def update_ai_classification(
        trend_id: int,
        is_trend: bool,
        trend_name: Optional[str],
        ai_score: Optional[int],
        scam_probability: Optional[int],
        ai_summary: Optional[str],
        ai_status: str = "processed",
    ) -> bool:
        """Update AI classification fields and mark status as processed/failed."""
        with get_db_connection() as conn:
            cursor = conn.execute(
                """
                UPDATE trends
                SET is_trend = ?,
                    trend_name = ?,
                    ai_score = ?,
                    scam_probability = ?,
                    ai_summary = ?,
                    ai_status = ?
                WHERE id = ?
                """,
                (
                    1 if is_trend else 0,
                    trend_name,
                    ai_score,
                    scam_probability,
                    ai_summary,
                    ai_status,
                    trend_id,
                ),
            )
            return cursor.rowcount > 0

    @staticmethod
    def get_by_id(trend_id: int) -> Optional[Dict[str, Any]]:
        """Get trend by ID with source details joined."""
        with get_db_connection() as conn:
            cursor = conn.execute(
                """
                SELECT t.*, s.name as source_name, s.source_type
                FROM trends t
                LEFT JOIN sources s ON t.source_id = s.id
                WHERE t.id = ?
                """,
                (trend_id,),
            )
            row = cursor.fetchone()
            return dict(row) if row else None

    @staticmethod
    def get_trends(
        skip: int = 0,
        limit: int = 50,
        min_score: Optional[int] = None,
        max_scam: Optional[int] = None,
        status: Optional[str] = None,
        source_id: Optional[int] = None,
        only_trends: Optional[bool] = None,
        tab: Optional[str] = None,
        is_liked: Optional[bool] = None,
        is_new: Optional[bool] = None,
        search_query: Optional[str] = None,
    ) -> List[Dict[str, Any]]:
        """
        Query trends with filters and pagination.
        Tab: 'inbox' (is_new=1, is_liked=0, default), 'liked' (is_liked=1), 'database'/'history'/'archive' (is_new=0), or 'all'.
        Status: 'new' (is_reviewed=0), 'reviewed' (is_reviewed=1), or None (all).
        """
        conditions: List[str] = []
        params: List[Any] = []

        if min_score is not None:
            conditions.append("t.ai_score >= ?")
            params.append(min_score)

        if max_scam is not None:
            conditions.append("t.scam_probability <= ?")
            params.append(max_scam)

        if status == "new":
            conditions.append("t.is_reviewed = 0")
        elif status == "reviewed":
            conditions.append("t.is_reviewed = 1")

        if source_id is not None:
            conditions.append("t.source_id = ?")
            params.append(source_id)

        if only_trends is True:
            conditions.append("t.is_trend = 1")
        elif only_trends is False:
            conditions.append("t.is_trend = 0")

        # Tab / Inbox Zero / Trend Database filtering
        if tab == "inbox":
            conditions.append("t.is_new = 1 AND t.is_liked = 0")
        elif tab in ("database", "history", "archive"):
            conditions.append("t.is_new = 0")
        elif tab == "liked" or is_liked is True:
            conditions.append("t.is_liked = 1")
        elif tab == "all":
            # No tab filter
            pass
        elif tab is None:
            if is_new is not None:
                conditions.append(f"t.is_new = {1 if is_new else 0}")
            if is_liked is not None:
                conditions.append(f"t.is_liked = {1 if is_liked else 0}")
            if is_new is None and is_liked is None:
                # Default to Inbox (unliked, new items)
                conditions.append("t.is_new = 1 AND t.is_liked = 0")

        if search_query:
            conditions.append("(t.trend_name LIKE ? OR t.ai_summary LIKE ? OR t.original_text LIKE ?)")
            params.extend([f"%{search_query}%", f"%{search_query}%", f"%{search_query}%"])

        where_clause = f"WHERE {' AND '.join(conditions)}" if conditions else ""
        query = f"""
            SELECT t.*, s.name as source_name, s.source_type
            FROM trends t
            LEFT JOIN sources s ON t.source_id = s.id
            {where_clause}
            ORDER BY t.parsed_date DESC, t.id DESC
            LIMIT ? OFFSET ?
        """
        params.extend([limit, skip])

        with get_db_connection() as conn:
            cursor = conn.execute(query, params)
            return [dict(row) for row in cursor.fetchall()]

    @staticmethod
    def mark_reviewed(trend_id: int, is_reviewed: bool = True) -> bool:
        """Mark trend as reviewed or unreviewed."""
        with get_db_connection() as conn:
            cursor = conn.execute(
                "UPDATE trends SET is_reviewed = ? WHERE id = ?",
                (1 if is_reviewed else 0, trend_id),
            )
            return cursor.rowcount > 0

    @staticmethod
    def toggle_like(trend_id: int, is_liked: Optional[bool] = None) -> Optional[bool]:
        """Toggle or set is_liked status for a trend. Returns new boolean state or None if not found."""
        with get_db_connection() as conn:
            if is_liked is None:
                cursor = conn.execute("SELECT is_liked FROM trends WHERE id = ?", (trend_id,))
                row = cursor.fetchone()
                if not row:
                    return None
                new_status = 0 if row["is_liked"] else 1
            else:
                new_status = 1 if is_liked else 0

            cursor = conn.execute(
                "UPDATE trends SET is_liked = ? WHERE id = ?",
                (new_status, trend_id),
            )
            if cursor.rowcount == 0:
                return None
            return bool(new_status)

    @staticmethod
    def save_detailed_report(trend_id: int, detailed_report: str) -> bool:
        """Save AI-generated deep analytical report for a specific trend."""
        with get_db_connection() as conn:
            cursor = conn.execute(
                "UPDATE trends SET detailed_report = ? WHERE id = ?",
                (detailed_report, trend_id),
            )
            return cursor.rowcount > 0

    @staticmethod
    def exists_by_hash(content_hash: str) -> bool:
        """Check if trend with specific content hash already exists."""
        with get_db_connection() as conn:
            cursor = conn.execute(
                "SELECT 1 FROM trends WHERE content_hash = ? LIMIT 1",
                (content_hash,),
            )
            return cursor.fetchone() is not None

    @staticmethod
    def get_existing_hashes(hashes: List[str]) -> Set[str]:
        """Given a list of hashes, return the subset that already exists in DB."""
        if not hashes:
            return set()
        placeholders = ",".join("?" for _ in hashes)
        with get_db_connection() as conn:
            cursor = conn.execute(
                f"SELECT content_hash FROM trends WHERE content_hash IN ({placeholders})",
                hashes,
            )
            return {row["content_hash"] for row in cursor.fetchall()}

    @staticmethod
    def get_stats() -> Dict[str, Any]:
        """Aggregate statistical metrics for dashboard system status."""
        with get_db_connection() as conn:
            cursor = conn.execute(
                """
                SELECT
                    COUNT(*) as total_count,
                    SUM(CASE WHEN is_reviewed = 1 THEN 1 ELSE 0 END) as reviewed_count,
                    SUM(CASE WHEN is_new = 1 THEN 1 ELSE 0 END) as new_count,
                    SUM(CASE WHEN is_trend = 1 THEN 1 ELSE 0 END) as confirmed_trends_count,
                    SUM(CASE WHEN is_liked = 1 THEN 1 ELSE 0 END) as liked_count,
                    SUM(CASE WHEN is_new = 1 AND is_liked = 0 THEN 1 ELSE 0 END) as inbox_count,
                    SUM(CASE WHEN is_new = 0 THEN 1 ELSE 0 END) as database_count,
                    SUM(CASE WHEN ai_status = 'pending' THEN 1 ELSE 0 END) as pending_ai_count,
                    AVG(ai_score) as avg_score,
                    AVG(scam_probability) as avg_scam_probability
                FROM trends
                """
            )
            row = cursor.fetchone()
            stats = dict(row) if row else {}
            stats["total_count"] = stats.get("total_count") or 0
            stats["reviewed_count"] = stats.get("reviewed_count") or 0
            stats["new_count"] = stats.get("new_count") or 0
            stats["confirmed_trends_count"] = stats.get("confirmed_trends_count") or 0
            stats["liked_count"] = stats.get("liked_count") or 0
            stats["inbox_count"] = stats.get("inbox_count") or 0
            stats["database_count"] = stats.get("database_count") or 0
            stats["pending_ai_count"] = stats.get("pending_ai_count") or 0
            stats["avg_score"] = round(stats.get("avg_score") or 0, 1)
            stats["avg_scam_probability"] = round(stats.get("avg_scam_probability") or 0, 1)
            return stats

    @staticmethod
    def increment_mention_count(trend_id: int, merged_text: Optional[str] = None) -> bool:
        """Increment mention count and optionally update original_text with merged context."""
        with get_db_connection() as conn:
            if merged_text is not None:
                cursor = conn.execute(
                    """
                    UPDATE trends
                    SET mention_count = mention_count + 1,
                        original_text = ?
                    WHERE id = ?
                    """,
                    (merged_text, trend_id),
                )
            else:
                cursor = conn.execute(
                    "UPDATE trends SET mention_count = mention_count + 1 WHERE id = ?",
                    (trend_id,),
                )
            return cursor.rowcount > 0

    @staticmethod
    def get_last_scan_time() -> Optional[str]:
        """Retrieve the most recent parsed_date timestamp from trends."""
        with get_db_connection() as conn:
            cursor = conn.execute("SELECT MAX(parsed_date) as max_parsed FROM trends WHERE parsed_date IS NOT NULL")
            row = cursor.fetchone()
            return str(row["max_parsed"]) if row and row["max_parsed"] else None

    @staticmethod
    def delete(trend_id: int) -> bool:
        """Delete trend record."""
        with get_db_connection() as conn:
            cursor = conn.execute("DELETE FROM trends WHERE id = ?", (trend_id,))
            return cursor.rowcount > 0
