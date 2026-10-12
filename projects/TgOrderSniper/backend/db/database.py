"""Database management and repository operations for OmniLeadSniper.

Provides async SQLite session management, table initialization,
lead deduplication queries, and persistent storage.
"""

from pathlib import Path
import logging

from sqlalchemy import and_, or_, select
from sqlalchemy.ext.asyncio import (
    AsyncEngine,
    AsyncSession,
    async_sessionmaker,
    create_async_engine,
)

from backend.db.models import Base, ProcessedLead

logger = logging.getLogger(__name__)


class Database:
    """Async database client managing SQLite connection and ProcessedLead queries."""

    def __init__(self, db_path: str = "data/sniper.db") -> None:
        """Initializes async engine and session factory.

        Args:
            db_path: Path to the SQLite database file.
        """
        self.db_path = Path(db_path)
        normalized_path = self.db_path.as_posix()
        self.url = f"sqlite+aiosqlite:///{normalized_path}"
        self.engine: AsyncEngine = create_async_engine(self.url, echo=False)
        self.session_factory: async_sessionmaker[AsyncSession] = async_sessionmaker(
            self.engine,
            expire_on_commit=False,
            class_=AsyncSession,
        )

    async def init_db(self) -> None:
        """Ensures storage directory exists and initializes database schema."""
        parent_dir = self.db_path.parent
        if str(parent_dir) and str(parent_dir) != "." and not parent_dir.exists():
            parent_dir.mkdir(parents=True, exist_ok=True)
            logger.info("Created database directory: %s", parent_dir)

        async with self.engine.begin() as conn:
            await conn.run_sync(Base.metadata.create_all)
        logger.info("Initialized SQLite database schema at %s", self.db_path)

    async def is_already_processed(
        self, source: str, external_id: str, content_hash: str
    ) -> bool:
        """Checks if a lead with the given source+external_id or content_hash exists.

        Args:
            source: Source identifier (e.g. 'telegram:-100123', 'web:habr').
            external_id: Unique entity ID in the source.
            content_hash: SHA-256 hash of normalized text.

        Returns:
            True if matching record exists in DB, False otherwise.
        """
        async with self.session_factory() as session:
            stmt = (
                select(ProcessedLead.id)
                .where(
                    or_(
                        and_(
                            ProcessedLead.source == source,
                            ProcessedLead.external_id == external_id,
                        ),
                        ProcessedLead.content_hash == content_hash,
                    )
                )
                .limit(1)
            )
            result = await session.execute(stmt)
            return result.scalar_one_or_none() is not None

    async def save_lead(
        self,
        source: str,
        external_id: str,
        content_hash: str,
        raw_text: str,
        url: str,
        is_lead: bool,
        title: str | None = None,
        summary: str | None = None,
        budget: str | None = None,
        tech_stack: str | None = None,
    ) -> ProcessedLead:
        """Persists a new lead record to the database.

        Args:
            source: Origin source of lead.
            external_id: Source-specific unique ID.
            content_hash: SHA-256 hash of lead content.
            raw_text: Original raw task or message text.
            url: Direct link to the task or message.
            is_lead: Whether the item is classified as a commercial lead.
            title: Optional task title.
            summary: Optional brief summary of requirements.
            budget: Extracted or stated budget string.
            tech_stack: Detected technology stack keywords.

        Returns:
            Saved ProcessedLead instance with assigned database ID.
        """
        async with self.session_factory() as session:
            lead = ProcessedLead(
                source=source,
                external_id=external_id,
                content_hash=content_hash,
                title=title,
                raw_text=raw_text,
                summary=summary,
                budget=budget,
                tech_stack=tech_stack,
                url=url,
                is_lead=is_lead,
            )
            session.add(lead)
            await session.commit()
            await session.refresh(lead)
            logger.debug(
                "Persisted lead ID=%s from %s (is_lead=%s)", lead.id, source, is_lead
            )
            return lead

    async def close(self) -> None:
        """Closes the async database engine and disposes of connections."""
        await self.engine.dispose()
        logger.info("Closed SQLite database engine.")
