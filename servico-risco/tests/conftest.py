"""Fixtures compartilhadas pelos testes.

Os testes do domínio leem a lista de feriados direto do JSON (é um arquivo local e
pequeno), sem passar pela infraestrutura, para o domínio ser testado sozinho.
"""

import json
from datetime import UTC, date, datetime
from pathlib import Path

import pytest

from app.config import MODELO_DIR_PADRAO
from app.dominio.calendario import CalendarioFeriados

# Exemplo de referência do contrato (seção 8).
INICIO_REFERENCIA = datetime(2026, 11, 12, 20, 0, tzinfo=UTC)
FIM_REFERENCIA = datetime(2026, 11, 13, 0, 0, tzinfo=UTC)
PREVISOES_REFERENCIA = [0.26925, 0.27508, 0.28257, 0.25319]
P95_REFERENCIA = 0.322927


@pytest.fixture(scope="session")
def diretorio_modelo() -> Path:
    """Diretório real dos artefatos (ml/modelo/ do repositório)."""
    return MODELO_DIR_PADRAO


@pytest.fixture(scope="session")
def datas_feriados_lista(diretorio_modelo: Path) -> set[date]:
    """Datas de feriados.datas de metadados_modelo.json (2019 a 2030)."""
    caminho = diretorio_modelo / "metadados_modelo.json"
    metadados = json.loads(caminho.read_text(encoding="utf-8"))
    return {date.fromisoformat(item["data"]) for item in metadados["feriados"]["datas"]}


@pytest.fixture(scope="session")
def calendario(datas_feriados_lista: set[date]) -> CalendarioFeriados:
    """Calendário real de feriados, montado com a lista dos metadados."""
    return CalendarioFeriados(datas_feriados_lista)
