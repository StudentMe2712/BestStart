"""events: lightweight observability log (Observability panel — P0)

Append-only метрики local-first: вызовы провайдеров, фолбэки, батчи эмбеддингов,
импорты, прогоны Лектора, времена ответа. Пишется best-effort из
`metrics.record_event` (никогда не на критическом пути). Агрегируется в `/stats`.

Revision ID: e4f5a6b7c8d9
Revises: d3e4f5a6b7c8
Create Date: 2026-06-13 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects import postgresql


# revision identifiers, used by Alembic.
revision: str = "e4f5a6b7c8d9"
down_revision: Union[str, None] = "d3e4f5a6b7c8"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.create_table(
        "events",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("kind", sa.String(length=32), nullable=False),
        sa.Column("provider", sa.String(length=32), nullable=True),
        sa.Column("status", sa.String(length=16), nullable=False, server_default="ok"),
        sa.Column("duration_ms", sa.Integer(), nullable=True),
        sa.Column("detail", sa.String(length=255), nullable=True),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            server_default=sa.func.now(),
            nullable=False,
        ),
    )
    op.create_index("ix_events_kind_created", "events", ["kind", "created_at"])
    op.create_index("ix_events_created", "events", ["created_at"])


def downgrade() -> None:
    op.drop_index("ix_events_created", table_name="events")
    op.drop_index("ix_events_kind_created", table_name="events")
    op.drop_table("events")
