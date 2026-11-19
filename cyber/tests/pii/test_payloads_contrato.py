"""Payloads simulados no formato do Contrato de Integração e o resultado esperado de cada um.

Cada caso lista os pares (caminho, categoria) que o motor deve encontrar, nem
mais nem menos. GUIDs, timestamps, enums e números de telemetria não aparecem
nos esperados porque não são dados pessoais.
"""

import json

import pytest

from helmcare_cyber.pii import Categoria
from tests import dados_simulados as d

CADASTRO_ENTREGADOR = {
    "nome": "Nome Simulado",
    "cpf": d.CPF_VALIDO_MASCARA,
    "cnh": d.CNH_VALIDA,
    "telefone": d.CELULAR_MASCARA,
    "email": d.EMAIL,
    "senha": d.SENHA,
}

FICHA_MEDICA = {
    "id": d.GUID_OCORRENCIA,
    "entregadorId": d.GUID_ENTREGADOR,
    "tipoSanguineo": "O+",
    "alergias": "Dipirona",
    "condicoesCriticas": "Asma",
    "medicamentos": "Bombinha de salbutamol",
}

CONTATO_EMERGENCIA = {"nome": "Contato Simulado", "telefone": d.CELULAR_CONTATO, "parentesco": "Irmã"}

OCORRENCIA = {
    "id": d.GUID_OCORRENCIA,
    "entregadorId": d.GUID_ENTREGADOR,
    "estado": "SOS_CAMADA_1",
    "camada": 1,
    "latitude": -23.5641,
    "longitude": -46.6525,
    "criadaEm": "2026-11-12T21:16:02Z",
    "aceitaEm": None,
    "tempoAteAceiteSegundos": None,
    "encerradaAutomaticamente": False,
    "cronometro": {"segundosDesdeQueda": 37, "metaSegundos": 120, "parado": False},
}

TELEMETRIA_MQTT = {
    "dispositivo": "HC-0001",
    "ts": "2026-11-12T21:15:30Z",
    "estado": "ARMADO",
    "temperaturaC": 33.4,
    "umidadePct": 58,
    "indiceCalorC": 37.1,
    "presenca": True,
    "velocidadeKmh": 32,
    "bateriaPct": 81,
}

EVENTO_SOS_MQTT = {
    "dispositivo": "HC-0001",
    "ts": "2026-11-12T21:16:02Z",
    "tipo": "SOS",
    "picoG": 4.8,
    "orientacaoAnormal": True,
    "pilotandoAntes": True,
    "presencaAntes": True,
    "latitude": -23.5641,
    "longitude": -46.6525,
}

CHAMADO_PROXIMO = {
    "ocorrenciaId": d.GUID_OCORRENCIA,
    "estado": "SOS_CAMADA_1",
    "camada": 1,
    "criadaEm": "2026-11-12T21:16:02Z",
    "latitude": -23.56,
    "longitude": -46.65,
    "distanciaKm": 1.2,
    "cronometro": {"segundosDesdeQueda": 12, "metaSegundos": 120, "parado": False},
}

RESPOSTA_LOGIN = {
    "tokenType": "Bearer",
    "accessToken": d.ACCESS_TOKEN,
    "expiresIn": 3600,
    "refreshToken": d.REFRESH_TOKEN,
    "entregadorId": d.GUID_ENTREGADOR,
    "nome": "Nome Simulado",
    "voluntarioId": None,
    "codigoDispositivo": "HC-0001",
    "turnoAtivoId": None,
    "ocorrenciaAtivaId": None,
}

RESPOSTA_DISPOSITIVO = {
    "id": d.GUID_OCORRENCIA,
    "codigo": "HC-0001",
    "entregadorId": d.GUID_ENTREGADOR,
    "qrToken": d.QR_TOKEN,
    "urlCofre": f"http://localhost:5000/cofre/{d.QR_TOKEN}",
}

GET_ENTREGADOR_MASCARADO = {
    "id": d.GUID_ENTREGADOR,
    "nome": "Nome Simulado",
    "cpf": "***.***.***-37",
    "cnh": "*******8331",
    "telefone": "(11) *****-4321",
    "email": "e***@exemplo.com",
    "possuiFichaMedica": True,
}

RESPOSTA_COFRE = {
    "origem": "SOS_ATIVO",
    "ocorrenciaId": d.GUID_OCORRENCIA,
    "acessadoEm": "2026-11-18T22:10:00Z",
    "expiraEm": "2026-11-18T23:10:00Z",
    "tipoSanguineo": "O+",
    "alergias": "Dipirona",
    "condicoesCriticas": "Asma",
    "contatos": [{"nome": "Contato Simulado", "telefone": d.CELULAR_CONTATO, "parentesco": "Irmã"}],
}

