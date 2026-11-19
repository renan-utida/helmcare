"""A matriz da política, escrita aqui de forma independente da tabela do código."""

import pytest

from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.taint import politica
from helmcare_cyber.taint.modelo import Acao, ContextoSaida, Destinatario, Destino
from helmcare_cyber.taint.politica import decidir, esta_redigido, linha_para, ultimo_segmento
from helmcare_cyber.taint.sanitizadores import hash_senha
from tests.taint.conftest import criar_achado

P, M, A, R, B = Acao.PERMITIR, Acao.MASCARAR, Acao.ARREDONDAR, Acao.REDIGIR, Acao.BLOQUEAR
C = Categoria


def colunas(saude, documentos, telefone_nome, email, loc_exata, loc_aprox, senha, token, qr, segredo):
    """As colunas da matriz: SAUDE | CPF, CNH, RG | TELEFONE, NOME | EMAIL | LOC_EXATA | LOC_APROX | credenciais."""
    return {
        C.SAUDE: saude,
        C.CPF: documentos,
        C.CNH: documentos,
        C.RG: documentos,
        C.TELEFONE: telefone_nome,
        C.NOME: telefone_nome,
        C.EMAIL: email,
        C.LOCALIZACAO_EXATA: loc_exata,
        C.LOCALIZACAO_APROXIMADA: loc_aprox,
        C.SENHA: senha,
        C.TOKEN_ACESSO: token,
        C.QR_TOKEN: qr,
        C.SEGREDO_API: segredo,
    }


TODAS_REDIGIR = {categoria: R for categoria in C}
TODAS_BLOQUEAR = {categoria: B for categoria in C}
SO_LOCALIZACAO = colunas(B, B, B, B, P, P, B, B, B, B)

MATRIZ = [
    (Destino.RESPOSTA_PROPRIO_ENTREGADOR, Destinatario.PROPRIO_ENTREGADOR, "TA-PROPRIO-01", colunas(P, M, P, M, P, P, B, B, B, B)),
    (Destino.CHAMADO_VOLUNTARIO, Destinatario.VOLUNTARIO_ANTES_ACEITE, "TA-VOLUNT-01", colunas(B, B, B, B, A, P, B, B, B, B)),
    (Destino.NOTIFICACAO_PUSH, Destinatario.VOLUNTARIO_ANTES_ACEITE, "TA-VOLUNT-01", colunas(B, B, B, B, A, P, B, B, B, B)),
    (Destino.CHAMADO_VOLUNTARIO, Destinatario.VOLUNTARIO_QUE_ACEITOU, "TA-ACEITOU-01", SO_LOCALIZACAO),
    (Destino.NOTIFICACAO_PUSH, Destinatario.VOLUNTARIO_QUE_ACEITOU, "TA-ACEITOU-01", SO_LOCALIZACAO),
    (Destino.CONTATO_EMERGENCIA, Destinatario.CONTATO_EMERGENCIA, "TA-CONTATO-01", SO_LOCALIZACAO),
    (Destino.CONTATO_EMERGENCIA, Destinatario.SISTEMA, "TA-CONTATO-01", SO_LOCALIZACAO),
    (Destino.NOTIFICACAO_PUSH, Destinatario.CONTATO_EMERGENCIA, "TA-CONTATO-01", SO_LOCALIZACAO),
    (Destino.LOG, Destinatario.SISTEMA, "TA-LOG-01", TODAS_REDIGIR),
    (Destino.LOG, Destinatario.PROPRIO_ENTREGADOR, "TA-LOG-01", TODAS_REDIGIR),
    (Destino.RESPOSTA_API_PUBLICA, Destinatario.SISTEMA, "TA-PUBLICO-01", TODAS_BLOQUEAR),
    (Destino.MENSAGEM_ERRO, Destinatario.PROPRIO_ENTREGADOR, "TA-PUBLICO-01", TODAS_BLOQUEAR),
    (Destino.AUDITORIA, Destinatario.SISTEMA, "TA-PUBLICO-01", TODAS_BLOQUEAR),
    (Destino.DOCUMENTACAO_PUBLICA, Destinatario.SISTEMA, "TA-PUBLICO-01", TODAS_BLOQUEAR),
    (Destino.PERSISTENCIA, Destinatario.SISTEMA, "TA-PERS-01", colunas(P, P, P, P, P, P, B, B, P, B)),
]

