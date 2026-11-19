import pytest

from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.simulacao import dados_simulados as dados
from helmcare_cyber.taint import sanitizadores
from helmcare_cyber.taint.modelo import ContextoSaida, DecisaoAchado, Acao, Estado, pior_estado
from helmcare_cyber.taint.rastro import Passo, Rastro, TipoPasso
from tests.taint.conftest import criar_achado


class TestRastro:
    def test_encadeia_e_ordena(self):
        rastro = (
            Rastro()
            .adicionar_fonte("CADASTRO_ENTREGADOR", "POST /entregadores campo $.cpf", [Categoria.CPF], "***.***.***-37")
            .adicionar_propagacao("f-string em mensagem")
            .adicionar_sanitizador("redigir", [Categoria.CPF], "[REDIGIDO:CPF]")
            .adicionar_destino("LOG para SISTEMA, REGRA TA-LOG-01, ACAO REDIGIR", [Categoria.SENHA, Categoria.CPF, Categoria.CPF])
        )
        assert [p.tipo for p in rastro.passos] == list(TipoPasso)
        assert rastro.passos[-1].categorias == (Categoria.CPF, Categoria.SENHA)
        assert len(rastro) == 4 and rastro.tem_fonte()
        assert repr(rastro) == "Rastro(4 passos)"

    def test_texto(self):
        rastro = Rastro().adicionar_fonte("CADASTRO_ENTREGADOR", "campo $.cpf", [Categoria.CPF], "***.***.***-37")
        rastro.adicionar_propagacao("f-string em mensagem")
        assert rastro.para_texto().splitlines() == [
            "FONTE        CADASTRO_ENTREGADOR campo $.cpf [CPF] = ***.***.***-37",
            "PROPAGACAO   f-string em mensagem",
        ]

    def test_dict(self):
        rastro = Rastro().adicionar_destino("LOG", [Categoria.NOME])
        assert rastro.para_dict() == [
            {"tipo": "DESTINO", "descricao": "LOG", "categorias": ["NOME"], "valorMascarado": None}
        ]

    def test_fontes_a_partir_de_achados(self):
        achados = [criar_achado(Categoria.CPF, "$.cpf"), criar_achado(Categoria.NOME, "$.nome")]
        rastro = Rastro().adicionar_fontes("LOGIN", achados)
        assert [p.descricao for p in rastro.passos] == ["LOGIN campo $.cpf", "LOGIN campo $.nome"]
        assert all(p.valor_mascarado == "[MASCARADO]" for p in rastro.passos)

    def test_sem_fonte(self):
        assert not Rastro().adicionar_propagacao("x").tem_fonte()

    def test_passo_e_imutavel(self):
        with pytest.raises(AttributeError):
            Passo(TipoPasso.FONTE, "x").descricao = "y"


class TestSanitizadores:
    def test_mascarar(self):
        rastro = Rastro()
        assert sanitizadores.mascarar(Categoria.CPF, dados.CPF, rastro) == "***.***.***-37"
        assert rastro.passos[0].valor_mascarado == "***.***.***-37"

    def test_redigir_e_bloquear(self):
        rastro = Rastro()
        assert sanitizadores.redigir(Categoria.NOME, rastro) == "[REDIGIDO:NOME]"
        assert sanitizadores.bloquear(Categoria.NOME, rastro) == "[BLOQUEADO:NOME]"
        assert [p.descricao for p in rastro.passos] == ["redigir", "bloquear"]

    def test_arredondar(self):
        rastro = Rastro()
        assert sanitizadores.arredondar_localizacao(-23.565, rastro) == -23.57
        assert rastro.passos[0].valor_mascarado == "-23.57"

    def test_sem_rastro(self):
        assert sanitizadores.mascarar(Categoria.EMAIL, dados.EMAIL) == "f***@exemplo.com"
        assert sanitizadores.redigir(Categoria.CPF) == "[REDIGIDO:CPF]"
        assert sanitizadores.bloquear(Categoria.CPF) == "[BLOQUEADO:CPF]"
        assert sanitizadores.arredondar_localizacao(-46.6525) == -46.65


