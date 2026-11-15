"""Portas do domínio: o que ele precisa do mundo de fora, sem saber como é feito.

São typing.Protocol: qualquer classe com os mesmos métodos serve, sem herança. A
infraestrutura implementa cada porta (XGBoost, PostgreSQL, relógio do sistema) e os
testes usam versões falsas escritas à mão.
"""

from collections.abc import Sequence
from datetime import datetime
from typing import Protocol

from app.dominio.calculadora_indice import RegistroCalculoIndice
from app.dominio.horas_turno import HoraTurno
from app.dominio.trecho_critico import ConjuntoTrechosCriticos


class PrevisorSinistros(Protocol):
    """Modelo que prevê os sinistros graves esperados em cada hora local."""

    @property
    def p95(self) -> float:
        """P95 das previsões do modelo, usado na normalização do componente sinistro."""

    @property
    def versao_modelo(self) -> str:
        """Versão do modelo (versaoModelo de metadados_modelo.json), gravada no histórico."""

    def prever(self, horas: Sequence[HoraTurno]) -> list[float]:
        """Devolve uma previsão (float do Python) para cada hora, na mesma ordem."""


class Relogio(Protocol):
    """Fonte do instante atual, para o domínio e os serviços nunca chamarem datetime.now()."""

    def agora(self) -> datetime:
        """Instante atual com fuso (UTC)."""


class RepositorioTrechosCriticos(Protocol):
    """Leitura dos trechos críticos carregados no banco."""

    def obter_mais_recentes(self, limite: int) -> ConjuntoTrechosCriticos | None:
        """Devolve os primeiros trechos do conjunto mais recente, ou None se não houver carga."""


class RepositorioCalculos(Protocol):
    """Gravação do histórico de cálculos do índice."""

    def salvar(self, registro: RegistroCalculoIndice) -> None:
        """Grava o cálculo e as suas horas numa única transação."""
