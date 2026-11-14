"""Testes da agregação das horas do turno no componente sinistro (média ponderada)."""

from datetime import UTC, datetime

import pytest

from app.dominio.agregacao import REGRA_AGREGACAO, agregar_sinistro, media_ponderada
from app.dominio.calendario import CalendarioFeriados
from app.dominio.excecoes import ValidacaoDominioError
from app.dominio.horas_turno import gerar_horas_turno
from tests.conftest import FIM_REFERENCIA, INICIO_REFERENCIA, P95_REFERENCIA, PREVISOES_REFERENCIA


def test_media_ponderada_pesos_diferentes_valor_esperado() -> None:
    # Arrange: (80 * 1800 + 40 * 3600) / 5400 = 53,33...
    valores, pesos = [80.0, 40.0], [1800.0, 3600.0]

    # Act
    resultado = media_ponderada(valores, pesos)

    # Assert
    assert resultado == pytest.approx(53.3333333)


def test_media_ponderada_tamanhos_diferentes_lanca_validacao() -> None:
    # Act e Assert
    with pytest.raises(ValidacaoDominioError):
        media_ponderada([1.0, 2.0], [1.0])


def test_media_ponderada_pesos_zerados_lanca_validacao() -> None:
    # Act e Assert
    with pytest.raises(ValidacaoDominioError):
        media_ponderada([1.0], [0.0])


def test_agregar_sinistro_exemplo_referencia_media_83_62_componente_84(
    calendario: CalendarioFeriados,
) -> None:
    # Arrange
    horas = gerar_horas_turno(INICIO_REFERENCIA, FIM_REFERENCIA, calendario)

    # Act
    resultado = agregar_sinistro(horas, PREVISOES_REFERENCIA, P95_REFERENCIA)

    # Assert
    assert resultado.media == pytest.approx(83.62, abs=0.01)
    assert resultado.componente == 84
    assert resultado.hora_pico.hora.hora == 19
    # Cada hora guarda o valor normalizado sem arredondar.
    assert resultado.horas[0].componente == pytest.approx(83.37798, abs=1e-5)


def test_agregar_sinistro_hora_parcial_pesa_menos(calendario: CalendarioFeriados) -> None:
    # Arrange: 15 min na primeira hora (valor 100) e 60 min na segunda (valor 50).
    inicio = datetime(2026, 11, 12, 20, 45, tzinfo=UTC)
    fim = datetime(2026, 11, 12, 22, 0, tzinfo=UTC)
    horas = gerar_horas_turno(inicio, fim, calendario)
    previsoes = [P95_REFERENCIA, P95_REFERENCIA / 2]

    # Act
    resultado = agregar_sinistro(horas, previsoes, P95_REFERENCIA)

    # Assert: (100 * 900 + 50 * 3600) / 4500 = 60; a média simples daria 75.
    assert resultado.media == pytest.approx(60.0)
    assert resultado.componente == 60


def test_agregar_sinistro_arredonda_uma_vez_so_no_fim(calendario: CalendarioFeriados) -> None:
    # Arrange: quatro horas inteiras com 64,4; 64,4; 64,4 e 64,8.
    horas = gerar_horas_turno(INICIO_REFERENCIA, FIM_REFERENCIA, calendario)
    previsoes = [p * P95_REFERENCIA for p in (0.644, 0.644, 0.644, 0.648)]

    # Act
    resultado = agregar_sinistro(horas, previsoes, P95_REFERENCIA)

    # Assert: a média sem arredondar é 64,5 e vira 65. Arredondando cada hora antes
    # (passo 6 dos metadados, não usado), seria (64 + 64 + 64 + 65) / 4 = 64,25, ou 64.
    assert resultado.media == pytest.approx(64.5)
    assert resultado.componente == 65


def test_agregar_sinistro_empate_no_pico_fica_a_primeira_hora(
    calendario: CalendarioFeriados,
) -> None:
    # Arrange
    horas = gerar_horas_turno(INICIO_REFERENCIA, FIM_REFERENCIA, calendario)

    # Act
    resultado = agregar_sinistro(horas, [0.1, 0.2, 0.2, 0.1], P95_REFERENCIA)

    # Assert
    assert resultado.hora_pico.hora.hora == 18


def test_agregar_sinistro_sem_horas_lanca_validacao() -> None:
    # Act e Assert
    with pytest.raises(ValidacaoDominioError):
        agregar_sinistro([], [], P95_REFERENCIA)


def test_agregar_sinistro_previsoes_faltando_lanca_validacao(
    calendario: CalendarioFeriados,
) -> None:
    # Arrange
    horas = gerar_horas_turno(INICIO_REFERENCIA, FIM_REFERENCIA, calendario)

    # Act e Assert
    with pytest.raises(ValidacaoDominioError):
        agregar_sinistro(horas, [0.2], P95_REFERENCIA)


def test_regra_agregacao_texto_fixo() -> None:
    # Assert
    assert REGRA_AGREGACAO == "MEDIA_PONDERADA_MINUTOS_V1"
