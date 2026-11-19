"""Catálogo de regras do motor de PII, escrito como dados.

Cada Regra diz o que procurar (padrão), como confirmar (validador), que palavra
de contexto precisa aparecer antes e com que confiança marcar. O motor só
percorre este catálogo: para incluir uma regra nova, basta acrescentar uma
entrada aqui e um teste.

Há duas famílias:
- regras de NOME DE CAMPO, para dados estruturados (dict e JSON), pelo mapa
  NOMES_DE_CAMPO;
- regras de VALOR, para texto livre, em ORDEM_TEXTO (a ordem decide quem fica
  com um trecho quando dois padrões casam no mesmo lugar).
"""

import re
import unicodedata
from collections.abc import Callable
from dataclasses import dataclass
from enum import StrEnum

from helmcare_cyber.pii.categorias import Categoria, Confianca
from helmcare_cyber.pii.validadores import (
    cnh_valida,
    coordenada_valida,
    cpf_valido,
    numero_de_casas_decimais,
    somente_digitos,
    telefone_br_valido,
)


class Exigencia(StrEnum):
    """O que precisa acontecer, além do padrão casar, para a regra marcar."""

    NADA = "NADA"
    VALIDADOR = "VALIDADOR"
    CONTEXTO = "CONTEXTO"
    VALIDADOR_E_CONTEXTO = "VALIDADOR_E_CONTEXTO"
    # Marca se o validador passa (confiança da regra) ou, sem isso, se há
    # contexto (confiança MEDIA). É a regra do Contrato para CPF e CNH em texto.
    VALIDADOR_OU_CONTEXTO = "VALIDADOR_OU_CONTEXTO"
    # Decidida pela função desempatar_onze_digitos, não pelo padrão sozinho.
    DESEMPATE = "DESEMPATE"
    # Regras de nome de campo: o nome basta.
    NOME_CAMPO = "NOME_CAMPO"


class Alvo(StrEnum):
    """Sobre qual versão do texto o padrão roda (as três têm o mesmo tamanho).

    TEXTO: o texto normalizado em NFKC.
    DOBRADO: o mesmo texto sem acentos e em minúsculas, para palavras.
    PROTEGIDO: o texto com GUIDs, timestamps ISO 8601 e números de versão
    trocados por "_", para que os padrões numéricos nunca casem dentro deles.
    """

    TEXTO = "TEXTO"
    DOBRADO = "DOBRADO"
    PROTEGIDO = "PROTEGIDO"


@dataclass(frozen=True)
class Regra:
    """Uma regra de detecção. Se o padrão tem o grupo "v", só ele é o dado sensível."""

    id: str
    categoria: Categoria
    descricao: str
    confianca: Confianca = Confianca.ALTA
    padrao: re.Pattern | None = None
    validador: Callable[[str], bool] | None = None
    contexto: re.Pattern | None = None
    exige: Exigencia = Exigencia.NADA
    alvo: Alvo = Alvo.PROTEGIDO


# ---------------------------------------------------------------------------
# Normalização de texto e de nomes de campo
# ---------------------------------------------------------------------------


def dobrar(texto: str) -> str:
    """Tira acentos e passa para minúsculas, mantendo o tamanho do texto.

    Trabalha caractere a caractere (NFD e fica só a letra base) justamente para
    que a posição de cada caractere continue a mesma do texto original: assim
    um trecho achado no texto dobrado aponta para o mesmo trecho do original.
    """
    saida = []
    for caractere in texto:
        base = unicodedata.normalize("NFD", caractere)[0]
        minuscula = base.lower()
        saida.append(minuscula if len(minuscula) == 1 else base)
    return "".join(saida)


def normalizar_nome_campo(nome) -> str:
    """tipoSanguineo, tipo_sanguineo e "Tipo Sanguíneo" viram todos "tiposanguineo"."""
    texto = dobrar(unicodedata.normalize("NFKC", str(nome)))
    return re.sub(r"[^a-z0-9]", "", texto)


# ---------------------------------------------------------------------------
# Palavras de contexto (procuradas no texto dobrado, antes do número)
# ---------------------------------------------------------------------------

JANELA_CONTEXTO = 40  # caracteres olhados para trás a partir do início do número

