"""Configuração do módulo, lida de variáveis de ambiente.

A biblioteca (pii, taint, eventos, ia e risco) lê só os.environ e nunca abre
arquivo de configuração. Quem quer usar o arquivo cyber/.env chama
carregar_dotenv() no início: os scripts e a API que expõe o painel. Assim, um
teste ou outro programa que importe a biblioteca nunca carrega segredos sem
pedir.

CYBER_MODO: PROTECAO (padrão) ou AUDITORIA. Valor desconhecido vira PROTECAO,
porque, na dúvida, o módulo bloqueia.
RISCO_JANELA_MIN: janela em minutos do indicador Risco Cidadão (padrão 1440, um dia).
CYBER_EVENTOS_JSONL: caminho do arquivo em que os eventos de segurança são
gravados, um por linha. Vazio (padrão) deixa os eventos só em memória.
GEMINI_API_KEY e GEMINI_MODEL: chave e modelo da tradução dos alertas, lidos
pelo cliente do Gemini direto do ambiente. A chave não entra na Config, para
nunca aparecer num repr ou num log da configuração.
"""

import os
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path

from helmcare_cyber.taint.modelo import Modo

RISCO_JANELA_MIN_PADRAO = 1440

# cyber/.env, ao lado da pasta do pacote (o arquivo é ignorado pelo Git).
CAMINHO_DOTENV_PADRAO = Path(__file__).resolve().parents[1] / ".env"


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


def carregar_dotenv(caminho: str | Path | None = None) -> bool:
    """Carrega um arquivo .env em os.environ e diz se havia algo para carregar.

    override=False: uma variável que já existe no ambiente vale mais que o
    arquivo, como é o costume (o ambiente da máquina manda). O conteúdo nunca é
    impresso nem registrado. Sem caminho, usa cyber/.env.
    """
    from dotenv import load_dotenv

    alvo = Path(caminho) if caminho else CAMINHO_DOTENV_PADRAO
    if not alvo.is_file():
        return False
    return bool(load_dotenv(alvo, override=False))
