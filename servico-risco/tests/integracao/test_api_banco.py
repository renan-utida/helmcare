"""Integração: a API com o startup real (modelo real, banco de testes e carga dos trechos)."""

from collections.abc import Iterator
from pathlib import Path

import pytest
from fastapi.testclient import TestClient
from pydantic import SecretStr
from sqlalchemy import Engine, func, select
from sqlalchemy.orm import Session

from app.config import Configuracoes
from app.infraestrutura.persistencia.modelos import CalculoIndiceHoraModelo, CalculoIndiceModelo
from app.main import criar_app, inicializar_recursos
from tests.api.conftest import CORPO_REFERENCIA
from tests.integracao.conftest import limpar_tabelas

pytestmark = pytest.mark.integracao


@pytest.fixture
def cliente(engine_teste: Engine, url_testes: str, diretorio_modelo: Path) -> Iterator[TestClient]:
    """Cliente da aplicação com o mesmo startup da produção, mas no banco de testes."""
    limpar_tabelas(engine_teste)
    configuracoes = Configuracoes(
        _env_file=None, database_url=SecretStr(url_testes), modelo_dir=diretorio_modelo
    )
    app = criar_app(inicializar=lambda: inicializar_recursos(configuracoes))
    with TestClient(app) as cliente:
        yield cliente


def test_api_indice_turno_grava_calculo_e_4_horas_no_banco(
    cliente: TestClient, engine_teste: Engine
) -> None:
    # Act
    resposta = cliente.post("/risco/v1/indice-turno", json=CORPO_REFERENCIA)

    # Assert
    assert resposta.status_code == 200
    assert resposta.json()["indiceRisco"] == 60
    with Session(engine_teste) as sessao:
        assert sessao.scalar(select(func.count()).select_from(CalculoIndiceModelo)) == 1
        assert sessao.scalar(select(func.count()).select_from(CalculoIndiceHoraModelo)) == 4
        calculo = sessao.scalars(select(CalculoIndiceModelo)).one()
        assert calculo.versao_modelo == "1.0.0"
        assert calculo.explicacao == resposta.json()["explicacao"]


def test_api_trechos_carregados_no_startup_iguais_ao_exemplo_do_contrato(
    cliente: TestClient,
) -> None:
    # Act
    resposta = cliente.get("/risco/v1/trechos-criticos", params={"limite": 2})

    # Assert: o mesmo JSON do exemplo da seção 8 do contrato.
    assert resposta.status_code == 200
    assert resposta.json() == {
        "periodo": "2019 a 2025",
        "trechos": [
            {
                "posicao": 1,
                "logradouro": "SP 270 (RODOVIA RAPOSO TAVARES)",
                "latitude": -23.58341,
                "longitude": -46.77771,
                "quantidadeSinistrosGraves": 46,
            },
            {
                "posicao": 2,
                "logradouro": "SP 270 (RODOVIA RAPOSO TAVARES)",
                "latitude": -23.58481,
                "longitude": -46.78738,
                "quantidadeSinistrosGraves": 38,
            },
        ],
    }


def test_api_saude_com_modelo_e_banco_200_healthy(cliente: TestClient) -> None:
    # Act
    resposta = cliente.get("/risco/v1/saude")

    # Assert
    assert resposta.status_code == 200
    assert resposta.json() == {"status": "Healthy"}