CONTEXTO_CPF = re.compile(r"\bcpf\b")
CONTEXTO_CNH = re.compile(r"\b(?:cnh|habilitacao|carteira)\b")
CONTEXTO_TELEFONE = re.compile(r"\b(?:telefone|tel|celular|cel|fone|whatsapp|contato)\b")
CONTEXTO_RG = re.compile(r"\b(?:rg|identidade)\b")
CONTEXTO_SANGUE = re.compile(r"\b(?:sangue|sanguineo)\b")


# Uma sequência de 4 ou mais dígitos encerra o contexto: em "celular 1198... e
# 4829...", a palavra "celular" descreve só o primeiro número.
_NUMERO_ANTERIOR = re.compile(r"\d{4,}")


def _inicio_da_janela(dobrado: str, inicio: int) -> int:
    """Começo da janela de contexto: até JANELA_CONTEXTO para trás, sem passar do número anterior."""
    janela_inicio = max(0, inicio - JANELA_CONTEXTO)
    for anterior in _NUMERO_ANTERIOR.finditer(dobrado, janela_inicio, inicio):
        janela_inicio = anterior.end()
    return janela_inicio


def tem_contexto(dobrado: str, inicio: int, contexto: re.Pattern) -> bool:
    """Procura a palavra de contexto na janela antes do trecho."""
    return contexto.search(dobrado, _inicio_da_janela(dobrado, inicio), inicio) is not None


def contexto_mais_proximo(dobrado: str, inicio: int) -> Categoria | None:
    """Entre CPF, CNH e telefone, devolve a palavra de contexto mais perto do número.

    "CPF e telefone: 1198..." tem as duas palavras na janela; a mais próxima é
    a que descreve o número.
    """
    janela_inicio = _inicio_da_janela(dobrado, inicio)
    melhor: tuple[int, Categoria] | None = None
    for categoria, padrao in (
        (Categoria.CPF, CONTEXTO_CPF),
        (Categoria.CNH, CONTEXTO_CNH),
        (Categoria.TELEFONE, CONTEXTO_TELEFONE),
    ):
        for achado in padrao.finditer(dobrado, janela_inicio, inicio):
            if melhor is None or achado.end() > melhor[0]:
                melhor = (achado.end(), categoria)
    return melhor[1] if melhor else None


# ---------------------------------------------------------------------------
# Zonas protegidas: nada dentro delas é PII
# ---------------------------------------------------------------------------

PADRAO_GUID = re.compile(r"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")
PADRAO_ISO_8601 = re.compile(
    r"\d{4}-\d{2}-\d{2}(?:[T ]\d{2}:\d{2}(?::\d{2}(?:[.,]\d+)?)?(?:Z|[+-]\d{2}(?::?\d{2})?)?)?"
)
# Três ou quatro partes numéricas separadas por ponto (3.13.17, 10.0.26200,
# 192.168.0.1). O lookahead não deixa pegar o começo de um CPF ou RG com traço.
PADRAO_VERSAO = re.compile(r"(?<![\w.])v?\d+(?:\.\d+){2,3}(?!\w|\.\d|-\d)")

ZONAS_PROTEGIDAS = (PADRAO_GUID, PADRAO_ISO_8601, PADRAO_VERSAO)


def proteger(texto: str) -> str:
    """Troca cada caractere das zonas protegidas por "_", sem mudar o tamanho.

    O "_" conta como letra para os lookarounds (\\w), então um número colado a
    uma zona protegida também não é marcado.
    """
    caracteres = list(texto)
    for padrao in ZONAS_PROTEGIDAS:
        for achado in padrao.finditer(texto):
            caracteres[achado.start() : achado.end()] = "_" * (achado.end() - achado.start())
    return "".join(caracteres)


# ---------------------------------------------------------------------------
# Validadores usados pelas regras de texto
# ---------------------------------------------------------------------------

_NUMERO_DECIMAL = re.compile(r"-?\d+\.\d+")


def _par_de_coordenadas(trecho: str) -> tuple[str, str] | None:
    numeros = _NUMERO_DECIMAL.findall(trecho)
    if len(numeros) != 2 or not coordenada_valida(numeros[0], numeros[1]):
        return None
    return numeros[0], numeros[1]


