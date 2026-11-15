"""Testes da rota POST /risco/v1/indice-turno com o TestClient."""

import copy
from collections.abc import Callable

import pytest
from fastapi.testclient import TestClient

from app.infraestrutura.ml.previsor_xgboost import PrevisorXgboost
from tests.api.conftest import CORPO_REFERENCIA, Cenario, CorpoJson
from tests.fakes.repositorios_memoria import RepositorioCalculosMemoria

ROTA = "/risco/v1/indice-turno"
TIPO_PROBLEMA = "application/problem+json"


def corpo_com(**mudancas: object) -> CorpoJson:
    """Cópia do corpo de referência com campos trocados (previsao_x muda previsao.x)."""
    corpo = copy.deepcopy(CORPO_REFERENCIA)
    for chave, valor in mudancas.items():
        if chave.startswith("previsao_"):
            previsao = corpo["previsao"]
            assert isinstance(previsao, dict)
            previsao[chave.removeprefix("previsao_")] = valor
        else:
            corpo[chave] = valor
    return corpo


def test_indice_turno_exemplo_referencia_modelo_real_84_70_0_60_alto(
    cenario: Cenario, criar_cliente: Callable[..., TestClient], previsor_real: PrevisorXgboost
) -> None:
    # Arrange: o modelo real e o histórico em memória.
    cenario.previsor = previsor_real
    cliente = criar_cliente()

    # Act
    resposta = cliente.post(ROTA, json=CORPO_REFERENCIA)

    # Assert: exatamente os campos do contrato, com os valores do exemplo da seção 8.
    assert resposta.status_code == 200
    assert resposta.json() == {
        "indiceRisco": 60,
        "faixaRisco": "ALTO",
        "componentes": {"sinistro": 84, "chuva": 70, "calor": 0},
        "explicacao": (
            "Risco alto: chuva prevista no turno e o seu horário tem histórico de sinistros "
            "graves (pico às 19h)."
        ),
    }
    assert len(cenario.repositorio_calculos.registros) == 1


def test_indice_turno_historico_falha_continua_200(
    cenario: Cenario, criar_cliente: Callable[..., TestClient]
) -> None:
    # Arrange
    cenario.repositorio_calculos = RepositorioCalculosMemoria(falhar=True)
    cliente = criar_cliente()

    # Act
    resposta = cliente.post(ROTA, json=CORPO_REFERENCIA)

    # Assert
    assert resposta.status_code == 200
    assert set(resposta.json()) == {"indiceRisco", "faixaRisco", "componentes", "explicacao"}


def test_indice_turno_modelo_nao_carregado_503_problem(
    cenario: Cenario, criar_cliente: Callable[..., TestClient]
) -> None:
    # Arrange
    cenario.previsor = None
    cliente = criar_cliente()

    # Act
    resposta = cliente.post(ROTA, json=CORPO_REFERENCIA)

    # Assert
    assert resposta.status_code == 503
    assert resposta.headers["content-type"] == TIPO_PROBLEMA
    assert resposta.json()["title"] == "Serviço indisponível"


def test_indice_turno_fuso_menos_3_e_campo_desconhecido_aceitos(
    criar_cliente: Callable[..., TestClient],
) -> None:
    # Arrange: mesmo turno em -03:00 e um campo que o contrato não tem.
    corpo = corpo_com(
        inicio="2026-11-12T17:00:00-03:00",
        fim="2026-11-12T21:00:00-03:00",
        entregadorId="nao-deve-ser-usado",
    )

    # Act
    resposta = criar_cliente().post(ROTA, json=corpo)

    # Assert
    assert resposta.status_code == 200


def test_indice_turno_exatamente_24h_aceito(criar_cliente: Callable[..., TestClient]) -> None:
    # Act
    resposta = criar_cliente().post(ROTA, json=corpo_com(fim="2026-11-13T20:00:00Z"))

    # Assert
    assert resposta.status_code == 200


@pytest.mark.parametrize(
    ("corpo", "campo", "trecho_mensagem"),
    [
        (corpo_com(inicio="2026-11-12T20:00:00"), "inicio", "fuso"),
        (corpo_com(fim="2026-11-13T00:00:00"), "fim", "fuso"),
        (corpo_com(inicio="ontem"), "inicio", "ISO 8601"),
        (corpo_com(fim="2026-11-12T20:00:00Z"), "fim", "depois do início"),
        (corpo_com(fim="2026-11-12T19:00:00Z"), "fim", "depois do início"),
        (corpo_com(fim="2026-11-13T20:00:01Z"), "fim", "24 horas"),
        (corpo_com(previsao_chuvaMmHora=-0.1), "previsao.chuvaMmHora", "maior ou igual a 0"),
        (corpo_com(previsao_chuvaMmHora=200.1), "previsao.chuvaMmHora", "menor ou igual a 200"),
        (corpo_com(previsao_temperaturaC=-10.5), "previsao.temperaturaC", "maior ou igual a -10"),
        (corpo_com(previsao_temperaturaC=50.5), "previsao.temperaturaC", "menor ou igual a 50"),
        (corpo_com(previsao_umidadePct=-1), "previsao.umidadePct", "maior ou igual a 0"),
        (corpo_com(previsao_umidadePct=101), "previsao.umidadePct", "menor ou igual a 100"),
        (corpo_com(previsao_umidadePct="muita"), "previsao.umidadePct", "número"),
        (corpo_com(previsao=None), "previsao", "objeto JSON"),
        (
            {"inicio": "2026-11-12T20:00:00Z", "fim": "2026-11-13T00:00:00Z"},
            "previsao",
            "obrigatório",
        ),
    ],
)
def test_indice_turno_entrada_invalida_400_problem_com_campo(
    criar_cliente: Callable[..., TestClient], corpo: CorpoJson, campo: str, trecho_mensagem: str
) -> None:
    # Act
    resposta = criar_cliente().post(ROTA, json=corpo)

    # Assert
    assert resposta.status_code == 400
    assert resposta.headers["content-type"] == TIPO_PROBLEMA
    problema = resposta.json()
    assert problema["status"] == 400
    assert problema["type"] == "https://tools.ietf.org/html/rfc9110#section-15.5.1"
    assert any(trecho_mensagem in m for m in problema["errors"][campo]), problema["errors"]


def test_indice_turno_json_invalido_400_sem_ecoar_corpo(
    criar_cliente: Callable[..., TestClient],
) -> None:
    # Arrange
    corpo_quebrado = '{"inicio": "SEGREDO-NAO-ECOAR", '

    # Act
    resposta = criar_cliente().post(
        ROTA, content=corpo_quebrado, headers={"content-type": "application/json"}
    )

    # Assert
    assert resposta.status_code == 400
    assert resposta.json()["errors"] == {"corpo": ["O corpo não é um JSON válido."]}
    assert "SEGREDO-NAO-ECOAR" not in resposta.text


def test_indice_turno_sem_corpo_400(criar_cliente: Callable[..., TestClient]) -> None:
    # Act
    resposta = criar_cliente().post(ROTA)

    # Assert
    assert resposta.status_code == 400
    assert resposta.json()["errors"] == {"corpo": ["Campo obrigatório."]}


def test_indice_turno_valor_invalido_nao_ecoado(criar_cliente: Callable[..., TestClient]) -> None:
    # Act
    resposta = criar_cliente().post(ROTA, json=corpo_com(previsao_umidadePct="VALOR-ESPIAO"))

    # Assert
    assert resposta.status_code == 400
    assert "VALOR-ESPIAO" not in resposta.text
