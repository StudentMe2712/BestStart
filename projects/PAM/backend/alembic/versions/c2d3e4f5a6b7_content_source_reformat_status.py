"""content_sources: reformat_status + reformat_progress (#6 background reformat)

Adds:
- reformat_status   — статус фонового реформата (NULL|running|done|failed).
- reformat_progress — прогресс реформата в процентах (0..100), default 0.

Revision ID: c2d3e4f5a6b7
Revises: b1f2c3d4e5a6
Create Date: 2026-06-13 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = "c2d3e4f5a6b7"
down_revision: Union[str, None] = "b1f2c3d4e5a6"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.add_column(
        "content_sources",
        sa.Column("reformat_status", sa.String(length=16), nullable=True),
    )
    op.add_column(
        "content_sources",
        sa.Column(
            "reformat_progress",
            sa.Integer(),
            nullable=False,
            server_default="0",
        ),
    )


def downgrade() -> None:
    op.drop_column("content_sources", "reformat_progress")
    op.drop_column("content_sources", "reformat_status")
