"""Cria as tabelas dos trechos críticos: conjunto_trechos_criticos e trecho_critico.

Revisão: 0001
Anterior: nenhuma (primeira revisão)
Escrita à mão. Os nomes das constraints seguem a naming_convention de base.py.
"""

from collections.abc import Sequence

import sqlalchemy as sa
from alembic import op

revision: str = "0001"
down_revision: str | None = None
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    """Cria o conjunto (uma carga do arquivo) e os trechos de cada conjunto."""
    op.create_table(
        "conjunto_trechos_criticos",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("periodo", sa.String(length=100), nullable=False),
        sa.Column("fonte", sa.Text(), nullable=False),
        sa.Column("criterio", sa.Text(), nullable=False),
        sa.Column("hash_arquivo", sa.String(length=64), nullable=False),
        sa.Column("carregado_em", sa.DateTime(timezone=True), nullable=False),
        sa.PrimaryKeyConstraint("id", name=op.f("pk_conjunto_trechos_criticos")),
        # O hash único garante que o mesmo arquivo não seja carregado duas vezes.
        sa.UniqueConstraint("hash_arquivo", name=op.f("uq_conjunto_trechos_criticos_hash_arquivo")),
    )
    op.create_table(
        "trecho_critico",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("conjunto_id", sa.Uuid(), nullable=False),
        sa.Column("posicao", sa.Integer(), nullable=False),
        sa.Column("logradouro", sa.String(length=200), nullable=False),
        sa.Column("latitude", sa.Float(), nullable=False),
        sa.Column("longitude", sa.Float(), nullable=False),
        sa.Column("quantidade_sinistros_graves", sa.Integer(), nullable=False),
        sa.PrimaryKeyConstraint("id", name=op.f("pk_trecho_critico")),
        sa.ForeignKeyConstraint(
            ["conjunto_id"],
            ["conjunto_trechos_criticos.id"],
            name=op.f("fk_trecho_critico_conjunto_id_conjunto_trechos_criticos"),
        ),
        # Uma posição por conjunto: o ranking não tem dois trechos no mesmo lugar.
        sa.UniqueConstraint(
            "conjunto_id", "posicao", name=op.f("uq_trecho_critico_conjunto_id_posicao")
        ),
        sa.CheckConstraint("posicao >= 1", name=op.f("ck_trecho_critico_posicao_minima")),
        sa.CheckConstraint(
            "quantidade_sinistros_graves >= 0",
            name=op.f("ck_trecho_critico_quantidade_nao_negativa"),
        ),
    )


def downgrade() -> None:
    """Remove as tabelas na ordem inversa (primeiro a que tem a chave estrangeira)."""
    op.drop_table("trecho_critico")
    op.drop_table("conjunto_trechos_criticos")
