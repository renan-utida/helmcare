"""Executa o corpus rotulado de tests/corpus/corpus_pii.json."""

from collections import Counter
from pathlib import Path

import pytest

from helmcare_cyber.pii import Categoria, MotorPII
from helmcare_cyber.pii.avaliacao import (
    GRUPOS_SEM_FALSO_POSITIVO,
    Metricas,
    avaliar,
    carregar_corpus,
    categorias_encontradas,
    comparar,
)

CAMINHO_CORPUS = Path(__file__).resolve().parents[1] / "corpus" / "corpus_pii.json"
CASOS = carregar_corpus(CAMINHO_CORPUS)
CASOS_NORMAIS = [c for c in CASOS if not c.get("limitacaoConhecida")]
CASOS_LIMITACAO = [c for c in CASOS if c.get("limitacaoConhecida")]


def test_tamanho_do_corpus():
    assert len(CASOS) >= 60
    assert len({c["id"] for c in CASOS}) == len(CASOS)


def test_corpus_tem_positivos_e_negativos_por_categoria():
    positivas = {cat for c in CASOS for cat in c["esperado"]}
    assert positivas == {c.value for c in Categoria}
    negativos = {c["grupo"] for c in CASOS if not c["esperado"]}
    assert GRUPOS_SEM_FALSO_POSITIVO <= negativos


@pytest.mark.parametrize("caso", CASOS_NORMAIS, ids=[c["id"] for c in CASOS_NORMAIS])
def test_caso(caso):
    assert categorias_encontradas(MotorPII(), caso["entrada"]) == Counter(caso["esperado"])


@pytest.mark.parametrize("caso", CASOS_LIMITACAO, ids=[c["id"] for c in CASOS_LIMITACAO])
def test_limitacao_conhecida_continua_valendo(caso):
    # Se um dia o motor passar a achar o nome, este teste avisa para atualizar o corpus e o README.
    assert categorias_encontradas(MotorPII(), caso["entrada"]) != Counter(caso["esperado"])


def test_avaliacao_geral():
    resultado = avaliar(CASOS)
    assert resultado.quantidade_casos == len(CASOS)
    assert resultado.falsos_positivos_protegidos == []
    assert sorted(resultado.divergentes) == sorted(c["id"] for c in CASOS_LIMITACAO)
    assert resultado.total.fp == 0
    assert resultado.por_categoria[Categoria.NOME].fn == len(CASOS_LIMITACAO)


def test_comparar_por_contagem():
    metricas = comparar(Counter({"SAUDE": 2}), Counter({"SAUDE": 3, "CPF": 1}))
    assert (metricas[Categoria.SAUDE].vp, metricas[Categoria.SAUDE].fp) == (2, 1)
    assert metricas[Categoria.CPF].fp == 1


def test_metricas():
    assert Metricas().precisao is None and Metricas().recall is None
    m = Metricas(vp=3, fp=1, fn=1)
    assert (m.precisao, m.recall) == (0.75, 0.75)


def test_falso_positivo_em_grupo_protegido_e_apontado():
    casos = [{"id": "x", "grupo": "telemetria", "entrada": {"cpf": "1"}, "esperado": []}]
    assert avaliar(casos).falsos_positivos_protegidos == ["x"]
