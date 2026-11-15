"""Testa a regra das camadas: o domínio não importa framework, banco, ML nem configuração."""

import ast
from pathlib import Path

import pytest

DIRETORIO_DOMINIO = Path(__file__).resolve().parents[2] / "app" / "dominio"
MODULOS_PROIBIDOS = ("fastapi", "pydantic", "sqlalchemy", "xgboost", "numpy", "app.config")
ARQUIVOS_DOMINIO = sorted(DIRETORIO_DOMINIO.glob("*.py"))


def modulos_importados(arquivo: Path) -> set[str]:
    """Lista os módulos importados por um arquivo, lendo a árvore sintática do código."""
    arvore = ast.parse(arquivo.read_text(encoding="utf-8"))
    modulos: set[str] = set()
    for no in ast.walk(arvore):
        if isinstance(no, ast.Import):
            modulos.update(alias.name for alias in no.names)
        elif isinstance(no, ast.ImportFrom) and no.module:
            modulos.add(no.module)
    return modulos


@pytest.mark.parametrize("arquivo", ARQUIVOS_DOMINIO, ids=lambda a: a.name)
def test_dominio_imports_sem_modulos_proibidos(arquivo: Path) -> None:
    # Act
    importados = modulos_importados(arquivo)

    # Assert
    proibidos = {
        m for m in importados for p in MODULOS_PROIBIDOS if m == p or m.startswith(p + ".")
    }
    assert not proibidos, f"{arquivo.name} importa {proibidos}"


def test_dominio_arquivos_encontrados() -> None:
    # Assert: protege o teste acima de passar sem olhar nenhum arquivo.
    assert len(ARQUIVOS_DOMINIO) >= 11
