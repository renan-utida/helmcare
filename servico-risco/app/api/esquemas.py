"""Esquemas Pydantic de entrada e saída da API (seção 8 do contrato).

Os atributos ficam em snake_case no Python e viram camelCase no JSON pelo
alias_generator. Os campos de saída são exatamente os do contrato, sem nenhum a mais.
Campos desconhecidos na requisição são ignorados (extra="ignore").
"""

from datetime import datetime
from typing import Literal

from pydantic import AwareDatetime, BaseModel, ConfigDict, Field, ValidationInfo, field_validator
from pydantic.alias_generators import to_camel

from app.dominio.calculadora_indice import (
    CHUVA_MAXIMA_MM_HORA,
    CHUVA_MINIMA_MM_HORA,
    TEMPERATURA_MAXIMA_C,
    TEMPERATURA_MINIMA_C,
    UMIDADE_MAXIMA_PCT,
    UMIDADE_MINIMA_PCT,
    PrevisaoTempo,
    ResultadoIndice,
)
from app.dominio.excecoes import ValidacaoDominioError
from app.dominio.faixa_risco import FaixaRisco
from app.dominio.horas_turno import validar_turno
from app.dominio.trecho_critico import ConjuntoTrechosCriticos

# Exemplo de referência do contrato (seção 8), usado na documentação do Swagger.
EXEMPLO_REQUISICAO = {
    "inicio": "2026-11-12T20:00:00Z",
    "fim": "2026-11-13T00:00:00Z",
    "previsao": {"chuvaMmHora": 7.0, "temperaturaC": 24.0, "umidadePct": 85},
}
EXEMPLO_RESPOSTA = {
    "indiceRisco": 60,
    "faixaRisco": "ALTO",
    "componentes": {"sinistro": 84, "chuva": 70, "calor": 0},
    "explicacao": (
        "Risco alto: chuva prevista no turno e o seu horário tem histórico de sinistros "
        "graves (pico às 19h)."
    ),
}
EXEMPLO_TRECHOS = {
    "periodo": "2019 a 2025",
    "trechos": [
        {
            "posicao": 1,
            "logradouro": "SP 270 (RODOVIA RAPOSO TAVARES)",
            "latitude": -23.58341,
            "longitude": -46.77771,
            "quantidadeSinistrosGraves": 46,
        },
        {
            "posicao": 2,
            "logradouro": "SP 270 (RODOVIA RAPOSO TAVARES)",
            "latitude": -23.58481,
            "longitude": -46.78738,
            "quantidadeSinistrosGraves": 38,
        },
    ],
}


