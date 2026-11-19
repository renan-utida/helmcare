"""Fábrica da API do módulo: monta o estado, as rotas, o painel e os controles de segurança.

criar_app não carrega o cyber/.env e não fala com a rede: quem quer o arquivo
chama carregar_dotenv() antes (o scripts/servir.py faz isso). Tudo que a API
usa pode ser injetado, para os testes rodarem com tradutor falso.

Rotas em /cyber/v1, painel em /painel e porta 8100 são decisões locais do
módulo, fora do Contrato de Integração.
"""

import logging
import sys
from pathlib import Path

from fastapi import FastAPI
from fastapi.exceptions import RequestValidationError
from fastapi.responses import RedirectResponse, Response
from starlette.exceptions import HTTPException as StarletteHTTPException
from starlette.staticfiles import StaticFiles

from helmcare_cyber.api.banco import BancoSimulado
from helmcare_cyber.api.estado import EstadoApp
from helmcare_cyber.api.interceptador import CENARIO_DEMO
from helmcare_cyber.api.rotas import roteador
from helmcare_cyber.api.seguranca import (
    LIMITE_CORPO_BYTES,
    LimiteCorpoMiddleware,
    SegurancaMiddleware,
    tratar_http,
    tratar_validacao,
)
from helmcare_cyber.config import carregar_config
from helmcare_cyber.eventos.repositorio import RepositorioEventos
from helmcare_cyber.ia.tradutor import TradutorAlertas, criar_tradutor
from helmcare_cyber.pii.motor import MotorPII
from helmcare_cyber.taint.filtro_log import FiltroLogGuarda, instalar_filtro
from helmcare_cyber.taint.guarda import GuardaTaint
from helmcare_cyber.taint.modelo import Fonte, Modo
from helmcare_cyber.taint.registro import TaintRegistry

PASTA_PAINEL = Path(__file__).resolve().parents[1] / "painel"
NOME_LOGGER_DEMO = "helmcare_cyber.api.demo"
FORMATO_LOG = "%(asctime)s %(levelname)s %(name)s: %(message)s"


class _SaidaDeErroAtual(logging.Handler):
    """Escreve no sys.stderr do momento (e não no da criação), para conviver com quem troca o stderr."""

    def emit(self, registro: logging.LogRecord) -> None:
        try:
            sys.stderr.write(self.format(registro) + "\n")
        except Exception:  # noqa: BLE001 (o log nunca derruba a requisição)
            self.handleError(registro)


def _preparar_logger(logger: logging.Logger, guarda: GuardaTaint) -> FiltroLogGuarda:
    """Logger próprio da demonstração, com o filtro do guarda e sem propagar para o root.

    Sem propagar, nenhuma linha sai pelo handler do uvicorn sem passar pelo
    filtro. Um filtro de uma instância anterior (nos testes, outra app) é trocado.
    """
    logger.setLevel(logging.INFO)
    logger.propagate = False
    for filtro in [f for f in logger.filters if isinstance(f, FiltroLogGuarda)]:
        logger.removeFilter(filtro)
    if not any(isinstance(h, _SaidaDeErroAtual) for h in logger.handlers):
        saida = _SaidaDeErroAtual()
        saida.setFormatter(logging.Formatter(FORMATO_LOG))
        logger.addHandler(saida)
    return instalar_filtro(logger, guarda, fonte=Fonte.CADASTRO_ENTREGADOR, cenario=CENARIO_DEMO)


def criar_app(
    tradutor: TradutorAlertas | None = None,
    repositorio: RepositorioEventos | None = None,
    modo: Modo | str | None = None,
    motor: MotorPII | None = None,
    registro: TaintRegistry | None = None,
    logger: logging.Logger | None = None,
) -> FastAPI:
    config = carregar_config()
    motor = motor or MotorPII()
    guarda = GuardaTaint(
        motor=motor,
        registro=registro if registro is not None else TaintRegistry(),
        repositorio=repositorio if repositorio is not None else RepositorioEventos(config.eventos_jsonl),
        modo=Modo(modo) if modo is not None else config.modo,
    )
    logger = logger or logging.getLogger(NOME_LOGGER_DEMO)
    filtro = _preparar_logger(logger, guarda)

    app = FastAPI(
        title="HelmCare Cyber",
        description="Proteção dos dados do entregador: motor de PII, Taint Analysis e Risco Cidadão.",
        version="1.0.0",
    )
    app.state.cyber = EstadoApp(
        guarda=guarda,
        tradutor=tradutor if tradutor is not None else criar_tradutor(),
        banco=BancoSimulado(),
        logger_demo=logger,
        filtro_log=filtro,
    )

    app.add_exception_handler(RequestValidationError, tratar_validacao)
    app.add_exception_handler(StarletteHTTPException, tratar_http)
    # O último adicionado fica por fora: a segurança envolve também o 413 do limite de corpo.
    app.add_middleware(LimiteCorpoMiddleware, limite=LIMITE_CORPO_BYTES)
    app.add_middleware(SegurancaMiddleware)

    app.include_router(roteador)
    app.mount("/painel", StaticFiles(directory=PASTA_PAINEL, html=True), name="painel")

    @app.get("/", include_in_schema=False)
    def inicio() -> RedirectResponse:
        return RedirectResponse("/painel/")

    # O navegador pede o favicon sozinho; 204 evita um 404 no log a cada carga do painel.
    @app.get("/favicon.ico", include_in_schema=False)
    def favicon() -> Response:
        return Response(status_code=204)

    return app
