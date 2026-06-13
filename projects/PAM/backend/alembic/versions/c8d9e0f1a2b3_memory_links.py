"""memory_links: links between knowledge objects (Project Memory P2, stage 5)

Полиморфные связи (kind + id, без жёсткого FK), чтобы выражать примеры:
idea→inspired_by→article, idea→became→decision, tool→used_in→project,
fact→derived_from→conversation. Только PostgreSQL — никакой графовой БД.

Revision ID: c8d9e0f1a2b3
Revises: b7c8d9e0f1a2
Create Date: 2026-06-14 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects import postgresql


# revision identifiers, used by Alembic.
revision: str = "c8d9e0f1a2b3"
down_revision: Union[str, None] = "b7c8d9e0f1a2"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.create_table(
        "memory_links",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("source_kind", sa.String(length=16), nullable=False, server_default="memory_item"),
        sa.Column("source_id", postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column("target_kind", sa.String(length=16), nullable=False, server_default="memory_item"),
        sa.Column("target_id", postgresql.UUID(as_uuid=True), nullable=False),
        sa.Column("relation", sa.String(length=32), nullable=False),
        sa.Column("confidence", sa.Float(), nullable=False, server_default="1.0"),
        sa.Column(
            "created_at", sa.DateTime(timezone=True),
            server_default=sa.func.now(), nullable=False,
        ),
        sa.UniqueConstraint(
            "source_kind", "source_id", "target_kind", "target_id", "relation",
            name="uq_memory_link",
        ),
    )
    op.create_index(
        "ix_memory_links_source", "memory_links", ["source_kind", "source_id"]
    )
    op.create_index(
        "ix_memory_links_target", "memory_links", ["target_kind", "target_id"]
    )


def downgrade() -> None:
    op.drop_index("ix_memory_links_target", table_name="memory_links")
    op.drop_index("ix_memory_links_source", table_name="memory_links")
    op.drop_table("memory_links")
