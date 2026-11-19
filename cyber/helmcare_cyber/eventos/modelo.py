"""Evento de segurança: o registro de uma decisão do guarda sobre uma categoria numa saída.

Não carrega valor original nem peso de risco. O peso é calculado depois, pelo
indicador Risco Cidadão, a partir da categoria e do estado. mensagem_cidadao e
origem_mensagem ficam vazios aqui e são preenchidos pela tradução dos alertas.
"""

import json
import uuid
from dataclasses import dataclass, field
from datetime import UTC, datetime

from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.taint.modelo import Acao, Destinatario, Destino, Estado, Fonte, Modo


def agora_iso() -> str:
    """Instante atual em UTC, ISO 8601 com milissegundos e Z (2026-11-19T21:15:30.123Z)."""
    instante = datetime.now(UTC)
    return instante.strftime("%Y-%m-%dT%H:%M:%S.") + f"{instante.microsecond // 1000:03d}Z"


def ler_iso(texto: str) -> datetime:
    return datetime.fromisoformat(texto.replace("Z", "+00:00"))


@dataclass(frozen=True)
class EventoSeguranca:
    cenario: str | None
    fonte: Fonte | None
    destino: Destino
    destinatario: Destinatario
    categoria: Categoria | None
    acao: Acao
    estado: Estado
    regra_id: str
    modo: Modo
    quantidade: int
    caminhos: tuple[str, ...] = ()
    rastro: tuple[dict, ...] = ()
    mensagem_cidadao: str | None = None
    origem_mensagem: str | None = None
    id: str = field(default_factory=lambda: str(uuid.uuid4()))
    ts: str = field(default_factory=agora_iso)

    def para_dict(self) -> dict:
        return {
            "id": self.id,
            "ts": self.ts,
            "cenario": self.cenario,
            "fonte": self.fonte.value if self.fonte else None,
            "destino": self.destino.value,
            "destinatario": self.destinatario.value,
            "categoria": self.categoria.value if self.categoria else None,
            "acao": self.acao.value,
            "estado": self.estado.value,
            "regraId": self.regra_id,
            "modo": self.modo.value,
            "quantidade": self.quantidade,
            "caminhos": list(self.caminhos),
            "rastro": [dict(passo) for passo in self.rastro],
            "mensagemCidadao": self.mensagem_cidadao,
            "origemMensagem": self.origem_mensagem,
        }

    def para_json(self) -> str:
        return json.dumps(self.para_dict(), ensure_ascii=False)
