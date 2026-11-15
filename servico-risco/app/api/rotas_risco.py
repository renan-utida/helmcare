"""Rotas do Serviço de Risco, sob o prefixo /risco/v1 (seção 8 do contrato).

As rotas são finas: validam a entrada (pelos esquemas), chamam um serviço de aplicação
e devolvem o esquema de saída. Nenhuma regra de negócio e nenhum SQL aqui. São
funções def (não async) porque o XGBoost e o SQLAlchemy síncrono bloqueiam: o FastAPI
roda rotas def num pool de threads, sem travar o servidor.
"""

from typing import Annotated, Any

from fastapi import APIRouter, Depends, Query
from fastapi.responses import JSONResponse

from app.api.dependencias import (
    obter_servico_indice_turno,
    obter_servico_saude,
    obter_servico_trechos_criticos,
)
from app.api.esquemas import (
    IndiceTurnoEntrada,
    IndiceTurnoSaida,
    ProblemaDetalhes,
    SaudeSaida,
    TrechosCriticosSaida,
)
from app.aplicacao.servico_indice_turno import ServicoIndiceTurno
from app.aplicacao.servico_saude import ServicoSaude
from app.aplicacao.servico_trechos_criticos import ServicoTrechosCriticos
from app.dominio.trecho_critico import LIMITE_MAXIMO, LIMITE_MINIMO, LIMITE_PADRAO

PREFIXO = "/risco/v1"
TAG_INDICE = "Índice de risco"
TAG_TRECHOS = "Trechos críticos"
TAG_SAUDE = "Saúde"

router = APIRouter(prefix=PREFIXO)


def resposta_erro(descricao: str, exemplo: dict[str, Any]) -> dict[str, Any]:
    """Documentação de uma resposta de erro em ProblemDetails, com exemplo."""
    return {
        "model": ProblemaDetalhes,
        "description": descricao,
        "content": {"application/json": {"example": exemplo}},
    }


def exemplo_problema(status: int, secao: str, titulo: str, detalhe: str) -> dict[str, Any]:
    """Exemplo de corpo ProblemDetails para a documentação."""
    return {
        "type": f"https://tools.ietf.org/html/rfc9110#section-{secao}",
        "title": titulo,
        "status": status,
        "detail": detalhe,
    }


ERRO_400_INDICE = resposta_erro(
    "Requisição inválida: data sem fuso, fim antes do início, turno acima de 24 h ou "
    "previsão fora das faixas.",
    exemplo_problema(
        400, "15.5.1", "Requisição inválida", "Um ou mais campos da requisição são inválidos."
    )
    | {"errors": {"fim": ["O fim do turno deve ser depois do início."]}},
)
ERRO_400_TRECHOS = resposta_erro(
    "Parâmetro limite fora de 1 a 50 ou não numérico.",
    exemplo_problema(
        400, "15.5.1", "Requisição inválida", "Um ou mais campos da requisição são inválidos."
    )
    | {"errors": {"limite": ["Deve ser menor ou igual a 50."]}},
)
ERRO_503_INDICE = resposta_erro(
    "Modelo de ML não carregado.",
    exemplo_problema(503, "15.6.4", "Serviço indisponível", "O modelo de ML não está carregado."),
)
ERRO_503_TRECHOS = resposta_erro(
    "Banco de dados fora do ar ou nenhum conjunto de trechos carregado.",
    exemplo_problema(
        503, "15.6.4", "Serviço indisponível", "Não foi possível ler os trechos críticos."
    ),
)


ServicoIndiceDep = Annotated[ServicoIndiceTurno, Depends(obter_servico_indice_turno)]
ServicoTrechosDep = Annotated[ServicoTrechosCriticos, Depends(obter_servico_trechos_criticos)]
ServicoSaudeDep = Annotated[ServicoSaude, Depends(obter_servico_saude)]


@router.post(
    "/indice-turno",
    response_model=IndiceTurnoSaida,
    tags=[TAG_INDICE],
    summary="Calcula o Índice de Risco do Turno",
    description=(
        "Calcula o índice de 0 a 100 de um turno: 50% sinistro (modelo de ML, média "
        "ponderada das horas do turno), 25% chuva e 25% calor. A previsão do tempo é uma "
        "só para o turno inteiro. Cada cálculo é gravado no histórico; se a gravação "
        "falhar, a resposta continua 200."
    ),
    responses={400: ERRO_400_INDICE, 503: ERRO_503_INDICE},
)
def calcular_indice_turno(
    entrada: IndiceTurnoEntrada, servico: ServicoIndiceDep
) -> IndiceTurnoSaida:
    """Calcula o índice do turno pedido."""
    resultado = servico.calcular(entrada.inicio, entrada.fim, entrada.previsao.para_dominio())
    return IndiceTurnoSaida.de_resultado(resultado)


@router.get(
    "/trechos-criticos",
    response_model=TrechosCriticosSaida,
    tags=[TAG_TRECHOS],
    summary="Lista os trechos com mais sinistros graves",
    description=(
        "Devolve os trechos de vias públicas com mais sinistros graves com motocicleta, "
        "em ordem decrescente de quantidade."
    ),
    responses={400: ERRO_400_TRECHOS, 503: ERRO_503_TRECHOS},
)
def listar_trechos_criticos(
    servico: ServicoTrechosDep,
    limite: Annotated[
        int,
        Query(ge=LIMITE_MINIMO, le=LIMITE_MAXIMO, description="Quantidade de trechos (1 a 50)."),
    ] = LIMITE_PADRAO,
) -> TrechosCriticosSaida:
    """Lista os primeiros "limite" trechos."""
    return TrechosCriticosSaida.de_conjunto(servico.listar(limite))


@router.get(
    "/saude",
    response_model=SaudeSaida,
    tags=[TAG_SAUDE],
    summary="Verificação de saúde",
    description=(
        "200 com Healthy quando o modelo está carregado e o banco responde; 503 com "
        "Unhealthy caso contrário. O monitoramento olha o status HTTP."
    ),
    responses={
        200: {"content": {"application/json": {"example": {"status": "Healthy"}}}},
        503: {
            "model": SaudeSaida,
            "description": "Modelo não carregado ou banco indisponível.",
            "content": {"application/json": {"example": {"status": "Unhealthy"}}},
        },
    },
)
def verificar_saude(servico: ServicoSaudeDep) -> JSONResponse:
    """Responde 200 ou 503 conforme o modelo e o banco."""
    if servico.saudavel():
        return JSONResponse(SaudeSaida(status="Healthy").model_dump(), status_code=200)
    return JSONResponse(SaudeSaida(status="Unhealthy").model_dump(), status_code=503)
