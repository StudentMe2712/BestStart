"""projects + project scope (Project Memory P2, stage 1)

Создаёт таблицу `projects` (лёгкая сущность-зонтик) и добавляет необязательную
привязку существующих сущностей к проекту: nullable `project_id` на
`conversations` и `content_sources` (FK→projects, ON DELETE SET NULL — удаление
проекта НЕ удаляет контент). Аддитивно: NULL = вне проекта, текущее поведение не
меняется.

Revision ID: a6b7c8d9e0f1
Revises: f5a6b7c8d9e0
Create Date: 2026-06-14 00:00:00.000000

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects import postgresql


# revision identifiers, used by Alembic.
revision: str = "a6b7c8d9e0f1"
down_revision: Union[str, None] = "f5a6b7c8d9e0"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.create_table(
        "projects",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("name", sa.String(length=200), nullable=False),
        sa.Column("description", sa.Text(), nullable=True),
        sa.Column("status", sa.String(length=16), nullable=False, server_default="active"),
        sa.Column(
            "created_at", sa.DateTime(timezone=True),
            server_default=sa.func.now(), nullable=False,
        ),
        sa.Column(
            "updated_at", sa.DateTime(timezone=True),
            server_default=sa.func.now(), nullable=False,
        ),
    )
    op.create_index("ix_projects_status", "projects", ["status"])

    # conversations.project_id
    op.add_column(
        "conversations",
        sa.Column("project_id", postgresql.UUID(as_uuid=True), nullable=True),
    )
    op.create_foreign_key(
        "fk_conversations_project", "conversations", "projects",
        ["project_id"], ["id"], ondelete="SET NULL",
    )
    op.create_index("ix_conversations_project", "conversations", ["project_id"])

    # content_sources.project_id
    op.add_column(
        "content_sources",
        sa.Column("project_id", postgresql.UUID(as_uuid=True), nullable=True),
    )
    op.create_foreign_key(
        "fk_content_sources_project", "content_sources", "projects",
        ["project_id"], ["id"], ondelete="SET NULL",
    )
    op.create_index("ix_content_sources_project", "content_sources", ["project_id"])


def downgrade() -> None:
    op.drop_index("ix_content_sources_project", table_name="content_sources")
    op.drop_constraint("fk_content_sources_project", "content_sources", type_="foreignkey")
    op.drop_column("content_sources", "project_id")

    op.drop_index("ix_conversations_project", table_name="conversations")
    op.drop_constraint("fk_conversations_project", "conversations", type_="foreignkey")
    op.drop_column("conversations", "project_id")

    op.drop_index("ix_projects_status", table_name="projects")
    op.drop_table("projects")
