"""Testes dos componentes do índice e do arredondamento com 0,5 para cima."""

import pytest

from app.dominio.componentes import (
    Componentes,
    arredondar_meio_para_cima,
    calcular_componente_calor,
    calcular_componente_chuva,
    limitar,
    normalizar_sinistro_hora,
)
from app.dominio.excecoes import ValidacaoDominioError


@pytest.mark.parametrize(
    ("valor", "esperado"),
    [(64.5, 65), (83.5, 84), (2.5, 3), (0.5, 1), (64.4, 64), (83.62, 84), (0.0, 0), (100.0, 100)],
)
def test_arredondar_meio_para_cima_valores_diversos_arredonda_meio_para_cima(
    valor: float, esperado: int
) -> None:
    # Act
    resultado = arredondar_meio_para_cima(valor)

    # Assert
    assert resultado == esperado


def test_arredondar_meio_para_cima_ruido_de_ponto_flutuante_ainda_sobe() -> None:
    # Arrange: 64.5 com erro de representação logo abaixo.
    valor = 64.49999999999999

    # Act
    resultado = arredondar_meio_para_cima(valor)

    # Assert: o round() do Python daria 64 (bancário e sem tolerância).
    assert resultado == 65
    assert round(64.5) == 64


@pytest.mark.parametrize(
    ("chuva", "esperado"), [(0.0, 0), (5.0, 50), (7.0, 70), (10.0, 100), (200.0, 100)]
)
def test_calcular_componente_chuva_limites_valor_esperado(chuva: float, esperado: int) -> None:
    # Act
    resultado = calcular_componente_chuva(chuva)

    # Assert
    assert resultado == esperado


@pytest.mark.parametrize(
    ("indice_calor", "esperado"),
    [(20.0, 0), (24.7, 0), (27.0, 0), (34.0, 50), (39.8, 91), (41.0, 100), (48.0, 100)],
)
def test_calcular_componente_calor_limites_valor_esperado(
    indice_calor: float, esperado: int
) -> None:
    # Act
    resultado = calcular_componente_calor(indice_calor)

    # Assert
    assert resultado == esperado


def test_normalizar_sinistro_hora_previsao_igual_p95_vale_100() -> None:
    # Act
    resultado = normalizar_sinistro_hora(0.322927, 0.322927)

    # Assert
    assert resultado == pytest.approx(100.0)


def test_normalizar_sinistro_hora_previsao_acima_p95_limita_em_100() -> None:
    # Act
    resultado = normalizar_sinistro_hora(0.5, 0.322927)

    # Assert
    assert resultado == 100.0


def test_normalizar_sinistro_hora_sem_arredondar_mantem_decimais() -> None:
    # Act
    resultado = normalizar_sinistro_hora(0.26925, 0.322927)

    # Assert: 100 * 0,26925 / 0,322927 = 83,37798..., sem arredondamento por hora.
    assert resultado == pytest.approx(83.37798, abs=1e-5)


def test_normalizar_sinistro_hora_p95_zero_lanca_validacao() -> None:
    # Act e Assert
    with pytest.raises(ValidacaoDominioError):
        normalizar_sinistro_hora(0.3, 0.0)


def test_limitar_fora_da_faixa_fica_nos_extremos() -> None:
    # Act e Assert
    assert limitar(-5.0) == 0
    assert limitar(150.0) == 100
    assert limitar(42.0) == 42.0


@pytest.mark.parametrize("valor", [-1, 101])
def test_componentes_valor_fora_de_0_a_100_lanca_validacao(valor: int) -> None:
    # Act e Assert
    with pytest.raises(ValidacaoDominioError):
        Componentes(sinistro=valor, chuva=0, calor=0)