def par_exato(trecho: str) -> bool:
    """Par válido em que pelo menos um número tem 3 ou mais casas (menos de 1 km)."""
    par = _par_de_coordenadas(trecho)
    return bool(par) and max(numero_de_casas_decimais(n) for n in par) >= 3


def par_aproximado(trecho: str) -> bool:
    """Par válido com até 2 casas nos dois números (a forma aproximada do Contrato)."""
    par = _par_de_coordenadas(trecho)
    return bool(par) and max(numero_de_casas_decimais(n) for n in par) <= 2


def coordenada_exata(trecho: str) -> bool:
    """Um valor só (lat=, lon=): o rótulo não diz se é latitude ou longitude,
    então a faixa conferida é a mais larga (-180 a 180)."""
    return coordenada_valida(0, trecho) and numero_de_casas_decimais(trecho) >= 3


def coordenada_aproximada(trecho: str) -> bool:
    return coordenada_valida(0, trecho) and numero_de_casas_decimais(trecho) <= 2


def telefone_com_separador(trecho: str) -> bool:
    """Telefone escrito com parênteses, espaço, ponto, traço ou +55, e válido.

    O número só com dígitos fica para outras regras, que pedem contexto ou
    passam pelo desempate dos 11 dígitos.
    """
    return not trecho.isdigit() and telefone_br_valido(trecho)


def telefone_so_digitos(trecho: str) -> bool:
    """10, 12 ou 13 dígitos que formam telefone. Os de 11 vão para o desempate."""
    return len(trecho) != 11 and telefone_br_valido(trecho)


# ---------------------------------------------------------------------------
# Regras de NOME DE CAMPO
# ---------------------------------------------------------------------------

NOMES_DE_CAMPO: dict[str, Categoria] = {
    nome: categoria
    for categoria, nomes in {
        Categoria.CPF: ("cpf",),
        Categoria.CNH: ("cnh",),
        Categoria.RG: ("rg", "identidade"),
        Categoria.TELEFONE: ("telefone", "celular", "fone"),
        Categoria.EMAIL: ("email",),
        Categoria.NOME: ("nome",),
        Categoria.SAUDE: (
            "tiposanguineo",
            "alergias",
            "condicoescriticas",
            "medicamentos",
            "laudo",
            "diagnostico",
            "prontuario",
        ),
        Categoria.LOCALIZACAO_EXATA: ("latitude", "longitude", "lat", "lng", "lon"),
        Categoria.SENHA: ("senha", "password", "pwd", "secret"),
        Categoria.TOKEN_ACESSO: ("accesstoken", "refreshtoken", "authorization"),
        Categoria.QR_TOKEN: ("qrtoken", "urlcofre"),
        Categoria.SEGREDO_API: ("apikey", "chaveapi", "geminiapikey"),
    }.items()
    for nome in nomes
}


def _regra_campo(id_regra: str, categoria: Categoria, descricao: str) -> Regra:
    return Regra(id=id_regra, categoria=categoria, descricao=descricao, exige=Exigencia.NOME_CAMPO)


REGRA_CPF_CAMPO = _regra_campo("PII-CPF-01", Categoria.CPF, "Campo cpf")
REGRA_CNH_CAMPO = _regra_campo("PII-CNH-01", Categoria.CNH, "Campo cnh")
REGRA_RG_CAMPO = _regra_campo("PII-RG-01", Categoria.RG, "Campo rg ou identidade")
REGRA_TEL_CAMPO = _regra_campo("PII-TEL-01", Categoria.TELEFONE, "Campo telefone, celular ou fone")
REGRA_EMAIL_CAMPO = _regra_campo("PII-EMAIL-01", Categoria.EMAIL, "Campo email ou e-mail")
REGRA_NOME_CAMPO = _regra_campo("PII-NOME-01", Categoria.NOME, "Campo nome")
REGRA_SAUDE_CAMPO = _regra_campo("PII-SAUDE-01", Categoria.SAUDE, "Campos da FichaMedica e de saúde")
REGRA_LOC_EXATA_CAMPO = _regra_campo(
    "PII-LOC-01", Categoria.LOCALIZACAO_EXATA, "Campo de coordenada com 3 ou mais casas decimais"
)
REGRA_LOC_APROX_CAMPO = _regra_campo(
    "PII-LOC-02", Categoria.LOCALIZACAO_APROXIMADA, "Campo de coordenada com até 2 casas decimais"
)
REGRA_SENHA_CAMPO = _regra_campo("PII-SENHA-01", Categoria.SENHA, "Campo senha, password, pwd ou secret")
REGRA_TOKEN_CAMPO = _regra_campo(
    "PII-TOKEN-01", Categoria.TOKEN_ACESSO, "Campo accessToken, refreshToken ou authorization"
)
REGRA_QR_CAMPO = _regra_campo("PII-QR-01", Categoria.QR_TOKEN, "Campo qrToken ou urlCofre")
REGRA_SEGREDO_CAMPO = _regra_campo("PII-SEGREDO-01", Categoria.SEGREDO_API, "Campo apiKey ou chaveApi")

