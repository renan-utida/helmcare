"""Todos os eventos dos 11 cenários nos dois modos: nada sensível vai ao LLM nem volta na mensagem."""

import json

import pytest

from helmcare_cyber.eventos.repositorio import RepositorioEventos
from helmcare_cyber.ia.prompt import PROMPT_SISTEMA, montar_mensagem_usuario
from helmcare_cyber.ia.reserva import OrigemMensagem
from helmcare_cyber.ia.tradutor import TradutorAlertas
from helmcare_cyber.risco.risco_cidadao import CorSemaforo, calcular_risco
from helmcare_cyber.simulacao import dados_simulados as dados
from helmcare_cyber.simulacao.cenarios import executar_todos
from helmcare_cyber.taint.guarda import GuardaTaint
from helmcare_cyber.taint.modelo import Modo
from tests.ia.conftest import CHAVE_FALSA, TransporteFalso, cliente_com, mensagem_json, resposta_gemini


@pytest.fixture(scope="module")
def eventos():
    repositorio = RepositorioEventos()
    for modo in Modo:
        executar_todos(GuardaTaint(repositorio=repositorio, modo=modo))
    return repositorio.listar()


def sem_valores_sensiveis(texto: str) -> list[str]:
    return [valor for valor in dados.VALORES_SENSIVEIS if valor in texto]


def test_ha_eventos_de_todos_os_cenarios(eventos):
    assert {e.cenario for e in eventos} == {f"C{n}" for n in range(1, 12)}


def test_prompt_e_mensagem_do_usuario(eventos):
    assert sem_valores_sensiveis(PROMPT_SISTEMA) == []
    for evento in eventos:
        for cor in CorSemaforo:
            consulta = TradutorAlertas.consulta_do_evento(evento, cor)
            assert sem_valores_sensiveis(montar_mensagem_usuario(consulta)) == []


def test_corpo_enviado_ao_llm(eventos):
    falso = TransporteFalso(resposta=resposta_gemini(mensagem_json("Mensagem simulada e calma.")))
    tradutor = TradutorAlertas(cliente_com(falso))
    for evento in eventos:
        tradutor.traduzir_evento(evento)
    assert falso.requisicoes
    for requisicao in falso.requisicoes:
        enviado = str(requisicao.url) + requisicao.content.decode("utf-8")
        assert sem_valores_sensiveis(enviado) == []
        assert CHAVE_FALSA not in enviado
        assert requisicao.headers["x-goog-api-key"] == CHAVE_FALSA


@pytest.mark.parametrize("com_ia", [False, True], ids=["reserva", "ia-simulada"])
def test_mensagens_traduzidas(eventos, com_ia):
    if com_ia:
        falso = TransporteFalso(resposta=resposta_gemini(mensagem_json("Seus dados ficaram protegidos pelo sistema.")))
        tradutor = TradutorAlertas(cliente_com(falso))
        origem = OrigemMensagem.IA
    else:
        tradutor = TradutorAlertas()
        origem = OrigemMensagem.RESERVA
    traduzidos = [TradutorAlertas.com_traducao(e, tradutor.traduzir_evento(e)) for e in eventos]
    assert {e.origem_mensagem for e in traduzidos} == {origem.value}
    serializado = "\n".join(e.para_json() for e in traduzidos)
    assert sem_valores_sensiveis(serializado) == []

    risco = calcular_risco(eventos)
    mensagem = tradutor.traduzir_risco(risco, eventos).mensagem
    assert sem_valores_sensiveis(json.dumps(risco.para_dict(), ensure_ascii=False) + mensagem) == []
