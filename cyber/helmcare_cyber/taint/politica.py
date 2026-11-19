"""Política de saída do Taint Analysis, baseada na seção 9 do Contrato.

A política é dado: uma tabela de linhas (destino, destinatário) com a ação de
cada categoria, mais exceções com condição (caminho do campo no Cofre,
identificação pessoal no MQTT, hash na persistência, credencial no corpo do
login). decidir() só consulta a tabela.

Negação por padrão: toda combinação que não está na tabela, e toda categoria
que uma linha não cita, é BLOQUEAR. Uma saída nova nunca nasce aberta.

Ordem de avaliação:
1. TA-MASCARADO-01: valor já redigido ([REDIGIDO:...] ou [BLOQUEADO:...]) passa,
   porque não sobra nada do dado.
2. TA-NEGADO-01: destinatário não autorizado bloqueia tudo, em qualquer destino.
3. A linha do destino com aquele destinatário; depois a linha do destino para
   qualquer destinatário.
4. TA-PADRAO-01: nada encontrado, bloqueia.
"""

import re
from collections.abc import Callable, Mapping
from dataclasses import dataclass, field

from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.pii.mascaras import PADRAO_REDIGIDO
from helmcare_cyber.pii.motor import Achado
from helmcare_cyber.taint.modelo import Acao, ContextoSaida, Destinatario, Destino
from helmcare_cyber.taint.sanitizadores import eh_hash_senha

# ---------------------------------------------------------------------------
# Identificadores das regras (citados no rastro, nos eventos e no relatório)
# ---------------------------------------------------------------------------

TA_PADRAO_01 = "TA-PADRAO-01"
TA_MASCARADO_01 = "TA-MASCARADO-01"
TA_PROPRIO_01 = "TA-PROPRIO-01"
TA_PROPRIO_02 = "TA-PROPRIO-02"
TA_VOLUNT_01 = "TA-VOLUNT-01"
TA_ACEITOU_01 = "TA-ACEITOU-01"
TA_CONTATO_01 = "TA-CONTATO-01"
TA_COFRE_01 = "TA-COFRE-01"
TA_COFRE_02 = "TA-COFRE-02"
TA_COFRE_03 = "TA-COFRE-03"
TA_NEGADO_01 = "TA-NEGADO-01"
TA_LOG_01 = "TA-LOG-01"
TA_MQTT_01 = "TA-MQTT-01"
TA_MQTT_02 = "TA-MQTT-02"
TA_PUBLICO_01 = "TA-PUBLICO-01"
TA_PERS_01 = "TA-PERS-01"
TA_PERS_02 = "TA-PERS-02"
# Regras do próprio guarda (não são da tabela, mas aparecem nos eventos).
TA_ERRO_01 = "TA-ERRO-01"  # falha interna: a saída é bloqueada inteira
TA_REG_01 = "TA-REG-01"  # achado encontrado pelo valor registrado na fonte