REGRAS_CAMPO: dict[Categoria, Regra] = {
    regra.categoria: regra
    for regra in (
        REGRA_CPF_CAMPO,
        REGRA_CNH_CAMPO,
        REGRA_RG_CAMPO,
        REGRA_TEL_CAMPO,
        REGRA_EMAIL_CAMPO,
        REGRA_NOME_CAMPO,
        REGRA_SAUDE_CAMPO,
        REGRA_LOC_EXATA_CAMPO,
        REGRA_LOC_APROX_CAMPO,
        REGRA_SENHA_CAMPO,
        REGRA_TOKEN_CAMPO,
        REGRA_QR_CAMPO,
        REGRA_SEGREDO_CAMPO,
    )
}


# ---------------------------------------------------------------------------
# Regras de VALOR (texto livre)
# ---------------------------------------------------------------------------

# Onze dígitos isolados: nem colados a letras ou a outros dígitos, nem parte
# de um número decimal. É o candidato do desempate entre CPF, CNH e telefone.
PADRAO_ONZE_DIGITOS = re.compile(r"(?<![\w.])\d{11}(?!\w|[.,]\d)")

REGRA_SEGREDO_GOOGLE = Regra(
    id="PII-SEGREDO-02",
    categoria=Categoria.SEGREDO_API,
    descricao="Chave de API do Google (AIza e mais 35 caracteres)",
    padrao=re.compile(r"(?<![A-Za-z0-9_-])AIza[0-9A-Za-z_-]{35}(?![0-9A-Za-z_-])"),
    alvo=Alvo.TEXTO,
)
REGRA_SEGREDO_SK = Regra(
    id="PII-SEGREDO-03",
    categoria=Categoria.SEGREDO_API,
    descricao="Chave genérica sk- (20 ou mais caracteres)",
    confianca=Confianca.MEDIA,
    padrao=re.compile(r"(?<![A-Za-z0-9_-])sk-[A-Za-z0-9_-]{20,}(?![A-Za-z0-9_-])"),
    alvo=Alvo.TEXTO,
)
REGRA_TOKEN_BEARER = Regra(
    id="PII-TOKEN-02",
    categoria=Categoria.TOKEN_ACESSO,
    descricao="Bearer seguido de token (16 ou mais caracteres)",
    padrao=re.compile(r"\bBearer\s+(?P<v>[A-Za-z0-9\-._~+/]{16,}=*)", re.IGNORECASE),
    alvo=Alvo.TEXTO,
)
REGRA_TOKEN_JWT = Regra(
    id="PII-TOKEN-03",
    categoria=Categoria.TOKEN_ACESSO,
    descricao="JWT (três partes base64url, começando em eyJ)",
    padrao=re.compile(r"(?<![A-Za-z0-9_-])eyJ[A-Za-z0-9_-]{8,}\.eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]*"),
    alvo=Alvo.TEXTO,
)
REGRA_QR_CAMINHO = Regra(
    id="PII-QR-02",
    categoria=Categoria.QR_TOKEN,
    descricao="Caminho /cofre/{token} com 16 ou mais caracteres de URL",
    padrao=re.compile(r"/cofre/(?P<v>[A-Za-z0-9_-]{16,})(?![A-Za-z0-9_-])"),
    alvo=Alvo.TEXTO,
)
REGRA_SENHA_TEXTO = Regra(
    id="PII-SENHA-02",
    categoria=Categoria.SENHA,
    descricao="senha=, senha:, password=, pwd= ou secret= seguido do valor",
    padrao=re.compile(
        r"\b(?:senha|password|passwd|pwd|secret)\b[\"']?\s*[:=]\s*[\"']?"
        r"(?P<v>\[redigido:[a-z_]+\]|[^\s,;&\"'}\]]+)"
    ),
    alvo=Alvo.DOBRADO,
)
REGRA_EMAIL_TEXTO = Regra(
    id="PII-EMAIL-02",
    categoria=Categoria.EMAIL,
    descricao="Endereço de e-mail",
    padrao=re.compile(
        r"(?<![\w.+%-])[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}(?![\w-])"
    ),
    alvo=Alvo.TEXTO,
)
REGRA_CPF_MASCARA = Regra(
    id="PII-CPF-02",
    categoria=Categoria.CPF,
    descricao="CPF com pontos e traço (123.456.789-09 ou 123456789-09)",
    padrao=re.compile(r"(?<![\w.])\d{3}\.?\d{3}\.?\d{3}-\d{2}(?!\w|[.-]\d)"),
    validador=cpf_valido,
    contexto=CONTEXTO_CPF,
    exige=Exigencia.VALIDADOR_OU_CONTEXTO,
)
REGRA_RG_TEXTO = Regra(
    id="PII-RG-02",
    categoria=Categoria.RG,
    descricao="RG (12.345.678-9) com a palavra rg ou identidade antes",
    padrao=re.compile(r"(?<![\w.])\d{1,2}\.?\d{3}\.?\d{3}-?[\dXx](?!\w|[.-]\d)"),
    contexto=CONTEXTO_RG,
    exige=Exigencia.CONTEXTO,
)
_PADRAO_PAR = re.compile(r"(?<![\w.])-?\d{1,2}\.\d{2,}\s*[,;]\s*-?\d{1,3}\.\d{2,}(?!\w|\.\d)")
REGRA_LOC_PAR_EXATO = Regra(
    id="PII-LOC-03",
    categoria=Categoria.LOCALIZACAO_EXATA,
    descricao="Par de coordenadas com 3 ou mais casas decimais",
    padrao=_PADRAO_PAR,
    validador=par_exato,
    exige=Exigencia.VALIDADOR,
)
REGRA_LOC_PAR_APROX = Regra(
    id="PII-LOC-04",
    categoria=Categoria.LOCALIZACAO_APROXIMADA,
    descricao="Par de coordenadas com até 2 casas decimais",
    padrao=_PADRAO_PAR,
    validador=par_aproximado,
    exige=Exigencia.VALIDADOR,
)
_PADRAO_COORDENADA_ROTULADA = re.compile(
    r"\b(?:latitude|longitude|lat|lng|lon)\b[\"']?\s*[:=]\s*[\"']?(?P<v>-?\d{1,3}\.\d+)(?!\w|\.\d)"
)
REGRA_LOC_ROTULO_EXATO = Regra(
    id="PII-LOC-05",
    categoria=Categoria.LOCALIZACAO_EXATA,
    descricao="lat=, latitude:, lon= e afins com 3 ou mais casas",
    padrao=_PADRAO_COORDENADA_ROTULADA,
    validador=coordenada_exata,
    exige=Exigencia.VALIDADOR,
    alvo=Alvo.DOBRADO,
)
REGRA_LOC_ROTULO_APROX = Regra(
    id="PII-LOC-06",
    categoria=Categoria.LOCALIZACAO_APROXIMADA,
    descricao="lat=, latitude:, lon= e afins com até 2 casas",
    padrao=_PADRAO_COORDENADA_ROTULADA,
    validador=coordenada_aproximada,
    exige=Exigencia.VALIDADOR,
    alvo=Alvo.DOBRADO,
)

