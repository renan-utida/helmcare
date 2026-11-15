"""Modelos das quatro tabelas do banco helmcare_risco (SQLAlchemy 2, Mapped[]).

Nomes no singular e colunas em snake_case. Nenhuma tabela guarda dado pessoal: a
requisição do índice nem tem identificador de entregador, e as coordenadas dos trechos
são de vias públicas. Os nomes das CHECK são curtos porque a naming_convention da
base acrescenta o prefixo ck_<tabela>_.
"""

import uuid
from datetime import datetime

from sqlalchemy import (
    Boolean,
    CheckConstraint,
    DateTime,
    Float,
    ForeignKey,
    Integer,
    String,
    Text,
    UniqueConstraint,
    Uuid,
)
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.dominio.faixa_risco import FaixaRisco
from app.infraestrutura.persistencia.base import Base

# Lista SQL das quatro faixas do contrato, tirada do enum do domínio:
# "'BAIXO', 'MODERADO', 'ALTO', 'MUITO_ALTO'".
FAIXAS_SQL = ", ".join(f"'{faixa.value}'" for faixa in FaixaRisco)


def _entre_0_e_100(coluna: str) -> CheckConstraint:
    """CHECK que limita uma coluna inteira a 0..100 (componentes e índice)."""
    return CheckConstraint(f"{coluna} BETWEEN 0 AND 100", name=f"{coluna}_0_a_100")


class ConjuntoTrechosCriticosModelo(Base):
    """Uma carga de trechos_criticos.json, identificada pelo sha256 do arquivo."""

    __tablename__ = "conjunto_trechos_criticos"

    id: Mapped[uuid.UUID] = mapped_column(Uuid, primary_key=True, default=uuid.uuid4)
    periodo: Mapped[str] = mapped_column(String(100))
    fonte: Mapped[str] = mapped_column(Text)
    criterio: Mapped[str] = mapped_column(Text)
    # O hash único é o que torna a carga idempotente: o mesmo arquivo não entra duas vezes.
    hash_arquivo: Mapped[str] = mapped_column(String(64), unique=True)
    carregado_em: Mapped[datetime] = mapped_column(DateTime(timezone=True))

    trechos: Mapped[list["TrechoCriticoModelo"]] = relationship(
        back_populates="conjunto", order_by="TrechoCriticoModelo.posicao"
    )


class TrechoCriticoModelo(Base):
    """Um trecho do ranking, ligado ao conjunto em que foi carregado."""

    __tablename__ = "trecho_critico"
    __table_args__ = (
        UniqueConstraint("conjunto_id", "posicao"),
        CheckConstraint("posicao >= 1", name="posicao_minima"),
        CheckConstraint("quantidade_sinistros_graves >= 0", name="quantidade_nao_negativa"),
    )

    id: Mapped[uuid.UUID] = mapped_column(Uuid, primary_key=True, default=uuid.uuid4)
    conjunto_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("conjunto_trechos_criticos.id"))
    posicao: Mapped[int] = mapped_column(Integer)
    logradouro: Mapped[str] = mapped_column(String(200))
    latitude: Mapped[float] = mapped_column(Float)
    longitude: Mapped[float] = mapped_column(Float)
    quantidade_sinistros_graves: Mapped[int] = mapped_column(Integer)

    conjunto: Mapped[ConjuntoTrechosCriticosModelo] = relationship(back_populates="trechos")


class CalculoIndiceModelo(Base):
    """Um cálculo do Índice de Risco do Turno: entradas, resultado e versão do modelo."""

    __tablename__ = "calculo_indice"
    __table_args__ = (
        _entre_0_e_100("componente_sinistro"),
        _entre_0_e_100("componente_chuva"),
        _entre_0_e_100("componente_calor"),
        _entre_0_e_100("indice_risco"),
        CheckConstraint(f"faixa_risco IN ({FAIXAS_SQL})", name="faixa_risco_valida"),
    )

    id: Mapped[uuid.UUID] = mapped_column(Uuid, primary_key=True)
    calculado_em: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    inicio: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    fim: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    chuva_mm_hora: Mapped[float] = mapped_column(Float)
    temperatura_c: Mapped[float] = mapped_column(Float)
    umidade_pct: Mapped[float] = mapped_column(Float)
    indice_calor_c: Mapped[float] = mapped_column(Float)
    componente_sinistro: Mapped[int] = mapped_column(Integer)
    componente_chuva: Mapped[int] = mapped_column(Integer)
    componente_calor: Mapped[int] = mapped_column(Integer)
    indice_risco: Mapped[int] = mapped_column(Integer)
    faixa_risco: Mapped[str] = mapped_column(String(20))
    explicacao: Mapped[str] = mapped_column(Text)
    versao_modelo: Mapped[str] = mapped_column(String(20))
    regra_agregacao: Mapped[str] = mapped_column(String(50))

    # cascade: gravar o cálculo grava as horas junto, na mesma transação.
    horas: Mapped[list["CalculoIndiceHoraModelo"]] = relationship(
        back_populates="calculo", cascade="all, delete-orphan"
    )


class CalculoIndiceHoraModelo(Base):
    """Uma hora do turno de um cálculo: variáveis do modelo, previsão e peso na média."""

    __tablename__ = "calculo_indice_hora"
    __table_args__ = (
        UniqueConstraint("calculo_id", "hora_inicio_utc"),
        CheckConstraint("hora_local BETWEEN 0 AND 23", name="hora_local_0_a_23"),
    )

    id: Mapped[uuid.UUID] = mapped_column(Uuid, primary_key=True, default=uuid.uuid4)
    calculo_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("calculo_indice.id", ondelete="CASCADE")
    )
    hora_inicio_utc: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    # Hora local de America/Sao_Paulo (0 a 23), a variável "hora" do modelo.
    hora_local: Mapped[int] = mapped_column(Integer)
    dia_semana: Mapped[int] = mapped_column(Integer)
    mes: Mapped[int] = mapped_column(Integer)
    feriado: Mapped[bool] = mapped_column(Boolean)
    previsao_sinistros: Mapped[float] = mapped_column(Float)
    # Valor normalizado da hora (100 * previsao / p95, limitado a 100), sem arredondar.
    componente_sinistro_hora: Mapped[float] = mapped_column(Float)
    # Float porque a primeira e a última hora podem ter minutos quebrados (segundos / 60).
    minutos_no_turno: Mapped[float] = mapped_column(Float)

    calculo: Mapped[CalculoIndiceModelo] = relationship(back_populates="horas")