DESCRICOES_REGRAS: dict[str, str] = {
    TA_PADRAO_01: "Combinação de destino, destinatário e categoria fora da política: bloqueia (negação por padrão).",
    TA_MASCARADO_01: "Valor já redigido: não sobra nada do dado, então pode sair.",
    TA_PROPRIO_01: "Resposta ao próprio entregador: vê os próprios dados, com CPF, CNH, RG e e-mail mascarados; "
    "credenciais não saem.",
    TA_PROPRIO_02: "Token de acesso e qrToken só no corpo do login, da renovação e do POST /dispositivos.",
    TA_VOLUNT_01: "Voluntário antes do aceite: só a localização aproximada (2 casas).",
    TA_ACEITOU_01: "Voluntário que aceitou: recebe a localização exata, nada além dela.",
    TA_CONTATO_01: "Contato de emergência: recebe a localização, exata ou aproximada, nada além dela.",
    TA_COFRE_01: "Cofre ao socorrista verificado na janela: só tipoSanguineo, alergias e condicoesCriticas.",
    TA_COFRE_02: "Cofre ao socorrista verificado na janela: nome e telefone dos contatos de emergência.",
    TA_COFRE_03: "Cofre: qualquer outro dado (medicamentos, nome, CPF, CNH, localização, credenciais) é bloqueado.",
    TA_NEGADO_01: "Destinatário sem autorização (socorrista não autorizado, anônimo ou outro usuário): bloqueia tudo.",
    TA_LOG_01: "Log: todo dado pessoal é redigido, inclusive a localização aproximada.",
    TA_MQTT_01: "MQTT sem identificação pessoal: a coordenada pode seguir com o código do dispositivo.",
    TA_MQTT_02: "MQTT com identificação pessoal: bloqueia o dado e a coordenada.",
    TA_PUBLICO_01: "Resposta pública, mensagem de erro, auditoria e documentação pública: nenhum dado pessoal.",
    TA_PERS_01: "Persistência: senha só como hash; token de acesso e segredo de API nunca são gravados.",
    TA_PERS_02: "Persistência: senha no formato de hash pode ser gravada.",
    TA_ERRO_01: "Erro interno na verificação: a saída é bloqueada inteira (falha fechada).",
    TA_REG_01: "Valor registrado na fonte encontrado na saída (detecção pelo registro de taint).",
}

# ---------------------------------------------------------------------------
# Grupos de categorias
# ---------------------------------------------------------------------------

DOCUMENTOS = (Categoria.CPF, Categoria.CNH, Categoria.RG)
CREDENCIAIS = (Categoria.SENHA, Categoria.TOKEN_ACESSO, Categoria.QR_TOKEN, Categoria.SEGREDO_API)
LOCALIZACAO = (Categoria.LOCALIZACAO_EXATA, Categoria.LOCALIZACAO_APROXIMADA)
# O que torna um payload "com identificação pessoal" para a regra do MQTT.
IDENTIFICACAO_PESSOAL = frozenset(set(Categoria) - set(LOCALIZACAO))

DESTINATARIOS_NEGADOS = frozenset(
    {Destinatario.SOCORRISTA_NAO_AUTORIZADO, Destinatario.ANONIMO, Destinatario.OUTRO_USUARIO}
)

CAMPOS_COFRE_SAUDE = frozenset({"tipoSanguineo", "alergias", "condicoesCriticas"})
_CAMINHO_CONTATO_COFRE = re.compile(r"\$\.contatos\[\d+\](?:\.|\[|$)")
_INDICES_FINAIS = re.compile(r"(?:\[\d+\])+$")
_ULTIMO_SEGMENTO = re.compile(r"(?:\.([A-Za-z_][A-Za-z0-9_]*)|\[\"((?:[^\"\\]|\\.)*)\"\])$")
_MARCADOR_BLOQUEADO = re.compile(r"\[BLOQUEADO:[A-Z_]+\]")

# Condição de uma exceção: (achado, contexto, identificacao_pessoal_no_payload) -> bool.
Condicao = Callable[[Achado, ContextoSaida, bool], bool]


@dataclass(frozen=True)
class Excecao:
    """Troca a ação de algumas categorias quando a condição vale."""

    regra_id: str
    categorias: frozenset[Categoria]
    acao: Acao
    condicao: Condicao
    motivo: str


@dataclass(frozen=True)
class LinhaPolitica:
    """Ação por categoria para um destino (e destinatário). Categoria ausente usa padrao."""

    regra_id: str
    acoes: Mapping[Categoria, Acao]
    padrao: Acao = Acao.BLOQUEAR
    excecoes: tuple[Excecao, ...] = field(default=())


def ultimo_segmento(caminho: str) -> str:
    """$.alergias[0] e $.ficha.alergias devolvem "alergias"; índices finais são ignorados."""
    sem_indices = _INDICES_FINAIS.sub("", caminho)
    casamento = _ULTIMO_SEGMENTO.search(sem_indices)
    if not casamento:
        return ""
    return casamento.group(1) if casamento.group(1) is not None else casamento.group(2)


