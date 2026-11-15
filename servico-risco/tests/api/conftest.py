"""Fixtures dos testes de API: a aplicação com as dependências trocadas por fakes.

A aplicação é criada com um "inicializar" vazio, então o lifespan não lê o .env, não
carrega o modelo e não conecta ao banco. Cada teste escolhe os fakes dos serviços.
"""

from collections.abc import Callable, Iterator
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from app.api.dependencias import (
    RecursosServico,
    obter_servico_indice_turno,
    obter_servico_saude,
    obter_servico_trechos_criticos,
)
from app.aplicacao.servico_indice_turno import ServicoIndiceTurno
from app.aplicacao.servico_saude import ServicoSaude
from app.aplicacao.servico_trechos_criticos import ServicoTrechosCriticos
from app.dominio.calendario import CalendarioFeriados
from app.infraestrutura.ml.previsor_xgboost import PrevisorXgboost
from app.main import criar_app
from tests.fakes.previsor_falso import PrevisorFalso
from tests.fakes.relogio_fixo import RelogioFixo
from tests.fakes.repositorios_memoria import (
    RepositorioCalculosMemoria,
    RepositorioTrechosMemoria,
    criar_conjunto_trechos,
)

CorpoJson = dict[str, object]

CORPO_REFERENCIA: CorpoJson = {
    "inicio": "2026-11-12T20:00:00Z",
    "fim": "2026-11-13T00:00:00Z",
    "previsao": {"chuvaMmHora": 7.0, "temperaturaC": 24.0, "umidadePct": 85},
}


class Cenario:
    """Fakes de um teste de API; o teste muda o que precisar antes de criar o cliente."""

    def __init__(self, calendario: CalendarioFeriados) -> None:
        """Começa com tudo funcionando: previsor falso, repositórios em memória, banco ok."""
        self.calendario = calendario
        self.previsor: PrevisorFalso | PrevisorXgboost | None = PrevisorFalso()
        self.repositorio_calculos = RepositorioCalculosMemoria()
        self.repositorio_trechos = RepositorioTrechosMemoria(criar_conjunto_trechos())
        self.banco_ok = True


@pytest.fixture
def cenario(calendario: CalendarioFeriados) -> Cenario:
    """Cenário padrão, com tudo funcionando."""
    return Cenario(calendario)


@pytest.fixture
def criar_cliente(cenario: Cenario) -> Iterator[Callable[..., TestClient]]:
    """Fábrica de TestClient com as dependências trocadas pelos fakes do cenário."""
    clientes: list[TestClient] = []

    def fabricar(raise_server_exceptions: bool = True) -> TestClient:
        app = criar_app(inicializar=RecursosServico)
        app.dependency_overrides[obter_servico_indice_turno] = lambda: ServicoIndiceTurno(
            cenario.previsor,
            cenario.calendario if cenario.previsor is not None else None,
            cenario.repositorio_calculos,
            RelogioFixo(),
        )
        app.dependency_overrides[obter_servico_trechos_criticos] = lambda: ServicoTrechosCriticos(
            cenario.repositorio_trechos
        )
        app.dependency_overrides[obter_servico_saude] = lambda: ServicoSaude(
            cenario.previsor is not None, lambda: cenario.banco_ok
        )
        cliente = TestClient(app, raise_server_exceptions=raise_server_exceptions)
        cliente.__enter__()  # roda o lifespan (com o inicializar vazio)
        clientes.append(cliente)
        return cliente

    yield fabricar
    for cliente in clientes:
        cliente.__exit__(None, None, None)


@pytest.fixture(scope="session")
def previsor_real(diretorio_modelo: Path) -> PrevisorXgboost:
    """Previsor com o modelo real, para o exemplo de referência de ponta a ponta na API."""
    return PrevisorXgboost.carregar(diretorio_modelo)
