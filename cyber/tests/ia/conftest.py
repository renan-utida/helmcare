"""Apoio dos testes de IA: tudo passa por httpx.MockTransport, nenhuma chamada sai para a rede."""

import json

import httpx
import pytest

from helmcare_cyber.ia.cliente_llm import ClienteGemini

# Chave e modelo falsos, montados por concatenação (não se parecem com uma chave real).
CHAVE_FALSA = "chave" + "-falsa-" + "somente-para-teste"
MODELO_FALSO = "modelo-falso-de-teste"


def resposta_gemini(texto: str) -> dict:
    """Corpo de uma resposta generateContent com um único candidato."""
    return {"candidates": [{"content": {"parts": [{"text": texto}]}}]}


def mensagem_json(mensagem: str) -> str:
    return json.dumps({"mensagem": mensagem}, ensure_ascii=False)


def erro_google(mensagem: str) -> dict:
    """Corpo de erro no formato do Google (error.message)."""
    return {"error": {"code": 0, "message": mensagem, "status": "UNAVAILABLE"}}


class TransporteFalso:
    """Guarda cada requisição e responde com o que o teste mandar.

    Com sequencia, cada chamada usa o próximo (status, corpo) da lista, e o
    último se repete. Sem ela, todas as chamadas recebem status e resposta.
    """

    def __init__(
        self,
        resposta=None,
        status: int = 200,
        excecao: type[Exception] | None = None,
        sequencia: list[tuple[int, object]] | None = None,
    ) -> None:
        self.requisicoes: list[httpx.Request] = []
        self.resposta = resposta if resposta is not None else resposta_gemini(mensagem_json("Mensagem simulada."))
        self.status = status
        self.excecao = excecao
        self.sequencia = list(sequencia or [])

    def __call__(self, requisicao: httpx.Request) -> httpx.Response:
        self.requisicoes.append(requisicao)
        if self.excecao is not None:
            raise self.excecao("falha simulada", request=requisicao)
        status, corpo = self.status, self.resposta
        if self.sequencia:
            status, corpo = self.sequencia.pop(0) if len(self.sequencia) > 1 else self.sequencia[0]
        if isinstance(corpo, (bytes, str)):
            return httpx.Response(status, content=corpo)
        return httpx.Response(status, json=corpo)

    @property
    def transporte(self) -> httpx.MockTransport:
        return httpx.MockTransport(self)

    def corpos(self) -> list[dict]:
        return [json.loads(r.content) for r in self.requisicoes]


class Dorminhoco:
    """Substitui time.sleep: só anota as esperas, sem dormir de verdade."""

    def __init__(self) -> None:
        self.esperas: list[float] = []

    def __call__(self, segundos: float) -> None:
        self.esperas.append(segundos)


def cliente_com(falso: TransporteFalso, dormir=None, **opcoes) -> ClienteGemini:
    return ClienteGemini(
        CHAVE_FALSA, MODELO_FALSO, transporte=falso.transporte, dormir=dormir or Dorminhoco(), **opcoes
    )


@pytest.fixture
def falso() -> TransporteFalso:
    return TransporteFalso()
