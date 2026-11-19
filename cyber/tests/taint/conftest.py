import pytest

from helmcare_cyber.eventos.repositorio import RepositorioEventos
from helmcare_cyber.pii.categorias import Categoria, Confianca, Metodo
from helmcare_cyber.pii.motor import Achado, ValorProtegido
from helmcare_cyber.taint.guarda import GuardaTaint
from helmcare_cyber.taint.modelo import Modo


def criar_achado(
    categoria: Categoria, caminho: str = "$.campo", valor: str = "valor simulado", metodo=Metodo.NOME_CAMPO
) -> Achado:
    """Achado montado à mão, para testar a política sem depender do motor."""
    return Achado(
        caminho=caminho,
        categoria=categoria,
        metodo=metodo,
        confianca=Confianca.ALTA,
        regra_id="PII-TESTE-01",
        dv_confere=None,
        ja_mascarado=False,
        valor_mascarado="[MASCARADO]",
        _original=ValorProtegido(valor),
    )


@pytest.fixture
def repositorio() -> RepositorioEventos:
    return RepositorioEventos()


@pytest.fixture
def guarda(repositorio) -> GuardaTaint:
    return GuardaTaint(repositorio=repositorio, modo=Modo.PROTECAO)


@pytest.fixture
def guarda_auditoria(repositorio) -> GuardaTaint:
    return GuardaTaint(repositorio=repositorio, modo=Modo.AUDITORIA)
