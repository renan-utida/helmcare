"""Testes do índice de calor da NOAA (valores de referência da seção 6.1 do contrato)."""

import pytest

from app.dominio.indice_calor import calcular_c


@pytest.mark.parametrize(
    ("temperatura", "umidade", "esperado"),
    [
        (33.4, 58, 39.8),  # referência do contrato (Rothfusz)
        (23.5, 58, 23.4),  # referência do contrato (Steadman)
        (24.0, 85, 24.7),  # exemplo de referência do índice (seção 8)
    ],
)
def test_calcular_c_valores_referencia_uma_casa_decimal(
    temperatura: float, umidade: float, esperado: float
) -> None:
    # Arrange e Act
    resultado = calcular_c(temperatura, umidade)

    # Assert
    assert resultado == pytest.approx(esperado, abs=0.05)


def test_calcular_c_umidade_muito_baixa_aplica_ajuste_de_secura() -> None:
    # Arrange: 38 °C (100,4 °F) e 10% caem no ajuste de umidade abaixo de 13%.
    temperatura, umidade = 38.0, 10.0

    # Act
    resultado = calcular_c(temperatura, umidade)

    # Assert: valor da fórmula do C# (sem o ajuste daria 35,07 °C).
    assert resultado == pytest.approx(34.727, abs=0.001)


def test_calcular_c_umidade_muito_alta_aplica_ajuste_de_umidade() -> None:
    # Arrange: 29 °C (84,2 °F) e 90% caem no ajuste de umidade acima de 85%.
    temperatura, umidade = 29.0, 90.0

    # Act
    resultado = calcular_c(temperatura, umidade)

    # Assert: valor da fórmula do C# (sem o ajuste daria 37,08 °C).
    assert resultado == pytest.approx(37.231, abs=0.001)
