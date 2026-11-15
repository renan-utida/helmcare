"""Repositórios em memória, com opção de simular falha do banco."""

from app.dominio.calculadora_indice import RegistroCalculoIndice
from app.dominio.trecho_critico import ConjuntoTrechosCriticos, TrechoCritico


class FalhaBancoFalsa(Exception):
    """Simula um erro do banco (no lugar de um erro do SQLAlchemy)."""


def criar_conjunto_trechos(quantidade: int = 100) -> ConjuntoTrechosCriticos:
    """Conjunto válido com quantidades decrescentes (100, 99, 98...)."""
    return ConjuntoTrechosCriticos(
        periodo="2019 a 2025",
        fonte="Infosiga SP",
        criterio="teste",
        trechos=tuple(
            TrechoCritico(
                posicao=i,
                logradouro=f"AV TESTE {i}",
                latitude=-23.5,
                longitude=-46.6,
                quantidade_sinistros_graves=quantidade + 1 - i,
            )
            for i in range(1, quantidade + 1)
        ),
    )


class RepositorioTrechosMemoria:
    """Implementa RepositorioTrechosCriticos com um conjunto fixo (ou nenhum)."""

    def __init__(self, conjunto: ConjuntoTrechosCriticos | None, falhar: bool = False) -> None:
        """conjunto None simula banco sem carga; falhar=True simula banco fora do ar."""
        self._conjunto = conjunto
        self._falhar = falhar
        self.limites_pedidos: list[int] = []

    def obter_mais_recentes(self, limite: int) -> ConjuntoTrechosCriticos | None:
        """Devolve os primeiros trechos, ou lança FalhaBancoFalsa."""
        self.limites_pedidos.append(limite)
        if self._falhar:
            raise FalhaBancoFalsa("banco fora do ar")
        if self._conjunto is None:
            return None
        return self._conjunto.primeiros(limite)


class RepositorioCalculosMemoria:
    """Implementa RepositorioCalculos guardando os registros numa lista."""

    def __init__(self, falhar: bool = False) -> None:
        """falhar=True simula a gravação do histórico falhando."""
        self._falhar = falhar
        self.registros: list[RegistroCalculoIndice] = []

    def salvar(self, registro: RegistroCalculoIndice) -> None:
        """Guarda o registro, ou lança FalhaBancoFalsa."""
        if self._falhar:
            raise FalhaBancoFalsa("falha ao gravar")
        self.registros.append(registro)
