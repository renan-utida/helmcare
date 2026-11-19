import pickle

import pytest

from helmcare_cyber.pii import MotorPII
from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.simulacao import dados_simulados as dados
from helmcare_cyber.taint.modelo import Fonte
from helmcare_cyber.taint.registro import TaintRegistry
from tests.taint.conftest import criar_achado

CPF_DIGITOS = dados.calcular_dv_cpf("482917365")


@pytest.fixture
def registro() -> TaintRegistry:
    registro = TaintRegistry()
    registro.registrar(Fonte.CADASTRO_ENTREGADOR, MotorPII().varrer(dados.cadastro_entregador()))
    return registro


def categorias(ocorrencias):
    return [o.categoria for o in ocorrencias]


class TestRegistrar:
    def test_conta_as_formas(self):
        registro = TaintRegistry()
        # CPF entra duas vezes (como veio e só com dígitos); o nome, uma.
        novos = registro.registrar(
            Fonte.CADASTRO_ENTREGADOR, [criar_achado(Categoria.CPF, valor=dados.CPF), criar_achado(Categoria.NOME, valor="Fulano Simulado")]
        )
        assert novos == 3 and len(registro) == 3

    def test_nao_repete(self, registro):
        antes = len(registro)
        registro.registrar(Fonte.CADASTRO_ENTREGADOR, MotorPII().varrer(dados.cadastro_entregador()))
        assert len(registro) == antes

    def test_ignora_ja_mascarado_e_curto(self):
        registro = TaintRegistry()
        achados = MotorPII().varrer({"cpf": "***.***.***-37", "tipoSanguineo": "O+"})
        assert registro.registrar(Fonte.FICHA_MEDICA, achados) == 0


class TestProcurar:
    def test_nome_em_f_string(self, registro):
        mensagem = f"Bem-vindo, {dados.NOME_ENTREGADOR}! Seu cadastro foi concluído."
        ocorrencias = registro.procurar(mensagem)
        assert categorias(ocorrencias) == [Categoria.NOME]
        assert ocorrencias[0].trecho() == dados.NOME_ENTREGADOR
        assert ocorrencias[0].fonte is Fonte.CADASTRO_ENTREGADOR
        assert ocorrencias[0].caminho_fonte == "$.nome"

    def test_sem_diferenciar_maiusculas(self, registro):
        assert categorias(registro.procurar(dados.NOME_ENTREGADOR.upper())) == [Categoria.NOME]

    @pytest.mark.parametrize("forma", [CPF_DIGITOS, dados.CPF, "482 917 365 37", "482.917.365/37"])
    def test_cpf_em_qualquer_formato(self, registro, forma):
        assert categorias(registro.procurar(f"doc {forma} ok")) == [Categoria.CPF]

    def test_cpf_registrado_so_com_digitos(self):
        registro = TaintRegistry()
        registro.registrar(Fonte.CADASTRO_ENTREGADOR, MotorPII().varrer({"cpf": CPF_DIGITOS}))
        assert categorias(registro.procurar(f"cpf {dados.CPF}")) == [Categoria.CPF]

    @pytest.mark.parametrize("forma", ["11987654321", "+55 11 98765-4321", "(11)98765-4321"])
    def test_telefone_em_qualquer_formato(self, registro, forma):
        assert categorias(registro.procurar(forma)) == [Categoria.TELEFONE]

    @pytest.mark.parametrize(
        "texto",
        [f"9{CPF_DIGITOS}", f"{CPF_DIGITOS}0", "X" + dados.NOME_ENTREGADOR.replace(" ", ""), "Fulano Simulado"],
    )
    def test_nao_casa_pedaco(self, registro, texto):
        assert registro.procurar(texto) == []

    def test_mais_longo_primeiro_sem_sobreposicao(self):
        registro = TaintRegistry()
        registro.registrar(
            Fonte.CADASTRO_ENTREGADOR,
            [criar_achado(Categoria.NOME, valor="Fulano"), criar_achado(Categoria.NOME, valor="Fulano Simulado")],
        )
        ocorrencias = registro.procurar("oi Fulano Simulado e Fulano")
        assert [o.trecho() for o in ocorrencias] == ["Fulano Simulado", "Fulano"]

    def test_aceita_nao_texto(self, registro):
        assert categorias(registro.procurar(int(CPF_DIGITOS))) == [Categoria.CPF]


class TestProtecao:
    def test_limpar(self, registro):
        registro.limpar()
        assert len(registro) == 0
        assert registro.procurar(dados.NOME_ENTREGADOR) == []

    def test_repr_sem_valores(self, registro):
        texto = repr(registro) + repr(registro.procurar(dados.NOME_ENTREGADOR))
        assert dados.NOME_ENTREGADOR not in texto and dados.CPF not in texto
        assert "formas registradas" in repr(registro)

    def test_pickle_bloqueado(self, registro):
        with pytest.raises(TypeError):
            pickle.dumps(registro)
