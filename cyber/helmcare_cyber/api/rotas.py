"""Rotas da API do módulo, em /cyber/v1 (decisão local, fora do Contrato de Integração).

Todas são síncronas (def): o tradutor faz uma chamada HTTP bloqueante, e o
FastAPI roda rotas síncronas numa thread, sem travar as outras requisições.
Nenhuma resposta leva valor original: os achados e os eventos só têm valores
mascarados, e a saída emitida pelos cenários nunca entra aqui.
"""

from dataclasses import replace
from typing import Any

from fastapi import APIRouter, Body, Depends, HTTPException, Query
from pydantic import BaseModel, ConfigDict

from helmcare_cyber.api.estado import EstadoApp, obter_estado
from helmcare_cyber.api.interceptador import EntradaInterceptada, interceptar_cadastro, registrar_cadastro
from helmcare_cyber.ia.reserva import OrigemMensagem
from helmcare_cyber.risco.risco_cidadao import calcular_risco
from helmcare_cyber.simulacao.cenarios import CENARIOS, CENARIOS_POR_ID, ResultadoCenario, executar_cenario
from helmcare_cyber.taint.modelo import Estado, Modo

LIMITE_EVENTOS_PADRAO = 50
LIMITE_EVENTOS_MAXIMO = 200

roteador = APIRouter(prefix="/cyber/v1")


class CorpoModo(BaseModel):
    model_config = ConfigDict(extra="forbid")

    modo: Modo


def _resultado_cenario(resultado: ResultadoCenario, eventos: int) -> dict:
    """O cenário sem a saída emitida: só a decisão, as regras, os estados e o rastro mascarado."""
    estados = {estado.value: 0 for estado in Estado}
    for guarda in resultado.resultados:
        for decisao in guarda.decisoes:
            estados[decisao.estado.value] += 1
    cenario = resultado.cenario
    return {
        "id": cenario.id,
        "titulo": cenario.titulo,
        "rotulo": cenario.rotulo,
        "fonte": cenario.fonte.value,
        "destino": cenario.destino.value,
        "destinatario": cenario.destinatario.value,
        "modo": resultado.modo.value,
        "decisaoGeral": resultado.decisao_geral.value,
        "esperado": cenario.esperado(resultado.modo).value,
        "passou": resultado.passou,
        "regras": resultado.regras_aplicadas(),
        "estados": estados,
        "rastro": [guarda.rastro.para_texto() for guarda in resultado.resultados],
        "eventos": eventos,
    }


def _rodar(estado: EstadoApp, ids: list[str]) -> list[dict]:
    with estado.lock:
        saida = []
        for identificador in ids:
            antes = len(estado.guarda.repositorio)
            resultado = executar_cenario(identificador, estado.guarda)
            saida.append(_resultado_cenario(resultado, len(estado.guarda.repositorio) - antes))
        return saida


@roteador.get("/saude")
def saude(estado: EstadoApp = Depends(obter_estado)) -> dict:
    cliente = estado.tradutor.cliente
    return {
        "status": "ok",
        "modo": estado.modo.value,
        "ia": {
            "configurada": cliente is not None,
            "modelo": getattr(cliente, "modelo", None),
            "disjuntorAberto": estado.tradutor.disjuntor_aberto,
        },
        "eventos": len(estado.guarda.repositorio),
    }


@roteador.post("/pii/analisar")
def analisar_pii(corpo: Any = Body(...), estado: EstadoApp = Depends(obter_estado)) -> dict:
    """Varre um JSON qualquer (ou um texto em JSON) e devolve os achados, só com valores mascarados."""
    achados = estado.guarda.motor.varrer(corpo)
    return {"quantidade": len(achados), "achados": [a.para_dict() for a in achados]}


@roteador.post("/demo/entregadores", status_code=201)
def demo_entregadores(
    entrada: EntradaInterceptada = Depends(interceptar_cadastro), estado: EstadoApp = Depends(obter_estado)
) -> dict:
    return registrar_cadastro(entrada, estado)


