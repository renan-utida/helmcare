"""Testes das rotas GET /risco/v1/trechos-criticos e GET /risco/v1/saude."""

from collections.abc import Callable

import pytest
from fastapi.testclient import TestClient

from tests.api.conftest import Cenario
from tests.fakes.repositorios_memoria import RepositorioTrechosMemoria

ROTA_TRECHOS = "/risco/v1/trechos-criticos"
ROTA_SAUDE = "/risco/v1/saude"
TIPO_PROBLEMA = "application/problem+json"


def test_trechos_sem_limite_padrao_10_em_ordem_decrescente(
    criar_cliente: Callable[..., TestClient],
) -> None:
    # Act
    resposta = criar_cliente().get(ROTA_TRECHOS)

    # Assert
    assert resposta.status_code == 200
    corpo = resposta.json()
    assert set(corpo) == {"periodo", "trechos"}
    assert corpo["periodo"] == "2019 a 2025"
    assert len(corpo["trechos"]) == 10
    quantidades = [t["quantidadeSinistrosGraves"] for t in corpo["trechos"]]
    assert quantidades == sorted(quantidades, reverse=True)
    assert set(corpo["trechos"][0]) == {
        "posicao",
        "logradouro",
        "latitude",
        "longitude",
        "quantidadeSinistrosGraves",
    }


@pytest.mark.parametrize("limite", [1, 2, 50])
def test_trechos_limite_valido_devolve_essa_quantidade(
    criar_cliente: Callable[..., TestClient], limite: int
) -> None:
    # Act
    resposta = criar_cliente().get(ROTA_TRECHOS, params={"limite": limite})

    # Assert
    assert resposta.status_code == 200
    assert len(resposta.json()["trechos"]) == limite


@pytest.mark.parametrize(
    ("limite", "mensagem"),
    [
        ("0", "Deve ser maior ou igual a 1."),
        ("51", "Deve ser menor ou igual a 50."),
        ("abc", "Deve ser um número inteiro."),
    ],
)
def test_trechos_limite_invalido_400_problem(
    criar_cliente: Callable[..., TestClient], limite: str, mensagem: str
) -> None:
    # Act
    resposta = criar_cliente().get(ROTA_TRECHOS, params={"limite": limite})

    # Assert
    assert resposta.status_code == 400
    assert resposta.headers["content-type"] == TIPO_PROBLEMA
    assert resposta.json()["errors"] == {"limite": [mensagem]}


def test_trechos_repositorio_falha_503_problem(
    cenario: Cenario, criar_cliente: Callable[..., TestClient]
) -> None:
    # Arrange
    cenario.repositorio_trechos = RepositorioTrechosMemoria(None, falhar=True)

    # Act
    resposta = criar_cliente().get(ROTA_TRECHOS)

    # Assert
    assert resposta.status_code == 503
    assert resposta.headers["content-type"] == TIPO_PROBLEMA


def test_trechos_sem_carga_503_problem(
    cenario: Cenario, criar_cliente: Callable[..., TestClient]
) -> None:
    # Arrange
    cenario.repositorio_trechos = RepositorioTrechosMemoria(None)

    # Act
    resposta = criar_cliente().get(ROTA_TRECHOS)

    # Assert
    assert resposta.status_code == 503
    assert "Nenhum conjunto" in resposta.json()["detail"]


def test_saude_tudo_ok_200_healthy(criar_cliente: Callable[..., TestClient]) -> None:
    # Act
    resposta = criar_cliente().get(ROTA_SAUDE)

    # Assert
    assert resposta.status_code == 200
    assert resposta.json() == {"status": "Healthy"}


def test_saude_banco_fora_503_unhealthy(
    cenario: Cenario, criar_cliente: Callable[..., TestClient]
) -> None:
    # Arrange
    cenario.banco_ok = False

    # Act
    resposta = criar_cliente().get(ROTA_SAUDE)

    # Assert
    assert resposta.status_code == 503
    assert resposta.json() == {"status": "Unhealthy"}


def test_saude_modelo_nao_carregado_503_unhealthy(
    cenario: Cenario, criar_cliente: Callable[..., TestClient]
) -> None:
    # Arrange
    cenario.previsor = None

    # Act
    resposta = criar_cliente().get(ROTA_SAUDE)

    # Assert
    assert resposta.status_code == 503
    assert resposta.json() == {"status": "Unhealthy"}
