"""Tradutor dos alertas: transforma um evento de segurança numa frase para uma pessoa leiga.

Ordem de traduzir(consulta):
1. Cache: a mesma consulta já respondida pela IA volta sem nova chamada.
2. Sem cliente (sem chave ou sem modelo): reserva com SEM_CHAVE.
3. Disjuntor aberto (falha recente de rede, tempo ou HTTP): reserva com DISJUNTOR,
   para o painel não esperar o tempo limite a cada evento.
4. Chama o LLM e valida a resposta. Válida: guarda no cache e devolve IA.
   Inválida ou com erro: reserva com o motivo.

A saída do LLM é tratada como não confiável: validar_mensagem confere formato,
tamanho, número de frases, travessão, jargão e, com o motor de PII, que nenhum
dado pessoal apareceu (a IA não recebe dado, mas poderia inventar um).
"""

import json
import re
import threading
import time
from collections.abc import Callable, Iterable, Mapping
from dataclasses import dataclass, replace

from helmcare_cyber.eventos.modelo import EventoSeguranca
from helmcare_cyber.ia.cliente_llm import ClienteGemini, ErroLLM
from helmcare_cyber.ia.prompt import PROMPT_SISTEMA, ConsultaTraducao, montar_mensagem_usuario
from helmcare_cyber.ia.reserva import MENSAGEM_SEM_EVENTOS, SELO_RESERVA, MotivoReserva, OrigemMensagem, frase_reserva
from helmcare_cyber.pii.motor import MotorPII
from helmcare_cyber.risco.risco_cidadao import CorSemaforo, RiscoCidadao, cor_por_pontos, ordenar_por_peso, pontos_evento

LIMITE_CARACTERES = 280
LIMITE_FRASES = 2
DISJUNTOR_PADRAO_S = 60.0

# Falhas de infraestrutura abrem o disjuntor; uma resposta mal formada, não
# (a próxima consulta pode dar certo).
MOTIVOS_QUE_ABREM_O_DISJUNTOR = frozenset({MotivoReserva.TIMEOUT, MotivoReserva.ERRO_HTTP, MotivoReserva.ERRO_REDE})

_CERCA = re.compile(r"^\s*```(?:json)?\s*(.*?)\s*```\s*$", re.DOTALL | re.IGNORECASE)
_TERMINADOR = re.compile(r"[.!?](?=\s|$)")
_TRAVESSAO = re.compile("[\u2013\u2014]")
_JARGAO = re.compile(r"\b(?:pii|logs?|payload|mqtt|api|token|hash|endpoint|json|taint)\b", re.IGNORECASE)


@dataclass(frozen=True)
class Traducao:
    mensagem: str
    origem: OrigemMensagem
    motivo_reserva: MotivoReserva | None = None
    # Diagnóstico da falha do LLM (por exemplo, "503: The model is overloaded"), já sem a chave.
    detalhe_reserva: str | None = None

    @property
    def selo(self) -> str | None:
        """"modelo de reserva" quando o texto não veio da IA; o painel mostra junto da mensagem."""
        return SELO_RESERVA if self.origem is OrigemMensagem.RESERVA else None

    def motivo_descrito(self) -> str:
        """"ERRO_HTTP (503: ...)", "SEM_CHAVE" ou "-" quando a mensagem veio da IA."""
        if self.motivo_reserva is None:
            return "-"
        if self.detalhe_reserva:
            return f"{self.motivo_reserva.value} ({self.detalhe_reserva})"
        return self.motivo_reserva.value


def validar_mensagem(texto, motor: MotorPII | None = None) -> str:
    """Devolve a mensagem limpa ou levanta ValueError dizendo qual regra falhou."""
    if not isinstance(texto, str):
        raise ValueError("a resposta não é texto")
    cerca = _CERCA.match(texto)
    bruto = cerca.group(1) if cerca else texto
    try:
        dados = json.loads(bruto)
    except ValueError:
        raise ValueError("a resposta não é JSON") from None
    if not isinstance(dados, dict) or not isinstance(dados.get("mensagem"), str):
        raise ValueError('a resposta não tem a chave "mensagem" em texto')

    mensagem = dados["mensagem"].strip()
    if not mensagem:
        raise ValueError("a mensagem está vazia")
    if len(mensagem) > LIMITE_CARACTERES:
        raise ValueError(f"a mensagem passa de {LIMITE_CARACTERES} caracteres")
    if len(_TERMINADOR.findall(mensagem)) > LIMITE_FRASES:
        raise ValueError(f"a mensagem tem mais de {LIMITE_FRASES} frases")
    if _TRAVESSAO.search(mensagem):
        raise ValueError("a mensagem tem travessão")
    if _JARGAO.search(mensagem):
        raise ValueError("a mensagem tem termo técnico")
    if (motor or MotorPII()).varrer(mensagem):
        raise ValueError("a mensagem contém dado pessoal")
    return mensagem


