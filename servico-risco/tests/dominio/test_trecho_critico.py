"""Testes dos trechos críticos: validações, limite e regras da carga."""

import pytest

from app.dominio.excecoes import ValidacaoDominioError
from app.dominio.trecho_critico import (
    LIMITE_PADRAO,
    ConjuntoTrechosCriticos,
    TrechoCritico,
    validar_carga,
    validar_limite,
)


def criar_trecho(posicao: int, quantidade: int) -> TrechoCritico:
    """Cria um trecho válido com a posição e a quantidade indicadas."""
    return TrechoCritico(
        posicao=posicao,
        logradouro="SP 270 (RODOVIA RAPOSO TAVARES)",
        latitude=-23.58341,
        longitude=-46.77771,
        quantidade_sinistros_graves=quantidade,
    )


def criar_conjunto(quantidade_trechos: int) -> ConjuntoTrechosCriticos:
    """Cria um conjunto válido com quantidades decrescentes."""
    trechos = tuple(criar_trecho(i, 200 - i) for i in range(1, quantidade_trechos + 1))
    return ConjuntoTrechosCriticos(
        periodo="2019 a 2025", fonte="Infosiga SP", criterio="teste", trechos=trechos
    )


@pytest.mark.parametrize(
    "argumentos",
    [
        {"posicao": 0},
        {"quantidade_sinistros_graves": -1},
        {"logradouro": "  "},
        {"latitude": -91.0},
        {"longitude": 181.0},
    ],
)
def test_trecho_critico_campo_invalido_lanca_validacao(argumentos: dict[str, object]) -> None:
    # Arrange
    dados: dict[str, object] = {
        "posicao": 1,
        "logradouro": "AV TESTE",
        "latitude": -23.5,
        "longitude": -46.6,
        "quantidade_sinistros_graves": 10,
    } | argumentos

    # Act e Assert
    with pytest.raises(ValidacaoDominioError):
        TrechoCritico(**dados)


def test_conjunto_posicoes_fora_de_sequencia_lanca_validacao() -> None:
    # Act e Assert
    with pytest.raises(ValidacaoDominioError, match="sequência"):
        ConjuntoTrechosCriticos("p", "f", "c", (criar_trecho(1, 10), criar_trecho(3, 9)))


def test_conjunto_quantidade_crescente_lanca_validacao() -> None:
    # Act e Assert
    with pytest.raises(ValidacaoDominioError, match="decrescente"):
        ConjuntoTrechosCriticos("p", "f", "c", (criar_trecho(1, 10), criar_trecho(2, 11)))


def test_conjunto_quantidades_iguais_e_valido() -> None:
    # Act
    conjunto = ConjuntoTrechosCriticos("p", "f", "c", (criar_trecho(1, 10), criar_trecho(2, 10)))

    # Assert
    assert len(conjunto.trechos) == 2


def test_conjunto_primeiros_limite_padrao_10_trechos_em_ordem() -> None:
    # Arrange
    conjunto = criar_conjunto(100)

    # Act
    resultado = conjunto.primeiros(LIMITE_PADRAO)

    # Assert
    assert [t.posicao for t in resultado.trechos] == list(range(1, 11))
    assert resultado.periodo == "2019 a 2025"


@pytest.mark.parametrize("limite", [0, 51])
def test_validar_limite_fora_de_1_a_50_lanca_validacao(limite: int) -> None:
    # Act e Assert
    with pytest.raises(ValidacaoDominioError):
        validar_limite(limite)


@pytest.mark.parametrize("limite", [1, 50])
def test_validar_limite_extremos_passam(limite: int) -> None:
    # Act e Assert
    assert validar_limite(limite) == limite


def test_validar_carga_quantidade_declarada_diferente_lanca_validacao() -> None:
    # Act e Assert
    with pytest.raises(ValidacaoDominioError, match="quantidadeTrechos"):
        validar_carga(criar_conjunto(60), quantidade_declarada=100)


def test_validar_carga_menos_de_50_trechos_lanca_validacao() -> None:
    # Act e Assert
    with pytest.raises(ValidacaoDominioError, match="50"):
        validar_carga(criar_conjunto(49), quantidade_declarada=49)


def test_validar_carga_100_trechos_passa() -> None:
    # Act e Assert (não lança exceção)
    validar_carga(criar_conjunto(100), quantidade_declarada=100)
