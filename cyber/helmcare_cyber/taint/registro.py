"""Registro de taint: os valores que entraram por uma fonte, guardados só em memória.

A regex do motor de PII reconhece formatos (CPF, telefone, e-mail), mas não
reconhece um nome de pessoa nem uma alergia escrita como texto comum. O
registro resolve isso: quando um payload entra por uma fonte, os valores dos
seus achados são registrados; depois, qualquer saída que contenha um desses
valores, mesmo concatenado numa f-string, é apontada.

O registro nunca é persistido nem serializado: os valores ficam dentro de
ValorProtegido, o repr só mostra a quantidade e pickle é bloqueado.
"""

import re
import threading
import unicodedata
from dataclasses import dataclass, field

from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.pii.motor import Achado, ValorProtegido
from helmcare_cyber.pii.validadores import somente_digitos
from helmcare_cyber.taint.modelo import Fonte

TAMANHO_MINIMO = 4  # valores menores (como "O+") dariam falso positivo em qualquer texto

# Categorias procuradas pelos dígitos, com qualquer separador entre eles: o CPF
# registrado como 48291736537 é achado escrito como 482.917.365-37 e vice-versa.
CATEGORIAS_POR_DIGITOS = frozenset({Categoria.CPF, Categoria.CNH, Categoria.RG, Categoria.TELEFONE})
_SEPARADORES = r"[\s.\-/()]*"


@dataclass(frozen=True)
class Ocorrencia:
    """Um valor registrado encontrado num texto. O trecho fica protegido."""

    categoria: Categoria
    fonte: Fonte
    caminho_fonte: str
    inicio: int
    fim: int
    _trecho: ValorProtegido = field(repr=False, compare=False, default_factory=lambda: ValorProtegido(""))

    def trecho(self) -> str:
        """O texto exato que casou. Só para substituir na saída; nunca registre."""
        return self._trecho.revelar()


@dataclass(frozen=True)
class _Entrada:
    categoria: Categoria
    fonte: Fonte
    caminho: str
    padrao: re.Pattern = field(repr=False)
    tamanho: int


def _padrao_literal(valor: str) -> re.Pattern:
    """Literal sem diferenciar maiúsculas, sem casar no meio de outra palavra ou número."""
    antes = r"(?<!\w)" if re.match(r"\w", valor) else ""
    depois = r"(?!\w)" if re.search(r"\w$", valor) else ""
    return re.compile(antes + re.escape(valor) + depois, re.IGNORECASE)


def _padrao_digitos(digitos: str) -> re.Pattern:
    return re.compile(r"(?<!\d)" + _SEPARADORES.join(digitos) + r"(?!\d)")


class TaintRegistry:
    """Valores contaminados por fonte, em memória e com lock (uso seguro entre threads)."""

    def __init__(self) -> None:
        self._entradas: dict[tuple[Categoria, tuple], _Entrada] = {}
        self._lock = threading.RLock()

    def registrar(self, fonte: Fonte, achados: list[Achado]) -> int:
        """Registra os valores dos achados ainda não mascarados. Devolve quantos entraram."""
        novos = 0
        with self._lock:
            for achado in achados:
                if achado.ja_mascarado:
                    continue
                valor = unicodedata.normalize("NFKC", achado.valor_original()).strip()
                if len(valor) < TAMANHO_MINIMO:
                    continue
                # A chave separa a forma literal da forma por dígitos: um CPF que
                # chega só com dígitos precisa das duas (a segunda aceita separadores).
                formas: list[tuple[tuple, re.Pattern, int]] = [
                    (("literal", valor.casefold()), _padrao_literal(valor), len(valor))
                ]
                if achado.categoria in CATEGORIAS_POR_DIGITOS:
                    digitos = somente_digitos(valor)
                    if len(digitos) >= TAMANHO_MINIMO:
                        formas.append((("digitos", digitos), _padrao_digitos(digitos), len(digitos)))
                for chave, padrao, tamanho in formas:
                    if (achado.categoria, chave) in self._entradas:
                        continue
                    self._entradas[(achado.categoria, chave)] = _Entrada(
                        achado.categoria, fonte, achado.caminho, padrao, tamanho
                    )
                    novos += 1
        return novos

    def procurar(self, texto) -> list[Ocorrencia]:
        """Valores registrados que aparecem no texto, sem sobreposição, em ordem de posição.

        Os valores mais longos são procurados primeiro, para "Fulano Simulado"
        ganhar de um pedaço dele.
        """
        texto = str(texto)
        with self._lock:
            entradas = sorted(self._entradas.values(), key=lambda e: e.tamanho, reverse=True)
        ocupados: list[tuple[int, int]] = []
        ocorrencias: list[Ocorrencia] = []
        for entrada in entradas:
            for casamento in entrada.padrao.finditer(texto):
                inicio, fim = casamento.span()
                if any(inicio < o_fim and o_inicio < fim for o_inicio, o_fim in ocupados):
                    continue
                ocupados.append((inicio, fim))
                ocorrencias.append(
                    Ocorrencia(
                        entrada.categoria,
                        entrada.fonte,
                        entrada.caminho,
                        inicio,
                        fim,
                        ValorProtegido(casamento.group(0)),
                    )
                )
        return sorted(ocorrencias, key=lambda o: o.inicio)

    def limpar(self) -> None:
        with self._lock:
            self._entradas.clear()

    def __len__(self) -> int:
        with self._lock:
            return len(self._entradas)

    def __repr__(self) -> str:
        return f"TaintRegistry({len(self)} formas registradas)"

    def __reduce_ex__(self, protocolo):
        raise TypeError("TaintRegistry não pode ser serializado")
