"""Testes das faixas do índice nas fronteiras do contrato."""

import pytest

from app.dominio.excecoes import ValidacaoDominioError
from app.dominio.faixa_risco import FaixaRisco, classificar_faixa


@pytest.mark.parametrize(
    ("indice", "esperada"),
    [
        (0, FaixaRisco.BAIXO),
        (25, FaixaRisco.BAIXO),
        (26, FaixaRisco.MODERADO),
        (50, FaixaRisco.MODERADO),
        (51, FaixaRisco.ALTO),
        (75, FaixaRisco.ALTO),
        (76, FaixaRisco.MUITO_ALTO),
        (100, FaixaRisco.MUITO_ALTO),
    ],
)
def test_classificar_faixa_fronteiras_faixa_esperada(indice: int, esperada: FaixaRisco) -> None:
    # Act
    resultado = classificar_faixa(indice)

    # Assert
    assert resultado is esperada


@pytest.mark.parametrize("indice", [-1, 101])
def test_classificar_faixa_fora_de_0_a_100_lanca_validacao(indice: int) -> None:
    # Act e Assert
    with pytest.raises(ValidacaoDominioError):
        classificar_faixa(indice)


def test_faixa_risco_valores_iguais_ao_contrato() -> None:
    # Assert
    assert [f.value for f in FaixaRisco] == ["BAIXO", "MODERADO", "ALTO", "MUITO_ALTO"]


def test_faixa_risco_rotulo_minusculas_e_com_espaco() -> None:
    # Assert
    assert [f.rotulo for f in FaixaRisco] == ["baixo", "moderado", "alto", "muito alto"]