CASOS_MATRIZ = [
    (destino, destinatario, categoria, acao, regra)
    for destino, destinatario, regra, acoes in MATRIZ
    for categoria, acao in acoes.items()
]


@pytest.mark.parametrize(
    "destino, destinatario, categoria, acao, regra",
    CASOS_MATRIZ,
    ids=[f"{d.value}-{p.value}-{c.value}" for d, p, c, _, _ in CASOS_MATRIZ],
)
def test_matriz(destino, destinatario, categoria, acao, regra):
    obtida, regra_obtida, motivo = decidir(criar_achado(categoria), ContextoSaida(destino, destinatario))
    assert (obtida, regra_obtida) == (acao, regra)
    assert motivo


class TestNegacaoPorPadrao:
    @pytest.mark.parametrize(
        "destino, destinatario",
        [
            (Destino.RESPOSTA_PROPRIO_ENTREGADOR, Destinatario.VOLUNTARIO_ANTES_ACEITE),
            (Destino.RESPOSTA_PROPRIO_ENTREGADOR, Destinatario.SISTEMA),
            (Destino.CHAMADO_VOLUNTARIO, Destinatario.SISTEMA),
            (Destino.NOTIFICACAO_PUSH, Destinatario.PROPRIO_ENTREGADOR),
            (Destino.COFRE_SOCORRISTA, Destinatario.PROPRIO_ENTREGADOR),
            (Destino.COFRE_SOCORRISTA, Destinatario.SISTEMA),
        ],
    )
    @pytest.mark.parametrize("categoria", [C.LOCALIZACAO_APROXIMADA, C.NOME, C.SAUDE])
    def test_combinacao_nao_prevista_bloqueia(self, destino, destinatario, categoria):
        contexto = ContextoSaida(destino, destinatario)
        assert linha_para(contexto) is None
        acao, regra, _ = decidir(criar_achado(categoria), contexto)
        assert (acao, regra) == (B, "TA-PADRAO-01")

    def test_categoria_fora_da_linha_bloqueia_pela_linha(self):
        # Uma linha que não cita a categoria usa o padrão dela, que é BLOQUEAR.
        acao, regra, _ = decidir(criar_achado(C.NOME), ContextoSaida(Destino.CONTATO_EMERGENCIA))
        assert (acao, regra) == (B, "TA-CONTATO-01")


class TestNegado:
    @pytest.mark.parametrize("destino", list(Destino))
    @pytest.mark.parametrize(
        "destinatario",
        [Destinatario.SOCORRISTA_NAO_AUTORIZADO, Destinatario.ANONIMO, Destinatario.OUTRO_USUARIO],
    )
    def test_vence_qualquer_destino(self, destino, destinatario):
        for categoria in (C.LOCALIZACAO_APROXIMADA, C.EMAIL, C.SAUDE):
            acao, regra, _ = decidir(criar_achado(categoria), ContextoSaida(destino, destinatario))
            assert (acao, regra) == (B, "TA-NEGADO-01")

    def test_vence_credencial_autorizada(self):
        contexto = ContextoSaida(Destino.RESPOSTA_PROPRIO_ENTREGADOR, Destinatario.OUTRO_USUARIO, True)
        assert decidir(criar_achado(C.TOKEN_ACESSO), contexto)[:2] == (B, "TA-NEGADO-01")


