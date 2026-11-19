"""Configuração do módulo, lida só de variáveis de ambiente (nunca de arquivo .env).

CYBER_MODO: PROTECAO (padrão) ou AUDITORIA. Valor desconhecido vira PROTECAO,
porque, na dúvida, o módulo bloqueia.
RISCO_JANELA_MIN: janela em minutos do indicador Risco Cidadão (padrão 1440, um dia).
CYBER_EVENTOS_JSONL: caminho do arquivo em que os eventos de segurança são
gravados, um por linha. Vazio (padrão) deixa os eventos só em memória.
"""

import os
from collections.abc import Mapping
from dataclasses import dataclass

from helmcare_cyber.taint.modelo import Modo

RISCO_JANELA_MIN_PADRAO = 1440


@dataclass(frozen=True)
class Config:
    modo: Modo = Modo.PROTECAO
    risco_janela_min: int = RISCO_JANELA_MIN_PADRAO
    eventos_jsonl: str | None = None


def carregar_config(ambiente: Mapping[str, str] | None = None) -> Config:
    """Monta a Config a partir de os.environ (ou de um dicionário, nos testes)."""
    ambiente = os.environ if ambiente is None else ambiente

    try:
        modo = Modo(ambiente.get("CYBER_MODO", "").strip().upper() or Modo.PROTECAO)
    except ValueError:
        modo = Modo.PROTECAO

    try:
        janela = int(ambiente.get("RISCO_JANELA_MIN", RISCO_JANELA_MIN_PADRAO))
    except (TypeError, ValueError):
        janela = RISCO_JANELA_MIN_PADRAO
    if janela <= 0:
        janela = RISCO_JANELA_MIN_PADRAO

    eventos = ambiente.get("CYBER_EVENTOS_JSONL", "").strip() or None
    return Config(modo=modo, risco_janela_min=janela, eventos_jsonl=eventos)
