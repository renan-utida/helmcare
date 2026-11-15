"""Testes da divisão do turno em horas cheias e das variáveis de cada hora."""

from datetime import UTC, datetime, timedelta, timezone

import pytest

from app.dominio.calendario import CalendarioFeriados
from app.dominio.excecoes import ValidacaoDominioError
from app.dominio.horas_turno import ORDEM_VARIAVEIS, gerar_horas_turno, validar_turno
from tests.conftest import FIM_REFERENCIA, INICIO_REFERENCIA

FUSO_MENOS_3 = timezone(timedelta(hours=-3))


def test_gerar_horas_turno_exemplo_referencia_horas_locais_17_a_20_de_quinta(
    calendario: CalendarioFeriados,
) -> None:
    # Act
    horas = gerar_horas_turno(INICIO_REFERENCIA, FIM_REFERENCIA, calendario)

    # Assert
    assert [h.hora for h in horas] == [17, 18, 19, 20]
    assert all(h.dia_semana == 3 for h in horas)  # 3 = quinta-feira
    assert all(h.mes == 11 and h.feriado == 0 for h in horas)
    assert all(h.segundos_no_turno == 3600 for h in horas)
    assert horas[0].inicio_utc == INICIO_REFERENCIA
    assert horas[0].inicio_local.isoformat() == "2026-11-12T17:00:00-03:00"
    assert horas[0].variaveis_modelo() == (17, 3, 11, 0)


def test_gerar_horas_turno_hora_parcial_peso_proporcional(calendario: CalendarioFeriados) -> None:
    # Arrange: das 20h30 às 22h15 UTC.
    inicio = datetime(2026, 11, 12, 20, 30, tzinfo=UTC)
    fim = datetime(2026, 11, 12, 22, 15, tzinfo=UTC)

    # Act
    horas = gerar_horas_turno(inicio, fim, calendario)

    # Assert: 30 min, 60 min e 15 min.
    assert [h.segundos_no_turno for h in horas] == [1800, 3600, 900]
    assert [h.inicio_utc.hour for h in horas] == [20, 21, 22]


def test_gerar_horas_turno_fim_na_hora_cheia_nao_inclui_hora_seguinte(
    calendario: CalendarioFeriados,
) -> None:
    # Arrange
    inicio = datetime(2026, 11, 12, 20, 0, tzinfo=UTC)
    fim = datetime(2026, 11, 12, 22, 0, tzinfo=UTC)

    # Act
    horas = gerar_horas_turno(inicio, fim, calendario)

    # Assert
    assert len(horas) == 2


def test_gerar_horas_turno_24h_alinhado_24_horas(calendario: CalendarioFeriados) -> None:
    # Act
    horas = gerar_horas_turno(
        INICIO_REFERENCIA, INICIO_REFERENCIA + timedelta(hours=24), calendario
    )

    # Assert
    assert len(horas) == 24
    assert sum(h.segundos_no_turno for h in horas) == 86400


def test_gerar_horas_turno_24h_desalinhado_25_horas(calendario: CalendarioFeriados) -> None:
    # Arrange
    inicio = datetime(2026, 11, 12, 20, 30, tzinfo=UTC)

    # Act
    horas = gerar_horas_turno(inicio, inicio + timedelta(hours=24), calendario)

    # Assert
    assert len(horas) == 25
    assert horas[0].segundos_no_turno == 1800
    assert horas[-1].segundos_no_turno == 1800
    assert sum(h.segundos_no_turno for h in horas) == 86400


def test_gerar_horas_turno_fuso_menos_3_normaliza_para_utc(calendario: CalendarioFeriados) -> None:
    # Arrange: o mesmo turno do exemplo, escrito em -03:00.
    inicio = datetime(2026, 11, 12, 17, 0, tzinfo=FUSO_MENOS_3)
    fim = datetime(2026, 11, 12, 21, 0, tzinfo=FUSO_MENOS_3)

    # Act
    horas = gerar_horas_turno(inicio, fim, calendario)

    # Assert
    assert horas[0].inicio_utc == INICIO_REFERENCIA
    assert horas[0].inicio_utc.tzinfo is UTC
    assert [h.hora for h in horas] == [17, 18, 19, 20]


def test_gerar_horas_turno_feriado_local_marca_feriado(calendario: CalendarioFeriados) -> None:
    # Arrange: 20/11/2026 às 10h locais (13h UTC).
    inicio = datetime(2026, 11, 20, 13, 0, tzinfo=UTC)

    # Act
    horas = gerar_horas_turno(inicio, inicio + timedelta(hours=1), calendario)

    # Assert
    assert horas[0].feriado == 1
    assert horas[0].dia_semana == 4  # sexta-feira


def test_gerar_horas_turno_meia_noite_utc_usa_data_local_anterior(
    calendario: CalendarioFeriados,
) -> None:
    # Arrange: 21/11/2026 às 01h UTC ainda é 20/11 (feriado) às 22h locais.
    inicio = datetime(2026, 11, 21, 1, 0, tzinfo=UTC)

    # Act
    horas = gerar_horas_turno(inicio, inicio + timedelta(hours=1), calendario)

    # Assert
    assert horas[0].hora == 22
    assert horas[0].feriado == 1


@pytest.mark.parametrize("campo_sem_fuso", ["inicio", "fim"])
def test_validar_turno_data_sem_fuso_lanca_validacao(campo_sem_fuso: str) -> None:
    # Arrange
    inicio, fim = INICIO_REFERENCIA, FIM_REFERENCIA
    if campo_sem_fuso == "inicio":
        inicio = inicio.replace(tzinfo=None)
    else:
        fim = fim.replace(tzinfo=None)

    # Act e Assert
    with pytest.raises(ValidacaoDominioError, match=campo_sem_fuso):
        validar_turno(inicio, fim)


@pytest.mark.parametrize("deslocamento", [timedelta(0), timedelta(hours=-1)])
def test_validar_turno_fim_menor_ou_igual_ao_inicio_lanca_validacao(
    deslocamento: timedelta,
) -> None:
    # Act e Assert
    with pytest.raises(ValidacaoDominioError, match="depois do início"):
        validar_turno(INICIO_REFERENCIA, INICIO_REFERENCIA + deslocamento)


def test_validar_turno_mais_de_24h_lanca_validacao() -> None:
    # Act e Assert
    with pytest.raises(ValidacaoDominioError, match="24 horas"):
        validar_turno(INICIO_REFERENCIA, INICIO_REFERENCIA + timedelta(hours=24, seconds=1))


def test_validar_turno_exatamente_24h_passa() -> None:
    # Act
    inicio, fim = validar_turno(INICIO_REFERENCIA, INICIO_REFERENCIA + timedelta(hours=24))

    # Assert
    assert fim - inicio == timedelta(hours=24)


def test_ordem_variaveis_igual_ao_contrato() -> None:
    # Assert
    assert ORDEM_VARIAVEIS == ("hora", "dia_semana", "mes", "feriado")
