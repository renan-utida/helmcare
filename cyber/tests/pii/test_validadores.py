import math

import pytest

from helmcare_cyber.pii.validadores import (
    DDDS_BRASIL,
    cnh_valida,
    coordenada_valida,
    cpf_valido,
    numero_de_casas_decimais,
    somente_digitos,
    telefone_br_valido,
    tipo_sanguineo_valido,
)
from tests import dados_simulados as d


class TestSomenteDigitos:
    def test_tira_mascara(self):
        assert somente_digitos(d.CPF_VALIDO_MASCARA) == d.CPF_VALIDO

    def test_none_vira_vazio(self):
        assert somente_digitos(None) == ""

    def test_numero(self):
        assert somente_digitos(12345) == "12345"

    def test_ignora_digitos_nao_ascii(self):
        # "²" passa em str.isdigit, mas não é dígito de documento.
        assert somente_digitos("12²3") == "123"


class TestCpf:
    @pytest.mark.parametrize("valor", [d.CPF_VALIDO, d.CPF_VALIDO_MASCARA, d.CPF_VALIDO_2, "731.048.296-40"])
    def test_validos(self, valor):
        assert cpf_valido(valor)

    @pytest.mark.parametrize("valor", [d.CPF_DV_ERRADO, "48291736527", "482.917.365-00"])
    def test_dv_errado(self, valor):
        assert not cpf_valido(valor)

    @pytest.mark.parametrize("digito", "0123456789")
    def test_sequencia_repetida(self, digito):
        assert not cpf_valido(digito * 11)

    @pytest.mark.parametrize("valor", ["4829173653", "482917365370", "", None, "abc"])
    def test_tamanho_errado(self, valor):
        assert not cpf_valido(valor)

    def test_cnh_valida_nao_e_cpf(self):
        assert not cpf_valido(d.CNH_VALIDA)


class TestCnh:
    @pytest.mark.parametrize("valor", [d.CNH_VALIDA, d.CNH_VALIDA_2, d.CNH_COM_DESCONTO, "472.016.395-25"])
    def test_validas(self, valor):
        assert cnh_valida(valor)

    def test_com_desconto_no_segundo_dv(self):
        # Primeiro resto 10 ou mais: DV1 = 0 e o segundo DV perde 2.
        assert d.CNH_COM_DESCONTO[9] == "0"
        assert cnh_valida(d.CNH_COM_DESCONTO)
        assert not cnh_valida(d.CNH_COM_DESCONTO[:10] + "7")

    def test_segundo_dv_negativo_soma_onze(self):
        # Base 478245040: primeiro resto 10 (DV1 = 0, desconto 2) e segundo resto 0.
        # 0 - 2 = -2, que vira 9 somando 11 (ajuste da implementação de referência).
        base = "478245040"
        soma1 = sum(int(c) * p for c, p in zip(base, range(9, 0, -1)))
        soma2 = sum(int(c) * p for c, p in zip(base, range(1, 10)))
        assert soma1 % 11 == 10 and soma2 % 11 == 0
        assert cnh_valida(base + "09")
        assert not cnh_valida(base + "00")

    @pytest.mark.parametrize("valor", [d.CNH_DV_ERRADO, "60471928301", "13958274601"])
    def test_dv_errado(self, valor):
        assert not cnh_valida(valor)

    @pytest.mark.parametrize("digito", "0123456789")
    def test_sequencia_repetida(self, digito):
        assert not cnh_valida(digito * 11)

    @pytest.mark.parametrize("valor", ["6047192833", "604719283310", "", None])
    def test_tamanho_errado(self, valor):
        assert not cnh_valida(valor)

    def test_cpf_valido_nao_e_cnh(self):
        assert not cnh_valida(d.CPF_VALIDO)


class TestTelefone:
    @pytest.mark.parametrize(
        "valor",
        [d.CELULAR, d.CELULAR_MASCARA, d.FIXO, d.FIXO_MASCARA, "+55 11 98765-4321", "5511987654321", "(21) 2345-6789"],
    )
    def test_validos(self, valor):
        assert telefone_br_valido(valor)

    @pytest.mark.parametrize(
        "valor",
        [
            "10987654321",  # DDD 10 não existe
            "20987654321",  # DDD 20 não existe
            "11887654321",  # celular sem o 9 na frente
            "1167890123",  # fixo começando em 6
            "119876543",  # curto
            "119876543210",  # longo sem 55
            "",
            None,
        ],
    )
    def test_invalidos(self, valor):
        assert not telefone_br_valido(valor)

    def test_lista_de_ddds(self):
        assert len(DDDS_BRASIL) == 67
        assert {11, 21, 61, 99} <= DDDS_BRASIL
        assert not {10, 20, 23, 25, 26, 29, 30, 36, 39, 40, 50, 52, 56, 60, 70, 72, 76, 78, 80, 90} & DDDS_BRASIL


class TestTipoSanguineo:
    @pytest.mark.parametrize("valor", ["A+", "A-", "B+", "B-", "AB+", "AB-", "O+", " O- "])
    def test_validos(self, valor):
        assert tipo_sanguineo_valido(valor)

    @pytest.mark.parametrize("valor", ["a+", "C+", "O", "AB", "0+", "", None, 1])
    def test_invalidos(self, valor):
        assert not tipo_sanguineo_valido(valor)


class TestCoordenadas:
    @pytest.mark.parametrize(
        "lat, lon", [(-23.5641, -46.6525), ("-23.5641", "-46.6525"), (90, 180), (-90, -180), ("-23,56", "-46,65")]
    )
    def test_validas(self, lat, lon):
        assert coordenada_valida(lat, lon)

    @pytest.mark.parametrize(
        "lat, lon", [(91, 0), (0, 181), (None, 0), (True, 0), ("abc", 0), (math.nan, 0), (math.inf, 0), ([], 0)]
    )
    def test_invalidas(self, lat, lon):
        assert not coordenada_valida(lat, lon)

    @pytest.mark.parametrize(
        "valor, casas",
        [
            ("-23.5641", 4),
            (-23.5641, 4),
            (-23.56, 2),
            ("-23.5600", 4),
            ("-23,56", 2),
            (10, 0),
            ("1e-3", 3),
            ("abc", 0),
            (None, 0),
            (True, 0),
            ("nan", 0),
        ],
    )
    def test_casas_decimais(self, valor, casas):
        assert numero_de_casas_decimais(valor) == casas
