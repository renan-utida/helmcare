"""Reserva por tabela: a frase usada quando a IA não responde, não tem chave ou erra o formato.

A frase é montada com os mesmos dicionários do prompt (no EXPOSTO, a ação
está no próprio molde), então diz a mesma coisa que a IA diria, sem rede e
sem variação. As estruturas evitam
concordância de gênero (aparecer, usar), porque o tema pode ser "seu CPF" ou
"sua senha". O painel mostra o selo "modelo de reserva" junto dessas frases,
para a pessoa saber que o texto não veio da IA.
"""

from enum import StrEnum

from helmcare_cyber.ia.prompt import DESCRICAO_CATEGORIA, DESCRICAO_DESTINO, ConsultaTraducao, descricao_acao
from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.taint.modelo import Acao, Estado


class MotivoReserva(StrEnum):
    SEM_CHAVE = "SEM_CHAVE"
    TIMEOUT = "TIMEOUT"
    ERRO_HTTP = "ERRO_HTTP"
    ERRO_REDE = "ERRO_REDE"
    RESPOSTA_INVALIDA = "RESPOSTA_INVALIDA"
    DISJUNTOR = "DISJUNTOR"


class OrigemMensagem(StrEnum):
    IA = "IA"
    RESERVA = "RESERVA"


SELO_RESERVA = "modelo de reserva"
MENSAGEM_SEM_EVENTOS = "Tudo certo: seus dados estão protegidos e nada importante aconteceu."

_MODELOS = {
    # O EXPOSTO tem a ação escrita no próprio molde: "apareceu" já diz que o dado
    # saiu de verdade, e o texto longo do prompt repetiria isso na mesma frase.
    Estado.EXPOSTO: (
        "{T} {apareceu} {onde}, e o sistema não conseguiu impedir, só registrou o ocorrido. "
        "A equipe precisa corrigir isso o quanto antes."
    ),
    Estado.NEUTRALIZADO: (
        "{T} {poderia} ter aparecido {onde}, mas o sistema {acao} antes que saísse. "
        "Seus dados continuam protegidos, e a equipe só precisa corrigir o código."
    ),
    Estado.PERMITIDO: "O sistema usou {tema} {onde}, porque esse é um caminho autorizado. Não há nada a fazer.",
}

# Os verbos não mudam com o gênero, mas mudam com o número: "seus dados de saúde" é plural.
TEMAS_NO_PLURAL = frozenset({Categoria.SAUDE})
_VERBOS = {
    False: {"apareceu": "apareceu", "poderia": "poderia"},
    True: {"apareceu": "apareceram", "poderia": "poderiam"},
}


def frase_reserva(consulta: ConsultaTraducao) -> str:
    """Frase determinística para a consulta (a mesma consulta dá sempre a mesma frase)."""
    tema = DESCRICAO_CATEGORIA[consulta.categoria]
    # NEUTRALIZADO com PERMITIR não tem ação a contar: para a pessoa, é um uso autorizado.
    estado = consulta.estado
    if estado is Estado.NEUTRALIZADO and consulta.acao is Acao.PERMITIR:
        estado = Estado.PERMITIDO
    return _MODELOS[estado].format(
        T=tema[0].upper() + tema[1:],
        tema=tema,
        onde=DESCRICAO_DESTINO[consulta.destino],
        acao=descricao_acao(consulta.acao, consulta.estado),
        **_VERBOS[consulta.categoria in TEMAS_NO_PLURAL],
    )