# As quatro regras abaixo saem do desempate dos 11 dígitos (função mais abaixo).
REGRA_CPF_DIGITOS = Regra(
    id="PII-CPF-03",
    categoria=Categoria.CPF,
    descricao="CPF só com dígitos: contexto cpf ou DV do CPF",
    padrao=PADRAO_ONZE_DIGITOS,
    validador=cpf_valido,
    contexto=CONTEXTO_CPF,
    exige=Exigencia.DESEMPATE,
)
REGRA_CNH_CONTEXTO = Regra(
    id="PII-CNH-02",
    categoria=Categoria.CNH,
    descricao="CNH (11 dígitos) com cnh, habilitação ou carteira antes",
    padrao=PADRAO_ONZE_DIGITOS,
    validador=cnh_valida,
    contexto=CONTEXTO_CNH,
    exige=Exigencia.DESEMPATE,
)
REGRA_CNH_DV = Regra(
    id="PII-CNH-03",
    categoria=Categoria.CNH,
    descricao="CNH (11 dígitos) sem contexto, pelo DV da CNH",
    confianca=Confianca.MEDIA,
    padrao=PADRAO_ONZE_DIGITOS,
    validador=cnh_valida,
    exige=Exigencia.DESEMPATE,
)
REGRA_TEL_ONZE = Regra(
    id="PII-TEL-04",
    categoria=Categoria.TELEFONE,
    descricao="Celular só com 11 dígitos, sem contexto e sem DV de CPF ou CNH",
    confianca=Confianca.MEDIA,
    padrao=PADRAO_ONZE_DIGITOS,
    validador=telefone_br_valido,
    exige=Exigencia.DESEMPATE,
)

