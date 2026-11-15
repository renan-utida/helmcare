"""Tradução de erros para application/problem+json (RFC 9457, antigo RFC 7807).

- RequestValidationError e ValidacaoDominioError viram 400 (o FastAPI devolveria 422);
- RegraNegocioError vira 409 e ServicoIndisponivelError vira 503;
- 404 e 405 do roteador saem no mesmo formato;
- qualquer outro erro vira 500 genérico.
Nenhuma resposta leva stack trace nem ecoa o corpo da requisição.
"""

import logging
from collections.abc import Sequence
from http import HTTPStatus
from typing import Any

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from starlette.exceptions import HTTPException as StarletteHTTPException

from app.api.esquemas import ProblemaDetalhes
from app.aplicacao.excecoes import ServicoIndisponivelError
from app.dominio.excecoes import RegraNegocioError, ValidacaoDominioError

logger = logging.getLogger(__name__)

TIPO_CONTEUDO_PROBLEMA = "application/problem+json"

# O "type" aponta para a seção do RFC 9110 de cada status, como o ASP.NET faz.
URL_RFC_9110 = "https://tools.ietf.org/html/rfc9110#section-"
SECOES_RFC_9110 = {
    400: "15.5.1",
    404: "15.5.5",
    405: "15.5.6",
    409: "15.5.10",
    500: "15.6.1",
    503: "15.6.4",
}
TITULOS = {
    400: "Requisição inválida",
    404: "Recurso não encontrado",
    405: "Método não permitido",
    409: "Conflito",
    500: "Erro interno",
    503: "Serviço indisponível",
}
DETALHES_ROTEADOR = {
    404: "O recurso pedido não existe neste serviço.",
    405: "O método HTTP não é aceito nesta rota.",
}
DETALHE_VALIDACAO = "Um ou mais campos da requisição são inválidos."
DETALHE_ERRO_INTERNO = "Ocorreu um erro inesperado no Serviço de Risco."

# Origens que o FastAPI põe no começo do "loc" de cada erro; o campo vem depois delas.
ORIGENS_DO_ERRO = ("body", "query", "path", "header", "cookie")
CHAVE_CORPO = "corpo"


def resposta_problema(
    status: int,
    detalhe: str,
    erros: dict[str, list[str]] | None = None,
    cabecalhos: dict[str, str] | None = None,
) -> JSONResponse:
    """Monta a resposta de erro com type, title, status, detail e, se houver, errors."""
    secao = SECOES_RFC_9110.get(status)
    corpo = ProblemaDetalhes(
        type=f"{URL_RFC_9110}{secao}" if secao else "about:blank",
        title=TITULOS.get(status, HTTPStatus(status).phrase),
        status=status,
        detail=detalhe,
        errors=erros,
    )
    return JSONResponse(
        corpo.model_dump(exclude_none=True),
        status_code=status,
        media_type=TIPO_CONTEUDO_PROBLEMA,
        headers=cabecalhos,
    )


def chave_do_campo(erro: dict[str, Any]) -> str:
    """Nome do campo do erro, com pontos ("previsao.chuvaMmHora"), ou "corpo".

    JSON inválido e corpo ausente ou com formato errado não têm campo: viram "corpo".
    """
    if erro.get("type") == "json_invalid":
        return CHAVE_CORPO
    loc: Sequence[Any] = erro.get("loc", ())
    if loc and loc[0] in ORIGENS_DO_ERRO:
        loc = loc[1:]
    return ".".join(str(parte) for parte in loc) or CHAVE_CORPO


def formatar_numero(valor: Any) -> str:
    """Mostra 200.0 como "200" e 0.5 como "0.5" (o formato g tira o ".0" sobrando)."""
    return f"{valor:g}" if isinstance(valor, int | float) else str(valor)


