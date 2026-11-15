"""Testes do caso de uso de cálculo do índice, com previsor, relógio e repositório falsos."""

import logging
import re
from datetime import UTC, datetime, timedelta, timezone

import pytest

from app.aplicacao.excecoes import ServicoIndisponivelError
from app.aplicacao.servico_indice_turno import ServicoIndiceTurno
from app.dominio.agregacao import REGRA_AGREGACAO
from app.dominio.calculadora_indice import PrevisaoTempo
from app.dominio.calendario import CalendarioFeriados
from app.dominio.excecoes import ValidacaoDominioError
from app.dominio.faixa_risco import FaixaRisco
from tests.conftest import FIM_REFERENCIA, INICIO_REFERENCIA, PREVISOES_REFERENCIA
from tests.fakes.previsor_falso import PrevisorFalso
from tests.fakes.relogio_fixo import INSTANTE_PADRAO, RelogioFixo
from tests.fakes.repositorios_memoria import RepositorioCalculosMemoria

TEMPO_REFERENCIA = PrevisaoTempo(chuva_mm_hora=7.0, temperatura_c=24.0, umidade_pct=85)


def criar_servico(
    calendario: CalendarioFeriados,
    repositorio: RepositorioCalculosMemoria | None,
    previsor: PrevisorFalso | None = None,
) -> ServicoIndiceTurno:
    """Serviço com o previsor das previsões de referência e o relógio fixo."""
    previsor = previsor or PrevisorFalso(PREVISOES_REFERENCIA)
    return ServicoIndiceTurno(previsor, calendario, repositorio, RelogioFixo())


def test_calcular_exemplo_referencia_resultado_e_historico_gravado(
    calendario: CalendarioFeriados,
) -> None:
    # Arrange
    repositorio = RepositorioCalculosMemoria()
    servico = criar_servico(calendario, repositorio)

    # Act
    resultado = servico.calcular(INICIO_REFERENCIA, FIM_REFERENCIA, TEMPO_REFERENCIA)

    # Assert
    assert (resultado.indice_risco, resultado.faixa_risco) == (60, FaixaRisco.ALTO)
    assert len(repositorio.registros) == 1
    registro = repositorio.registros[0]
    assert registro.calculado_em == INSTANTE_PADRAO
    assert registro.versao_modelo == "falso-1.0"
    assert registro.regra_agregacao == REGRA_AGREGACAO
    assert registro.resultado is resultado
    assert len(registro.resultado.sinistro.horas) == 4


def test_calcular_fuso_menos_3_grava_inicio_e_fim_em_utc(calendario: CalendarioFeriados) -> None:
    # Arrange
    fuso = timezone(timedelta(hours=-3))
    repositorio = RepositorioCalculosMemoria()
    servico = criar_servico(calendario, repositorio)

    # Act
    servico.calcular(
        datetime(2026, 11, 12, 17, tzinfo=fuso),
        datetime(2026, 11, 12, 21, tzinfo=fuso),
        TEMPO_REFERENCIA,
    )

    # Assert
    registro = repositorio.registros[0]
    assert registro.inicio == INICIO_REFERENCIA and registro.inicio.tzinfo is UTC
    assert registro.fim == FIM_REFERENCIA


def test_calcular_historico_falha_devolve_resultado_e_registra_log(
    calendario: CalendarioFeriados, caplog: pytest.LogCaptureFixture
) -> None:
    # Arrange
    servico = criar_servico(calendario, RepositorioCalculosMemoria(falhar=True))

    # Act
    with caplog.at_level(logging.ERROR):
        resultado = servico.calcular(INICIO_REFERENCIA, FIM_REFERENCIA, TEMPO_REFERENCIA)

    # Assert: o cálculo volta normalmente, e o log tem só o id e o tipo do erro (nada da
    # requisição): a mensagem inteira precisa bater com o padrão.
    assert resultado.indice_risco == 60
    mensagens = [r.getMessage() for r in caplog.records if r.levelno >= logging.ERROR]
    assert len(mensagens) == 1
    assert re.fullmatch(
        r"Falha ao gravar o histórico do cálculo [0-9a-f-]{36} \(FalhaBancoFalsa\)\.", mensagens[0]
    )


def test_calcular_sem_repositorio_devolve_resultado_e_registra_log(
    calendario: CalendarioFeriados, caplog: pytest.LogCaptureFixture
) -> None:
    # Arrange
    servico = criar_servico(calendario, repositorio=None)

    # Act
    with caplog.at_level(logging.ERROR):
        resultado = servico.calcular(INICIO_REFERENCIA, FIM_REFERENCIA, TEMPO_REFERENCIA)

    # Assert
    assert resultado.indice_risco == 60
    assert "banco não configurado" in caplog.text


def test_calcular_sem_modelo_lanca_servico_indisponivel(calendario: CalendarioFeriados) -> None:
    # Arrange
    servico = ServicoIndiceTurno(None, None, RepositorioCalculosMemoria(), RelogioFixo())

    # Act e Assert
    with pytest.raises(ServicoIndisponivelError, match="modelo"):
        servico.calcular(INICIO_REFERENCIA, FIM_REFERENCIA, TEMPO_REFERENCIA)


def test_calcular_turno_invalido_lanca_validacao_sem_gravar(calendario: CalendarioFeriados) -> None:
    # Arrange
    repositorio = RepositorioCalculosMemoria()
    servico = criar_servico(calendario, repositorio)

    # Act e Assert
    with pytest.raises(ValidacaoDominioError):
        servico.calcular(FIM_REFERENCIA, INICIO_REFERENCIA, TEMPO_REFERENCIA)
    assert repositorio.registros == []


def test_calcular_previsor_recebe_todas_as_horas_numa_chamada(
    calendario: CalendarioFeriados,
) -> None:
    # Arrange
    previsor = PrevisorFalso(valor_padrao=0.1)
    servico = criar_servico(calendario, RepositorioCalculosMemoria(), previsor)

    # Act
    servico.calcular(INICIO_REFERENCIA, INICIO_REFERENCIA + timedelta(hours=24), TEMPO_REFERENCIA)

    # Assert
    assert len(previsor.horas_recebidas) == 24