CASOS = [
    (
        "cadastro_entregador",
        CADASTRO_ENTREGADOR,
        [
            ("$.nome", Categoria.NOME),
            ("$.cpf", Categoria.CPF),
            ("$.cnh", Categoria.CNH),
            ("$.telefone", Categoria.TELEFONE),
            ("$.email", Categoria.EMAIL),
            ("$.senha", Categoria.SENHA),
        ],
    ),
    (
        "ficha_medica",
        FICHA_MEDICA,
        [
            ("$.tipoSanguineo", Categoria.SAUDE),
            ("$.alergias", Categoria.SAUDE),
            ("$.condicoesCriticas", Categoria.SAUDE),
            ("$.medicamentos", Categoria.SAUDE),
        ],
    ),
    ("contato_emergencia", CONTATO_EMERGENCIA, [("$.nome", Categoria.NOME), ("$.telefone", Categoria.TELEFONE)]),
    (
        "ocorrencia",
        OCORRENCIA,
        [("$.latitude", Categoria.LOCALIZACAO_EXATA), ("$.longitude", Categoria.LOCALIZACAO_EXATA)],
    ),
    ("telemetria_mqtt", TELEMETRIA_MQTT, []),
    (
        "evento_sos_mqtt",
        EVENTO_SOS_MQTT,
        [("$.latitude", Categoria.LOCALIZACAO_EXATA), ("$.longitude", Categoria.LOCALIZACAO_EXATA)],
    ),
    (
        "chamado_proximo",
        CHAMADO_PROXIMO,
        [("$.latitude", Categoria.LOCALIZACAO_APROXIMADA), ("$.longitude", Categoria.LOCALIZACAO_APROXIMADA)],
    ),
    (
        "resposta_login",
        RESPOSTA_LOGIN,
        [
            ("$.accessToken", Categoria.TOKEN_ACESSO),
            ("$.refreshToken", Categoria.TOKEN_ACESSO),
            ("$.nome", Categoria.NOME),
        ],
    ),
    (
        "resposta_dispositivo",
        RESPOSTA_DISPOSITIVO,
        [("$.qrToken", Categoria.QR_TOKEN), ("$.urlCofre", Categoria.QR_TOKEN)],
    ),
    (
        "get_entregador_mascarado",
        GET_ENTREGADOR_MASCARADO,
        [
            ("$.nome", Categoria.NOME),
            ("$.cpf", Categoria.CPF),
            ("$.cnh", Categoria.CNH),
            ("$.telefone", Categoria.TELEFONE),
            ("$.email", Categoria.EMAIL),
        ],
    ),
    (
        "resposta_cofre",
        RESPOSTA_COFRE,
        [
            ("$.tipoSanguineo", Categoria.SAUDE),
            ("$.alergias", Categoria.SAUDE),
            ("$.condicoesCriticas", Categoria.SAUDE),
            ("$.contatos[0].nome", Categoria.NOME),
            ("$.contatos[0].telefone", Categoria.TELEFONE),
        ],
    ),
]


@pytest.mark.parametrize("nome, payload, esperado", CASOS, ids=[c[0] for c in CASOS])
def test_payload(motor, nome, payload, esperado):
    achados = motor.varrer(payload)
    assert [(a.caminho, a.categoria) for a in achados] == esperado


@pytest.mark.parametrize("nome, payload, esperado", CASOS, ids=[c[0] for c in CASOS])
def test_payload_como_json_em_texto(motor, nome, payload, esperado):
    # Mensagem MQTT ou corpo HTTP chegam como texto: o resultado é o mesmo.
    achados = motor.varrer(json.dumps(payload, ensure_ascii=False))
    assert [(a.caminho, a.categoria) for a in achados] == esperado


def test_telemetria_sem_pii(motor):
    copia, achados = motor.mascarar(TELEMETRIA_MQTT)
    assert achados == []
    assert copia == TELEMETRIA_MQTT


def test_chamado_proximo_ja_vem_aproximado(motor):
    copia, achados = motor.mascarar(CHAMADO_PROXIMO)
    assert all(a.ja_mascarado for a in achados)
    assert copia == CHAMADO_PROXIMO


def test_get_entregador_ja_vem_mascarado(motor):
    # O GET /entregadores/{id} só devolve PII mascarada: nada aqui é vazamento,
    # exceto o nome, que o Contrato permite nas telas do próprio entregador.
    achados = motor.varrer(GET_ENTREGADOR_MASCARADO)
    assert {a.categoria for a in achados if not a.ja_mascarado} == {Categoria.NOME}


def test_ocorrencia_mascarada_fica_aproximada(motor):
    copia, _ = motor.mascarar(OCORRENCIA)
    assert (copia["latitude"], copia["longitude"]) == (-23.56, -46.65)
    assert copia["cronometro"] == OCORRENCIA["cronometro"]


def test_cadastro_dv_conferido(motor):
    achados = {a.caminho: a for a in motor.varrer(CADASTRO_ENTREGADOR)}
    assert achados["$.cpf"].dv_confere is True
    assert achados["$.cnh"].dv_confere is True
