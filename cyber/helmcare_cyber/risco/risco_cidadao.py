"""Risco Cidadão: um índice de 0 a 100, com semáforo, sobre a exposição dos dados do entregador.

Tudo aqui é regra de negócio explicável, com Decimal para os fatores (assim
10 x 9 x 0,9 x 0,3 dá 24,3, e não 24,300000000000004).

Pontos de um evento = 10 x peso da categoria x fator do destino x fator do estado.
Índice = min(100, pior evento + volume), arredondado com 0,5 para cima.
Volume = min(25, 2 x (N - 1)), com N = pares (destino, fonte) distintos entre
os eventos que pesam (estado diferente de PERMITIDO).

O pior evento puxa o índice: um único vazamento de saúde já é perigo, não
importa quantos eventos leves houve. O volume soma pouco e tem teto, para
indicar que o problema se espalhou por vários caminhos sem esconder a
gravidade do pior.
"""

from collections.abc import Iterable
from dataclasses import dataclass
from datetime import UTC, datetime, timedelta
from decimal import ROUND_HALF_UP, Decimal
from enum import StrEnum

from helmcare_cyber.config import carregar_config
from helmcare_cyber.eventos.modelo import EventoSeguranca, ler_iso
from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.taint.modelo import Destino, Estado


class CorSemaforo(StrEnum):
    VERDE = "VERDE"
    AMARELO = "AMARELO"
    VERMELHO = "VERMELHO"


BASE_PONTOS = Decimal(10)
TETO_INDICE = 100
TETO_VOLUME = 25
PONTOS_POR_PAR_EXTRA = 2

# Peso por categoria (0 a 10): quanto dano o dado causa se vazar.
PESO_CATEGORIA: dict[Categoria, Decimal] = {
    Categoria.SENHA: Decimal(10),
    Categoria.SAUDE: Decimal(9),
    Categoria.TOKEN_ACESSO: Decimal(8),
    Categoria.SEGREDO_API: Decimal(8),
    Categoria.LOCALIZACAO_EXATA: Decimal(7),
    Categoria.CPF: Decimal(7),
    Categoria.CNH: Decimal(7),
    Categoria.RG: Decimal(7),
    Categoria.TELEFONE: Decimal(5),
    Categoria.EMAIL: Decimal(5),
    Categoria.NOME: Decimal(5),
    # O QR sozinho não abre a ficha: o Cofre exige socorrista verificado e janela aberta.
    Categoria.QR_TOKEN: Decimal(4),
    Categoria.LOCALIZACAO_APROXIMADA: Decimal(2),
}
# Evento sem categoria: o erro interno do guarda (TA-ERRO-01).
PESO_SEM_CATEGORIA = Decimal(5)

# Fator por destino (0 a 1): quanto a saída alcança gente que não devia ver.
FATOR_DESTINO: dict[Destino, Decimal] = {
    Destino.RESPOSTA_API_PUBLICA: Decimal("1.0"),
    Destino.MENSAGEM_ERRO: Decimal("1.0"),
    Destino.MQTT_PAYLOAD: Decimal("1.0"),
    Destino.DOCUMENTACAO_PUBLICA: Decimal("1.0"),
    Destino.CHAMADO_VOLUNTARIO: Decimal("0.9"),
    Destino.NOTIFICACAO_PUSH: Decimal("0.9"),
    # Os três abaixo também chegam a uma pessoa, como os dois de cima.
    Destino.COFRE_SOCORRISTA: Decimal("0.9"),
    Destino.CONTATO_EMERGENCIA: Decimal("0.9"),
    Destino.RESPOSTA_PROPRIO_ENTREGADOR: Decimal("0.9"),
    Destino.AUDITORIA: Decimal("0.6"),
    Destino.LOG: Decimal("0.5"),
    Destino.PERSISTENCIA: Decimal("0.4"),
}

# Fator por estado: exposto pesa inteiro; neutralizado pesa um pouco (o código
# tem um defeito a corrigir); permitido é uso legítimo e não pesa.
FATOR_ESTADO: dict[Estado, Decimal] = {
    Estado.EXPOSTO: Decimal("1.0"),
    Estado.NEUTRALIZADO: Decimal("0.3"),
    Estado.PERMITIDO: Decimal(0),
}

# Limite superior (inclusivo) de cada faixa, em pontos inteiros.
FAIXAS: tuple[tuple[int, CorSemaforo], ...] = (
    (29, CorSemaforo.VERDE),
    (59, CorSemaforo.AMARELO),
    (100, CorSemaforo.VERMELHO),
)

# O painel mostra texto e ícone, não só a cor (acessibilidade para quem não distingue cores).
ROTULO_COR: dict[CorSemaforo, str] = {
    CorSemaforo.VERDE: "Seus dados estão protegidos",
    CorSemaforo.AMARELO: "Atenção: houve tentativa de vazamento",
    CorSemaforo.VERMELHO: "Perigo: dado sensível exposto",
}
ICONE_COR: dict[CorSemaforo, str] = {
    CorSemaforo.VERDE: "OK",
    CorSemaforo.AMARELO: "ATENCAO",
    CorSemaforo.VERMELHO: "PERIGO",
}

ITENS_POR_QUE = 3


