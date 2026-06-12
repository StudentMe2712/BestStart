"""content_sources: formatted_text + original file bytes (preview)

Adds:
- formatted_text  — AI-причёсанная версия исходного текста (кэш кнопки
  «Улучшить читаемость»).
- original_data    — оригинальные байты загруженного PDF (нативный предпросмотр).
- original_mime    — MIME оригинала (флаг наличия файла для предпросмотра).

Revision ID: b1f2c3d4e5a6
Revises: 68113919ba65
Create Date: 2026-06-12 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = "b1f2c3d4e5a6"
down_revision: Union[str, None] = "68113919ba65"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.add_column("content_sources", sa.Column("formatted_text", sa.Text(), nullable=True))
    op.add_column("content_sources", sa.Column("original_data", sa.LargeBinary(), nullable=True))
    op.add_column("content_sources", sa.Column("original_mime", sa.String(length=128), nullable=True))


def downgrade() -> None:
    op.drop_column("content_sources", "original_mime")
    op.drop_column("content_sources", "original_data")
    op.drop_column("content_sources", "formatted_text")
