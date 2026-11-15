"""Dependências do FastAPI: montam os serviços de aplicação para cada requisição.

Os recursos de longa duração (modelo, engine do banco) são criados uma vez no startup
e ficam em app.state.recursos. A sessão do banco é aberta e fechada por requisição. Os
testes trocam estas funções por fakes com app.dependency_overrides.
"""

from collections.abc import Iterator
from dataclasses import dataclass, field
from typing import Annotated

from fastapi import Depends, Request
from sqlalchemy import Engine
from sqlalchemy.orm import Session, sessionmaker

from app.aplicacao.servico_indice_turno import ServicoIndiceTurno
from app.aplicacao.servico_saude import ServicoSaude
from app.aplicacao.servico_trechos_criticos import ServicoTrechosCriticos
from app.dominio.calendario import CalendarioFeriados
from app.dominio.portas import PrevisorSinistros, Relogio
from app.infraestrutura.persistencia.repositorios import (
    RepositorioCalculosSql,
    RepositorioTrechosCriticosSql,
)
from app.infraestrutura.persistencia.sessao import verificar_conexao
from app.infraestrutura.relogio import RelogioSistema


@dataclass
class RecursosServico:
    """O que o startup conseguiu preparar. None indica uma dependência indisponível.

    Com o modelo None, /indice-turno e /saude respondem 503. Com o banco None, os
    trechos e a /saude respondem 503, e o índice é calculado sem gravar histórico.
    """

    previsor: PrevisorSinistros | None = None
    calendario: CalendarioFeriados | None = None
    engine: Engine | None = None
    fabrica_sessoes: sessionmaker[Session] | None = None
    relogio: Relogio = field(default_factory=RelogioSistema)

    def fechar(self) -> None:
        """Fecha as conexões do pool no desligamento do serviço."""
        if self.engine is not None:
            self.engine.dispose()


def obter_recursos(request: Request) -> RecursosServico:
    """Recursos criados no startup (lifespan)."""
    return request.app.state.recursos


RecursosDep = Annotated[RecursosServico, Depends(obter_recursos)]


def obter_sessao(recursos: RecursosDep) -> Iterator[Session | None]:
    """Abre uma sessão para a requisição e a fecha no fim; None se não houver banco."""
    if recursos.fabrica_sessoes is None:
        yield None
        return
    with recursos.fabrica_sessoes() as sessao:
        yield sessao


SessaoDep = Annotated[Session | None, Depends(obter_sessao)]


def obter_servico_indice_turno(recursos: RecursosDep, sessao: SessaoDep) -> ServicoIndiceTurno:
    """Serviço do índice com o previsor do startup e o repositório da sessão."""
    repositorio = RepositorioCalculosSql(sessao) if sessao is not None else None
    return ServicoIndiceTurno(recursos.previsor, recursos.calendario, repositorio, recursos.relogio)


def obter_servico_trechos_criticos(sessao: SessaoDep) -> ServicoTrechosCriticos:
    """Serviço dos trechos com o repositório da sessão."""
    repositorio = RepositorioTrechosCriticosSql(sessao) if sessao is not None else None
    return ServicoTrechosCriticos(repositorio)


def obter_servico_saude(recursos: RecursosDep) -> ServicoSaude:
    """Serviço de saúde: modelo carregado e banco respondendo ao SELECT 1."""
    engine = recursos.engine

    def verificar_banco() -> bool:
        return engine is not None and verificar_conexao(engine)

    return ServicoSaude(recursos.previsor is not None, verificar_banco)