class TestJaRedigido:
    @pytest.mark.parametrize("valor", ["[REDIGIDO:CPF]", " [REDIGIDO:SENHA] ", "[BLOQUEADO:SAUDE]"])
    def test_redigido_sempre_passa(self, valor):
        contexto = ContextoSaida(Destino.DOCUMENTACAO_PUBLICA, Destinatario.ANONIMO)
        assert decidir(criar_achado(C.CPF, valor=valor), contexto)[:2] == (P, "TA-MASCARADO-01")

    def test_mascara_parcial_segue_a_matriz(self):
        contexto = ContextoSaida(Destino.RESPOSTA_API_PUBLICA)
        assert decidir(criar_achado(C.CPF, valor="***.***.***-37"), contexto)[:2] == (B, "TA-PUBLICO-01")

    def test_valor_mascarado_nao_conta_como_redigido(self):
        # Sem original, o valor_mascarado ([MASCARADO]) não pode liberar o dado.
        achado = criar_achado(C.SAUDE, valor="")
        assert decidir(achado, ContextoSaida(Destino.RESPOSTA_API_PUBLICA))[0] is B

    def test_esta_redigido(self):
        assert esta_redigido("[REDIGIDO:NOME]") and not esta_redigido("[REDIGIDO:NOME] e mais")


class TestCredencialAutorizada:
    @pytest.mark.parametrize("categoria", [C.TOKEN_ACESSO, C.QR_TOKEN])
    def test_libera_token_e_qr(self, categoria):
        contexto = ContextoSaida(Destino.RESPOSTA_PROPRIO_ENTREGADOR, Destinatario.PROPRIO_ENTREGADOR, True)
        assert decidir(criar_achado(categoria), contexto)[:2] == (P, "TA-PROPRIO-02")

    @pytest.mark.parametrize("categoria", [C.SENHA, C.SEGREDO_API])
    def test_nao_libera_senha_nem_segredo(self, categoria):
        contexto = ContextoSaida(Destino.RESPOSTA_PROPRIO_ENTREGADOR, Destinatario.PROPRIO_ENTREGADOR, True)
        assert decidir(criar_achado(categoria), contexto)[:2] == (B, "TA-PROPRIO-01")

    def test_nao_vale_fora_da_resposta_ao_entregador(self):
        contexto = ContextoSaida(Destino.LOG, Destinatario.SISTEMA, True)
        assert decidir(criar_achado(C.TOKEN_ACESSO), contexto)[:2] == (R, "TA-LOG-01")


class TestCofre:
    COFRE = ContextoSaida(Destino.COFRE_SOCORRISTA, Destinatario.SOCORRISTA_VERIFICADO_NA_JANELA)

    @pytest.mark.parametrize(
        "categoria, caminho, acao, regra",
        [
            (C.SAUDE, "$.tipoSanguineo", P, "TA-COFRE-01"),
            (C.SAUDE, "$.alergias", P, "TA-COFRE-01"),
            (C.SAUDE, "$.alergias[0]", P, "TA-COFRE-01"),
            (C.SAUDE, "$.condicoesCriticas", P, "TA-COFRE-01"),
            (C.SAUDE, "$.medicamentos", B, "TA-COFRE-03"),
            (C.SAUDE, "$.laudo", B, "TA-COFRE-03"),
            (C.NOME, "$.contatos[0].nome", P, "TA-COFRE-02"),
            (C.TELEFONE, "$.contatos[2].telefone", P, "TA-COFRE-02"),
            (C.NOME, "$.nome", B, "TA-COFRE-03"),
            (C.TELEFONE, "$.telefone", B, "TA-COFRE-03"),
            (C.NOME, "$.contatosAntigos[0].nome", B, "TA-COFRE-03"),
            (C.CPF, "$.contatos[0].cpf", B, "TA-COFRE-03"),
            (C.CPF, "$.cpf", B, "TA-COFRE-03"),
            (C.CNH, "$.cnh", B, "TA-COFRE-03"),
            (C.LOCALIZACAO_EXATA, "$.latitude", B, "TA-COFRE-03"),
            (C.QR_TOKEN, "$.qrToken", B, "TA-COFRE-03"),
            (C.TOKEN_ACESSO, "$.accessToken", B, "TA-COFRE-03"),
        ],
    )
    def test_por_caminho(self, categoria, caminho, acao, regra):
        assert decidir(criar_achado(categoria, caminho), self.COFRE)[:2] == (acao, regra)

    @pytest.mark.parametrize(
        "caminho, segmento",
        [
            ("$.alergias", "alergias"),
            ("$.ficha.alergias[1]", "alergias"),
            ('$["tipo sanguineo"]', "tipo sanguineo"),
            ("$", ""),
            ("$[0]", ""),
        ],
    )
    def test_ultimo_segmento(self, caminho, segmento):
        assert ultimo_segmento(caminho) == segmento


