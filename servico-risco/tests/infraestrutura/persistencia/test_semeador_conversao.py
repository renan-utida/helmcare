"""Testes da conversão do trechos_criticos.json (sem banco; a gravação está na integração)."""

import json
from pathlib import Path

import pytest

from app.config import Configuracoes
from app.dominio.excecoes import ValidacaoDominioError
from app.infraestrutura.persistencia import semeador_trechos
from app.infraestrutura.persistencia.semeador_trechos import (
    ARQUIVO_TRECHOS,
    calcular_hash,
    converter_conjunto,
)


@pytest.fixture(scope="module")
def dados_arquivo(diretorio_modelo: Path) -> dict[str, object]:
    """Conteúdo do trechos_criticos.json real."""
    return json.loads((diretorio_modelo / ARQUIVO_TRECHOS).read_text(encoding="utf-8"))


def test_converter_conjunto_arquivo_real_100_trechos_em_ordem(
    dados_arquivo: dict[str, object],
) -> None:
    # Act
    conjunto = converter_conjunto(dados_arquivo)

    # Assert
    assert len(conjunto.trechos) == 100
    assert conjunto.periodo == "2019 a 2025"
    assert conjunto.trechos[0].logradouro == "SP 270 (RODOVIA RAPOSO TAVARES)"
    assert conjunto.trechos[0].quantidade_sinistros_graves == 46


def test_converter_conjunto_quantidade_declarada_errada_lanca_validacao(
    dados_arquivo: dict[str, object],
) -> None:
    # Arrange
    dados = dict(dados_arquivo) | {"quantidadeTrechos": 99}

    # Act e Assert
    with pytest.raises(ValidacaoDominioError, match="quantidadeTrechos"):
        converter_conjunto(dados)


def test_converter_conjunto_campo_ausente_lanca_validacao(dados_arquivo: dict[str, object]) -> None:
    # Arrange
    dados = {k: v for k, v in dados_arquivo.items() if k != "periodo"}

    # Act e Assert
    with pytest.raises(ValidacaoDominioError, match="campo inválido"):
        converter_conjunto(dados)


def test_calcular_hash_sha256_hexadecimal_64() -> None:
    # Act
    resultado = calcular_hash(b"abc")

    # Assert: valor conhecido do sha256 de "abc".
    assert resultado == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"


def test_main_sem_url_do_banco_retorna_1(monkeypatch: pytest.MonkeyPatch) -> None:
    # Arrange: configuração sem banco, sem ler o .env real.
    monkeypatch.setattr(
        "app.config.obter_configuracoes", lambda: Configuracoes(_env_file=None, database_url=None)
    )

    # Act e Assert
    assert semeador_trechos.main() == 1