def esta_redigido(valor: str) -> bool:
    texto = valor.strip()
    return bool(PADRAO_REDIGIDO.fullmatch(texto) or _MARCADOR_BLOQUEADO.fullmatch(texto))


def _todas(acao: Acao, *categorias: Categoria) -> dict[Categoria, Acao]:
    return {categoria: acao for categoria in categorias}


# ---------------------------------------------------------------------------
# Linhas da matriz
# ---------------------------------------------------------------------------

_P, _M, _A, _R, _B = Acao.PERMITIR, Acao.MASCARAR, Acao.ARREDONDAR, Acao.REDIGIR, Acao.BLOQUEAR

LINHA_PROPRIO = LinhaPolitica(
    regra_id=TA_PROPRIO_01,
    acoes={
        Categoria.SAUDE: _P,
        **_todas(_M, *DOCUMENTOS),
        Categoria.TELEFONE: _P,
        Categoria.NOME: _P,
        Categoria.EMAIL: _M,
        **_todas(_P, *LOCALIZACAO),
        **_todas(_B, *CREDENCIAIS),
    },
    excecoes=(
        Excecao(
            TA_PROPRIO_02,
            frozenset({Categoria.TOKEN_ACESSO, Categoria.QR_TOKEN}),
            _P,
            lambda achado, contexto, ident: contexto.credencial_autorizada,
            "credencial no corpo autorizado (login, renovação ou POST /dispositivos)",
        ),
    ),
)

LINHA_VOLUNTARIO_ANTES = LinhaPolitica(
    regra_id=TA_VOLUNT_01,
    acoes={Categoria.LOCALIZACAO_EXATA: _A, Categoria.LOCALIZACAO_APROXIMADA: _P},
)

LINHA_VOLUNTARIO_ACEITOU = LinhaPolitica(
    regra_id=TA_ACEITOU_01,
    acoes={Categoria.LOCALIZACAO_EXATA: _P, Categoria.LOCALIZACAO_APROXIMADA: _P},
)

LINHA_CONTATO = LinhaPolitica(regra_id=TA_CONTATO_01, acoes=_todas(_P, *LOCALIZACAO))

LINHA_COFRE = LinhaPolitica(
    regra_id=TA_COFRE_03,
    acoes={},
    excecoes=(
        Excecao(
            TA_COFRE_01,
            frozenset({Categoria.SAUDE}),
            _P,
            lambda achado, contexto, ident: ultimo_segmento(achado.caminho) in CAMPOS_COFRE_SAUDE,
            "campo da ficha mínima do Cofre",
        ),
        Excecao(
            TA_COFRE_02,
            frozenset({Categoria.NOME, Categoria.TELEFONE}),
            _P,
            lambda achado, contexto, ident: bool(_CAMINHO_CONTATO_COFRE.match(achado.caminho)),
            "contato de emergência, com telefone completo para a ligação",
        ),
    ),
)

LINHA_LOG = LinhaPolitica(regra_id=TA_LOG_01, acoes={}, padrao=_R)

LINHA_MQTT = LinhaPolitica(
    regra_id=TA_MQTT_02,
    acoes={},
    excecoes=(
        Excecao(
            TA_MQTT_01,
            frozenset(LOCALIZACAO),
            _P,
            lambda achado, contexto, ident: not ident,
            "coordenada sem identificação pessoal no payload",
        ),
    ),
)

LINHA_PUBLICO = LinhaPolitica(regra_id=TA_PUBLICO_01, acoes={})

LINHA_PERSISTENCIA = LinhaPolitica(
    regra_id=TA_PERS_01,
    acoes={categoria: _P for categoria in Categoria}
    | _todas(_B, Categoria.SENHA, Categoria.TOKEN_ACESSO, Categoria.SEGREDO_API),
    excecoes=(
        Excecao(
            TA_PERS_02,
            frozenset({Categoria.SENHA}),
            _P,
            lambda achado, contexto, ident: eh_hash_senha(achado.valor_original()),
            "senha já no formato de hash",
        ),
    ),
)

