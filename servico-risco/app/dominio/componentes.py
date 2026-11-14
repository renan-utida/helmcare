"""Componentes do índice (sinistro, chuva e calor) e o arredondamento único do serviço.

Cada componente é um inteiro de 0 a 100 (seção 8 do contrato). Todo arredondamento
do serviço passa por arredondar_meio_para_cima, para a regra ser uma só.
"""

import math
from dataclasses import dataclass

from app.dominio.excecoes import ValidacaoDominioError

COMPONENTE_MINIMO = 0
COMPONENTE_MAXIMO = 100

# Chuva: 0 mm/h vale 0 e 10 mm/h ou mais vale 100, linear entre eles.
CHUVA_MAXIMA_MM_HORA = 10.0

# Calor: índice de calor de 27 °C vale 0 e 41 °C ou mais vale 100, linear entre eles.
CALOR_MINIMO_C = 27.0
CALOR_MAXIMO_C = 41.0

# Margem contra o ruído do ponto flutuante: 0.5 * 129 deveria ser 64.5, mas uma conta
# com float pode dar 64.49999999999999, e sem a margem isso viraria 64 em vez de 65.
TOLERANCIA_ARREDONDAMENTO = 1e-9


def arredondar_meio_para_cima(valor: float) -> int:
    """Arredonda para o inteiro mais próximo, com 0,5 sempre para cima (64,5 vira 65).

    Não usa o round() do Python porque ele faz o arredondamento bancário (para o par
    mais próximo): round(64.5) dá 64 e round(2.5) dá 2, o que contraria o contrato.
    A tolerância de 1e-9 absorve o erro de representação dos floats.
    """
    return math.floor(valor + 0.5 + TOLERANCIA_ARREDONDAMENTO)


def limitar(
    valor: float, minimo: float = COMPONENTE_MINIMO, maximo: float = COMPONENTE_MAXIMO
) -> float:
    """Limita o valor ao intervalo [minimo, maximo] (padrão: 0 a 100)."""
    return max(minimo, min(maximo, valor))


def normalizar_sinistro_hora(previsao: float, p95: float) -> float:
    """Normaliza a previsão de uma hora: min(100, 100 * previsao / p95), sem arredondar.

    O valor fica sem arredondamento porque ainda entra na média ponderada do turno
    (agregacao.py), que arredonda uma única vez no fim. Previsões do modelo de Poisson
    nunca são negativas; o limite inferior 0 é só uma proteção.
    """
    if p95 <= 0:
        raise ValidacaoDominioError("O P95 dos sinistros graves deve ser maior que zero.")
    return limitar(100 * previsao / p95)


def calcular_componente_chuva(chuva_mm_hora: float) -> int:
    """Componente chuva: chuva / 10 * 100, limitado a 0..100 e arredondado uma vez."""
    return arredondar_meio_para_cima(limitar(chuva_mm_hora / CHUVA_MAXIMA_MM_HORA * 100))


def calcular_componente_calor(indice_calor_c: float) -> int:
    """Componente calor: (índice de calor - 27) / (41 - 27) * 100, limitado e arredondado."""
    proporcao = (indice_calor_c - CALOR_MINIMO_C) / (CALOR_MAXIMO_C - CALOR_MINIMO_C)
    return arredondar_meio_para_cima(limitar(proporcao * 100))


@dataclass(frozen=True)
class Componentes:
    """Os três componentes já arredondados, cada um inteiro de 0 a 100."""

    sinistro: int
    chuva: int
    calor: int

    def __post_init__(self) -> None:
        """Garante que cada componente esteja entre 0 e 100."""
        for nome, valor in (
            ("sinistro", self.sinistro),
            ("chuva", self.chuva),
            ("calor", self.calor),
        ):
            if not COMPONENTE_MINIMO <= valor <= COMPONENTE_MAXIMO:
                raise ValidacaoDominioError(
                    f"O componente {nome} deve ficar entre 0 e 100 (recebido {valor})."
                )