def arredondar(valor) -> int:
    """Inteiro mais próximo, com 0,5 para cima (Decimal, sem a regra do banqueiro)."""
    return int(Decimal(str(valor)).quantize(Decimal(1), rounding=ROUND_HALF_UP))


def cor_por_pontos(valor) -> CorSemaforo:
    """VERDE de 0 a 29, AMARELO de 30 a 59, VERMELHO de 60 a 100 (o valor é arredondado antes)."""
    inteiro = arredondar(valor)
    for limite, cor in FAIXAS:
        if inteiro <= limite:
            return cor
    return CorSemaforo.VERMELHO


def pontos_evento(evento: EventoSeguranca) -> Decimal:
    peso = PESO_CATEGORIA[evento.categoria] if evento.categoria is not None else PESO_SEM_CATEGORIA
    return BASE_PONTOS * peso * FATOR_DESTINO[evento.destino] * FATOR_ESTADO[evento.estado]


def ordenar_por_peso(eventos: Iterable[EventoSeguranca]) -> list[tuple[Decimal, EventoSeguranca]]:
    """(pontos, evento) dos eventos que pesam, do maior para o menor; no empate, o mais recente primeiro."""
    pontuados = [(pontos_evento(e), e) for e in eventos]
    pesados = [(p, e) for p, e in pontuados if p > 0]
    # sorted é estável: primeiro pela data (mais recente antes), depois pelos pontos.
    por_data = sorted(pesados, key=lambda item: ler_iso(item[1].ts), reverse=True)
    return sorted(por_data, key=lambda item: item[0], reverse=True)


@dataclass(frozen=True)
class Contadores:
    eventos: int = 0
    neutralizados: int = 0
    expostos: int = 0
    permitidos: int = 0

    def para_dict(self) -> dict:
        return {
            "eventos": self.eventos,
            "neutralizados": self.neutralizados,
            "expostos": self.expostos,
            "permitidos": self.permitidos,
        }


@dataclass(frozen=True)
class ItemPorQue:
    """Um dos eventos que mais pesaram no índice. Sem valor de dado, só a classificação."""

    evento_id: str
    categoria: Categoria | None
    destino: Destino
    estado: Estado
    pontos: Decimal

    def para_dict(self) -> dict:
        return {
            "eventoId": self.evento_id,
            "categoria": self.categoria.value if self.categoria else None,
            "destino": self.destino.value,
            "estado": self.estado.value,
            "pontos": float(self.pontos),
        }


@dataclass(frozen=True)
class RiscoCidadao:
    indice: int
    cor: CorSemaforo
    rotulo: str
    icone: str
    pior_evento_pontos: Decimal
    volume_pontos: int
    janela_min: int
    contadores: Contadores
    por_que: tuple[ItemPorQue, ...] = ()
    mensagem_cidadao: str | None = None
    origem_mensagem: str | None = None

    def para_dict(self) -> dict:
        return {
            "indice": self.indice,
            "cor": self.cor.value,
            "rotulo": self.rotulo,
            "icone": self.icone,
            "piorEventoPontos": float(self.pior_evento_pontos),
            "volumePontos": self.volume_pontos,
            "janelaMin": self.janela_min,
            "contadores": self.contadores.para_dict(),
            "porQue": [item.para_dict() for item in self.por_que],
            "mensagemCidadao": self.mensagem_cidadao,
            "origemMensagem": self.origem_mensagem,
        }


def _na_janela(eventos: Iterable[EventoSeguranca], agora: datetime, janela_min: int) -> list[EventoSeguranca]:
    inicio = agora - timedelta(minutes=janela_min)
    return [e for e in eventos if inicio <= ler_iso(e.ts) <= agora]


def calcular_risco(
    eventos: Iterable[EventoSeguranca], agora: datetime | None = None, janela_min: int | None = None
) -> RiscoCidadao:
    """Calcula o Risco Cidadão dos eventos dentro da janela que termina em agora."""
    janela_min = janela_min if janela_min is not None else carregar_config().risco_janela_min
    agora = agora if agora is not None else datetime.now(UTC)
    janela = _na_janela(eventos, agora, janela_min)

    ordenados = ordenar_por_peso(janela)
    pior = ordenados[0][0] if ordenados else Decimal(0)
    pares = {(e.destino, e.fonte) for e in janela if e.estado is not Estado.PERMITIDO}
    volume = min(TETO_VOLUME, PONTOS_POR_PAR_EXTRA * (len(pares) - 1)) if pares else 0
    indice = min(TETO_INDICE, arredondar(pior + volume))
    cor = cor_por_pontos(indice)

    contadores = Contadores(
        eventos=len(janela),
        neutralizados=sum(e.estado is Estado.NEUTRALIZADO for e in janela),
        expostos=sum(e.estado is Estado.EXPOSTO for e in janela),
        permitidos=sum(e.estado is Estado.PERMITIDO for e in janela),
    )
    por_que = tuple(
        ItemPorQue(e.id, e.categoria, e.destino, e.estado, pontos) for pontos, e in ordenados[:ITENS_POR_QUE]
    )
    return RiscoCidadao(
        indice=indice,
        cor=cor,
        rotulo=ROTULO_COR[cor],
        icone=ICONE_COR[cor],
        pior_evento_pontos=pior,
        volume_pontos=volume,
        janela_min=janela_min,
        contadores=contadores,
        por_que=por_que,
    )
