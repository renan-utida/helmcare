"""Testes da leitura e validação dos artefatos de ml/modelo/."""

import json
import logging
import shutil
from pathlib import Path

import pytest

from app.infraestrutura.ml.artefatos import (
    ARQUIVO_METADADOS,
    ARQUIVO_P95,
    ArtefatoModeloError,
    carregar_artefatos,
    comparar_versoes,
    versoes_instaladas,
)


def copiar_artefatos(origem: Path, destino: Path, com_modelo: bool = True) -> None:
    """Copia os artefatos reais para um diretório temporário, opcionalmente sem o modelo."""
    shutil.copy(origem / ARQUIVO_METADADOS, destino / ARQUIVO_METADADOS)
    shutil.copy(origem / ARQUIVO_P95, destino / ARQUIVO_P95)
    if com_modelo:
        shutil.copy(origem / "modelo_sinistros_xgb.json", destino / "modelo_sinistros_xgb.json")


def test_carregar_artefatos_diretorio_real_le_p95_versao_e_ordem(diretorio_modelo: Path) -> None:
    # Act
    artefatos = carregar_artefatos(diretorio_modelo)

    # Assert
    assert artefatos.p95 == pytest.approx(0.322927)
    assert artefatos.versao_modelo == "1.0.0"
    assert artefatos.ordem_variaveis == ("hora", "dia_semana", "mes", "feriado")
    assert artefatos.caminho_modelo.name == "modelo_sinistros_xgb.json"
    assert (artefatos.calendario.ano_inicial, artefatos.calendario.ano_final) == (2019, 2030)


def test_carregar_artefatos_versoes_instaladas_iguais_as_do_notebook(
    diretorio_modelo: Path,
) -> None:
    # Arrange
    artefatos = carregar_artefatos(diretorio_modelo)

    # Act
    avisos = comparar_versoes(artefatos.versoes_bibliotecas, versoes_instaladas())

    # Assert: o requirements.txt fixa as mesmas versões do notebook.
    assert avisos == []


@pytest.mark.parametrize("arquivo_faltando", [ARQUIVO_METADADOS, ARQUIVO_P95])
def test_carregar_artefatos_json_faltando_erro_claro_com_caminho(
    diretorio_modelo: Path, tmp_path: Path, arquivo_faltando: str
) -> None:
    # Arrange
    copiar_artefatos(diretorio_modelo, tmp_path)
    (tmp_path / arquivo_faltando).unlink()

    # Act e Assert
    with pytest.raises(ArtefatoModeloError, match=arquivo_faltando):
        carregar_artefatos(tmp_path)


def test_carregar_artefatos_modelo_faltando_erro_claro_com_caminho(
    diretorio_modelo: Path, tmp_path: Path
) -> None:
    # Arrange
    copiar_artefatos(diretorio_modelo, tmp_path, com_modelo=False)

    # Act e Assert
    with pytest.raises(ArtefatoModeloError, match="modelo_sinistros_xgb.json"):
        carregar_artefatos(tmp_path)


def test_carregar_artefatos_ordem_variaveis_trocada_lanca_erro(
    diretorio_modelo: Path, tmp_path: Path
) -> None:
    # Arrange
    copiar_artefatos(diretorio_modelo, tmp_path)
    caminho = tmp_path / ARQUIVO_METADADOS
    metadados = json.loads(caminho.read_text(encoding="utf-8"))
    metadados["ordemVariaveis"] = ["dia_semana", "hora", "mes", "feriado"]
    caminho.write_text(json.dumps(metadados, ensure_ascii=False), encoding="utf-8")

    # Act e Assert
    with pytest.raises(ArtefatoModeloError, match="ordemVariaveis"):
        carregar_artefatos(tmp_path)


def test_carregar_artefatos_json_invalido_lanca_erro(
    diretorio_modelo: Path, tmp_path: Path
) -> None:
    # Arrange
    copiar_artefatos(diretorio_modelo, tmp_path)
    (tmp_path / ARQUIVO_P95).write_text("{ isto não é json", encoding="utf-8")

    # Act e Assert
    with pytest.raises(ArtefatoModeloError, match="JSON inválido"):
        carregar_artefatos(tmp_path)


def test_carregar_artefatos_campo_ausente_lanca_erro(
    diretorio_modelo: Path, tmp_path: Path
) -> None:
    # Arrange
    copiar_artefatos(diretorio_modelo, tmp_path)
    (tmp_path / ARQUIVO_P95).write_text("{}", encoding="utf-8")

    # Act e Assert
    with pytest.raises(ArtefatoModeloError, match="Campo ausente"):
        carregar_artefatos(tmp_path)


def test_carregar_artefatos_p95_zero_lanca_erro(diretorio_modelo: Path, tmp_path: Path) -> None:
    # Arrange
    copiar_artefatos(diretorio_modelo, tmp_path)
    (tmp_path / ARQUIVO_P95).write_text('{"p95": 0}', encoding="utf-8")

    # Act e Assert
    with pytest.raises(ArtefatoModeloError, match="p95"):
        carregar_artefatos(tmp_path)


def test_comparar_versoes_diferentes_registra_aviso(caplog: pytest.LogCaptureFixture) -> None:
    # Arrange
    esperadas = {"xgboost": "3.4.1", "holidays": "0.108"}
    instaladas = {"xgboost": "3.5.0", "holidays": "0.108"}

    # Act
    with caplog.at_level(logging.WARNING):
        avisos = comparar_versoes(esperadas, instaladas)

    # Assert
    assert len(avisos) == 1
    assert "xgboost" in avisos[0] and "3.5.0" in avisos[0] and "3.4.1" in avisos[0]
    assert "xgboost" in caplog.text


def test_versoes_instaladas_biblioteca_ausente_informa_nao_instalada() -> None:
    # Act
    resultado = versoes_instaladas(("pacote-que-nao-existe-helmcare",))

    # Assert
    assert resultado == {"pacote-que-nao-existe-helmcare": "não instalada"}
