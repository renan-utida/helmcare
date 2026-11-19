"""Apoio dos testes da API: TestClient, tradutor falso e captura do log da demonstração. Sem rede."""

import logging

import pytest
from fastapi.testclient import TestClient

from helmcare_cyber.api.app import criar_app
from helmcare_cyber.eventos.repositorio import RepositorioEventos
from helmcare_cyber.ia.tradutor import TradutorAlertas
from helmcare_cyber.simulacao import dados_simulados as dados
from tests.ia.conftest import TransporteFalso, cliente_com, mensagem_json, resposta_gemini

API = "/cyber/v1"
C1_A_C10 = [f"C{n}" for n in range(1, 11)]
C1_A_C11 = [*C1_A_C10, "C11"]


class CapturaLog(logging.Handler):
    """Guarda as linhas já filtradas que chegariam ao terminal do servidor."""

    def __init__(self) -> None:
        super().__init__()
        self.linhas: list[str] = []

    def emit(self, registro: logging.LogRecord) -> None:
        self.linhas.append(registro.getMessage())

    @property
    def texto(self) -> str:
        return "\n".join(self.linhas)


def tradutor_com_ia(texto: str = "Seus dados ficaram protegidos pelo sistema.") -> TradutorAlertas:
    return TradutorAlertas(cliente_com(TransporteFalso(resposta=resposta_gemini(mensagem_json(texto)))))


def tradutor_falhando(status: int = 503, mensagem: str = "The model is overloaded.") -> TradutorAlertas:
    return TradutorAlertas(cliente_com(TransporteFalso(resposta={"error": {"message": mensagem}}, status=status)))


def novo_cliente(modo: str = "PROTECAO", tradutor: TradutorAlertas | None = None) -> TestClient:
    app = criar_app(tradutor=tradutor or TradutorAlertas(), repositorio=RepositorioEventos(), modo=modo)
    return TestClient(app)


def estado_de(cliente: TestClient):
    return cliente.app.state.cyber


@pytest.fixture
def cliente() -> TestClient:
    return novo_cliente("PROTECAO")


@pytest.fixture
def cliente_auditoria() -> TestClient:
    return novo_cliente("AUDITORIA")


@pytest.fixture
def captura(cliente):
    """Captura o log da demonstração do cliente PROTECAO (o filtro roda antes do handler)."""
    handler = CapturaLog()
    logger = estado_de(cliente).logger_demo
    logger.addHandler(handler)
    yield handler
    logger.removeHandler(handler)


def capturar(cliente: TestClient) -> CapturaLog:
    handler = CapturaLog()
    estado_de(cliente).logger_demo.addHandler(handler)
    return handler


def cadastro() -> dict:
    return dados.cadastro_entregador()
