"""Faixas do Índice de Risco do Turno (seção 8 do contrato).

0 a 25 BAIXO, 26 a 50 MODERADO, 51 a 75 ALTO e 76 a 100 MUITO_ALTO.
"""

from enum import StrEnum

from app.dominio.excecoes import ValidacaoDominioError

INDICE_MINIMO = 0
INDICE_MAXIMO = 100

# Limite superior (inclusive) de cada faixa, na ordem crescente.
LIMITE_BAIXO = 25
LIMITE_MODERADO = 50
LIMITE_ALTO = 75


class FaixaRisco(StrEnum):
    """Faixa do índice, com os valores do enum exatamente como no contrato."""

    BAIXO = "BAIXO"
    MODERADO = "MODERADO"
    ALTO = "ALTO"
    MUITO_ALTO = "MUITO_ALTO"

    @property
    def rotulo(self) -> str:
        """Nome da faixa para o texto da explicação: minúsculas e com espaço ("muito alto")."""
        return self.value.lower().replace("_", " ")


def classificar_faixa(indice: int) -> FaixaRisco:
    """Devolve a faixa de um índice inteiro de 0 a 100.

    Lança ValidacaoDominioError se o índice estiver fora de 0 a 100.
    """
    if not INDICE_MINIMO <= indice <= INDICE_MAXIMO:
        raise ValidacaoDominioError(
            f"O índice de risco deve ficar entre 0 e 100 (recebido {indice})."
        )
    if indice <= LIMITE_BAIXO:
        return FaixaRisco.BAIXO
    if indice <= LIMITE_MODERADO:
        return FaixaRisco.MODERADO
    if indice <= LIMITE_ALTO:
        return FaixaRisco.ALTO
    return FaixaRisco.MUITO_ALTO
