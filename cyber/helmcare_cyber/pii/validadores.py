"""Validadores puros dos formatos brasileiros usados pelo motor de PII.

Todas as funções recebem texto (ou número) e devolvem bool ou int, sem efeito
colateral, para serem testadas isoladamente. Nenhuma delas registra o valor
recebido em log: elas só respondem se o formato confere.
"""

import math
from decimal import Decimal, InvalidOperation

DIGITOS_ASCII = "0123456789"

# Lista oficial dos DDDs do Brasil (plano de numeração da Anatel).
# Fica como frozenset porque a consulta é só "pertence ou não".
DDDS_BRASIL = frozenset(
    {
        11, 12, 13, 14, 15, 16, 17, 18, 19,
        21, 22, 24, 27, 28,
        31, 32, 33, 34, 35, 37, 38,
        41, 42, 43, 44, 45, 46, 47, 48, 49,
        51, 53, 54, 55,
        61, 62, 63, 64, 65, 66, 67, 68, 69,
        71, 73, 74, 75, 77, 79,
        81, 82, 83, 84, 85, 86, 87, 88, 89,
        91, 92, 93, 94, 95, 96, 97, 98, 99,
    }
)

# Valores aceitos pelo Contrato (seção 3), só em maiúsculas.
TIPOS_SANGUINEOS = frozenset({"A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"})


def somente_digitos(valor) -> str:
    """Devolve só os dígitos ASCII do valor (None vira texto vazio).

    Usa a lista ASCII de propósito: str.isdigit aceitaria caracteres como "²",
    que não fazem parte de nenhum documento.
    """
    if valor is None:
        return ""
    return "".join(c for c in str(valor) if c in DIGITOS_ASCII)


def _sequencia_repetida(digitos: str) -> bool:
    """11111111111 e parecidos passam na conta do DV, mas não são documentos."""
    return len(set(digitos)) == 1


def cpf_valido(valor) -> bool:
    """Confere o CPF: 11 dígitos, sem sequência repetida e os dois DVs módulo 11.

    Primeiro DV: soma dos 9 primeiros dígitos com pesos de 10 a 2; o resto da
    divisão por 11 menor que 2 vira 0, senão o DV é 11 menos o resto.
    Segundo DV: mesma conta sobre os 10 primeiros dígitos, com pesos de 11 a 2.
    """
    digitos = somente_digitos(valor)
    if len(digitos) != 11 or _sequencia_repetida(digitos):
        return False
    numeros = [int(c) for c in digitos]
    for tamanho in (9, 10):
        soma = sum(n * peso for n, peso in zip(numeros[:tamanho], range(tamanho + 1, 1, -1)))
        resto = soma % 11
        dv = 0 if resto < 2 else 11 - resto
        if numeros[tamanho] != dv:
            return False
    return True


def cnh_valida(valor) -> bool:
    """Confere a CNH (número do registro): 11 dígitos, sem sequência repetida e os dois DVs.

    Algoritmo do Denatran:
    1. Primeiro DV: soma dos 9 primeiros dígitos com pesos de 9 a 1. O DV é o
       resto da divisão por 11. Se o resto for 10 ou mais, o DV vira 0 e passa a
       valer um desconto de 2 para o segundo DV.
    2. Segundo DV: soma dos mesmos 9 dígitos com pesos de 1 a 9. Do resto da
       divisão por 11 se tira o desconto; se ficar negativo, soma-se 11; se
       ficar 10 ou mais, o DV vira 0.
    O ajuste "negativo soma 11" segue a implementação de referência em Python
    (validate-docbr), que dá exatamente um par de DVs para cada base de 9
    dígitos. Quando não há desconto, todas as variantes publicadas concordam.
    """
    digitos = somente_digitos(valor)
    if len(digitos) != 11 or _sequencia_repetida(digitos):
        return False
    base = [int(c) for c in digitos[:9]]

    soma1 = sum(n * peso for n, peso in zip(base, range(9, 0, -1)))
    dv1 = soma1 % 11
    desconto = 0
    if dv1 >= 10:
        dv1 = 0
        desconto = 2

    soma2 = sum(n * peso for n, peso in zip(base, range(1, 10)))
    dv2 = soma2 % 11 - desconto
    if dv2 < 0:
        dv2 += 11
    if dv2 >= 10:
        dv2 = 0

    return digitos[9:] == f"{dv1}{dv2}"


def telefone_br_valido(valor) -> bool:
    """Confere um telefone brasileiro com DDD, com ou sem o código do país (+55).

    Celular: 11 dígitos (DDD + 9 dígitos começando em 9).
    Fixo: 10 dígitos (DDD + 8 dígitos começando de 2 a 5).
    O DDD precisa estar na lista oficial, o que derruba muitos números
    aleatórios que só têm o tamanho certo.
    """
    digitos = somente_digitos(valor)
    if len(digitos) in (12, 13) and digitos.startswith("55"):
        digitos = digitos[2:]
    if len(digitos) not in (10, 11):
        return False
    if int(digitos[:2]) not in DDDS_BRASIL:
        return False
    numero = digitos[2:]
    if len(numero) == 9:
        return numero[0] == "9"
    return numero[0] in "2345"


def tipo_sanguineo_valido(valor) -> bool:
    """Aceita só os oito tipos do Contrato, em maiúsculas (espaços nas pontas são ignorados)."""
    if not isinstance(valor, str):
        return False
    return valor.strip() in TIPOS_SANGUINEOS


def _para_float(valor) -> float | None:
    """Converte número ou texto em float; bool e valores inválidos viram None."""
    if isinstance(valor, bool) or valor is None:
        return None
    try:
        numero = float(str(valor).strip().replace(",", ".")) if isinstance(valor, str) else float(valor)
    except (TypeError, ValueError):
        return None
    if math.isnan(numero) or math.isinf(numero):
        return None
    return numero


def coordenada_valida(lat, lon) -> bool:
    """Latitude de -90 a 90 e longitude de -180 a 180 (as faixas do Contrato, seção 7)."""
    latitude = _para_float(lat)
    longitude = _para_float(lon)
    if latitude is None or longitude is None:
        return False
    return -90 <= latitude <= 90 and -180 <= longitude <= 180


def numero_de_casas_decimais(valor_texto) -> int:
    """Conta as casas decimais como o valor foi escrito ("-23.5600" tem 4).

    Para float, usa repr, que é a forma mais curta que volta ao mesmo número
    (-23.56 tem 2). Aceita vírgula decimal. Valor que não é número devolve 0.
    """
    if isinstance(valor_texto, bool) or valor_texto is None:
        return 0
    texto = repr(valor_texto) if isinstance(valor_texto, float) else str(valor_texto)
    texto = texto.strip().replace(",", ".")
    try:
        expoente = Decimal(texto).as_tuple().exponent
    except InvalidOperation:
        return 0
    # NaN e infinito têm expoente em texto ("n", "F"), não inteiro.
    if not isinstance(expoente, int):
        return 0
    return max(0, -expoente)