@roteador.get("/simulacoes")
def listar_simulacoes() -> dict:
    return {
        "cenarios": [
            {"id": c.id, "titulo": c.titulo, "rotulo": c.rotulo, "destino": c.destino.value} for c in CENARIOS
        ]
    }


# Declarada antes de /simulacoes/{cenario}, para "todas" não ser lido como id de cenário.
@roteador.post("/simulacoes/todas")
def simular_todas(estado: EstadoApp = Depends(obter_estado)) -> dict:
    cenarios = _rodar(estado, [c.id for c in CENARIOS])
    return {"modo": estado.modo.value, "cenarios": cenarios, "eventos": sum(c["eventos"] for c in cenarios)}


@roteador.post("/simulacoes/{cenario}")
def simular(cenario: str, estado: EstadoApp = Depends(obter_estado)) -> dict:
    identificador = cenario.upper()
    if identificador not in CENARIOS_POR_ID:
        raise HTTPException(status_code=404)
    return _rodar(estado, [identificador])[0]


@roteador.get("/modo")
def obter_modo(estado: EstadoApp = Depends(obter_estado)) -> dict:
    return {"modo": estado.modo.value}


@roteador.put("/modo")
def trocar_modo(corpo: CorpoModo, estado: EstadoApp = Depends(obter_estado)) -> dict:
    estado.trocar_modo(corpo.modo)
    return {"modo": estado.modo.value}


@roteador.get("/eventos")
def listar_eventos(
    limite: int = Query(LIMITE_EVENTOS_PADRAO, ge=1, le=LIMITE_EVENTOS_MAXIMO),
    traduzir: bool = Query(False),
    estado: EstadoApp = Depends(obter_estado),
) -> dict:
    """Os mais recentes primeiro. Com traduzir=true, cada evento vem com a mensagem para o cidadão.

    A tradução fica desligada por padrão porque cada combinação nova é uma
    chamada ao LLM; o painel usa a mensagem do risco, que é uma só.
    """
    eventos = list(reversed(estado.guarda.repositorio.listar()))[:limite]
    if traduzir:
        eventos = [estado.tradutor.com_traducao(e, estado.tradutor.traduzir_evento(e)) for e in eventos]
    return {"quantidade": len(eventos), "eventos": [e.para_dict() for e in eventos]}


@roteador.get("/risco-cidadao")
def risco_cidadao(traduzir: bool = Query(True), estado: EstadoApp = Depends(obter_estado)) -> dict:
    """Risco Cidadão da janela RISCO_JANELA_MIN e a mensagem do painel.

    Com traduzir=false, responde na hora, sem a mensagem: o painel mostra o
    semáforo e busca a tradução numa segunda chamada (que pode levar alguns
    segundos quando o LLM está lento).
    """
    eventos = estado.guarda.repositorio.listar()
    risco = calcular_risco(eventos)
    if not traduzir:
        return risco.para_dict() | {"mensagem": None}

    traducao = estado.tradutor.traduzir_risco(risco, eventos)
    risco = replace(risco, mensagem_cidadao=traducao.mensagem, origem_mensagem=traducao.origem.value)
    reserva = traducao.origem is OrigemMensagem.RESERVA
    return risco.para_dict() | {
        "mensagem": {
            "texto": traducao.mensagem,
            "origem": traducao.origem.value,
            "selo": traducao.selo or "",
            "motivo": traducao.motivo_descrito() if reserva and traducao.motivo_reserva else None,
        }
    }


@roteador.post("/reiniciar")
def reiniciar(estado: EstadoApp = Depends(obter_estado)) -> dict:
    """Limpa eventos, registro de taint e banco simulado. O cache do tradutor fica."""
    with estado.lock:
        estado.guarda.repositorio.limpar()
        estado.guarda.registro.limpar()
        estado.banco.limpar()
    return {"eventos": 0, "modo": estado.modo.value}
