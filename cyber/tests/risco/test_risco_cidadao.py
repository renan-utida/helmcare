import json
from datetime import UTC, datetime, timedelta
from decimal import Decimal

import pytest

from helmcare_cyber.eventos.modelo import EventoSeguranca
from helmcare_cyber.eventos.repositorio import RepositorioEventos
from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.risco.risco_cidadao import (
    FATOR_DESTINO,
    FATOR_ESTADO,
    ICONE_COR,
    PESO_CATEGORIA,
    ROTULO_COR,
    CorSemaforo,
    arredondar,
    calcular_risco,
    cor_por_pontos,
    pontos_evento,
)
from helmcare_cyber.simulacao.cenarios import executar_todos
from helmcare_cyber.taint.guarda import GuardaTaint
from helmcare_cyber.taint.modelo import Acao, Destinatario, Destino, Estado, Fonte, Modo

AGORA = datetime(2026, 11, 19, 12, 0, tzinfo=UTC)


def iso(instante: datetime) -> str:
    return instante.strftime("%Y-%m-%dT%H:%M:%S.000Z")


def evento(
    categoria=Categoria.CPF,
    destino=Destino.LOG,
    estado=Estado.NEUTRALIZADO,
    fonte=Fonte.CADASTRO_ENTREGADOR,
    minutos_atras: float = 1,
) -> EventoSeguranca:
    return EventoSeguranca(
        cenario=None,
        fonte=fonte,
        destino=destino,
        destinatario=Destinatario.SISTEMA,
        categoria=categoria,
        acao=Acao.BLOQUEAR,
        estado=estado,
        regra_id="TA-TESTE-01",
        modo=Modo.PROTECAO,
        quantidade=1,
        ts=iso(AGORA - timedelta(minutes=minutos_atras)),
    )


def risco(eventos, **opcoes):
    return calcular_risco(eventos, agora=AGORA, janela_min=opcoes.pop("janela_min", 1440), **opcoes)


class TestTabelas:
    def test_todo_valor_dos_enums_tem_peso_ou_fator(self):
        assert set(PESO_CATEGORIA) == set(Categoria)
        assert set(FATOR_DESTINO) == set(Destino)
        assert set(FATOR_ESTADO) == set(Estado)

    def test_rotulo_e_icone_por_cor(self):
        assert set(ROTULO_COR) == set(ICONE_COR) == set(CorSemaforo)
        assert ICONE_COR[CorSemaforo.VERMELHO] == "PERIGO"


class TestPontos:
    @pytest.mark.parametrize(
        "categoria, destino, estado, esperado",
        [
            (Categoria.SAUDE, Destino.CHAMADO_VOLUNTARIO, Estado.NEUTRALIZADO, "24.3"),
            (Categoria.SAUDE, Destino.RESPOSTA_API_PUBLICA, Estado.EXPOSTO, "90"),
            (Categoria.SENHA, Destino.LOG, Estado.NEUTRALIZADO, "15"),
            (Categoria.SENHA, Destino.PERSISTENCIA, Estado.EXPOSTO, "40"),
            (Categoria.QR_TOKEN, Destino.DOCUMENTACAO_PUBLICA, Estado.EXPOSTO, "40"),
            (Categoria.LOCALIZACAO_APROXIMADA, Destino.AUDITORIA, Estado.EXPOSTO, "12"),
            (Categoria.NOME, Destino.COFRE_SOCORRISTA, Estado.EXPOSTO, "45"),
            (Categoria.SAUDE, Destino.COFRE_SOCORRISTA, Estado.PERMITIDO, "0"),
        ],
    )
    def test_exemplos_a_mao(self, categoria, destino, estado, esperado):
        assert pontos_evento(evento(categoria, destino, estado)) == Decimal(esperado)

    def test_decimal_sem_erro_de_ponto_flutuante(self):
        pontos = pontos_evento(evento(Categoria.SAUDE, Destino.CHAMADO_VOLUNTARIO))
        assert float(pontos) == 24.3 and isinstance(pontos, Decimal)

    def test_evento_sem_categoria_pesa_5(self):
        assert pontos_evento(evento(None, Destino.RESPOSTA_API_PUBLICA, Estado.NEUTRALIZADO)) == Decimal(15)


