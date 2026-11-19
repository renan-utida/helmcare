"""Garante que o valor original de um dado pessoal nunca sai do Achado."""

import copy
import dataclasses
import json
import pickle

import pytest

from helmcare_cyber.pii.motor import ValorProtegido
from tests import dados_simulados as d

# (entrada, valor original que não pode vazar)
CASOS = [
    ({"cpf": d.CPF_VALIDO}, d.CPF_VALIDO),
    ({"cnh": d.CNH_VALIDA}, d.CNH_VALIDA),
    ({"telefone": d.CELULAR}, d.CELULAR),
    ({"email": d.EMAIL}, d.EMAIL),
    ({"senha": d.SENHA}, d.SENHA),
    ({"accessToken": d.ACCESS_TOKEN}, d.ACCESS_TOKEN),
    ({"qrToken": d.QR_TOKEN}, d.QR_TOKEN),
    ({"alergias": "Dipirona simulada"}, "Dipirona simulada"),
    ({"latitude": -23.5641}, "-23.5641"),
    (f"cpf {d.CPF_VALIDO_MASCARA}", d.CPF_VALIDO_MASCARA),
    (f"tel {d.CELULAR_MASCARA}", d.CELULAR_MASCARA),
    (f"senha={d.SENHA}", d.SENHA),
    (f"Bearer {d.ACCESS_TOKEN}", d.ACCESS_TOKEN),
    (f"chave {d.CHAVE_GOOGLE}", d.CHAVE_GOOGLE),
    (f"token {d.JWT}", d.JWT),
    ("posição -23.5641, -46.6525", "-23.5641"),
]


@pytest.fixture(params=CASOS, ids=lambda c: str(c[1])[:12])
def caso(request, motor):
    entrada, original = request.param
    achados = motor.varrer(entrada)
    assert achados, "o caso precisa gerar achado"
    return achados, original


def test_repr_e_str(caso):
    achados, original = caso
    for achado in achados:
        assert original not in repr(achado)
        assert original not in str(achado)


def test_json(caso):
    achados, original = caso
    for achado in achados:
        assert original not in achado.para_json()
        assert original not in json.dumps(achado.para_dict())


def test_valor_mascarado_nao_e_o_original(caso):
    achados, original = caso
    for achado in achados:
        assert original not in achado.valor_mascarado
        assert achado.valor_mascarado != original


def test_lista_de_achados(caso):
    achados, original = caso
    assert original not in repr(achados)


def test_asdict_nao_expoe(caso):
    achados, original = caso
    for achado in achados:
        como_dict = dataclasses.asdict(achado)
        assert original not in repr(como_dict)
        # O original continua embrulhado: json.dumps se recusa a serializar.
        with pytest.raises(TypeError):
            json.dumps(como_dict)


def test_pickle_bloqueado(caso):
    achados, _ = caso
    with pytest.raises(TypeError):
        pickle.dumps(achados[0])


def test_original_fica_disponivel_em_memoria(motor):
    achado = motor.varrer({"cpf": d.CPF_VALIDO})[0]
    assert achado.valor_original() == d.CPF_VALIDO


def test_original_nao_entra_na_comparacao(motor):
    a = motor.varrer({"cpf": "***.***.***-37"})[0]
    b = motor.varrer({"cpf": "***.***.***-37"})[0]
    assert a == b


def test_valor_protegido():
    protegido = ValorProtegido(d.SENHA)
    assert d.SENHA not in repr(protegido) and d.SENHA not in str(protegido)
    assert copy.copy(protegido) is protegido
    assert copy.deepcopy(protegido) is protegido
    assert protegido.revelar() == d.SENHA


def test_achado_padrao_sem_original():
    from helmcare_cyber.pii import Achado, Categoria, Confianca, Metodo

    achado = Achado("$", Categoria.CPF, Metodo.VALOR, Confianca.ALTA, "PII-CPF-02", True, False, "***.***.***-37")
    assert achado.valor_original() == ""
