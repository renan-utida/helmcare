"""Leitura e validação dos artefatos do modelo em ml/modelo/ (seção 8.1 do contrato).

Lê metadados_modelo.json e p95_sinistros_graves.json, confere a ordem das variáveis
e compara as versões do xgboost e do holidays instalados com as do notebook de ML.
Versão diferente gera só um aviso no log; arquivo faltando ou inválido gera
ArtefatoModeloError com o caminho, para o erro ser claro no startup.
"""

import json
import logging
from collections.abc import Mapping
from dataclasses import dataclass
from datetime import date
from importlib.metadata import PackageNotFoundError, version
from pathlib import Path
from typing import Any

from app.dominio.calendario import CalendarioFeriados
from app.dominio.horas_turno import ORDEM_VARIAVEIS

logger = logging.getLogger(__name__)

ARQUIVO_METADADOS = "metadados_modelo.json"
ARQUIVO_P95 = "p95_sinistros_graves.json"

# Bibliotecas que afetam o resultado: o xgboost faz a previsão e o holidays decide
# os feriados fora do período da lista.
BIBLIOTECAS_CONFERIDAS: tuple[str, ...] = ("xgboost", "holidays")


class ArtefatoModeloError(Exception):
    """Artefato do modelo ausente, ilegível ou inconsistente."""


@dataclass(frozen=True)
class ArtefatosModelo:
    """O que o serviço precisa dos artefatos: caminho do modelo, P95, versão e feriados."""

    caminho_modelo: Path
    versao_modelo: str
    p95: float
    ordem_variaveis: tuple[str, ...]
    calendario: CalendarioFeriados
    versoes_bibliotecas: dict[str, str]


def ler_json(caminho: Path) -> dict[str, Any]:
    """Lê um JSON em UTF-8 e lança ArtefatoModeloError se ele faltar ou for inválido."""
    if not caminho.is_file():
        raise ArtefatoModeloError(f"Arquivo do modelo não encontrado: {caminho}")
    try:
        with open(caminho, encoding="utf-8") as arquivo:
            return json.load(arquivo)
    except json.JSONDecodeError as erro:
        raise ArtefatoModeloError(f"JSON inválido em {caminho}: {erro}") from erro


def versoes_instaladas(bibliotecas: tuple[str, ...] = BIBLIOTECAS_CONFERIDAS) -> dict[str, str]:
    """Devolve a versão instalada de cada biblioteca (ou "não instalada")."""
    resultado: dict[str, str] = {}
    for nome in bibliotecas:
        try:
            resultado[nome] = version(nome)
        except PackageNotFoundError:
            resultado[nome] = "não instalada"
    return resultado


def comparar_versoes(esperadas: Mapping[str, str], instaladas: Mapping[str, str]) -> list[str]:
    """Compara as versões e registra um aviso para cada diferença.

    Devolve as mensagens de aviso (lista vazia quando tudo bate). Só confere as
    bibliotecas presentes em "instaladas".
    """
    avisos: list[str] = []
    for nome, instalada in instaladas.items():
        esperada = esperadas.get(nome)
        if esperada != instalada:
            mensagem = (
                f"Versão de {nome} diferente da do notebook de ML: "
                f"instalada {instalada}, esperada {esperada}."
            )
            logger.warning(mensagem)
            avisos.append(mensagem)
    return avisos


def carregar_artefatos(diretorio: Path) -> ArtefatosModelo:
    """Lê os artefatos de um diretório (normalmente ml/modelo/) e valida o conteúdo."""
    metadados = ler_json(diretorio / ARQUIVO_METADADOS)
    dados_p95 = ler_json(diretorio / ARQUIVO_P95)

    try:
        ordem = tuple(metadados["ordemVariaveis"])
        caminho_modelo = diretorio / metadados["arquivoModelo"]
        versao_modelo = str(metadados["versaoModelo"])
        versoes = dict(metadados["versoesBibliotecas"])
        datas = [date.fromisoformat(item["data"]) for item in metadados["feriados"]["datas"]]
        p95 = float(dados_p95["p95"])
    except (KeyError, TypeError, ValueError) as erro:
        raise ArtefatoModeloError(f"Campo ausente ou inválido nos artefatos: {erro!r}") from erro

    # A ordem errada trocaria as colunas do modelo sem nenhum erro visível.
    if ordem != ORDEM_VARIAVEIS:
        raise ArtefatoModeloError(
            f"ordemVariaveis {list(ordem)} difere da esperada {list(ORDEM_VARIAVEIS)}."
        )
    if p95 <= 0:
        raise ArtefatoModeloError(f"O p95 deve ser maior que zero (recebido {p95}).")
    if not caminho_modelo.is_file():
        raise ArtefatoModeloError(f"Arquivo do modelo não encontrado: {caminho_modelo}")

    comparar_versoes(versoes, versoes_instaladas())

    return ArtefatosModelo(
        caminho_modelo=caminho_modelo,
        versao_modelo=versao_modelo,
        p95=p95,
        ordem_variaveis=ordem,
        calendario=CalendarioFeriados(datas),
        versoes_bibliotecas=versoes,
    )
