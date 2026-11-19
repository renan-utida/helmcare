"""Dados pessoais SIMULADOS para a demonstração e os cenários do Taint Analysis.

Tudo aqui é inventado e obviamente falso: os nomes dizem "Simulado", os
e-mails usam o domínio reservado exemplo.com, os GUIDs são sequenciais e o
qrToken começa com "qrSimuladoFalso". CPF e CNH têm dígitos verificadores
válidos, calculados pelas funções abaixo a partir de bases inventadas, para
exercitar a validação do motor de PII.

Cada função devolve um dict novo, no formato do Contrato de Integração, para
que um cenário nunca altere os dados de outro.
"""

# ---------------------------------------------------------------------------
# Geradores de documentos com dígito verificador válido
# ---------------------------------------------------------------------------


def calcular_dv_cpf(base: str) -> str:
    """Devolve os 11 dígitos do CPF: a base de 9 e os dois DVs módulo 11 (pesos 10..2 e 11..2)."""
    numeros = [int(c) for c in base]
    if len(numeros) != 9:
        raise ValueError("a base do CPF tem 9 dígitos")
    for tamanho in (9, 10):
        soma = sum(n * peso for n, peso in zip(numeros, range(tamanho + 1, 1, -1)))
        resto = soma % 11
        numeros.append(0 if resto < 2 else 11 - resto)
    return "".join(str(n) for n in numeros)


def calcular_dv_cnh(base: str) -> str:
    """Devolve os 11 dígitos da CNH: pesos 9..1 no primeiro DV e 1..9 no segundo, com o desconto do Denatran."""
    numeros = [int(c) for c in base]
    if len(numeros) != 9:
        raise ValueError("a base da CNH tem 9 dígitos")
    dv1 = sum(n * peso for n, peso in zip(numeros, range(9, 0, -1))) % 11
    desconto = 0
    if dv1 >= 10:
        dv1, desconto = 0, 2
    dv2 = sum(n * peso for n, peso in zip(numeros, range(1, 10))) % 11 - desconto
    if dv2 < 0:
        dv2 += 11
    if dv2 >= 10:
        dv2 = 0
    return base + f"{dv1}{dv2}"


def formatar_cpf(digitos: str) -> str:
    return f"{digitos[:3]}.{digitos[3:6]}.{digitos[6:9]}-{digitos[9:]}"


# ---------------------------------------------------------------------------
# Valores simulados
# ---------------------------------------------------------------------------

CPF = formatar_cpf(calcular_dv_cpf("482917365"))
CNH = calcular_dv_cnh("604719283")
NOME_ENTREGADOR = "Fulano Simulado de Teste"
NOME_CONTATO = "Beltrana Simulada de Teste"
TELEFONE = "(11) 98765-4321"
TELEFONE_CONTATO = "11912345678"
EMAIL = "fulano.simulado@exemplo.com"
SENHA = "SenhaSimulada123"

TIPO_SANGUINEO = "O+"
ALERGIAS = "Dipirona"
CONDICOES_CRITICAS = "Asma"
MEDICAMENTOS = "Salbutamol"

LATITUDE_EXATA = -23.5641
LONGITUDE_EXATA = -46.6525
LATITUDE_APROXIMADA = -23.56
LONGITUDE_APROXIMADA = -46.65

QR_TOKEN = "qrSimulado" + "Falso0001Token"
CODIGO_DISPOSITIVO = "HC-0001"

ENTREGADOR_ID = "00000000-0000-4000-8000-000000000001"
OCORRENCIA_ID = "00000000-0000-4000-8000-000000000002"
DISPOSITIVO_ID = "00000000-0000-4000-8000-000000000003"
CONTATO_ID = "00000000-0000-4000-8000-000000000004"
FICHA_ID = "00000000-0000-4000-8000-000000000005"

# Todos os valores pessoais acima, para os testes conferirem que nenhum vaza.
VALORES_SENSIVEIS = (
    CPF,
    calcular_dv_cpf("482917365"),
    CNH,
    NOME_ENTREGADOR,
    NOME_CONTATO,
    TELEFONE,
    "11987654321",
    TELEFONE_CONTATO,
    EMAIL,
    SENHA,
    ALERGIAS,
    CONDICOES_CRITICAS,
    MEDICAMENTOS,
    QR_TOKEN,
    str(LATITUDE_EXATA),
    str(LONGITUDE_EXATA),
)


# ---------------------------------------------------------------------------
# Payloads no formato do Contrato
# ---------------------------------------------------------------------------