class TestCalibracao:
    @pytest.mark.parametrize(
        "eventos, pior, volume, indice, cor",
        [
            ([], "0", 0, 0, CorSemaforo.VERDE),
            ([evento(Categoria.SENHA, Destino.LOG)], "15", 0, 15, CorSemaforo.VERDE),
            ([evento(Categoria.SAUDE, Destino.RESPOSTA_API_PUBLICA, Estado.EXPOSTO)], "90", 0, 90, CorSemaforo.VERMELHO),
            ([evento(Categoria.SENHA, Destino.LOG, Estado.EXPOSTO)], "50", 0, 50, CorSemaforo.AMARELO),
            ([evento(Categoria.QR_TOKEN, Destino.DOCUMENTACAO_PUBLICA, Estado.EXPOSTO)], "40", 0, 40, CorSemaforo.AMARELO),
        ],
        ids=["sem-eventos", "senha-redigida-log", "saude-exposta-publica", "senha-exposta-log", "qr-exposto-doc"],
    )
    def test_tabela(self, eventos, pior, volume, indice, cor):
        resultado = risco(eventos)
        assert resultado.pior_evento_pontos == Decimal(pior)
        assert (resultado.volume_pontos, resultado.indice, resultado.cor) == (volume, indice, cor)
        assert resultado.rotulo == ROTULO_COR[cor] and resultado.icone == ICONE_COR[cor]


@pytest.fixture(scope="module")
def eventos_dos_cenarios():
    ids = [f"C{n}" for n in range(1, 11)]
    por_modo = {}
    for modo in Modo:
        repositorio = RepositorioEventos()
        executar_todos(GuardaTaint(repositorio=repositorio, modo=modo), ids)
        por_modo[modo] = repositorio.listar()
    return por_modo


class TestCenariosReais:
    def test_c1_a_c10_em_protecao(self, eventos_dos_cenarios):
        resultado = calcular_risco(eventos_dos_cenarios[Modo.PROTECAO])
        assert resultado.pior_evento_pontos == Decimal("24.3")
        assert resultado.volume_pontos == 14
        assert resultado.indice == 38
        assert resultado.cor is CorSemaforo.AMARELO

    def test_c1_a_c10_em_auditoria(self, eventos_dos_cenarios):
        resultado = calcular_risco(eventos_dos_cenarios[Modo.AUDITORIA])
        assert resultado.cor is CorSemaforo.VERMELHO
        assert (resultado.pior_evento_pontos, resultado.volume_pontos, resultado.indice) == (Decimal(81), 14, 95)

    def test_oito_pares_que_pesam(self, eventos_dos_cenarios):
        pares = {(e.destino, e.fonte) for e in eventos_dos_cenarios[Modo.PROTECAO] if e.estado is not Estado.PERMITIDO}
        assert len(pares) == 8


class TestVolume:
    def test_permitido_vale_zero_e_fica_fora_do_volume(self):
        permitidos = [evento(Categoria.SAUDE, d, Estado.PERMITIDO) for d in (Destino.COFRE_SOCORRISTA, Destino.LOG)]
        resultado = risco(permitidos + [evento(Categoria.SENHA, Destino.LOG)])
        assert (resultado.indice, resultado.volume_pontos) == (15, 0)
        assert resultado.contadores.permitidos == 2

    def test_so_permitidos(self):
        resultado = risco([evento(Categoria.SAUDE, Destino.COFRE_SOCORRISTA, Estado.PERMITIDO)])
        assert (resultado.indice, resultado.cor, resultado.por_que) == (0, CorSemaforo.VERDE, ())

    def test_mesmo_par_conta_uma_vez(self):
        resultado = risco([evento(Categoria.CPF), evento(Categoria.NOME), evento(Categoria.EMAIL)])
        assert resultado.volume_pontos == 0

    def test_dois_pares(self):
        resultado = risco([evento(fonte=Fonte.LOGIN), evento(fonte=Fonte.CADASTRO_ENTREGADOR)])
        assert resultado.volume_pontos == 2

    def test_teto_do_volume(self):
        eventos = [evento(destino=d, fonte=f) for d in Destino for f in Fonte]
        assert risco(eventos).volume_pontos == 25

    def test_teto_do_indice(self):
        eventos = [evento(Categoria.SENHA, d, Estado.EXPOSTO, f) for d in Destino for f in Fonte]
        assert risco(eventos).indice == 100


class TestFaixas:
    @pytest.mark.parametrize(
        "valor, cor",
        [
            (0, CorSemaforo.VERDE),
            (29, CorSemaforo.VERDE),
            (Decimal("29.4"), CorSemaforo.VERDE),
            (Decimal("29.5"), CorSemaforo.AMARELO),
            (30, CorSemaforo.AMARELO),
            (59, CorSemaforo.AMARELO),
            (60, CorSemaforo.VERMELHO),
            (100, CorSemaforo.VERMELHO),
            (150, CorSemaforo.VERMELHO),
        ],
    )
    def test_limites(self, valor, cor):
        assert cor_por_pontos(valor) is cor

    def test_meio_para_cima(self):
        # CPF redigido no log: 10 x 7 x 0,5 x 0,3 = 10,5; a regra do banqueiro daria 10.
        assert risco([evento(Categoria.CPF, Destino.LOG)]).indice == 11
        assert arredondar(Decimal("10.5")) == 11 and arredondar(Decimal("2.5")) == 3 and arredondar("2.4") == 2


