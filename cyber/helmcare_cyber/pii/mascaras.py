"""Máscaras de dados pessoais, conforme a seção 9 do Contrato.

Máscaras padrão do Contrato:
- CPF: ***.***.***-12 (só os 2 últimos dígitos ficam visíveis)
- CNH: *******8900 (só os 4 últimos)
- celular: (11) *****-1234 e telefone fixo: (11) ****-4567 (DDD e 4 últimos)
- e-mail: r***@exemplo.com (primeira letra, *** e o domínio, seção 7)
- localização aproximada: arredondada para 2 casas decimais (cerca de 1 km)

Para as categorias sem máscara definida no Contrato (saúde, nome, RG, senhas,
tokens, qrToken e segredos), a estratégia é redigir por completo: nenhum
pedaço do valor sobra. Toda máscara é idempotente: aplicar de novo sobre um
valor já mascarado devolve o mesmo valor.
"""

import re
from collections.abc import Callable
from decimal import ROUND_HALF_UP, Decimal, InvalidOperation

from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.pii.validadores import numero_de_casas_decimais, somente_digitos

PADRAO_REDIGIDO = re.compile(r"\[REDIGIDO:[A-Z_]+\]")

# Formas mascaradas que o próprio Contrato publica. Um valor nesse formato não é
# vazamento: é exatamente o que a API deve devolver (por exemplo, o email do
# GET /entregadores/{id}).
_MASCARAS_RECONHECIDAS: dict[Categoria, re.Pattern] = {
    Categoria.CPF: re.compile(r"\*{3}\.\*{3}\.\*{3}-\d{2}"),
    Categoria.CNH: re.compile(r"\*{7}\d{4}"),
    Categoria.TELEFONE: re.compile(r"\(\d{2}\) (?:\*{5}|\*{4})-\d{4}"),
    Categoria.EMAIL: re.compile(r"[^@\s*]\*{3}@[^@\s]+"),
}

# Número decimal com 3 ou mais casas: é o que denuncia uma coordenada exata.
_NUMERO_EXATO = re.compile(r"-?\d+\.\d{3,}")

# Um número ou um par de números com até 2 casas ("-23.56" ou "-23.56, -46.65"):
# é a coordenada que já passou pelo arredondamento.
_COORDENADAS_ARREDONDADAS = re.compile(r"-?\d+(?:[.,]\d{1,2})?(?:\s*[,;]\s*-?\d+(?:\.\d{1,2})?)?")


def redigir(categoria: Categoria) -> str:
    """Marcador sem nenhum dígito do valor, para logs e eventos: [REDIGIDO:CPF]."""
    return f"[REDIGIDO:{Categoria(categoria).value}]"


def mascarar_cpf(valor) -> str:
    """***.***.***-12. Sem 11 dígitos, não há como mascarar com segurança: redige."""
    digitos = somente_digitos(valor)
    if len(digitos) != 11:
        return redigir(Categoria.CPF)
    return f"***.***.***-{digitos[-2:]}"


def mascarar_cnh(valor) -> str:
    """*******8900. Sem 11 dígitos, redige."""
    digitos = somente_digitos(valor)
    if len(digitos) != 11:
        return redigir(Categoria.CNH)
    return f"*******{digitos[-4:]}"


def mascarar_telefone(valor) -> str:
    """(11) *****-1234 para celular e (11) ****-4567 para fixo.

    O código do país (55) é descartado antes, porque a máscara do Contrato
    começa pelo DDD. Tamanho fora de 10 ou 11 dígitos é redigido.
    """
    digitos = somente_digitos(valor)
    if len(digitos) in (12, 13) and digitos.startswith("55"):
        digitos = digitos[2:]
    if len(digitos) == 11:
        return f"({digitos[:2]}) *****-{digitos[-4:]}"
    if len(digitos) == 10:
        return f"({digitos[:2]}) ****-{digitos[-4:]}"
    return redigir(Categoria.TELEFONE)


def mascarar_email(valor) -> str:
    """r***@exemplo.com: a primeira letra, *** e o domínio. Sem @, redige."""
    texto = str(valor).strip() if valor is not None else ""
    local, arroba, dominio = texto.rpartition("@")
    if not arroba or not local or not dominio:
        return redigir(Categoria.EMAIL)
    return f"{local[0]}***@{dominio}"


