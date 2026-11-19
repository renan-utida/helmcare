import io
import logging

import pytest

from helmcare_cyber.simulacao import dados_simulados as dados
from helmcare_cyber.taint.filtro_log import (
    MENSAGEM_ERRO_FILTRO,
    FiltroLogGuarda,
    SaidaProtegida,
    instalar_filtro,
    proteger_print,
)
from helmcare_cyber.taint.guarda import GuardaTaint
from helmcare_cyber.taint.modelo import Estado, Fonte, Modo


@pytest.fixture
def logger_em_memoria():
    """Logger isolado que escreve num StringIO; desfaz tudo no fim."""
    logger = logging.getLogger("tests.taint.filtro")
    logger.setLevel(logging.INFO)
    logger.propagate = False
    fluxo = io.StringIO()
    handler = logging.StreamHandler(fluxo)
    handler.setFormatter(logging.Formatter("%(message)s"))
    logger.addHandler(handler)
    yield logger, fluxo
    logger.removeHandler(handler)
    for filtro in list(logger.filters):
        logger.removeFilter(filtro)


def saida(fluxo) -> str:
    return fluxo.getvalue().rstrip("\n")


class TestProtecao:
    def test_f_string_com_dict(self, guarda, logger_em_memoria):
        logger, fluxo = logger_em_memoria
        guarda.registrar_fonte(Fonte.CADASTRO_ENTREGADOR, dados.cadastro_entregador())
        instalar_filtro(logger, guarda)
        cadastro = dados.cadastro_entregador()
        logger.info(f"cadastro {cadastro}")
        texto = saida(fluxo)
        for valor in cadastro.values():
            assert valor not in texto
        assert "[REDIGIDO:NOME]" in texto and "[REDIGIDO:SENHA]" in texto

    def test_com_args(self, guarda, logger_em_memoria):
        logger, fluxo = logger_em_memoria
        filtro = instalar_filtro(logger, guarda)
        logger.info("login email=%s senha=%s", dados.EMAIL, dados.SENHA)
        assert saida(fluxo) == "login email=[REDIGIDO:EMAIL] senha=[REDIGIDO:SENHA]"
        assert filtro.ultimo_resultado.decisao_geral is Estado.NEUTRALIZADO

    def test_dict_de_saude_direto(self, guarda, logger_em_memoria):
        logger, fluxo = logger_em_memoria
        instalar_filtro(logger, guarda)
        logger.info(dados.ficha_medica())
        texto = saida(fluxo)
        assert dados.ALERGIAS not in texto and dados.MEDICAMENTOS not in texto
        assert texto.count("[REDIGIDO:SAUDE]") == 4

    def test_dict_como_argumento(self, guarda, logger_em_memoria):
        logger, fluxo = logger_em_memoria
        instalar_filtro(logger, guarda)
        logger.info("cadastro %s", dados.cadastro_entregador())
        assert dados.CPF not in saida(fluxo)

    def test_mensagem_sem_pii_intacta(self, guarda, logger_em_memoria):
        logger, fluxo = logger_em_memoria
        instalar_filtro(logger, guarda)
        logger.info("turno %s iniciado", "T-1")
        assert saida(fluxo) == "turno T-1 iniciado"

    def test_filtro_no_handler(self, guarda):
        fluxo = io.StringIO()
        handler = logging.StreamHandler(fluxo)
        instalar_filtro(handler, guarda)
        registro = logging.LogRecord("x", logging.INFO, __file__, 1, "cpf %s", (dados.CPF,), None)
        handler.handle(registro)
        assert dados.CPF not in fluxo.getvalue()


class TestAuditoria:
    def test_deixa_passar_e_marca_exposto(self, guarda_auditoria, logger_em_memoria, repositorio):
        logger, fluxo = logger_em_memoria
        filtro = instalar_filtro(logger, guarda_auditoria, cenario="T1")
        logger.info("login senha=%s", dados.SENHA)
        assert saida(fluxo) == f"login senha={dados.SENHA}"
        assert filtro.ultimo_resultado.decisao_geral is Estado.EXPOSTO
        assert [e.estado for e in repositorio.listar()] == [Estado.EXPOSTO]


