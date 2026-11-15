"""Testes dos erros do roteador (404, 405, 500) e do contrato exposto no OpenAPI."""

from collections.abc import Callable
from typing import NoReturn

from fastapi.testclient import TestClient

from app.api.dependencias import obter_servico_indice_turno
from app.api.erros import mensagem_em_portugues, resposta_problema
from app.dominio.excecoes import RegraNegocioError, ValidacaoDominioError
from tests.api.conftest import CORPO_REFERENCIA

TIPO_PROBLEMA = "application/problem+json"


class ServicoQueQuebra:
    """Substituto do ServicoIndiceTurno que lança um erro escolhido, para testar os tratadores.

    Não herda do serviço: a rota só precisa de um objeto com o método calcular.
    """

    def __init__(self, erro: Exception) -> None:
        """Guarda o erro que calcular() vai lançar."""
        self._erro = erro

    def calcular(self, *args: object, **kwargs: object) -> NoReturn:
        """Sempre lança o erro guardado."""
        raise self._erro


def cliente_com_erro(criar_cliente: Callable[..., TestClient], erro: Exception) -> TestClient:
    """Cliente cujo POST /indice-turno lança o erro indicado."""
    cliente = criar_cliente(raise_server_exceptions=False)
    cliente.app.dependency_overrides[obter_servico_indice_turno] = lambda: ServicoQueQuebra(erro)
    return cliente


def test_rota_inexistente_404_problem(criar_cliente: Callable[..., TestClient]) -> None:
    # Act
    resposta = criar_cliente().get("/risco/v1/nao-existe")

    # Assert
    assert resposta.status_code == 404
    assert resposta.headers["content-type"] == TIPO_PROBLEMA
    assert resposta.json() == {
        "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        "title": "Recurso não encontrado",
        "status": 404,
        "detail": "O recurso pedido não existe neste serviço.",
    }


def test_metodo_nao_permitido_405_problem_com_allow(
    criar_cliente: Callable[..., TestClient],
) -> None:
    # Act
    resposta = criar_cliente().delete("/risco/v1/saude")

    # Assert
    assert resposta.status_code == 405
    assert resposta.headers["content-type"] == TIPO_PROBLEMA
    assert resposta.headers["allow"] == "GET"


def test_erro_inesperado_500_generico_sem_stack_trace(
    criar_cliente: Callable[..., TestClient],
) -> None:
    # Arrange
    cliente = cliente_com_erro(criar_cliente, RuntimeError("detalhe-interno-secreto"))

    # Act
    resposta = cliente.post("/risco/v1/indice-turno", json=CORPO_REFERENCIA)

    # Assert
    assert resposta.status_code == 500
    assert resposta.headers["content-type"] == TIPO_PROBLEMA
    assert resposta.json()["detail"] == "Ocorreu um erro inesperado no Serviço de Risco."
    assert "detalhe-interno-secreto" not in resposta.text
    assert "Traceback" not in resposta.text


def test_validacao_do_dominio_fora_dos_esquemas_400(
    criar_cliente: Callable[..., TestClient],
) -> None:
    # Arrange
    cliente = cliente_com_erro(criar_cliente, ValidacaoDominioError("Regra do domínio violada."))

    # Act
    resposta = cliente.post("/risco/v1/indice-turno", json=CORPO_REFERENCIA)

    # Assert
    assert resposta.status_code == 400
    assert resposta.json()["detail"] == "Regra do domínio violada."
    assert "errors" not in resposta.json()


def test_regra_de_negocio_409(criar_cliente: Callable[..., TestClient]) -> None:
    # Arrange
    cliente = cliente_com_erro(criar_cliente, RegraNegocioError("Conflito de regra."))

    # Act
    resposta = cliente.post("/risco/v1/indice-turno", json=CORPO_REFERENCIA)

    # Assert
    assert resposta.status_code == 409
    assert resposta.json()["type"] == "https://tools.ietf.org/html/rfc9110#section-15.5.10"


def test_resposta_problema_status_sem_secao_conhecida_usa_about_blank() -> None:
    # Act
    resposta = resposta_problema(418, "Teste.")

    # Assert
    assert resposta.status_code == 418
    assert b'"type":"about:blank"' in resposta.body


def test_mensagem_em_portugues_tipo_desconhecido_valor_invalido() -> None:
    # Act e Assert
    assert mensagem_em_portugues({"type": "algo_novo"}) == "Valor inválido."


def test_openapi_tres_rotas_sob_risco_v1(criar_cliente: Callable[..., TestClient]) -> None:
    # Act
    esquema = criar_cliente().get("/risco/v1/openapi.json").json()

    # Assert
    assert set(esquema["paths"]) == {
        "/risco/v1/indice-turno",
        "/risco/v1/trechos-criticos",
        "/risco/v1/saude",
    }
    assert esquema["info"]["title"] == "HelmCare: Serviço de Risco"


def test_openapi_campos_em_camel_case(criar_cliente: Callable[..., TestClient]) -> None:
    # Act
    esquemas = criar_cliente().get("/risco/v1/openapi.json").json()["components"]["schemas"]

    # Assert
    assert set(esquemas["IndiceTurnoSaida"]["properties"]) == {
        "indiceRisco",
        "faixaRisco",
        "componentes",
        "explicacao",
    }
    assert set(esquemas["PrevisaoEntrada"]["properties"]) == {
        "chuvaMmHora",
        "temperaturaC",
        "umidadePct",
    }
    assert "quantidadeSinistrosGraves" in esquemas["TrechoCriticoSaida"]["properties"]
    assert esquemas["FaixaRisco"]["enum"] == ["BAIXO", "MODERADO", "ALTO", "MUITO_ALTO"]


def test_openapi_erros_em_problem_json_e_sem_422(criar_cliente: Callable[..., TestClient]) -> None:
    # Act
    esquema = criar_cliente().get("/risco/v1/openapi.json").json()

    # Assert
    for caminho in ("/risco/v1/indice-turno", "/risco/v1/trechos-criticos"):
        operacao = next(iter(esquema["paths"][caminho].values()))
        respostas = operacao["responses"]
        assert "422" not in respostas
        assert set(respostas["400"]["content"]) == {TIPO_PROBLEMA}
        assert set(respostas["503"]["content"]) == {TIPO_PROBLEMA}
        assert operacao["summary"]
    saude = esquema["paths"]["/risco/v1/saude"]["get"]["responses"]
    assert set(saude["503"]["content"]) == {"application/json"}
    assert "HTTPValidationError" not in esquema["components"]["schemas"]


def test_openapi_exemplo_de_referencia_na_requisicao_e_resposta(
    criar_cliente: Callable[..., TestClient],
) -> None:
    # Act
    esquemas = criar_cliente().get("/risco/v1/openapi.json").json()["components"]["schemas"]

    # Assert
    assert esquemas["IndiceTurnoEntrada"]["examples"][0] == CORPO_REFERENCIA
    assert esquemas["IndiceTurnoSaida"]["examples"][0]["indiceRisco"] == 60


def test_docs_swagger_disponivel_sob_prefixo(criar_cliente: Callable[..., TestClient]) -> None:
    # Act
    resposta = criar_cliente().get("/risco/v1/docs")

    # Assert
    assert resposta.status_code == 200
    assert "swagger" in resposta.text.lower()
