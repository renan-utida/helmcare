"""Vocabulário do Taint Analysis: de onde o dado vem, para onde vai e o que acontece com ele.

Todos os enums são StrEnum, para irem ao JSON como texto em UPPER_SNAKE_CASE,
no padrão dos enums do Contrato.
"""

from dataclasses import dataclass
from enum import StrEnum

from helmcare_cyber.pii.categorias import Categoria


class Fonte(StrEnum):
    """Onde o dado pessoal entra no sistema (a origem da contaminação)."""

    CADASTRO_ENTREGADOR = "CADASTRO_ENTREGADOR"
    FICHA_MEDICA = "FICHA_MEDICA"
    CONTATO_EMERGENCIA = "CONTATO_EMERGENCIA"
    OCORRENCIA_SOS = "OCORRENCIA_SOS"
    TELEMETRIA_MQTT = "TELEMETRIA_MQTT"
    LOGIN = "LOGIN"
    DISPOSITIVO = "DISPOSITIVO"


class Destino(StrEnum):
    """Por onde o dado sai (o ponto em que um vazamento acontece)."""

    LOG = "LOG"
    RESPOSTA_API_PUBLICA = "RESPOSTA_API_PUBLICA"
    MENSAGEM_ERRO = "MENSAGEM_ERRO"
    NOTIFICACAO_PUSH = "NOTIFICACAO_PUSH"
    MQTT_PAYLOAD = "MQTT_PAYLOAD"
    CHAMADO_VOLUNTARIO = "CHAMADO_VOLUNTARIO"
    AUDITORIA = "AUDITORIA"
    PERSISTENCIA = "PERSISTENCIA"
    COFRE_SOCORRISTA = "COFRE_SOCORRISTA"
    CONTATO_EMERGENCIA = "CONTATO_EMERGENCIA"
    RESPOSTA_PROPRIO_ENTREGADOR = "RESPOSTA_PROPRIO_ENTREGADOR"
    DOCUMENTACAO_PUBLICA = "DOCUMENTACAO_PUBLICA"


class Destinatario(StrEnum):
    """Quem recebe a saída. Decide, junto com o destino, o que pode passar."""

    PROPRIO_ENTREGADOR = "PROPRIO_ENTREGADOR"
    VOLUNTARIO_ANTES_ACEITE = "VOLUNTARIO_ANTES_ACEITE"
    VOLUNTARIO_QUE_ACEITOU = "VOLUNTARIO_QUE_ACEITOU"
    CONTATO_EMERGENCIA = "CONTATO_EMERGENCIA"
    SOCORRISTA_VERIFICADO_NA_JANELA = "SOCORRISTA_VERIFICADO_NA_JANELA"
    SOCORRISTA_NAO_AUTORIZADO = "SOCORRISTA_NAO_AUTORIZADO"
    ANONIMO = "ANONIMO"
    OUTRO_USUARIO = "OUTRO_USUARIO"
    SISTEMA = "SISTEMA"


class Acao(StrEnum):
    """O que a política manda fazer com o dado antes de ele sair."""

    PERMITIR = "PERMITIR"
    MASCARAR = "MASCARAR"
    ARREDONDAR = "ARREDONDAR"
    REDIGIR = "REDIGIR"
    BLOQUEAR = "BLOQUEAR"


class Modo(StrEnum):
    """PROTECAO aplica a ação; AUDITORIA só detecta e deixa a saída passar intacta."""

    PROTECAO = "PROTECAO"
    AUDITORIA = "AUDITORIA"


class Estado(StrEnum):
    """Como o dado terminou na saída.

    PERMITIDO: canal autorizado, ou o valor já estava na forma exigida.
    NEUTRALIZADO: a ação foi aplicada (modo PROTECAO).
    EXPOSTO: a política mandava agir, mas o modo AUDITORIA deixou passar.
    """

    PERMITIDO = "PERMITIDO"
    NEUTRALIZADO = "NEUTRALIZADO"
    EXPOSTO = "EXPOSTO"


# Ordem de gravidade, usada para a decisão geral de uma saída (o pior estado vence).
GRAVIDADE_ESTADO = {Estado.PERMITIDO: 0, Estado.NEUTRALIZADO: 1, Estado.EXPOSTO: 2}


def pior_estado(estados) -> Estado:
    """EXPOSTO > NEUTRALIZADO > PERMITIDO. Sem estados, PERMITIDO."""
    return max(estados, key=GRAVIDADE_ESTADO.__getitem__, default=Estado.PERMITIDO)


@dataclass(frozen=True)
class ContextoSaida:
    """Descreve a saída que está sendo verificada.

    credencial_autorizada vale True só no corpo do login, da renovação de token
    e do POST /dispositivos, as únicas respostas em que o Contrato deixa sair
    token de acesso e qrToken (seções 7 e 9).
    """

    destino: Destino
    destinatario: Destinatario = Destinatario.SISTEMA
    credencial_autorizada: bool = False


@dataclass(frozen=True)
class DecisaoAchado:
    """A decisão da política para um achado. Nunca guarda o valor, nem mascarado."""

    caminho: str
    categoria: Categoria
    acao: Acao
    regra_id: str
    estado: Estado
    motivo: str

    def para_dict(self) -> dict:
        return {
            "caminho": self.caminho,
            "categoria": self.categoria.value,
            "acao": self.acao.value,
            "regraId": self.regra_id,
            "estado": self.estado.value,
            "motivo": self.motivo,
        }
