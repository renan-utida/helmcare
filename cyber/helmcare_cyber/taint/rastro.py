"""Rastro explicável do caminho de um dado: fonte, propagações, sanitizadores e destino.

O rastro é o que permite explicar um bloqueio: "o CPF veio do cadastro, foi
parar numa f-string e o log o recebeu; a regra TA-LOG-01 mandou redigir".
Ele só guarda valores já mascarados. Quem monta um Passo passa sempre o
valor_mascarado do Achado ou a saída de um sanitizador, nunca o original.
"""

from collections.abc import Iterable
from dataclasses import dataclass
from enum import StrEnum

from helmcare_cyber.pii.categorias import Categoria


class TipoPasso(StrEnum):
    FONTE = "FONTE"
    PROPAGACAO = "PROPAGACAO"
    SANITIZADOR = "SANITIZADOR"
    DESTINO = "DESTINO"


@dataclass(frozen=True)
class Passo:
    tipo: TipoPasso
    descricao: str
    categorias: tuple[Categoria, ...] = ()
    valor_mascarado: str | None = None

    def para_dict(self) -> dict:
        return {
            "tipo": self.tipo.value,
            "descricao": self.descricao,
            "categorias": [c.value for c in self.categorias],
            "valorMascarado": self.valor_mascarado,
        }

    def para_texto(self) -> str:
        """Uma linha legível, como: FONTE CADASTRO_ENTREGADOR POST /entregadores campo $.cpf [CPF] = ***.***.***-37."""
        linha = f"{self.tipo.value:<12} {self.descricao}"
        if self.categorias:
            linha += f" [{', '.join(c.value for c in self.categorias)}]"
        if self.valor_mascarado is not None:
            linha += f" = {self.valor_mascarado}"
        return linha


def _ordenar(categorias: Iterable[Categoria]) -> tuple[Categoria, ...]:
    """Sem repetição e na ordem do enum, para o texto sair sempre igual."""
    vistas = set(categorias)
    return tuple(c for c in Categoria if c in vistas)


class Rastro:
    """Lista ordenada de passos. Cada método devolve o próprio rastro, para encadear."""

    def __init__(self) -> None:
        self.passos: list[Passo] = []

    def _adicionar(self, tipo, descricao, categorias=(), valor_mascarado=None) -> "Rastro":
        self.passos.append(Passo(tipo, descricao, _ordenar(categorias), valor_mascarado))
        return self

    def adicionar_fonte(self, fonte, descricao: str, categorias=(), valor_mascarado: str | None = None) -> "Rastro":
        return self._adicionar(TipoPasso.FONTE, f"{fonte} {descricao}", categorias, valor_mascarado)

    def adicionar_fontes(self, fonte, achados, descricao: str = "") -> "Rastro":
        """Um passo FONTE por achado, com o caminho do campo e o valor já mascarado."""
        for achado in achados:
            texto = f"{descricao} campo {achado.caminho}" if descricao else f"campo {achado.caminho}"
            self.adicionar_fonte(fonte, texto, [achado.categoria], achado.valor_mascarado)
        return self

    def adicionar_propagacao(self, descricao: str, categorias=()) -> "Rastro":
        return self._adicionar(TipoPasso.PROPAGACAO, descricao, categorias)

    def adicionar_sanitizador(self, nome: str, categorias=(), valor_mascarado: str | None = None) -> "Rastro":
        return self._adicionar(TipoPasso.SANITIZADOR, nome, categorias, valor_mascarado)

    def adicionar_destino(self, descricao: str, categorias=(), valor_mascarado: str | None = None) -> "Rastro":
        return self._adicionar(TipoPasso.DESTINO, descricao, categorias, valor_mascarado)

    def tem_fonte(self) -> bool:
        return any(p.tipo is TipoPasso.FONTE for p in self.passos)

    def para_dict(self) -> list[dict]:
        return [p.para_dict() for p in self.passos]

    def para_texto(self) -> str:
        return "\n".join(p.para_texto() for p in self.passos)

    def __len__(self) -> int:
        return len(self.passos)

    def __repr__(self) -> str:
        return f"Rastro({len(self.passos)} passos)"
