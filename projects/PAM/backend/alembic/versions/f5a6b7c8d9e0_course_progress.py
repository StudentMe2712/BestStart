"""course_progress: learning progress per course (P1)

Прогресс изучения по курсу (а значит по материалу): какие уроки пройдены
(`completed_lessons` — список ключей "модуль-урок"), результат квиза и снимок
общего числа уроков для расчёта процента. Одна строка на курс (`course_id`
уникален); пересоздание курса = новый course_id = чистый прогресс.

Revision ID: f5a6b7c8d9e0
Revises: e4f5a6b7c8d9
Create Date: 2026-06-13 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects import postgresql


# revision identifiers, used by Alembic.
revision: str = "f5a6b7c8d9e0"
down_revision: Union[str, None] = "e4f5a6b7c8d9"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.create_table(
        "course_progress",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column(
            "course_id",
            postgresql.UUID(as_uuid=True),
            sa.ForeignKey("courses.id", ondelete="CASCADE"),
            nullable=False,
            unique=True,
        ),
        sa.Column(
            "completed_lessons",
            postgresql.JSONB(),
            nullable=False,
            server_default="[]",
        ),
        sa.Column("lessons_total", sa.Integer(), nullable=False, server_default="0"),
        sa.Column("quiz_score", sa.Integer(), nullable=True),
        sa.Column("quiz_total", sa.Integer(), nullable=True),
        sa.Column("quiz_completed_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            server_default=sa.func.now(),
            nullable=False,
        ),
        sa.Column(
            "updated_at",
            sa.DateTime(timezone=True),
            server_default=sa.func.now(),
            nullable=False,
        ),
    )


def downgrade() -> None:
    op.drop_table("course_progress")
