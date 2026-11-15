"""Testes do cálculo do índice e do resultado completo do turno (com previsões fixas)."""

import pytest

from app.dominio.calculadora_indice import PrevisaoTempo, calcular_indice, calcular_resultado_indice
from app.dominio.calendario import CalendarioFeriados
from app.dominio.componentes import Componentes
from app.dominio.excecoes import ValidacaoDominioError
from app.dominio.faixa_risco import FaixaRisco
from app.dominio.horas_turno import gerar_horas_turno
from tests.conftest import FIM_REFERENCIA, INICIO_REFERENCIA, P95_REFERENCIA, PREVISOES_REFERENCIA


def test_calcular_indice_80_70_30_da_65() -> None:
    # Act
    resultado = calcular_indice(Componentes(sinistro=80, chuva=70, calor=30))

    # Assert: 40 + 17,5 + 7,5 = 65.
    assert resultado == 65


def test_calcular_indice_meio_exato_arredonda_para_cima() -> None:
    # Act: 0,5 * 83 + 0,25 * 0 + 0,25 * 0 = 41,5.
    resultado = calcular_indice(Componentes(sinistro=83, chuva=0, calor=0))

    # Assert
    assert resultado == 42


def test_calcular_indice_extremos_0_e_100() -> None:
    # Act e Assert
    assert calcular_indice(Componentes(0, 0, 0)) == 0
    assert calcular_indice(Componentes(100, 100, 100)) == 100


def test_calcular_resultado_indice_exemplo_referencia_84_70_0_60_alto(
    calendario: CalendarioFeriados,
) -> None:
    # Arrange: previsões do contrato (sem o modelo real, que é testado na camada de ML).
    horas = gerar_horas_turno(INICIO_REFERENCIA, FIM_REFERENCIA, calendario)
    tempo = PrevisaoTempo(chuva_mm_hora=7.0, temperatura_c=24.0, umidade_pct=85)

    # Act
    resultado = calcular_resultado_indice(horas, PREVISOES_REFERENCIA, P95_REFERENCIA, tempo)

    # Assert
    assert resultado.componentes == Componentes(sinistro=84, chuva=70, calor=0)
    assert resultado.indice_risco == 60
    assert resultado.faixa_risco is FaixaRisco.ALTO
    assert resultado.indice_calor_c == pytest.approx(24.7, abs=0.05)
    assert resultado.explicacao.startswith("Risco alto: ")


@pytest.mark.parametrize(
    ("chuva", "temperatura", "umidade"),
    [(-0.1, 24, 50), (200.1, 24, 50), (0, -10.1, 50), (0, 50.1, 50), (0, 24, -1), (0, 24, 100.1)],
)
def test_previsao_tempo_fora_das_faixas_lanca_validacao(
    chuva: float, temperatura: float, umidade: float
) -> None:
    # Act e Assert
    with pytest.raises(ValidacaoDominioError):
        PrevisaoTempo(chuva_mm_hora=chuva, temperatura_c=temperatura, umidade_pct=umidade)


def test_previsao_tempo_extremos_das_faixas_passam() -> None:
    # Act e Assert (não lança exceção)
    PrevisaoTempo(chuva_mm_hora=0, temperatura_c=-10, umidade_pct=0)
    PrevisaoTempo(chuva_mm_hora=200, temperatura_c=50, umidade_pct=100)