REGRA_TEL_SEPARADOR = Regra(
    id="PII-TEL-02",
    categoria=Categoria.TELEFONE,
    descricao="Telefone com DDD e separadores ou +55 ((11) 98765-4321, +55 11 3456-7890)",
    padrao=re.compile(
        r"(?<![\w.+-])(?:\+55[\s.-]?|55[\s.-])?(?:\(\d{2}\)[\s.-]?|\d{2}[\s.-]?)"
        r"(?:9\d{4}|\d{4})[\s.-]?\d{4}(?!\w|[.-]\d)"
    ),
    validador=telefone_com_separador,
    exige=Exigencia.VALIDADOR,
)
REGRA_TEL_DIGITOS = Regra(
    id="PII-TEL-03",
    categoria=Categoria.TELEFONE,
    descricao="Telefone só com dígitos (10, 12 ou 13) com telefone, celular ou fone antes",
    padrao=re.compile(r"(?<![\w.+])\d{10,13}(?!\w|[.,]\d)"),
    validador=telefone_so_digitos,
    contexto=CONTEXTO_TELEFONE,
    exige=Exigencia.VALIDADOR_E_CONTEXTO,
)
REGRA_SAUDE_CID = Regra(
    id="PII-SAUDE-04",
    categoria=Categoria.SAUDE,
    descricao="Código CID (CID J45, CID-10 E11.9)",
    padrao=re.compile(r"\bcid(?:[\s-]?10)?\s*[:-]?\s*(?P<v>[a-tv-z]\d{2}(?:\.\d{1,2})?)\b"),
    alvo=Alvo.DOBRADO,
)
REGRA_SAUDE_SANGUE = Regra(
    id="PII-SAUDE-02",
    categoria=Categoria.SAUDE,
    descricao="Tipo sanguíneo com sangue ou tipo sanguíneo antes",
    padrao=re.compile(r"(?<![\w+-])(?:AB|A|B|O)[+-](?![\w+-])"),
    contexto=CONTEXTO_SANGUE,
    exige=Exigencia.CONTEXTO,
    alvo=Alvo.TEXTO,
)
REGRA_SAUDE_LEXICO = Regra(
    id="PII-SAUDE-03",
    categoria=Categoria.SAUDE,
    descricao="Termos de saúde (alergia, diabetes, insulina, laudo, uso contínuo...)",
    confianca=Confianca.MEDIA,
    padrao=re.compile(
        r"\b(?:alergi(?:a|as|co|ca|cos|cas)|diabet(?:es|ico|ica|icos|icas)|hipertens(?:ao|o|a)"
        r"|asma(?:tico|tica)?|epilep(?:sia|tico|tica)|cardiopat(?:ia|ias|a|as)|marca-?passos?"
        r"|anticoagulantes?|insulina|laudos?|prontuarios?|diagnosticos?|medicamentos?"
        r"|uso continuo|condic(?:ao|oes) criticas?)\b"
    ),
    alvo=Alvo.DOBRADO,
)

