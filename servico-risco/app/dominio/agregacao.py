"""Agregação das horas do turno no componente sinistro (seção 8.1 do contrato).

Cada hora é normalizada pelo P95 (100 * previsao / p95, limitado a 100, sem
arredondar). O componente é a média ponderada desses valores pelo tempo que cada hora
fica dentro do turno, com um único arredondamento no fim (0,5 para cima). O passo 6
de metadados_modelo.json (arredondar cada hora) não é usado.
"""

from collections.abc import Sequence
from dataclasses import dataclass

from app.dominio.componentes import arredondar_meio_para_cima, normalizar_sinistro_hora
from app.dominio.excecoes import ValidacaoDominioError
from app.dominio.horas_turno import HoraTurno

# Nome fixo da regra, gravado no histórico de cálculos (coluna regra_agregacao).
# O contrato fala em minutos; o peso em segundos é proporcional e dá a mesma média.
REGRA_AGREGACAO = "MEDIA_PONDERADA_MINUTOS_V1"


@dataclass(frozen=True)
class SinistroHora:
    """Resultado de uma hora: a previsão do modelo e o valor normalizado, sem arredondar."""

    hora: HoraTurno
    previsao: float
    componente: float


@dataclass(frozen=True)
class ResultadoSinistro:
    """Resultado da agregação: as horas, a média sem arredondar e o componente final."""

    horas: tuple[SinistroHora, ...]
    media: float
    componente: int

    @property
    def hora_pico(self) -> SinistroHora:
        """Hora de maior valor normalizado (a primeira delas, em caso de empate)."""
        # max() devolve o primeiro elemento máximo, o que já resolve o empate.
        return max(self.horas, key=lambda h: h.componente)


def media_ponderada(valores: Sequence[float], pesos: Sequence[float]) -> float:
    """Média ponderada: soma de valor * peso dividida pela soma dos pesos."""
    if len(valores) != len(pesos):
        raise ValidacaoDominioError("Valores e pesos devem ter o mesmo tamanho.")
    soma_pesos = sum(pesos)
    if soma_pesos <= 0:
        raise ValidacaoDominioError("A soma dos pesos deve ser maior que zero.")
    return sum(v * p for v, p in zip(valores, pesos, strict=True)) / soma_pesos


def agregar_sinistro(
    horas: Sequence[HoraTurno], previsoes: Sequence[float], p95: float
) -> ResultadoSinistro:
    """Calcula o componente sinistro do turno a partir das previsões de cada hora.

    previsoes[i] é a previsão do modelo para horas[i]. O peso de cada hora é o
    número de segundos dela dentro do turno.
    """
    if not horas:
        raise ValidacaoDominioError("O turno precisa ter pelo menos uma hora.")
    if len(horas) != len(previsoes):
        raise ValidacaoDominioError("É preciso uma previsão para cada hora do turno.")

    por_hora = tuple(
        SinistroHora(hora=h, previsao=p, componente=normalizar_sinistro_hora(p, p95))
        for h, p in zip(horas, previsoes, strict=True)
    )
    media = media_ponderada(
        [h.componente for h in por_hora], [h.hora.segundos_no_turno for h in por_hora]
    )
    return ResultadoSinistro(
        horas=por_hora, media=media, componente=arredondar_meio_para_cima(media)
    )