def cadastro_entregador() -> dict:
    """Corpo do POST /entregadores (seções 3 e 7)."""
    return {"nome": NOME_ENTREGADOR, "cpf": CPF, "cnh": CNH, "telefone": TELEFONE, "email": EMAIL, "senha": SENHA}


def login() -> dict:
    """Corpo do POST /entregadores/login."""
    return {"email": EMAIL, "senha": SENHA}


def ficha_medica() -> dict:
    return {
        "id": FICHA_ID,
        "entregadorId": ENTREGADOR_ID,
        "tipoSanguineo": TIPO_SANGUINEO,
        "alergias": ALERGIAS,
        "condicoesCriticas": CONDICOES_CRITICAS,
        "medicamentos": MEDICAMENTOS,
    }


def contato_emergencia() -> dict:
    return {
        "id": CONTATO_ID,
        "entregadorId": ENTREGADOR_ID,
        "nome": NOME_CONTATO,
        "telefone": TELEFONE_CONTATO,
        "parentesco": "Irmã",
    }


def ocorrencia_sos() -> dict:
    """Corpo do GET /ocorrencias/{id}, com a localização exata."""
    return {
        "id": OCORRENCIA_ID,
        "entregadorId": ENTREGADOR_ID,
        "estado": "SOS_CAMADA_1",
        "camada": 1,
        "latitude": LATITUDE_EXATA,
        "longitude": LONGITUDE_EXATA,
        "criadaEm": "2026-11-12T21:16:02Z",
        "aceitaEm": None,
        "encerradaAutomaticamente": False,
        "cronometro": {"segundosDesdeQueda": 37, "metaSegundos": 120, "parado": False},
    }


def item_chamado_proximo() -> dict:
    """Item do GET /ocorrencias/proximas: localização já aproximada (2 casas)."""
    return {
        "ocorrenciaId": OCORRENCIA_ID,
        "estado": "SOS_CAMADA_1",
        "camada": 1,
        "criadaEm": "2026-11-12T21:16:02Z",
        "latitude": LATITUDE_APROXIMADA,
        "longitude": LONGITUDE_APROXIMADA,
        "distanciaKm": 1.2,
        "cronometro": {"segundosDesdeQueda": 12, "metaSegundos": 120, "parado": False},
    }


def evento_mqtt_sos() -> dict:
    """Evento SOS do capacete (seção 6.2): só o código do dispositivo e a coordenada."""
    return {
        "dispositivo": CODIGO_DISPOSITIVO,
        "ts": "2026-11-12T21:16:02Z",
        "tipo": "SOS",
        "picoG": 4.8,
        "orientacaoAnormal": True,
        "pilotandoAntes": True,
        "presencaAntes": True,
        "latitude": LATITUDE_EXATA,
        "longitude": LONGITUDE_EXATA,
    }


def telemetria() -> dict:
    """Telemetria do capacete (seção 6.1): nenhum dado pessoal."""
    return {
        "dispositivo": CODIGO_DISPOSITIVO,
        "ts": "2026-11-12T21:15:30Z",
        "estado": "ARMADO",
        "temperaturaC": 33.4,
        "umidadePct": 58,
        "indiceCalorC": 37.1,
        "presenca": True,
        "velocidadeKmh": 32,
        "bateriaPct": 81,
    }


def resposta_cofre(incluir_medicamentos: bool = False) -> dict:
    """Resposta do POST /cofre/{qrToken}/acesso (seção 7), com a ficha mínima e os contatos."""
    resposta = {
        "origem": "SOS_ATIVO",
        "ocorrenciaId": OCORRENCIA_ID,
        "acessadoEm": "2026-11-18T22:10:00Z",
        "expiraEm": "2026-11-18T23:10:00Z",
        "tipoSanguineo": TIPO_SANGUINEO,
        "alergias": ALERGIAS,
        "condicoesCriticas": CONDICOES_CRITICAS,
        "contatos": [{"nome": NOME_CONTATO, "telefone": TELEFONE_CONTATO, "parentesco": "Irmã"}],
    }
    if incluir_medicamentos:
        # O erro de código do cenário C8: o Contrato não deixa medicamentos sair no Cofre.
        resposta["medicamentos"] = MEDICAMENTOS
    return resposta


def resposta_dispositivo() -> dict:
    """Resposta do POST /dispositivos: a única que leva o qrToken e a urlCofre."""
    return {
        "id": DISPOSITIVO_ID,
        "codigo": CODIGO_DISPOSITIVO,
        "entregadorId": ENTREGADOR_ID,
        "qrToken": QR_TOKEN,
        "urlCofre": f"http://localhost:5000/cofre/{QR_TOKEN}",
    }