# Marca, dentro de ORDEM_TEXTO, o ponto em que o desempate dos 11 dígitos roda.
ETAPA_DESEMPATE = "DESEMPATE_ONZE_DIGITOS"

# Ordem de prioridade: quem vem antes fica com o trecho. Credenciais primeiro
# (senha=12345678909 é senha, não CPF), depois os formatos mais específicos
# (CPF com máscara antes do telefone, par de coordenadas antes de números
# soltos) e, por último, o léxico de saúde, que é o mais genérico.
ORDEM_TEXTO: tuple[Regra | str, ...] = (
    REGRA_SEGREDO_GOOGLE,
    REGRA_SEGREDO_SK,
    REGRA_TOKEN_JWT,
    REGRA_TOKEN_BEARER,
    REGRA_QR_CAMINHO,
    REGRA_SENHA_TEXTO,
    REGRA_EMAIL_TEXTO,
    REGRA_CPF_MASCARA,
    REGRA_RG_TEXTO,
    REGRA_LOC_PAR_EXATO,
    REGRA_LOC_PAR_APROX,
    REGRA_LOC_ROTULO_EXATO,
    REGRA_LOC_ROTULO_APROX,
    ETAPA_DESEMPATE,
    REGRA_TEL_SEPARADOR,
    REGRA_TEL_DIGITOS,
    REGRA_SAUDE_CID,
    REGRA_SAUDE_SANGUE,
    REGRA_SAUDE_LEXICO,
)

REGRAS_TEXTO: tuple[Regra, ...] = tuple(r for r in ORDEM_TEXTO if isinstance(r, Regra)) + (
    REGRA_CPF_DIGITOS,
    REGRA_CNH_CONTEXTO,
    REGRA_CNH_DV,
    REGRA_TEL_ONZE,
)

TODAS_AS_REGRAS: tuple[Regra, ...] = tuple(REGRAS_CAMPO.values()) + REGRAS_TEXTO
REGRAS_POR_ID: dict[str, Regra] = {regra.id: regra for regra in TODAS_AS_REGRAS}


@dataclass(frozen=True)
class Decisao:
    """Resultado do desempate: qual regra marcou, se o DV confere e com que confiança."""

    regra: Regra
    dv_confere: bool | None
    confianca: Confianca


def desempatar_onze_digitos(dobrado: str, inicio: int, trecho: str) -> Decisao | None:
    """Decide o que são 11 dígitos soltos no texto.

    1. A palavra de contexto mais próxima antes do número decide (cpf, cnh,
       habilitação, carteira, telefone, celular...). Com contexto, marca mesmo
       que o DV não confira: a confiança é ALTA se confere e MEDIA se não.
    2. Sem contexto, vale o dígito verificador: primeiro o do CPF, depois o da
       CNH (confiança MEDIA, porque 1 em cada 100 números passa no DV por acaso).
    3. Se nenhum DV confere e o número tem formato de celular, é telefone.
    4. Se nada disso vale, não marca.
    """
    digitos = somente_digitos(trecho)
    contexto = contexto_mais_proximo(dobrado, inicio)
    if contexto is Categoria.CPF:
        dv = cpf_valido(digitos)
        return Decisao(REGRA_CPF_DIGITOS, dv, Confianca.ALTA if dv else Confianca.MEDIA)
    if contexto is Categoria.CNH:
        dv = cnh_valida(digitos)
        return Decisao(REGRA_CNH_CONTEXTO, dv, Confianca.ALTA if dv else Confianca.MEDIA)
    if contexto is Categoria.TELEFONE:
        valido = telefone_br_valido(digitos)
        return Decisao(REGRA_TEL_DIGITOS, None, Confianca.ALTA if valido else Confianca.MEDIA)
    if cpf_valido(digitos):
        return Decisao(REGRA_CPF_DIGITOS, True, Confianca.MEDIA)
    if cnh_valida(digitos):
        return Decisao(REGRA_CNH_DV, True, Confianca.MEDIA)
    if telefone_br_valido(digitos):
        return Decisao(REGRA_TEL_ONZE, None, Confianca.MEDIA)
    return None
