"""Cria o histórico dos cálculos do índice: calculo_indice e calculo_indice_hora.

Revisão: 0002
Anterior: 0001
Escrita à mão. Nenhuma coluna guarda dado pessoal.
"""

from collections.abc import Sequence

import sqlalchemy as sa
from alembic import op

revision: str = "0002"
down_revision: str | None = "0001"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def _entre_0_e_100(coluna: str) -> sa.CheckConstraint:
    """CHECK que limita uma coluna inteira a 0..100, com o nome da naming_convention."""
    return sa.CheckConstraint(
        f"{coluna} BETWEEN 0 AND 100", name=op.f(f"ck_calculo_indice_{coluna}_0_a_100")
    )


def upgrade() -> None:
    """Cria o cálculo (entradas e resultado) e as horas de cada cálculo."""
    op.create_table(
        "calculo_indice",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("calculado_em", sa.DateTime(timezone=True), nullable=False),
        sa.Column("inicio", sa.DateTime(timezone=True), nullable=False),
        sa.Column("fim", sa.DateTime(timezone=True), nullable=False),
        sa.Column("chuva_mm_hora", sa.Float(), nullable=False),
        sa.Column("temperatura_c", sa.Float(), nullable=False),
        sa.Column("umidade_pct", sa.Float(), nullable=False),
        sa.Column("indice_calor_c", sa.Float(), nullable=False),
        sa.Column("componente_sinistro", sa.Integer(), nullable=False),
        sa.Column("componente_chuva", sa.Integer(), nullable=False),
        sa.Column("componente_calor", sa.Integer(), nullable=False),
        sa.Column("indice_risco", sa.Integer(), nullable=False),
        sa.Column("faixa_risco", sa.String(length=20), nullable=False),
        sa.Column("explicacao", sa.Text(), nullable=False),
        sa.Column("versao_modelo", sa.String(length=20), nullable=False),
        sa.Column("regra_agregacao", sa.String(length=50), nullable=False),
        sa.PrimaryKeyConstraint("id", name=op.f("pk_calculo_indice")),
        _entre_0_e_100("componente_sinistro"),
        _entre_0_e_100("componente_chuva"),
        _entre_0_e_100("componente_calor"),
        _entre_0_e_100("indice_risco"),
        # As quatro faixas do contrato, escritas por extenso (a migração não importa o domínio,
        # para continuar igual mesmo que o código mude depois).
        sa.CheckConstraint(
            "faixa_risco IN ('BAIXO', 'MODERADO', 'ALTO', 'MUITO_ALTO')",
            name=op.f("ck_calculo_indice_faixa_risco_valida"),
        ),
    )
    op.create_table(
        "calculo_indice_hora",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("calculo_id", sa.Uuid(), nullable=False),
        sa.Column("hora_inicio_utc", sa.DateTime(timezone=True), nullable=False),
        sa.Column("hora_local", sa.Integer(), nullable=False),
        sa.Column("dia_semana", sa.Integer(), nullable=False),
        sa.Column("mes", sa.Integer(), nullable=False),
        sa.Column("feriado", sa.Boolean(), nullable=False),
        sa.Column("previsao_sinistros", sa.Float(), nullable=False),
        sa.Column("componente_sinistro_hora", sa.Float(), nullable=False),
        sa.Column("minutos_no_turno", sa.Float(), nullable=False),
        sa.PrimaryKeyConstraint("id", name=op.f("pk_calculo_indice_hora")),
        # ON DELETE CASCADE: apagar um cálculo apaga as horas dele.
        sa.ForeignKeyConstraint(
            ["calculo_id"],
            ["calculo_indice.id"],
            name=op.f("fk_calculo_indice_hora_calculo_id_calculo_indice"),
            ondelete="CASCADE",
        ),
        sa.UniqueConstraint(
            "calculo_id",
            "hora_inicio_utc",
            name=op.f("uq_calculo_indice_hora_calculo_id_hora_inicio_utc"),
        ),
        sa.CheckConstraint(
            "hora_local BETWEEN 0 AND 23", name=op.f("ck_calculo_indice_hora_hora_local_0_a_23")
        ),
    )


def downgrade() -> None:
    """Remove as tabelas na ordem inversa (primeiro a que tem a chave estrangeira)."""
    op.drop_table("calculo_indice_hora")
    op.drop_table("calculo_indice")
