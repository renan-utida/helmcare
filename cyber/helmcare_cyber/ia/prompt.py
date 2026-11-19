"""O que vai ao LLM: só a classificação do evento, descrita em palavras do dia a dia.

A IA nunca vê valor. A consulta tem cinco campos (categoria, destino, ação,
estado e cor) e cada um vira um texto fixo destes dicionários. Nenhum trecho
do payload, do rastro ou do caminho do campo entra na mensagem.
"""

from dataclasses import dataclass

from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.risco.risco_cidadao import CorSemaforo
from helmcare_cyber.taint.modelo import Acao, Destino, Estado


@dataclass(frozen=True)
class ConsultaTraducao:
    """A pergunta feita à IA. É imutável e hashable porque também é a chave do cache."""

    categoria: Categoria | None
    destino: Destino
    acao: Acao
    estado: Estado
    cor: CorSemaforo


# O "tema": o que estava em risco, em minúsculas, para entrar no meio da frase.
DESCRICAO_CATEGORIA: dict[Categoria | None, str] = {
    Categoria.CPF: "seu CPF",
    Categoria.CNH: "sua carteira de habilitação",
    Categoria.RG: "seu RG",
    Categoria.TELEFONE: "seu telefone",
    Categoria.EMAIL: "seu e-mail",
    Categoria.NOME: "seu nome",
    Categoria.SAUDE: "seus dados de saúde",
    Categoria.LOCALIZACAO_EXATA: "sua localização exata",
    Categoria.LOCALIZACAO_APROXIMADA: "sua localização aproximada",
    Categoria.SENHA: "sua senha",
    Categoria.TOKEN_ACESSO: "seu acesso ao aplicativo",
    Categoria.QR_TOKEN: "o código do QR do seu capacete",
    Categoria.SEGREDO_API: "uma chave secreta do sistema",
    None: "uma informação do sistema",
}

# O "onde": sempre começa com preposição, para encaixar depois do verbo.
DESCRICAO_DESTINO: dict[Destino, str] = {
    Destino.LOG: "nos registros internos do sistema",
    Destino.RESPOSTA_API_PUBLICA: "numa resposta aberta do sistema",
    Destino.MENSAGEM_ERRO: "numa mensagem de erro na tela",
    Destino.NOTIFICACAO_PUSH: "numa notificação no celular de outra pessoa",
    Destino.MQTT_PAYLOAD: "nas mensagens trocadas com o capacete",
    Destino.CHAMADO_VOLUNTARIO: "num pedido de ajuda mostrado a voluntários",
    Destino.AUDITORIA: "nos registros de auditoria",
    Destino.PERSISTENCIA: "no armazenamento do sistema",
    Destino.COFRE_SOCORRISTA: "na ficha mostrada a um socorrista",
    Destino.CONTATO_EMERGENCIA: "no aviso ao seu contato de emergência",
    Destino.RESPOSTA_PROPRIO_ENTREGADOR: "na resposta que você mesmo recebe",
    Destino.DOCUMENTACAO_PUBLICA: "num documento público",
}

DESCRICAO_ACAO_NEUTRALIZADA: dict[Acao, str] = {
    Acao.BLOQUEAR: "bloqueou a informação",
    Acao.REDIGIR: "escondeu a informação",
    Acao.MASCARAR: "escondeu parte da informação",
    Acao.ARREDONDAR: "deixou o local apenas aproximado",
}
# EXPOSTO é um fato, não uma possibilidade: o texto diz que o dado apareceu de
# verdade, para a mensagem da IA não falar em "poderia ter sido visto".
DESCRICAO_EXPOSTO = "não conseguiu impedir: o dado chegou a aparecer de verdade, e o sistema só registrou o ocorrido"
DESCRICAO_PERMITIDO = "liberou, porque o canal é autorizado"

# Rótulo da linha do "onde": hipótese nos estados tratados, fato no EXPOSTO.
ROTULO_ONDE = "Onde poderia aparecer"
ROTULO_ONDE_EXPOSTO = "Onde apareceu de verdade"

# Só no EXPOSTO: o tom acolhedor do prompt do sistema não pode virar
# tranquilização de quem teve o dado exposto. A correção pela equipe é o que a
# reserva também anuncia, então não é uma ação inventada.
ROTULO_TOM = "Tom da mensagem"
ORIENTACAO_TOM_EXPOSTO = (
    "séria, sem frases de tranquilização como fique tranquilo ou não se preocupe, "
    "e dizendo que a equipe vai corrigir o problema"
)

DESCRICAO_COR: dict[CorSemaforo, str] = {
    CorSemaforo.VERDE: "verde, tudo protegido",
    CorSemaforo.AMARELO: "amarelo, atenção",
    CorSemaforo.VERMELHO: "vermelho, perigo",
}

PROMPT_SISTEMA = (
    "Você é o assistente de comunicação de segurança do HelmCare, um capacete inteligente para "
    "entregadores de moto. Explique um evento de segurança de dados para uma pessoa leiga de 60 anos, "
    "em português do Brasil, em no máximo 2 frases curtas, em tom calmo e acolhedor. Não use termos "
    "técnicos como PII, log, payload, MQTT ou API. Não invente fatos além do evento. Diga o que poderia "
    "ter acontecido com os dados da pessoa e o que o sistema fez. Não use travessões. Responda só com "
    'JSON no formato {"mensagem": "..."}.'
)


def descricao_acao(acao: Acao, estado: Estado) -> str:
    """O que o sistema fez, do ponto de vista de quem é dono do dado."""
    if estado is Estado.EXPOSTO:
        return DESCRICAO_EXPOSTO
    if estado is Estado.PERMITIDO or acao is Acao.PERMITIR:
        return DESCRICAO_PERMITIDO
    return DESCRICAO_ACAO_NEUTRALIZADA[acao]


def montar_mensagem_usuario(consulta: ConsultaTraducao) -> str:
    """Linhas rotuladas, todas vindas dos textos fixos acima (nunca de um valor).

    Quatro linhas nos estados NEUTRALIZADO e PERMITIDO; no EXPOSTO, uma quinta
    linha pede tom sério, sem tranquilização.
    """
    exposto = consulta.estado is Estado.EXPOSTO
    linhas = [
        f"Em risco: {DESCRICAO_CATEGORIA[consulta.categoria]}",
        f"{ROTULO_ONDE_EXPOSTO if exposto else ROTULO_ONDE}: {DESCRICAO_DESTINO[consulta.destino]}",
        f"O que o sistema fez: {descricao_acao(consulta.acao, consulta.estado)}",
        f"Cor do semáforo: {DESCRICAO_COR[consulta.cor]}",
    ]
    if exposto:
        linhas.append(f"{ROTULO_TOM}: {ORIENTACAO_TOM_EXPOSTO}")
    return "\n".join(linhas)
