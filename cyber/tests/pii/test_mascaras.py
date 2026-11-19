import pytest

from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.pii.mascaras import (
    ESTRATEGIAS,
    arredondar_localizacao,
    esta_mascarado,
    mascarar_cnh,
    mascarar_cpf,
    mascarar_email,
    mascarar_localizacao,
    mascarar_telefone,
    mascarar_valor,
    parece_numero,
    redigir,
)
from tests import dados_simulados as d


class TestMascarasDoContrato:
    def test_cpf(self):
        assert mascarar_cpf(d.CPF_VALIDO) == "***.***.***-37"
        assert mascarar_cpf(d.CPF_VALIDO_MASCARA) == "***.***.***-37"

    def test_cnh(self):
        assert mascarar_cnh(d.CNH_VALIDA) == "*******8331"

    def test_celular(self):
        assert mascarar_telefone(d.CELULAR) == "(11) *****-4321"
        assert mascarar_telefone(d.CELULAR_MASCARA) == "(11) *****-4321"
        assert mascarar_telefone("+55 (11) 98765-4321") == "(11) *****-4321"

    def test_fixo(self):
        assert mascarar_telefone(d.FIXO) == "(11) ****-7890"
        assert mascarar_telefone(d.FIXO_MASCARA) == "(11) ****-7890"

    def test_email(self):
        assert mascarar_email("renan@exemplo.com") == "r***@exemplo.com"
        assert mascarar_email(d.EMAIL) == "e***@exemplo.com"

    def test_formato_exato_do_contrato(self):
        # Os exemplos da seção 9: só os últimos dígitos ficam visíveis.
        assert mascarar_cpf("00000000012") == "***.***.***-12"
        assert mascarar_cnh("00000008900") == "*******8900"
        assert mascarar_telefone("11000001234") == "(11) *****-1234"
        assert mascarar_telefone("1100004567") == "(11) ****-4567"


class TestTamanhoErradoRedige:
    @pytest.mark.parametrize(
        "funcao, categoria",
        [
            (mascarar_cpf, Categoria.CPF),
            (mascarar_cnh, Categoria.CNH),
            (mascarar_telefone, Categoria.TELEFONE),
            (mascarar_email, Categoria.EMAIL),
        ],
    )
    @pytest.mark.parametrize("valor", ["123", "", None])
    def test_redige(self, funcao, categoria, valor):
        assert funcao(valor) == redigir(categoria)


class TestRedigir:
    @pytest.mark.parametrize("categoria", list(Categoria))
    def test_sem_digitos(self, categoria):
        marcador = redigir(categoria)
        assert marcador == f"[REDIGIDO:{categoria.value}]"
        assert not any(c.isdigit() for c in marcador)

    def test_aceita_texto_da_categoria(self):
        assert redigir("CPF") == "[REDIGIDO:CPF]"


class TestLocalizacao:
    @pytest.mark.parametrize(
        "valor, esperado",
        [(-23.5641, -23.56), (-46.6525, -46.65), ("-23.5641", -23.56), (2.675, 2.68), (-2.675, -2.68), (10, 10.0)],
    )
    def test_arredonda_duas_casas_meio_para_cima(self, valor, esperado):
        assert arredondar_localizacao(valor) == esperado

    def test_arredondar_invalido(self):
        with pytest.raises(ValueError):
            arredondar_localizacao("abc")

    def test_par_em_texto(self):
        assert mascarar_localizacao("-23.5641, -46.6525") == "-23.56, -46.65"

    def test_texto_com_um_numero_curto(self):
        assert mascarar_localizacao("-23.5") == -23.5

    def test_texto_invalido_redige(self):
        assert mascarar_localizacao("perto do metrô") == redigir(Categoria.LOCALIZACAO_EXATA)

    def test_aproximada_fica_como_esta(self):
        assert mascarar_valor(Categoria.LOCALIZACAO_APROXIMADA, -23.56) == -23.56


class TestJaMascarado:
    @pytest.mark.parametrize(
        "categoria, valor",
        [
            (Categoria.CPF, "***.***.***-37"),
            (Categoria.CNH, "*******8331"),
            (Categoria.TELEFONE, "(11) *****-4321"),
            (Categoria.TELEFONE, "(11) ****-7890"),
            (Categoria.EMAIL, "r***@exemplo.com"),
            (Categoria.SENHA, "[REDIGIDO:SENHA]"),
            (Categoria.SENHA, "********"),
            (Categoria.SAUDE, "[REDIGIDO:SAUDE]"),
            (Categoria.LOCALIZACAO_EXATA, -23.56),
            (Categoria.LOCALIZACAO_EXATA, "[REDIGIDO:LOCALIZACAO_EXATA]"),
            (Categoria.LOCALIZACAO_APROXIMADA, "-23.56, -46.65"),
        ],
    )
    def test_reconhece(self, categoria, valor):
        assert esta_mascarado(categoria, valor)

    @pytest.mark.parametrize(
        "categoria, valor",
        [
            (Categoria.CPF, d.CPF_VALIDO),
            (Categoria.CPF, "***.***.***-3"),
            (Categoria.CNH, d.CNH_VALIDA),
            (Categoria.TELEFONE, d.CELULAR_MASCARA),
            (Categoria.EMAIL, d.EMAIL),
            (Categoria.EMAIL, "***@exemplo.com"),
            (Categoria.SENHA, d.SENHA),
            (Categoria.SENHA, ""),
            (Categoria.SENHA, None),
            (Categoria.CPF, 48291736537),
            (Categoria.LOCALIZACAO_EXATA, -23.5641),
            (Categoria.LOCALIZACAO_EXATA, "longe"),
            (Categoria.LOCALIZACAO_EXATA, True),
        ],
    )
    def test_nao_reconhece(self, categoria, valor):
        assert not esta_mascarado(categoria, valor)

    def test_parece_numero(self):
        assert parece_numero(1) and parece_numero("-23,5") and not parece_numero(False) and not parece_numero("x")


class TestEstrategias:
    def test_toda_categoria_tem_estrategia(self):
        assert set(ESTRATEGIAS) == set(Categoria)

    @pytest.mark.parametrize(
        "categoria, valor",
        [
            (Categoria.CPF, d.CPF_VALIDO),
            (Categoria.CNH, d.CNH_VALIDA),
            (Categoria.TELEFONE, d.CELULAR),
            (Categoria.EMAIL, d.EMAIL),
            (Categoria.LOCALIZACAO_EXATA, -23.5641),
            (Categoria.LOCALIZACAO_EXATA, "-23.5641, -46.6525"),
            (Categoria.SAUDE, "asma"),
            (Categoria.SENHA, d.SENHA),
            (Categoria.NOME, "Nome Simulado"),
        ],
    )
    def test_idempotente(self, categoria, valor):
        uma_vez = mascarar_valor(categoria, valor)
        assert mascarar_valor(categoria, uma_vez) == uma_vez
        assert uma_vez != valor

    @pytest.mark.parametrize(
        "categoria",
        [Categoria.RG, Categoria.NOME, Categoria.SAUDE, Categoria.SENHA, Categoria.TOKEN_ACESSO, Categoria.QR_TOKEN, Categoria.SEGREDO_API],
    )
    def test_sem_mascara_no_contrato_redige(self, categoria):
        assert mascarar_valor(categoria, "valor simulado 123") == redigir(categoria)
