"""Nenhum valor simulado original aparece em evento, decisão, rastro ou arquivo JSONL.

Roda todos os cenários nos dois modos (inclusive AUDITORIA, em que a saída
emitida leva os valores sem proteção) e procura cada valor sensível em tudo o
que é serializado.
"""

import json

import pytest

from helmcare_cyber.eventos.repositorio import RepositorioEventos
from helmcare_cyber.simulacao import dados_simulados as dados
from helmcare_cyber.simulacao.cenarios import executar_todos
from helmcare_cyber.taint.guarda import GuardaTaint
from helmcare_cyber.taint.modelo import Modo


@pytest.fixture(scope="module")
def execucoes(tmp_path_factory):
    arquivo = tmp_path_factory.mktemp("eventos") / "eventos.jsonl"
    repositorio = RepositorioEventos(arquivo)
    resultados = []
    for modo in Modo:
        resultados.extend(executar_todos(GuardaTaint(repositorio=repositorio, modo=modo)))
    return resultados, repositorio, arquivo


@pytest.fixture(scope="module")
def tudo_serializado(execucoes) -> str:
    resultados, repositorio, arquivo = execucoes
    partes = [evento.para_json() for evento in repositorio.listar()]
    partes += [repr(evento) for evento in repositorio.listar()]
    for resultado in resultados:
        partes.append(repr(resultado))
        for guarda in resultado.resultados:
            partes.append(json.dumps([d.para_dict() for d in guarda.decisoes], ensure_ascii=False))
            partes.append(json.dumps(guarda.rastro.para_dict(), ensure_ascii=False))
            partes.append(guarda.rastro.para_texto())
            partes.append(repr(guarda))
    partes.append(arquivo.read_text(encoding="utf-8"))
    return "\n".join(partes)


def test_ha_eventos_dos_dois_modos(execucoes):
    _, repositorio, _ = execucoes
    assert {e.modo for e in repositorio.listar()} == set(Modo)
    assert {e.cenario for e in repositorio.listar()} == {f"C{n}" for n in range(1, 12)}


def test_a_saida_de_auditoria_tem_os_valores(execucoes):
    # Controle: o teste abaixo só vale porque os valores existem nas saídas.
    resultados, _, _ = execucoes
    saidas = " ".join(str(r.saida_emitida) for r in resultados if r.modo is Modo.AUDITORIA)
    assert dados.CPF in saidas and dados.SENHA in saidas and dados.QR_TOKEN in saidas


@pytest.mark.parametrize("valor", dados.VALORES_SENSIVEIS)
def test_valor_original_nao_aparece(tudo_serializado, valor):
    assert valor not in tudo_serializado