class _GuardaQueFalha:
    modo = Modo.PROTECAO

    def verificar_saida(self, *args, **kwargs):
        raise RuntimeError("falha simulada")


class _MotorQueFalha:
    def varrer(self, dado):
        raise RuntimeError("falha simulada")


class TestFalhaFechada:
    def test_erro_no_filtro_bloqueia_a_linha(self, logger_em_memoria):
        logger, fluxo = logger_em_memoria
        logger.addFilter(FiltroLogGuarda(_GuardaQueFalha()))
        logger.info("cpf %s", dados.CPF)
        assert saida(fluxo) == MENSAGEM_ERRO_FILTRO

    def test_erro_no_guarda_bloqueia_a_linha(self, logger_em_memoria):
        logger, fluxo = logger_em_memoria
        instalar_filtro(logger, GuardaTaint(motor=_MotorQueFalha()))
        logger.info("cpf %s", dados.CPF)
        assert saida(fluxo) == "[BLOQUEADO:ERRO_INTERNO]"

    def test_sem_recursao_no_logger_do_guarda(self):
        # Filtro instalado no logger que o próprio guarda usa para avisar do erro:
        # o aviso (sem conteúdo) passa sem nova verificação, sem recursão infinita.
        logger = logging.getLogger("helmcare_cyber.taint.guarda")
        fluxo = io.StringIO()
        handler = logging.StreamHandler(fluxo)
        logger.addHandler(handler)
        filtro = instalar_filtro(logger, GuardaTaint(motor=_MotorQueFalha()))
        try:
            logger.error("cpf %s", dados.CPF)
        finally:
            logger.removeFilter(filtro)
            logger.removeHandler(handler)
        linhas = fluxo.getvalue().splitlines()
        assert "falha interna na verificação de saída (RuntimeError)" in linhas[0]
        assert linhas[-1] == "[BLOQUEADO:ERRO_INTERNO]"
        assert dados.CPF not in fluxo.getvalue()


class TestPrint:
    def test_print_de_senha(self, guarda):
        fluxo = io.StringIO()
        with proteger_print(guarda, fluxo) as protegida:
            print(f"senha={dados.SENHA}")
            print("sem dado pessoal")
        assert fluxo.getvalue() == "senha=[REDIGIDO:SENHA]\nsem dado pessoal\n"
        assert protegida.ultimo_resultado.decisao_geral is Estado.PERMITIDO

    def test_flush_de_linha_incompleta(self, guarda):
        fluxo = io.StringIO()
        protegida = SaidaProtegida(guarda, fluxo)
        assert protegida.writable()
        protegida.write(f"cpf {dados.CPF}")
        assert fluxo.getvalue() == ""
        protegida.flush()
        assert fluxo.getvalue() == "cpf [REDIGIDO:CPF]"

    def test_falha_fechada(self):
        fluxo = io.StringIO()
        protegida = SaidaProtegida(_GuardaQueFalha(), fluxo)
        protegida.write(f"senha={dados.SENHA}\n")
        assert fluxo.getvalue() == MENSAGEM_ERRO_FILTRO + "\n"

    def test_auditoria_deixa_passar(self, guarda_auditoria):
        fluxo = io.StringIO()
        with proteger_print(guarda_auditoria, fluxo):
            print(f"senha={dados.SENHA}")
        assert fluxo.getvalue() == f"senha={dados.SENHA}\n"

    def test_destino_padrao_e_stdout(self, guarda, capsys):
        with proteger_print(guarda):
            print(f"cpf {dados.CPF}")
        assert capsys.readouterr().out == "cpf [REDIGIDO:CPF]\n"
