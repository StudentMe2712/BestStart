"""memory_items: universal knowledge container (Project Memory P2, stage 3)

Единый контейнер знаний (идея/заметка/статья/инструмент/код/промпт/урок/решение)
из Telegram / чата / вручную. Заменяет project_items из дизайн-дока (консолидация
по принципу «не создавать параллельные системы»). Поиск — полнотекст (content_tsv,
словарь simple) + теги (JSONB, GIN), без отдельного векторного индекса.

Revision ID: b7c8d9e0f1a2
Revises: a6b7c8d9e0f1
Create Date: 2026-06-14 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects import postgresql


# revision identifiers, used by Alembic.
revision: str = "b7c8d9e0f1a2"
down_revision: Union[str, None] = "a6b7c8d9e0f1"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.create_table(
        "memory_items",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column(
            "project_id", postgresql.UUID(as_uuid=True),
            sa.ForeignKey("projects.id", ondelete="SET NULL"), nullable=True,
        ),
        sa.Column("source", sa.String(length=32), nullable=False),
        sa.Column("source_ref", sa.String(length=255), nullable=True),
        sa.Column("title", sa.Text(), nullable=True),
        sa.Column("content", sa.Text(), nullable=False),
        sa.Column("summary", sa.Text(), nullable=True),
        sa.Column("item_type", sa.String(length=16), nullable=False, server_default="note"),
        sa.Column("importance", sa.Integer(), nullable=False, server_default="3"),
        sa.Column("tags", postgresql.JSONB(), nullable=False, server_default="[]"),
        sa.Column("status", sa.String(length=16), nullable=False, server_default="active"),
        sa.Column("content_tsv", postgresql.TSVECTOR(), nullable=True),
        sa.Column(
            "created_at", sa.DateTime(timezone=True),
            server_default=sa.func.now(), nullable=False,
        ),
        sa.Column(
            "updated_at", sa.DateTime(timezone=True),
            server_default=sa.func.now(), nullable=False,
        ),
    )
    op.create_index("ix_memory_items_project", "memory_items", ["project_id"])
    op.create_index("ix_memory_items_type", "memory_items", ["item_type"])
    op.create_index("ix_memory_items_created", "memory_items", ["created_at"])
    op.create_index(
        "ix_memory_items_tsv", "memory_items", ["content_tsv"], postgresql_using="gin"
    )
    op.create_index(
        "ix_memory_items_tags", "memory_items", ["tags"], postgresql_using="gin"
    )


def downgrade() -> None:
    op.drop_index("ix_memory_items_tags", table_name="memory_items")
    op.drop_index("ix_memory_items_tsv", table_name="memory_items")
    op.drop_index("ix_memory_items_created", table_name="memory_items")
    op.drop_index("ix_memory_items_type", table_name="memory_items")
    op.drop_index("ix_memory_items_project", table_name="memory_items")
    op.drop_table("memory_items")