class TestJanela:
    def test_so_eventos_dentro_da_janela(self):
        recente = evento(Categoria.SENHA, Destino.LOG, minutos_atras=10)
        antigo = evento(Categoria.SAUDE, Destino.RESPOSTA_API_PUBLICA, Estado.EXPOSTO, minutos_atras=120)
        futuro = evento(Categoria.SAUDE, Destino.RESPOSTA_API_PUBLICA, Estado.EXPOSTO, minutos_atras=-5)
        resultado = risco([recente, antigo, futuro], janela_min=60)
        assert (resultado.indice, resultado.contadores.eventos, resultado.janela_min) == (15, 1, 60)

    def test_limite_da_janela_entra(self):
        assert risco([evento(minutos_atras=60)], janela_min=60).contadores.eventos == 1

    def test_janela_padrao_da_config(self, monkeypatch):
        monkeypatch.setenv("RISCO_JANELA_MIN", "30")
        resultado = calcular_risco([evento(minutos_atras=45)], agora=AGORA)
        assert (resultado.janela_min, resultado.contadores.eventos) == (30, 0)

    def test_agora_padrao_e_o_relogio(self):
        assert calcular_risco([EventoSeguranca(**_campos_minimos())], janela_min=5).contadores.eventos == 1


def _campos_minimos() -> dict:
    return dict(
        cenario=None,
        fonte=None,
        destino=Destino.LOG,
        destinatario=Destinatario.SISTEMA,
        categoria=Categoria.CPF,
        acao=Acao.REDIGIR,
        estado=Estado.NEUTRALIZADO,
        regra_id="TA-LOG-01",
        modo=Modo.PROTECAO,
        quantidade=1,
    )


class TestPorQueEContadores:
    def test_ordem_e_desempate_pelo_mais_recente(self):
        antigo = evento(Categoria.SAUDE, Destino.CHAMADO_VOLUNTARIO, minutos_atras=30)
        recente = evento(Categoria.SAUDE, Destino.COFRE_SOCORRISTA, minutos_atras=5)
        senha = evento(Categoria.SENHA, Destino.LOG, minutos_atras=1)
        leve = evento(Categoria.LOCALIZACAO_APROXIMADA, Destino.LOG, minutos_atras=1)
        resultado = risco([leve, antigo, senha, recente])
        assert [item.evento_id for item in resultado.por_que] == [recente.id, antigo.id, senha.id]
        assert [item.pontos for item in resultado.por_que] == [Decimal("24.3"), Decimal("24.3"), Decimal(15)]

    def test_por_que_nao_traz_permitido(self):
        resultado = risco([evento(estado=Estado.PERMITIDO), evento(Categoria.NOME)])
        assert [item.categoria for item in resultado.por_que] == [Categoria.NOME]

    def test_contadores(self):
        eventos = [
            evento(estado=Estado.EXPOSTO),
            evento(estado=Estado.NEUTRALIZADO),
            evento(estado=Estado.NEUTRALIZADO),
            evento(estado=Estado.PERMITIDO),
        ]
        contadores = risco(eventos).contadores
        assert (contadores.eventos, contadores.expostos, contadores.neutralizados, contadores.permitidos) == (4, 1, 2, 1)

    def test_evento_sem_categoria(self):
        resultado = risco([evento(None, Destino.LOG)])
        assert resultado.pior_evento_pontos == Decimal("7.5")
        assert resultado.por_que[0].categoria is None
        assert resultado.por_que[0].para_dict()["categoria"] is None


class TestParaDict:
    def test_camel_case_e_json(self):
        resultado = risco([evento(Categoria.SAUDE, Destino.CHAMADO_VOLUNTARIO)])
        dicionario = resultado.para_dict()
        assert list(dicionario) == [
            "indice",
            "cor",
            "rotulo",
            "icone",
            "piorEventoPontos",
            "volumePontos",
            "janelaMin",
            "contadores",
            "porQue",
            "mensagemCidadao",
            "origemMensagem",
        ]
        assert dicionario["piorEventoPontos"] == 24.3 and dicionario["cor"] == "VERDE"
        assert dicionario["contadores"] == {"eventos": 1, "neutralizados": 1, "expostos": 0, "permitidos": 0}
        assert dicionario["porQue"][0] == {
            "eventoId": resultado.por_que[0].evento_id,
            "categoria": "SAUDE",
            "destino": "CHAMADO_VOLUNTARIO",
            "estado": "NEUTRALIZADO",
            "pontos": 24.3,
        }
        assert dicionario["mensagemCidadao"] is None and dicionario["origemMensagem"] is None
        json.dumps(dicionario)

    def test_imutavel(self):
        with pytest.raises(AttributeError):
            risco([]).indice = 1
