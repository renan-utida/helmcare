"""Trechos críticos de sinistros graves (seção 8 e 8.1 do contrato).

Os trechos vêm de trechos_criticos.json, em ordem decrescente de quantidade de
sinistros graves. As coordenadas são de vias públicas, não de pessoas.
"""

from dataclasses import dataclass

from app.dominio.excecoes import ValidacaoDominioError

LIMITE_PADRAO = 10
LIMITE_MINIMO = 1
LIMITE_MAXIMO = 50

# Mínimo de trechos numa carga, para atender qualquer ?limite= de 1 a 50.
QUANTIDADE_MINIMA_TRECHOS = LIMITE_MAXIMO


@dataclass(frozen=True)
class TrechoCritico:
    """Um trecho de via com a sua posição no ranking e a quantidade de sinistros graves."""

    posicao: int
    logradouro: str
    latitude: float
    longitude: float
    quantidade_sinistros_graves: int

    def __post_init__(self) -> None:
        """Valida posição, quantidade e coordenadas."""
        if self.posicao < 1:
            raise ValidacaoDominioError("A posição do trecho deve ser 1 ou mais.")
        if self.quantidade_sinistros_graves < 0:
            raise ValidacaoDominioError("A quantidade de sinistros graves não pode ser negativa.")
        if not self.logradouro.strip():
            raise ValidacaoDominioError("O logradouro do trecho é obrigatório.")
        if not -90 <= self.latitude <= 90 or not -180 <= self.longitude <= 180:
            raise ValidacaoDominioError("Coordenadas do trecho fora da faixa válida.")


@dataclass(frozen=True)
class ConjuntoTrechosCriticos:
    """Um conjunto de trechos com o período de referência (por exemplo, "2019 a 2025").

    Os trechos precisam estar com as posições 1, 2, 3... em sequência e em ordem
    decrescente (ou igual) de quantidade de sinistros graves.
    """

    periodo: str
    fonte: str
    criterio: str
    trechos: tuple[TrechoCritico, ...]

    def __post_init__(self) -> None:
        """Valida a sequência das posições e a ordem decrescente das quantidades."""
        for indice, trecho in enumerate(self.trechos, start=1):
            if trecho.posicao != indice:
                raise ValidacaoDominioError(
                    f"As posições dos trechos devem ser 1 a N em sequência (posição {indice})."
                )
        for anterior, atual in zip(self.trechos, self.trechos[1:], strict=False):
            if atual.quantidade_sinistros_graves > anterior.quantidade_sinistros_graves:
                raise ValidacaoDominioError(
                    "Os trechos devem estar em ordem decrescente de sinistros graves."
                )

    def primeiros(self, limite: int) -> "ConjuntoTrechosCriticos":
        """Devolve um conjunto com só os primeiros trechos, depois de validar o limite."""
        validar_limite(limite)
        return ConjuntoTrechosCriticos(
            periodo=self.periodo,
            fonte=self.fonte,
            criterio=self.criterio,
            trechos=self.trechos[:limite],
        )


def validar_limite(limite: int) -> int:
    """Confere que o ?limite= está entre 1 e 50 e o devolve."""
    if not LIMITE_MINIMO <= limite <= LIMITE_MAXIMO:
        raise ValidacaoDominioError(
            f"O limite deve ficar entre {LIMITE_MINIMO} e {LIMITE_MAXIMO} (recebido {limite})."
        )
    return limite


def validar_carga(conjunto: ConjuntoTrechosCriticos, quantidade_declarada: int) -> None:
    """Regras da carga do arquivo: quantidadeTrechos igual ao tamanho da lista e no mínimo 50."""
    if quantidade_declarada != len(conjunto.trechos):
        raise ValidacaoDominioError(
            f"quantidadeTrechos ({quantidade_declarada}) difere do tamanho da lista "
            f"({len(conjunto.trechos)})."
        )
    if len(conjunto.trechos) < QUANTIDADE_MINIMA_TRECHOS:
        raise ValidacaoDominioError(
            f"A carga precisa de pelo menos {QUANTIDADE_MINIMA_TRECHOS} trechos."
        )