# Chave: (destino, destinatário). Destinatário None vale para qualquer destinatário.
TABELA_POLITICA: dict[tuple[Destino, Destinatario | None], LinhaPolitica] = {
    (Destino.RESPOSTA_PROPRIO_ENTREGADOR, Destinatario.PROPRIO_ENTREGADOR): LINHA_PROPRIO,
    (Destino.CHAMADO_VOLUNTARIO, Destinatario.VOLUNTARIO_ANTES_ACEITE): LINHA_VOLUNTARIO_ANTES,
    (Destino.NOTIFICACAO_PUSH, Destinatario.VOLUNTARIO_ANTES_ACEITE): LINHA_VOLUNTARIO_ANTES,
    (Destino.CHAMADO_VOLUNTARIO, Destinatario.VOLUNTARIO_QUE_ACEITOU): LINHA_VOLUNTARIO_ACEITOU,
    (Destino.NOTIFICACAO_PUSH, Destinatario.VOLUNTARIO_QUE_ACEITOU): LINHA_VOLUNTARIO_ACEITOU,
    (Destino.CONTATO_EMERGENCIA, None): LINHA_CONTATO,
    (Destino.NOTIFICACAO_PUSH, Destinatario.CONTATO_EMERGENCIA): LINHA_CONTATO,
    (Destino.COFRE_SOCORRISTA, Destinatario.SOCORRISTA_VERIFICADO_NA_JANELA): LINHA_COFRE,
    (Destino.LOG, None): LINHA_LOG,
    (Destino.MQTT_PAYLOAD, None): LINHA_MQTT,
    (Destino.RESPOSTA_API_PUBLICA, None): LINHA_PUBLICO,
    (Destino.MENSAGEM_ERRO, None): LINHA_PUBLICO,
    (Destino.AUDITORIA, None): LINHA_PUBLICO,
    (Destino.DOCUMENTACAO_PUBLICA, None): LINHA_PUBLICO,
    (Destino.PERSISTENCIA, None): LINHA_PERSISTENCIA,
}


def linha_para(contexto: ContextoSaida) -> LinhaPolitica | None:
    """A linha do destinatário exato vem antes da linha genérica do destino."""
    return TABELA_POLITICA.get((contexto.destino, contexto.destinatario)) or TABELA_POLITICA.get(
        (contexto.destino, None)
    )


def decidir(
    achado: Achado, contexto: ContextoSaida, identificacao_pessoal_no_payload: bool = False
) -> tuple[Acao, str, str]:
    """Devolve (ação, regra_id, motivo) para um achado numa saída. Função pura."""
    # Só o original decide: o valor_mascarado de SAUDE, por exemplo, já é
    # [REDIGIDO:SAUDE] e faria qualquer dado de saúde passar.
    if esta_redigido(achado.valor_original()):
        return Acao.PERMITIR, TA_MASCARADO_01, "valor já redigido"

    if contexto.destinatario in DESTINATARIOS_NEGADOS:
        return Acao.BLOQUEAR, TA_NEGADO_01, f"destinatário {contexto.destinatario.value} sem autorização"

    linha = linha_para(contexto)
    if linha is None:
        return (
            Acao.BLOQUEAR,
            TA_PADRAO_01,
            f"{contexto.destino.value} para {contexto.destinatario.value} não está na política",
        )

    for excecao in linha.excecoes:
        if achado.categoria in excecao.categorias and excecao.condicao(
            achado, contexto, identificacao_pessoal_no_payload
        ):
            return excecao.acao, excecao.regra_id, excecao.motivo

    acao = linha.acoes.get(achado.categoria, linha.padrao)
    return acao, linha.regra_id, f"{achado.categoria.value} em {contexto.destino.value}: {acao.value}"
