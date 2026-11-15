"""Aplicação FastAPI do Serviço de Risco: startup, documentação e rotas.

Para rodar (a partir de servico-risco/):

    .venv/Scripts/python -m uvicorn app.main:app --host 127.0.0.1 --port 8000

No startup (lifespan), o serviço carrega o modelo, prepara o banco e carrega os
trechos críticos. Nada disso derruba o serviço: o que falhar fica indisponível, é
registrado no log, e as rotas que dependem dele respondem 503 (degradação aprovada,
que entra no Contrato v1.11).
"""

import logging
from collections.abc import AsyncIterator, Callable
from contextlib import asynccontextmanager
from typing import Any

from fastapi import FastAPI
from fastapi.openapi.utils import get_openapi

from app.api.dependencias import RecursosServico
from app.api.erros import TIPO_CONTEUDO_PROBLEMA, registrar_tratadores
from app.api.rotas_risco import PREFIXO, TAG_INDICE, TAG_SAUDE, TAG_TRECHOS, router
from app.config import Configuracoes, obter_configuracoes
from app.infraestrutura.ml.previsor_xgboost import PrevisorXgboost
from app.infraestrutura.persistencia.semeador_trechos import ARQUIVO_TRECHOS, carregar_trechos
from app.infraestrutura.persistencia.sessao import criar_engine, criar_fabrica_sessoes

logger = logging.getLogger(__name__)

TITULO = "HelmCare: Serviço de Risco"
DESCRICAO = (
    "Serviço independente do HelmCare que calcula o Índice de Risco do Turno (modelo de "
    "ML com o histórico de sinistros graves, mais chuva e calor) e fornece os trechos "
    "críticos de sinistros. Quem o chama é a API principal em C#; o app nunca o chama "
    "direto. Erros em application/problem+json (RFC 9457)."
)
TAGS = [
    {"name": TAG_INDICE, "description": "Cálculo do Índice de Risco do Turno."},
    {"name": TAG_TRECHOS, "description": "Trechos de vias com mais sinistros graves."},
    {"name": TAG_SAUDE, "description": "Verificação de saúde para o monitoramento."},
]


def configurar_logs() -> None:
    """Formato simples dos logs do serviço (o uvicorn cuida dos próprios logs)."""
    logging.basicConfig(
        level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s"
    )


def inicializar_recursos(configuracoes: Configuracoes | None = None) -> RecursosServico:
    """Prepara o modelo e o banco; cada falha é registrada e o serviço sobe mesmo assim."""
    configuracoes = configuracoes or obter_configuracoes()
    recursos = RecursosServico()

    try:
        previsor = PrevisorXgboost.carregar(configuracoes.modelo_dir)
        recursos.previsor = previsor
        recursos.calendario = previsor.artefatos.calendario
        logger.info("Modelo %s carregado.", previsor.versao_modelo)
    except Exception:
        logger.critical(
            "Modelo de ML não carregado: /indice-turno e /saude vão responder 503.", exc_info=True
        )

    if configuracoes.database_url is None:
        logger.error("RISCO_DATABASE_URL não configurada: o banco fica indisponível.")
        return recursos
    try:
        recursos.engine = criar_engine(configuracoes.database_url)
    except Exception as erro:
        # Sem exc_info de propósito: a mensagem de uma URL inválida pode conter a própria URL.
        logger.error("URL do banco inválida (%s): o banco fica indisponível.", type(erro).__name__)
        return recursos
    recursos.fabrica_sessoes = criar_fabrica_sessoes(recursos.engine)

    if configuracoes.carregar_trechos_na_inicializacao:
        try:
            with recursos.fabrica_sessoes() as sessao:
                carregar_trechos(
                    sessao, configuracoes.modelo_dir / ARQUIVO_TRECHOS, recursos.relogio
                )
        except Exception as erro:
            logger.exception("Carga dos trechos críticos falhou (%s).", type(erro).__name__)
    return recursos


def gerar_openapi(app: FastAPI) -> dict[str, Any]:
    """Gera o OpenAPI e o ajusta ao comportamento real do serviço.

    - Remove as respostas 422 que o FastAPI acrescenta sozinho: o serviço devolve 400.
    - Troca o tipo das respostas de erro ProblemaDetalhes para application/problem+json.
    """
    if app.openapi_schema:
        return app.openapi_schema
    esquema = get_openapi(
        title=app.title,
        version=app.version,
        description=app.description,
        routes=app.routes,
        tags=app.openapi_tags,
    )
    for operacoes in esquema.get("paths", {}).values():
        for operacao in operacoes.values():
            respostas = operacao.get("responses", {})
            respostas.pop("422", None)
            for resposta in respostas.values():
                conteudo = resposta.get("content", {})
                corpo_json = conteudo.get("application/json", {})
                if corpo_json.get("schema", {}).get("$ref", "").endswith("/ProblemaDetalhes"):
                    conteudo[TIPO_CONTEUDO_PROBLEMA] = conteudo.pop("application/json")
    esquemas = esquema.get("components", {}).get("schemas", {})
    esquemas.pop("HTTPValidationError", None)
    esquemas.pop("ValidationError", None)
    app.openapi_schema = esquema
    return esquema


def criar_app(
    inicializar: Callable[[], RecursosServico] = inicializar_recursos,
) -> FastAPI:
    """Cria a aplicação. Os testes passam outro "inicializar" para não tocar no banco real."""

    @asynccontextmanager
    async def lifespan(app: FastAPI) -> AsyncIterator[None]:
        configurar_logs()
        app.state.recursos = inicializar()
        try:
            yield
        finally:
            app.state.recursos.fechar()

    app = FastAPI(
        title=TITULO,
        description=DESCRICAO,
        version="1.0.0",
        openapi_tags=TAGS,
        # O nginx repassa /risco sem tirar o prefixo, então a documentação fica sob ele.
        docs_url=f"{PREFIXO}/docs",
        openapi_url=f"{PREFIXO}/openapi.json",
        redoc_url=None,
        lifespan=lifespan,
    )
    registrar_tratadores(app)
    app.include_router(router)
    # Substitui o gerador padrão pelo ajustado (sem 422 e com problem+json).
    app.openapi = lambda: gerar_openapi(app)
    return app


app = criar_app()