def arredondar_localizacao(valor) -> float:
    """Arredonda uma coordenada para 2 casas, com 0,5 para cima.

    Usa Decimal sobre o texto do número para não sofrer com a representação
    binária do float (round(2.675, 2) daria 2.67). Valor que não é número
    levanta ValueError, para quem chama decidir redigir.
    """
    texto = repr(valor) if isinstance(valor, float) else str(valor).strip().replace(",", ".")
    try:
        numero = Decimal(texto)
    except InvalidOperation as erro:
        raise ValueError("coordenada inválida") from erro
    return float(numero.quantize(Decimal("0.01"), rounding=ROUND_HALF_UP))


def mascarar_localizacao(valor) -> float | str:
    """Número vira número com 2 casas; texto tem cada coordenada exata arredondada.

    O texto cobre o par "-23.5641, -46.6525", que vira "-23.56, -46.65".
    """
    if isinstance(valor, (int, float)) and not isinstance(valor, bool):
        return arredondar_localizacao(valor)
    texto = str(valor)
    if _NUMERO_EXATO.search(texto):
        return _NUMERO_EXATO.sub(lambda m: f"{arredondar_localizacao(m.group(0)):.2f}", texto)
    try:
        return arredondar_localizacao(texto)
    except ValueError:
        return redigir(Categoria.LOCALIZACAO_EXATA)


def _manter(valor):
    """Localização aproximada já é a forma permitida pelo Contrato: fica como está."""
    return valor


def esta_mascarado(categoria: Categoria, valor) -> bool:
    """Diz se o valor já está numa forma segura, para não ser tratado como vazamento.

    Vale para: o marcador [REDIGIDO:...], um texto só de asteriscos, as máscaras
    do Contrato para cada categoria e coordenadas com até 2 casas decimais.
    """
    if categoria is Categoria.LOCALIZACAO_APROXIMADA:
        # A aproximada (2 casas) já é a forma que o Contrato permite mostrar.
        return True
    if categoria is Categoria.LOCALIZACAO_EXATA:
        if isinstance(valor, str):
            texto = valor.strip()
            return bool(PADRAO_REDIGIDO.fullmatch(texto) or _COORDENADAS_ARREDONDADAS.fullmatch(texto))
        return parece_numero(valor) and numero_de_casas_decimais(valor) <= 2
    if not isinstance(valor, str):
        return False
    texto = valor.strip()
    if not texto:
        return False
    if PADRAO_REDIGIDO.fullmatch(texto) or set(texto) == {"*"}:
        return True
    padrao = _MASCARAS_RECONHECIDAS.get(categoria)
    return bool(padrao and padrao.fullmatch(texto))


def parece_numero(valor) -> bool:
    """Número de verdade (int ou float, não bool) ou texto que vira número."""
    if isinstance(valor, bool):
        return False
    if isinstance(valor, (int, float)):
        return True
    try:
        Decimal(str(valor).strip().replace(",", "."))
    except InvalidOperation:
        return False
    return True


def _redigir_como(categoria: Categoria) -> Callable[[object], str]:
    return lambda _valor: redigir(categoria)


# Estratégia de máscara por categoria. Fica como dado (um dicionário) para a
# tabela do README e os testes conferirem que toda categoria tem uma estratégia.
ESTRATEGIAS: dict[Categoria, Callable[[object], object]] = {
    Categoria.CPF: mascarar_cpf,
    Categoria.CNH: mascarar_cnh,
    Categoria.TELEFONE: mascarar_telefone,
    Categoria.EMAIL: mascarar_email,
    Categoria.LOCALIZACAO_EXATA: mascarar_localizacao,
    Categoria.LOCALIZACAO_APROXIMADA: _manter,
    Categoria.RG: _redigir_como(Categoria.RG),
    Categoria.NOME: _redigir_como(Categoria.NOME),
    Categoria.SAUDE: _redigir_como(Categoria.SAUDE),
    Categoria.SENHA: _redigir_como(Categoria.SENHA),
    Categoria.TOKEN_ACESSO: _redigir_como(Categoria.TOKEN_ACESSO),
    Categoria.QR_TOKEN: _redigir_como(Categoria.QR_TOKEN),
    Categoria.SEGREDO_API: _redigir_como(Categoria.SEGREDO_API),
}


def mascarar_valor(categoria: Categoria, valor):
    """Aplica a estratégia da categoria, sem tocar em valor que já está mascarado."""
    if esta_mascarado(categoria, valor):
        return valor
    return ESTRATEGIAS[categoria](valor)