def mensagem_em_portugues(erro: dict[str, Any]) -> str:
    """Traduz o erro do Pydantic para português, sem repetir o valor recebido."""
    tipo = erro.get("type", "")
    contexto = erro.get("ctx") or {}
    if tipo == "missing":
        return "Campo obrigatório."
    if tipo == "timezone_aware":
        return "Data e hora precisam de fuso horário (por exemplo, Z ou -03:00)."
    if tipo.startswith("datetime"):
        return "Data e hora inválidas: use ISO 8601 com fuso (por exemplo, 2026-11-12T20:00:00Z)."
    if tipo == "greater_than_equal":
        return f"Deve ser maior ou igual a {formatar_numero(contexto.get('ge'))}."
    if tipo == "less_than_equal":
        return f"Deve ser menor ou igual a {formatar_numero(contexto.get('le'))}."
    if tipo.startswith("int"):
        return "Deve ser um número inteiro."
    if tipo.startswith("float"):
        return "Deve ser um número."
    if tipo == "json_invalid":
        return "O corpo não é um JSON válido."
    if tipo in ("model_attributes_type", "dict_type", "model_type"):
        return "Formato inválido: envie um objeto JSON."
    if tipo == "value_error" and "error" in contexto:
        # Mensagens das regras do domínio, que já estão em português.
        return str(contexto["error"])
    return "Valor inválido."


def agrupar_erros(erros: Sequence[dict[str, Any]]) -> dict[str, list[str]]:
    """Agrupa as mensagens por campo, no formato errors do ProblemDetails."""
    agrupados: dict[str, list[str]] = {}
    for erro in erros:
        agrupados.setdefault(chave_do_campo(erro), []).append(mensagem_em_portugues(erro))
    return agrupados


async def tratar_validacao_requisicao(
    request: Request, exc: RequestValidationError
) -> JSONResponse:
    """Erro de validação do FastAPI/Pydantic: 400 com a lista de erros por campo."""
    return resposta_problema(400, DETALHE_VALIDACAO, agrupar_erros(exc.errors()))


async def tratar_validacao_dominio(request: Request, exc: ValidacaoDominioError) -> JSONResponse:
    """Regra de validação do domínio que escapou dos esquemas: 400."""
    return resposta_problema(400, str(exc))


async def tratar_regra_negocio(request: Request, exc: RegraNegocioError) -> JSONResponse:
    """Regra de negócio violada: 409."""
    return resposta_problema(409, str(exc))


async def tratar_servico_indisponivel(
    request: Request, exc: ServicoIndisponivelError
) -> JSONResponse:
    """Modelo não carregado, banco fora do ar ou sem trechos: 503."""
    return resposta_problema(503, str(exc))


async def tratar_erro_http(request: Request, exc: StarletteHTTPException) -> JSONResponse:
    """404, 405 e outros erros do roteador, com detalhe fixo em português.

    O detalhe original não é repetido. Os cabeçalhos são mantidos (o 405 leva o Allow).
    """
    detalhe = DETALHES_ROTEADOR.get(exc.status_code, "A requisição não pôde ser atendida.")
    return resposta_problema(exc.status_code, detalhe, cabecalhos=exc.headers)


async def tratar_erro_inesperado(request: Request, exc: Exception) -> JSONResponse:
    """Qualquer outro erro: 500 genérico. O log leva o tipo e o rastro; a resposta, não."""
    logger.error("Erro inesperado (%s).", type(exc).__name__, exc_info=exc)
    return resposta_problema(500, DETALHE_ERRO_INTERNO)


def registrar_tratadores(app: FastAPI) -> None:
    """Liga cada tipo de erro ao seu tratador."""
    app.add_exception_handler(RequestValidationError, tratar_validacao_requisicao)
    app.add_exception_handler(ValidacaoDominioError, tratar_validacao_dominio)
    app.add_exception_handler(RegraNegocioError, tratar_regra_negocio)
    app.add_exception_handler(ServicoIndisponivelError, tratar_servico_indisponivel)
    app.add_exception_handler(StarletteHTTPException, tratar_erro_http)
    app.add_exception_handler(Exception, tratar_erro_inesperado)
