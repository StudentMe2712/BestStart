"""profile_facts: status (Fact Review Queue — P0)

Adds:
- status — статус факта в очереди проверки
  (pending_review | approved | rejected | edited).

Перед попаданием в долгую память факт должен пройти проверку. Новые факты
извлекаются как `pending_review` и не подмешиваются в чат, пока пользователь
их не примет. Существующие факты (на момент миграции) уже жили в памяти —
бэкфилл переводит их в `approved`, чтобы не выдёргивать накопленный профиль
в бэклог проверки. Go-forward default колонки — `pending_review`.

Revision ID: d3e4f5a6b7c8
Revises: c2d3e4f5a6b7
Create Date: 2026-06-13 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = "d3e4f5a6b7c8"
down_revision: Union[str, None] = "c2d3e4f5a6b7"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    # server_default 'approved' бэкфиллит уже существующие (живущие в памяти) факты.
    op.add_column(
        "profile_facts",
        sa.Column(
            "status",
            sa.String(length=16),
            nullable=False,
            server_default="approved",
        ),
    )
    # Дальше новые строки по умолчанию попадают в очередь проверки.
    op.alter_column("profile_facts", "status", server_default="pending_review")
    op.create_index("ix_profile_facts_status", "profile_facts", ["status"])


def downgrade() -> None:
    op.drop_index("ix_profile_facts_status", table_name="profile_facts")
    op.drop_column("profile_facts", "status")
