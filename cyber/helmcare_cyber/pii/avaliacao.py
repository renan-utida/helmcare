"""Avaliação do motor de PII sobre um corpus rotulado.

Cada caso do corpus diz quais categorias o motor deve encontrar (com
repetição). A comparação é por contagem: se o caso espera dois SAUDE e o motor
acha três, são dois verdadeiros positivos e um falso positivo.
"""

import json
from collections import Counter
from dataclasses import dataclass, field
from pathlib import Path

from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.pii.motor import MotorPII

# Grupos de negativos em que nenhum falso positivo é aceito: são os dados que o
# HelmCare mais produz (telemetria do capacete, ids, datas e versões).
GRUPOS_SEM_FALSO_POSITIVO = frozenset({"telemetria", "guid", "timestamp", "versao"})


@dataclass
class Metricas:
    vp: int = 0
    fp: int = 0
    fn: int = 0

    @property
    def precisao(self) -> float | None:
        """None quando o motor não marcou nada nessa categoria (não há o que medir)."""
        marcados = self.vp + self.fp
        return self.vp / marcados if marcados else None

    @property
    def recall(self) -> float | None:
        esperados = self.vp + self.fn
        return self.vp / esperados if esperados else None

    def somar(self, outra: "Metricas") -> None:
        self.vp += outra.vp
        self.fp += outra.fp
        self.fn += outra.fn


@dataclass
class ResultadoAvaliacao:
    por_categoria: dict[Categoria, Metricas]
    total: Metricas
    quantidade_casos: int
    divergentes: list[str] = field(default_factory=list)
    limitacoes: list[str] = field(default_factory=list)
    falsos_positivos_protegidos: list[str] = field(default_factory=list)


# Chaves de API simuladas usadas pelo corpus. No arquivo JSON elas aparecem só
# como {{CHAVE_GOOGLE}} e {{CHAVE_SK}}: uma chave escrita por extenso num arquivo
# versionado é barrada pelo secret scanning do GitHub, mesmo sendo inventada.
# Aqui elas são montadas por concatenação pelo mesmo motivo.
PLACEHOLDERS_CORPUS: dict[str, str] = {
    "{{CHAVE_GOOGLE}}": "AIza" + "SySimulada0123456789abcdefghijklmno",
    "{{CHAVE_SK}}": "sk" + "-" + "simulada0123456789abcdefXYZ",
}


def expandir_placeholders(valor):
    """Troca os placeholders do corpus pelas chaves simuladas, em qualquer profundidade.

    Percorre dict, list e str; os demais valores voltam como estão.
    """
    if isinstance(valor, str):
        for placeholder, chave in PLACEHOLDERS_CORPUS.items():
            valor = valor.replace(placeholder, chave)
        return valor
    if isinstance(valor, dict):
        return {expandir_placeholders(k): expandir_placeholders(v) for k, v in valor.items()}
    if isinstance(valor, list):
        return [expandir_placeholders(item) for item in valor]
    return valor


def carregar_corpus(caminho: str | Path) -> list[dict]:
    """Lê o corpus e expande os placeholders. É o único ponto de carregamento,
    usado pelos testes e pelo script de avaliação."""
    with open(caminho, encoding="utf-8") as arquivo:
        return expandir_placeholders(json.load(arquivo)["casos"])


def categorias_encontradas(motor: MotorPII, entrada) -> Counter:
    return Counter(achado.categoria.value for achado in motor.varrer(entrada))


def comparar(esperado: Counter, encontrado: Counter) -> dict[Categoria, Metricas]:
    """Verdadeiro positivo é o mínimo entre esperado e encontrado em cada categoria."""
    resultado: dict[Categoria, Metricas] = {}
    for nome in set(esperado) | set(encontrado):
        vp = min(esperado[nome], encontrado[nome])
        resultado[Categoria(nome)] = Metricas(vp=vp, fp=encontrado[nome] - vp, fn=esperado[nome] - vp)
    return resultado


def avaliar(casos: list[dict], motor: MotorPII | None = None) -> ResultadoAvaliacao:
    motor = motor or MotorPII()
    por_categoria = {categoria: Metricas() for categoria in Categoria}
    resultado = ResultadoAvaliacao(por_categoria=por_categoria, total=Metricas(), quantidade_casos=len(casos))
    for caso in casos:
        esperado = Counter(caso["esperado"])
        encontrado = categorias_encontradas(motor, caso["entrada"])
        if caso.get("limitacaoConhecida"):
            resultado.limitacoes.append(caso["id"])
        if esperado != encontrado:
            resultado.divergentes.append(caso["id"])
        if caso.get("grupo") in GRUPOS_SEM_FALSO_POSITIVO and encontrado:
            resultado.falsos_positivos_protegidos.append(caso["id"])
        for categoria, metricas in comparar(esperado, encontrado).items():
            por_categoria[categoria].somar(metricas)
            resultado.total.somar(metricas)
    return resultado
