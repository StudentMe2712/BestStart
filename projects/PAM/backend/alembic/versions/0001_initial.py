"""initial schema

Revision ID: 0001
Revises:
Create Date: 2026-06-01 00:00:00.000000

"""
from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op
from sqlalchemy.dialects import postgresql

revision: str = "0001"
down_revision: Union[str, None] = None
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    # Enable pgvector (we don't use it in Phase 1, but Phase 2 will, and the
    # image already has it. Enabling early keeps subsequent migrations simple.)
    op.execute("CREATE EXTENSION IF NOT EXISTS vector")

    op.create_table(
        "conversations",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("source", sa.String(32), nullable=False),
        sa.Column("external_id", sa.String(255), nullable=False),
        sa.Column("title", sa.Text, nullable=True),
        sa.Column("started_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column(
            "updated_at",
            sa.DateTime(timezone=True),
            server_default=sa.func.now(),
            nullable=False,
        ),
        sa.Column("raw_json", postgresql.JSONB, nullable=True),
        sa.UniqueConstraint("source", "external_id", name="uq_source_external"),
    )
    op.create_index("ix_conversations_source", "conversations", ["source"])
    op.create_index("ix_conversations_updated", "conversations", ["updated_at"])

    op.create_table(
        "messages",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column(
            "conversation_id",
            postgresql.UUID(as_uuid=True),
            sa.ForeignKey("conversations.id", ondelete="CASCADE"),
            nullable=False,
        ),
        sa.Column("role", sa.String(32), nullable=False),
        sa.Column("content", sa.Text, nullable=False),
        sa.Column("position", sa.Integer, nullable=False, server_default="0"),
        sa.Column("sent_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("content_tsv", postgresql.TSVECTOR, nullable=True),
    )
    op.create_index("ix_messages_conv", "messages", ["conversation_id"])
    op.create_index(
        "ix_messages_tsv",
        "messages",
        ["content_tsv"],
        postgresql_using="gin",
    )


def downgrade() -> None:
    op.drop_index("ix_messages_tsv", table_name="messages")
    op.drop_index("ix_messages_conv", table_name="messages")
    op.drop_table("messages")
    op.drop_index("ix_conversations_updated", table_name="conversations")
    op.drop_index("ix_conversations_source", table_name="conversations")
    op.drop_table("conversations")
