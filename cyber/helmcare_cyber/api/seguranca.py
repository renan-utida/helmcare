"""Controles de segurança da própria API: cabeçalhos, erros sem eco e limite de corpo.

Os dois middlewares são ASGI puros. O motivo: um tratador de Exception
registrado do jeito comum é chamado pelo middleware mais externo do Starlette,
que manda a resposta sem passar pelos middlewares de dentro (a resposta 500
sairia sem os cabeçalhos) e ainda relança o erro para o servidor registrar o
traceback. Aqui o erro é capturado antes, respondido com os cabeçalhos e
registrado só com o tipo da exceção.
"""

import json
import logging

from fastapi import Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from starlette.exceptions import HTTPException as StarletteHTTPException

from helmcare_cyber.pii.motor import MotorPII

LIMITE_CORPO_BYTES = 64 * 1024
PREFIXO_API = "/cyber/v1"
PREFIXO_PAINEL = "/painel"

# Painel: só recursos da própria origem, nenhum script ou estilo inline,
# nenhuma moldura de outro site e nenhum formulário para fora.
CSP_PAINEL = (
    "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; "
    "connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'"
)

MENSAGENS_HTTP = {404: "nao encontrado", 405: "metodo nao permitido", 413: "corpo grande demais"}

_LOG = logging.getLogger("helmcare_cyber.api")
_MOTOR = MotorPII()


def cabecalhos_seguranca(caminho: str) -> list[tuple[bytes, bytes]]:
    cabecalhos = [
        (b"x-content-type-options", b"nosniff"),
        (b"referrer-policy", b"no-referrer"),
        (b"x-frame-options", b"DENY"),
    ]
    if caminho.startswith(PREFIXO_API):
        cabecalhos.append((b"cache-control", b"no-store"))
    if caminho == "/" or caminho.startswith(PREFIXO_PAINEL):
        cabecalhos.append((b"content-security-policy", CSP_PAINEL.encode()))
    return cabecalhos


async def _responder_json(send, status: int, corpo: dict, caminho: str) -> None:
    dados = json.dumps(corpo, ensure_ascii=False).encode("utf-8")
    cabecalhos = [
        (b"content-type", b"application/json"),
        (b"content-length", str(len(dados)).encode()),
        *cabecalhos_seguranca(caminho),
    ]
    await send({"type": "http.response.start", "status": status, "headers": cabecalhos})
    await send({"type": "http.response.body", "body": dados})


class SegurancaMiddleware:
    """Põe os cabeçalhos de segurança em toda resposta e transforma qualquer erro em 500 sem detalhe."""

    def __init__(self, app) -> None:
        self.app = app

    async def __call__(self, scope, receive, send) -> None:
        if scope["type"] != "http":
            await self.app(scope, receive, send)
            return
        caminho = scope.get("path", "")
        iniciada = False

        async def enviar(mensagem):
            nonlocal iniciada
            if mensagem["type"] == "http.response.start":
                iniciada = True
                # Os cabeçalhos de segurança sempre vencem: um de mesmo nome vindo da rota é descartado.
                nossos = cabecalhos_seguranca(caminho)
                nomes = {nome for nome, _ in nossos}
                mantidos = [(n, v) for n, v in mensagem.get("headers", []) if n.lower() not in nomes]
                mensagem = {**mensagem, "headers": mantidos + nossos}
            await send(mensagem)

        try:
            await self.app(scope, receive, enviar)
        except Exception as erro:  # noqa: BLE001 (nenhum erro sai com detalhe)
            # Só o tipo: a mensagem de uma exceção pode conter o dado recebido.
            _LOG.error("falha interna em %s %s (%s)", scope.get("method"), caminho, type(erro).__name__)
            if not iniciada:
                await _responder_json(send, 500, {"erro": "falha interna"}, caminho)


class LimiteCorpoMiddleware:
    """Recusa com 413 qualquer corpo acima do limite, antes de a rota ler um byte dele."""

    def __init__(self, app, limite: int = LIMITE_CORPO_BYTES) -> None:
        self.app = app
        self.limite = limite

    async def __call__(self, scope, receive, send) -> None:
        if scope["type"] != "http":
            await self.app(scope, receive, send)
            return
        caminho = scope.get("path", "")
        declarado = dict(scope.get("headers", [])).get(b"content-length")
        if declarado is not None and declarado.isdigit() and int(declarado) > self.limite:
            await _responder_json(send, 413, {"erro": MENSAGENS_HTTP[413]}, caminho)
            return

        # Sem Content-Length (corpo em partes), conta enquanto lê, até o limite.
        corpo = bytearray()
        while True:
            mensagem = await receive()
            if mensagem["type"] != "http.request":
                await self.app(scope, receive, send)
                return
            corpo.extend(mensagem.get("body", b""))
            if len(corpo) > self.limite:
                await _responder_json(send, 413, {"erro": MENSAGENS_HTTP[413]}, caminho)
                return
            if not mensagem.get("more_body", False):
                break

        entregue = False

        async def reler():
            nonlocal entregue
            if not entregue:
                entregue = True
                return {"type": "http.request", "body": bytes(corpo), "more_body": False}
            return await receive()

        await self.app(scope, reler, send)


def _parte_segura(parte) -> str:
    """Um pedaço do caminho do erro, mascarado: a chave de um JSON também pode ser um CPF."""
    texto = str(parte)
    return str(_MOTOR.mascarar(texto)[0]) if _MOTOR.varrer(texto) else texto


async def tratar_validacao(request: Request, erro: RequestValidationError) -> JSONResponse:
    """422 só com os caminhos dos campos. O padrão do FastAPI devolveria o valor recebido (input)."""
    campos = [".".join(_parte_segura(p) for p in item.get("loc", ())) for item in erro.errors()]
    return JSONResponse({"erro": "requisicao invalida", "campos": campos}, status_code=422)


async def tratar_http(request: Request, erro: StarletteHTTPException) -> JSONResponse:
    """404, 405 e afins com uma mensagem fixa, nunca o detalhe de quem levantou."""
    corpo = {"erro": MENSAGENS_HTTP.get(erro.status_code, "erro na requisicao")}
    return JSONResponse(corpo, status_code=erro.status_code, headers=getattr(erro, "headers", None))
