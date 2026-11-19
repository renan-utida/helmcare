"""Taint Analysis do HelmCare: rastreia o dado pessoal da fonte ao destino e bloqueia o vazamento.

Uso típico:

    from helmcare_cyber.taint import GuardaTaint, ContextoSaida, Destino, Destinatario
    guarda = GuardaTaint()
    resultado = guarda.verificar_saida(payload, ContextoSaida(Destino.LOG))
    resultado.payload_seguro   # o que pode sair

Os nomes são carregados sob demanda (PEP 562) porque helmcare_cyber.eventos
importa taint.modelo e o guarda importa eventos: carregar tudo aqui no início
criaria uma importação circular.
"""

import importlib

_EXPORTADOS = {
    "Acao": "helmcare_cyber.taint.modelo",
    "ContextoSaida": "helmcare_cyber.taint.modelo",
    "DecisaoAchado": "helmcare_cyber.taint.modelo",
    "Destinatario": "helmcare_cyber.taint.modelo",
    "Destino": "helmcare_cyber.taint.modelo",
    "Estado": "helmcare_cyber.taint.modelo",
    "Fonte": "helmcare_cyber.taint.modelo",
    "Modo": "helmcare_cyber.taint.modelo",
    "GuardaTaint": "helmcare_cyber.taint.guarda",
    "ResultadoGuarda": "helmcare_cyber.taint.guarda",
    "Rastro": "helmcare_cyber.taint.rastro",
    "TaintRegistry": "helmcare_cyber.taint.registro",
}

__all__ = [*_EXPORTADOS, "criar_guarda"]


def __getattr__(nome: str):
    if nome in _EXPORTADOS:
        return getattr(importlib.import_module(_EXPORTADOS[nome]), nome)
    raise AttributeError(f"module {__name__!r} has no attribute {nome!r}")


def criar_guarda(config=None):
    """Guarda montado a partir da configuração do ambiente (modo e arquivo JSONL de eventos)."""
    from helmcare_cyber.config import carregar_config
    from helmcare_cyber.eventos.repositorio import RepositorioEventos
    from helmcare_cyber.taint.guarda import GuardaTaint

    config = config or carregar_config()
    return GuardaTaint(repositorio=RepositorioEventos(config.eventos_jsonl), modo=config.modo)
