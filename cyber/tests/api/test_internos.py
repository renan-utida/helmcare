"""Casos de borda da API: escopo que não é HTTP, desconexão no meio do corpo, repr e log com stderr quebrado."""

import asyncio
import logging

from fastapi.testclient import TestClient

from helmcare_cyber.api.app import _SaidaDeErroAtual
from helmcare_cyber.api.banco import BancoSimulado
from helmcare_cyber.api.seguranca import LimiteCorpoMiddleware
from helmcare_cyber.simulacao import dados_simulados as dados
from tests.api.conftest import API, estado_de, novo_cliente


def test_ciclo_de_vida_passa_pelos_middlewares():
    # O "with" dispara o escopo lifespan, que não é HTTP: os middlewares só repassam.
    with TestClient(novo_cliente().app) as cliente:
        assert cliente.get(f"{API}/saude").status_code == 200


def test_desconexao_no_meio_do_corpo():
    recebidos = []

    async def app(scope, receive, send):
        recebidos.append(await receive())

    mensagens = [{"type": "http.disconnect"}]

    async def receive():
        return mensagens.pop(0) if mensagens else {"type": "http.disconnect"}

    async def send(mensagem):  # pragma: no cover (nada é enviado numa desconexão)
        raise AssertionError("não devia responder")

    escopo = {"type": "http", "path": f"{API}/pii/analisar", "headers": []}
    asyncio.run(LimiteCorpoMiddleware(app)(escopo, receive, send))
    assert recebidos == [{"type": "http.disconnect"}]


def test_repr_sem_dados():
    cliente = novo_cliente()
    resposta = cliente.post(f"{API}/demo/entregadores", json=dados.cadastro_entregador())
    estado = estado_de(cliente)
    assert repr(estado.banco) == "BancoSimulado(1 registros)"
    assert repr(estado).startswith("EstadoApp(modo=PROTECAO, eventos=")
    assert dados.CPF not in repr(estado) + repr(estado.banco)
    assert estado.banco.obter("nao-existe") is None
    assert resposta.status_code == 201


def test_banco_isolado():
    banco = BancoSimulado()
    identificador = banco.salvar({"a": 1})
    copia = banco.obter(identificador)
    copia["a"] = 2
    assert banco.obter(identificador) == {"a": 1}
    banco.limpar()
    assert len(banco) == 0


def test_log_com_stderr_quebrado_nao_derruba(monkeypatch):
    class Quebrado:
        def write(self, texto):
            raise OSError("stderr fechado")

    monkeypatch.setattr("sys.stderr", Quebrado())
    monkeypatch.setattr(logging, "raiseExceptions", False)
    handler = _SaidaDeErroAtual()
    handler.emit(logging.LogRecord("x", logging.INFO, __file__, 1, "mensagem", None, None))