class TradutorAlertas:
    def __init__(
        self,
        cliente: ClienteGemini | None = None,
        motor: MotorPII | None = None,
        disjuntor_segundos: float = DISJUNTOR_PADRAO_S,
        relogio: Callable[[], float] = time.monotonic,
    ) -> None:
        self.cliente = cliente
        self.motor = motor or MotorPII()
        self.disjuntor_segundos = disjuntor_segundos
        self._relogio = relogio
        self._cache: dict[ConsultaTraducao, str] = {}
        self._aberto_ate: float | None = None
        self._lock = threading.Lock()

    @property
    def disjuntor_aberto(self) -> bool:
        with self._lock:
            return self._aberto_ate is not None and self._relogio() < self._aberto_ate

    def _reserva(self, consulta: ConsultaTraducao, motivo: MotivoReserva, detalhe: str | None = None) -> Traducao:
        return Traducao(frase_reserva(consulta), OrigemMensagem.RESERVA, motivo, detalhe)

    def _abrir_disjuntor(self) -> None:
        with self._lock:
            self._aberto_ate = self._relogio() + self.disjuntor_segundos

    def traduzir(self, consulta: ConsultaTraducao) -> Traducao:
        with self._lock:
            em_cache = self._cache.get(consulta)
        if em_cache is not None:
            return Traducao(em_cache, OrigemMensagem.IA)
        if self.cliente is None:
            return self._reserva(consulta, MotivoReserva.SEM_CHAVE)
        if self.disjuntor_aberto:
            return self._reserva(consulta, MotivoReserva.DISJUNTOR)

        try:
            texto = self.cliente.gerar_json(PROMPT_SISTEMA, montar_mensagem_usuario(consulta))
            mensagem = validar_mensagem(texto, self.motor)
        except ErroLLM as erro:
            # O cliente já fez a nova tentativa das falhas passageiras: se chegou
            # aqui, as duas falharam, e só então o disjuntor abre.
            if erro.motivo in MOTIVOS_QUE_ABREM_O_DISJUNTOR:
                self._abrir_disjuntor()
            return self._reserva(consulta, erro.motivo, erro.detalhe)
        except ValueError:
            return self._reserva(consulta, MotivoReserva.RESPOSTA_INVALIDA)
        except Exception as erro:  # noqa: BLE001 (o painel nunca cai por causa da IA)
            self._abrir_disjuntor()
            # Só o tipo: a mensagem de uma exceção qualquer poderia levar dados.
            return self._reserva(consulta, MotivoReserva.ERRO_REDE, f"falha inesperada ({type(erro).__name__})")

        with self._lock:
            self._cache[consulta] = mensagem
            self._aberto_ate = None
        return Traducao(mensagem, OrigemMensagem.IA)

    # ------------------------------------------------------------------
    # Eventos e risco
    # ------------------------------------------------------------------

    @staticmethod
    def consulta_do_evento(evento: EventoSeguranca, cor: CorSemaforo) -> ConsultaTraducao:
        return ConsultaTraducao(evento.categoria, evento.destino, evento.acao, evento.estado, cor)

    def traduzir_evento(self, evento: EventoSeguranca, cor: CorSemaforo | None = None) -> Traducao:
        """Sem cor, usa a cor dos pontos do próprio evento."""
        cor = cor if cor is not None else cor_por_pontos(pontos_evento(evento))
        return self.traduzir(self.consulta_do_evento(evento, cor))

    @staticmethod
    def com_traducao(evento: EventoSeguranca, traducao: Traducao) -> EventoSeguranca:
        """Novo evento com a mensagem preenchida (o evento é imutável)."""
        return replace(evento, mensagem_cidadao=traducao.mensagem, origem_mensagem=traducao.origem.value)

    def traduzir_risco(self, risco: RiscoCidadao, eventos: Iterable[EventoSeguranca]) -> Traducao:
        """Mensagem do painel: o evento que mais pesou, contado com a cor do índice geral."""
        eventos = list(eventos)
        principal = None
        if risco.por_que:
            principal = next((e for e in eventos if e.id == risco.por_que[0].evento_id), None)
        if principal is None:
            ordenados = ordenar_por_peso(eventos) if risco.por_que else []
            principal = ordenados[0][1] if ordenados else None
        if principal is None:
            return Traducao(MENSAGEM_SEM_EVENTOS, OrigemMensagem.RESERVA)
        return self.traduzir_evento(principal, risco.cor)


def criar_tradutor(ambiente: Mapping[str, str] | None = None) -> TradutorAlertas:
    """Tradutor com o cliente do ambiente; sem GEMINI_API_KEY ou GEMINI_MODEL, só a reserva."""
    return TradutorAlertas(cliente=ClienteGemini.de_ambiente(ambiente))
