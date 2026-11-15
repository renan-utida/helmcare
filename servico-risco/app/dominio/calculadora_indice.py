"""Cálculo do Índice de Risco do Turno (seção 8 do contrato).

índice = 0,5 * sinistro + 0,25 * chuva + 0,25 * calor, sobre os componentes já
arredondados, arredondado uma vez (0,5 para cima) e limitado a 0..100.
"""

from collections.abc import Sequence
from dataclasses import dataclass
from datetime import datetime
from uuid import UUID

from app.dominio.agregacao import REGRA_AGREGACAO, ResultadoSinistro, agregar_sinistro
from app.dominio.componentes import (
    Componentes,
    arredondar_meio_para_cima,
    calcular_componente_calor,
    calcular_componente_chuva,
    limitar,
)
from app.dominio.excecoes import ValidacaoDominioError
from app.dominio.explicacao import gerar_explicacao
from app.dominio.faixa_risco import FaixaRisco, classificar_faixa
from app.dominio.horas_turno import HoraTurno
from app.dominio.indice_calor import calcular_c

PESO_SINISTRO = 0.5
PESO_CHUVA = 0.25
PESO_CALOR = 0.25

# Faixas aceitas para a previsão do tempo (regras da requisição, seção 8 do contrato).
CHUVA_MINIMA_MM_HORA, CHUVA_MAXIMA_MM_HORA = 0.0, 200.0
TEMPERATURA_MINIMA_C, TEMPERATURA_MAXIMA_C = -10.0, 50.0
UMIDADE_MINIMA_PCT, UMIDADE_MAXIMA_PCT = 0.0, 100.0


@dataclass(frozen=True)
class PrevisaoTempo:
    """Previsão do tempo para o turno inteiro (uma só, como no contrato)."""

    chuva_mm_hora: float
    temperatura_c: float
    umidade_pct: float

    def __post_init__(self) -> None:
        """Valida as faixas: chuva de 0 a 200, temperatura de -10 a 50 e umidade de 0 a 100."""
        if not CHUVA_MINIMA_MM_HORA <= self.chuva_mm_hora <= CHUVA_MAXIMA_MM_HORA:
            raise ValidacaoDominioError("chuvaMmHora deve ficar entre 0 e 200.")
        if not TEMPERATURA_MINIMA_C <= self.temperatura_c <= TEMPERATURA_MAXIMA_C:
            raise ValidacaoDominioError("temperaturaC deve ficar entre -10 e 50.")
        if not UMIDADE_MINIMA_PCT <= self.umidade_pct <= UMIDADE_MAXIMA_PCT:
            raise ValidacaoDominioError("umidadePct deve ficar entre 0 e 100.")


@dataclass(frozen=True)
class ResultadoIndice:
    """Resultado completo do cálculo.

    A resposta da API usa só indice_risco, faixa_risco, componentes e explicacao. O
    índice de calor e o detalhe das horas ficam aqui para o histórico dos cálculos.
    """

    indice_risco: int
    faixa_risco: FaixaRisco
    componentes: Componentes
    explicacao: str
    indice_calor_c: float
    sinistro: ResultadoSinistro


@dataclass(frozen=True)
class RegistroCalculoIndice:
    """Um cálculo para gravar no histórico, sem nenhum dado pessoal."""

    id: UUID
    calculado_em: datetime
    inicio: datetime
    fim: datetime
    previsao_tempo: PrevisaoTempo
    resultado: ResultadoIndice
    versao_modelo: str
    regra_agregacao: str = REGRA_AGREGACAO


def calcular_indice(componentes: Componentes) -> int:
    """Combina os componentes arredondados no índice de 0 a 100.

    Exemplo do contrato: 80, 70 e 30 dão 40 + 17,5 + 7,5 = 65.
    """
    bruto = (
        PESO_SINISTRO * componentes.sinistro
        + PESO_CHUVA * componentes.chuva
        + PESO_CALOR * componentes.calor
    )
    return arredondar_meio_para_cima(limitar(bruto))


def calcular_resultado_indice(
    horas: Sequence[HoraTurno],
    previsoes: Sequence[float],
    p95: float,
    previsao_tempo: PrevisaoTempo,
) -> ResultadoIndice:
    """Calcula o resultado do turno a partir das horas, das previsões do modelo e do tempo.

    Função pura: quem chama o modelo é o serviço de aplicação (ou o teste), que
    passa as previsões já prontas, uma para cada hora, na mesma ordem.
    """
    sinistro = agregar_sinistro(horas, previsoes, p95)
    indice_calor_c = calcular_c(previsao_tempo.temperatura_c, previsao_tempo.umidade_pct)
    componentes = Componentes(
        sinistro=sinistro.componente,
        chuva=calcular_componente_chuva(previsao_tempo.chuva_mm_hora),
        calor=calcular_componente_calor(indice_calor_c),
    )
    indice = calcular_indice(componentes)
    faixa = classificar_faixa(indice)
    explicacao = gerar_explicacao(faixa, componentes, sinistro.hora_pico.hora.hora, indice_calor_c)
    return ResultadoIndice(
        indice_risco=indice,
        faixa_risco=faixa,
        componentes=componentes,
        explicacao=explicacao,
        indice_calor_c=indice_calor_c,
        sinistro=sinistro,
    )