class EsquemaBase(BaseModel):
    """Base dos esquemas: JSON em camelCase e campos desconhecidos ignorados."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True, extra="ignore")


class PrevisaoEntrada(EsquemaBase):
    """Previsão do tempo para o turno inteiro."""

    # As faixas vêm das constantes do domínio, para a regra existir num lugar só.
    chuva_mm_hora: float = Field(
        ge=CHUVA_MINIMA_MM_HORA, le=CHUVA_MAXIMA_MM_HORA, description="Chuva em mm/h (0 a 200)."
    )
    temperatura_c: float = Field(
        ge=TEMPERATURA_MINIMA_C,
        le=TEMPERATURA_MAXIMA_C,
        description="Temperatura em °C (-10 a 50).",
    )
    umidade_pct: float = Field(
        ge=UMIDADE_MINIMA_PCT, le=UMIDADE_MAXIMA_PCT, description="Umidade relativa em % (0 a 100)."
    )

    def para_dominio(self) -> PrevisaoTempo:
        """Converte para o objeto de valor do domínio."""
        return PrevisaoTempo(
            chuva_mm_hora=self.chuva_mm_hora,
            temperatura_c=self.temperatura_c,
            umidade_pct=self.umidade_pct,
        )


class IndiceTurnoEntrada(EsquemaBase):
    """Corpo do POST /risco/v1/indice-turno."""

    model_config = ConfigDict(json_schema_extra={"examples": [EXEMPLO_REQUISICAO]})

    # AwareDatetime recusa data sem fuso; o domínio normaliza para UTC.
    inicio: AwareDatetime = Field(description="Início do turno, ISO 8601 com fuso (Z ou -03:00).")
    fim: AwareDatetime = Field(
        description="Fim do turno, ISO 8601 com fuso; depois do início e no máximo 24 h depois."
    )
    previsao: PrevisaoEntrada = Field(description="Previsão do tempo para o turno inteiro.")

    @field_validator("fim")
    @classmethod
    def validar_periodo(cls, fim: datetime, info: ValidationInfo) -> datetime:
        """Aplica a regra do domínio (fim depois do início e no máximo 24 h) ao campo fim.

        Fica aqui, e não só no domínio, para o erro sair na validação com o campo "fim"
        identificado. A regra em si continua sendo a do domínio (validar_turno).
        """
        inicio = info.data.get("inicio")
        # Se o início já falhou na validação, ele não está em info.data e não há o que comparar.
        if inicio is not None:
            try:
                validar_turno(inicio, fim)
            except ValidacaoDominioError as erro:
                raise ValueError(str(erro)) from erro
        return fim


class ComponentesSaida(EsquemaBase):
    """Os três componentes do índice, cada um inteiro de 0 a 100."""

    sinistro: int
    chuva: int
    calor: int


class IndiceTurnoSaida(EsquemaBase):
    """Resposta do POST /risco/v1/indice-turno (exatamente os campos do contrato)."""

    model_config = ConfigDict(json_schema_extra={"examples": [EXEMPLO_RESPOSTA]})

    indice_risco: int = Field(description="Índice de Risco do Turno, de 0 a 100.")
    faixa_risco: FaixaRisco = Field(description="Faixa do índice.")
    componentes: ComponentesSaida
    explicacao: str = Field(description="Frase para o entregador.")

    @classmethod
    def de_resultado(cls, resultado: ResultadoIndice) -> "IndiceTurnoSaida":
        """Monta a resposta a partir do resultado do domínio."""
        return cls(
            indice_risco=resultado.indice_risco,
            faixa_risco=resultado.faixa_risco,
            componentes=ComponentesSaida(
                sinistro=resultado.componentes.sinistro,
                chuva=resultado.componentes.chuva,
                calor=resultado.componentes.calor,
            ),
            explicacao=resultado.explicacao,
        )


class TrechoCriticoSaida(EsquemaBase):
    """Um trecho crítico de via pública."""

    posicao: int
    logradouro: str
    latitude: float
    longitude: float
    quantidade_sinistros_graves: int


class TrechosCriticosSaida(EsquemaBase):
    """Resposta do GET /risco/v1/trechos-criticos."""

    model_config = ConfigDict(json_schema_extra={"examples": [EXEMPLO_TRECHOS]})

    periodo: str
    trechos: list[TrechoCriticoSaida]

    @classmethod
    def de_conjunto(cls, conjunto: ConjuntoTrechosCriticos) -> "TrechosCriticosSaida":
        """Monta a resposta a partir do conjunto do domínio (fonte e critério não saem)."""
        return cls(
            periodo=conjunto.periodo,
            trechos=[
                TrechoCriticoSaida(
                    posicao=t.posicao,
                    logradouro=t.logradouro,
                    latitude=t.latitude,
                    longitude=t.longitude,
                    quantidade_sinistros_graves=t.quantidade_sinistros_graves,
                )
                for t in conjunto.trechos
            ],
        )


class SaudeSaida(EsquemaBase):
    """Resposta do GET /risco/v1/saude, com os valores da API principal."""

    status: Literal["Healthy", "Unhealthy"]


class ProblemaDetalhes(BaseModel):
    """Corpo de erro em application/problem+json (RFC 9457, antigo RFC 7807).

    errors só aparece nos erros de validação: campo para a lista de mensagens.
    """

    type: str
    title: str
    status: int
    detail: str
    errors: dict[str, list[str]] | None = None