class TestMqtt:
    MQTT = ContextoSaida(Destino.MQTT_PAYLOAD)

    @pytest.mark.parametrize("categoria", [C.LOCALIZACAO_EXATA, C.LOCALIZACAO_APROXIMADA])
    def test_coordenada_sem_identificacao(self, categoria):
        assert decidir(criar_achado(categoria), self.MQTT, False)[:2] == (P, "TA-MQTT-01")

    @pytest.mark.parametrize("categoria", [C.LOCALIZACAO_EXATA, C.LOCALIZACAO_APROXIMADA])
    def test_coordenada_com_identificacao(self, categoria):
        assert decidir(criar_achado(categoria), self.MQTT, True)[:2] == (B, "TA-MQTT-02")

    @pytest.mark.parametrize("categoria", [C.NOME, C.CPF, C.TELEFONE, C.SAUDE, C.QR_TOKEN])
    def test_dado_pessoal_sempre_bloqueia(self, categoria):
        assert decidir(criar_achado(categoria), self.MQTT, False)[:2] == (B, "TA-MQTT-02")

    def test_identificacao_pessoal_sao_todas_menos_localizacao(self):
        assert politica.IDENTIFICACAO_PESSOAL == set(C) - {C.LOCALIZACAO_EXATA, C.LOCALIZACAO_APROXIMADA}


class TestPersistencia:
    PERS = ContextoSaida(Destino.PERSISTENCIA)

    def test_senha_pura_bloqueia(self):
        assert decidir(criar_achado(C.SENHA, valor="SenhaSimulada123"), self.PERS)[:2] == (B, "TA-PERS-01")

    def test_hash_pbkdf2_passa(self):
        valor = hash_senha("SenhaSimulada123", iteracoes=1000)
        assert decidir(criar_achado(C.SENHA, valor=valor), self.PERS)[:2] == (P, "TA-PERS-02")

    def test_hash_identity_passa(self):
        valor = "AQAAAAIAAYagAAAAE" + "Simulado0123456789abcdefghijABCDEFGHIJ" + "=="
        assert decidir(criar_achado(C.SENHA, valor=valor), self.PERS)[:2] == (P, "TA-PERS-02")

    @pytest.mark.parametrize("categoria", [C.TOKEN_ACESSO, C.SEGREDO_API])
    def test_token_e_segredo_nunca(self, categoria):
        assert decidir(criar_achado(categoria, valor=hash_senha("x", iteracoes=1000)), self.PERS)[0] is B


def test_toda_regra_tem_descricao():
    ids = {valor for nome, valor in vars(politica).items() if nome.startswith("TA_")}
    assert ids == set(politica.DESCRICOES_REGRAS)
    assert all(politica.DESCRICOES_REGRAS[i] for i in ids)


def test_toda_linha_da_tabela_tem_regra_descrita():
    for linha in politica.TABELA_POLITICA.values():
        assert linha.regra_id in politica.DESCRICOES_REGRAS
        assert all(e.regra_id in politica.DESCRICOES_REGRAS for e in linha.excecoes)
