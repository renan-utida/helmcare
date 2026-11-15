"""Caso de uso: calcular o Índice de Risco do Turno e gravar o histórico.

O serviço só orquestra: as regras ficam no domínio (horas do turno, agregação,
componentes, índice e explicação) e o mundo de fora entra pelas portas (previsor,
relógio e repositório). Se a gravação do histórico falhar, o cálculo é devolvido mesmo
assim e o erro vai para o log: o entregador precisa do índice mais do que o serviço
precisa do histórico (seção 8 do contrato).
"""

import logging
import time
import uuid
from datetime import datetime

from app.aplicacao.excecoes import ServicoIndisponivelError
from app.dominio.calculadora_indice import (
    PrevisaoTempo,
    RegistroCalculoIndice,
    ResultadoIndice,
    calcular_resultado_indice,
)
from app.dominio.calendario import CalendarioFeriados
from app.dominio.horas_turno import gerar_horas_turno, validar_turno
from app.dominio.portas import PrevisorSinistros, Relogio, RepositorioCalculos

logger = logging.getLogger(__name__)


class ServicoIndiceTurno:
    """Calcula o índice de um turno a partir do início, do fim e da previsão do tempo."""

    def __init__(
        self,
        previsor: PrevisorSinistros | None,
        calendario: CalendarioFeriados | None,
        repositorio: RepositorioCalculos | None,
        relogio: Relogio,
    ) -> None:
        """Recebe as dependências; previsor e calendario ficam None se o modelo não carregou.

        repositorio fica None quando o banco não está configurado: o cálculo continua, só
        o histórico deixa de ser gravado.
        """
        self._previsor = previsor
        self._calendario = calendario
        self._repositorio = repositorio
        self._relogio = relogio

    def calcular(
        self, inicio: datetime, fim: datetime, previsao_tempo: PrevisaoTempo
    ) -> ResultadoIndice:
        """Calcula o índice do turno e tenta gravar o histórico.

        Lança ValidacaoDominioError (400) para turno inválido e ServicoIndisponivelError
        (503) se o modelo não estiver carregado.
        """
        if self._previsor is None or self._calendario is None:
            raise ServicoIndisponivelError("O modelo de ML não está carregado.")

        inicio_utc, fim_utc = validar_turno(inicio, fim)
        id_calculo = uuid.uuid4()
        # perf_counter mede duração (não é relógio de parede), então não precisa da porta.
        comeco = time.perf_counter()

        horas = gerar_horas_turno(inicio_utc, fim_utc, self._calendario)
        previsoes = self._previsor.prever(horas)
        resultado = calcular_resultado_indice(horas, previsoes, self._previsor.p95, previsao_tempo)

        duracao_ms = (time.perf_counter() - comeco) * 1000
        logger.info(
            "Cálculo %s concluído em %.1f ms (%d horas).", id_calculo, duracao_ms, len(horas)
        )

        self._gravar_historico(
            RegistroCalculoIndice(
                id=id_calculo,
                calculado_em=self._relogio.agora(),
                inicio=inicio_utc,
                fim=fim_utc,
                previsao_tempo=previsao_tempo,
                resultado=resultado,
                versao_modelo=self._previsor.versao_modelo,
            )
        )
        return resultado

    def _gravar_historico(self, registro: RegistroCalculoIndice) -> None:
        """Grava o histórico; qualquer falha vira log com o id e o tipo do erro, nunca 500."""
        if self._repositorio is None:
            logger.error("Histórico do cálculo %s não gravado: banco não configurado.", registro.id)
            return
        try:
            self._repositorio.salvar(registro)
        except Exception as erro:
            # Captura ampla de propósito: a aplicação não conhece o SQLAlchemy, e nenhuma
            # falha do histórico pode derrubar a resposta. O log leva só o id e o tipo.
            logger.exception(
                "Falha ao gravar o histórico do cálculo %s (%s).", registro.id, type(erro).__name__
            )