class TestHashSenha:
    def test_formato_e_conferencia(self):
        rastro = Rastro()
        valor = sanitizadores.hash_senha(dados.SENHA, rastro, iteracoes=1000)
        algoritmo, iteracoes, _, _ = valor.split("$")
        assert (algoritmo, iteracoes) == ("pbkdf2_sha256", "1000")
        assert dados.SENHA not in valor
        assert sanitizadores.eh_hash_senha(valor)
        assert sanitizadores.conferir_senha(dados.SENHA, valor)
        assert not sanitizadores.conferir_senha("OutraSenha1", valor)
        assert dados.SENHA not in rastro.para_texto()

    def test_sal_aleatorio(self):
        assert sanitizadores.hash_senha("x", iteracoes=1000) != sanitizadores.hash_senha("x", iteracoes=1000)

    def test_sal_fixo_e_deterministico(self):
        sal = b"sal-fixo-teste!!"
        assert sanitizadores.hash_senha("x", iteracoes=1000, sal=sal) == sanitizadores.hash_senha("x", iteracoes=1000, sal=sal)

    def test_padrao_de_iteracoes(self):
        assert sanitizadores.ITERACOES_PADRAO >= 600_000

    @pytest.mark.parametrize("hash_gravado", ["texto", "md5$1$a$b", "pbkdf2_sha256$x$y$z"])
    def test_conferir_invalido(self, hash_gravado):
        assert not sanitizadores.conferir_senha("x", hash_gravado)

    @pytest.mark.parametrize(
        "valor, esperado",
        [
            ("AQAAAAIAAYagAAAAE" + "A" * 50, True),
            (dados.SENHA, False),
            ("pbkdf2_sha256$10$curto$curto", False),
            (None, False),
            (123, False),
        ],
    )
    def test_eh_hash(self, valor, esperado):
        assert sanitizadores.eh_hash_senha(valor) is esperado


class TestProjetarCampos:
    def test_lista_branca(self):
        rastro = Rastro()
        projetado = sanitizadores.projetar_campos(dados.resposta_cofre(True), ["tipoSanguineo", "alergias"], rastro)
        assert projetado == {"tipoSanguineo": dados.TIPO_SANGUINEO, "alergias": dados.ALERGIAS}
        assert "removidos: origem" in rastro.passos[0].descricao
        assert dados.MEDICAMENTOS not in rastro.para_texto()

    def test_nada_removido(self):
        rastro = Rastro()
        assert sanitizadores.projetar_campos({"a": 1}, ["a"], rastro) == {"a": 1}
        assert rastro.passos[0].descricao == "projetar_campos (mantidos: a)"

    def test_nenhum_permitido(self):
        assert sanitizadores.projetar_campos({"a": 1}, []) == {}


class TestModelo:
    def test_pior_estado(self):
        assert pior_estado([]) is Estado.PERMITIDO
        assert pior_estado([Estado.NEUTRALIZADO, Estado.PERMITIDO]) is Estado.NEUTRALIZADO
        assert pior_estado([Estado.NEUTRALIZADO, Estado.EXPOSTO]) is Estado.EXPOSTO

    def test_decisao_para_dict(self):
        decisao = DecisaoAchado("$.cpf", Categoria.CPF, Acao.BLOQUEAR, "TA-PUBLICO-01", Estado.NEUTRALIZADO, "m")
        assert decisao.para_dict() == {
            "caminho": "$.cpf",
            "categoria": "CPF",
            "acao": "BLOQUEAR",
            "regraId": "TA-PUBLICO-01",
            "estado": "NEUTRALIZADO",
            "motivo": "m",
        }

    def test_contexto_padrao(self):
        from helmcare_cyber.taint.modelo import Destinatario, Destino

        contexto = ContextoSaida(Destino.LOG)
        assert contexto.destinatario is Destinatario.SISTEMA and contexto.credencial_autorizada is False
