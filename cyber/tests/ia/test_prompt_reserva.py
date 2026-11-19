import json
from itertools import product

import pytest

from helmcare_cyber.ia.prompt import (
    DESCRICAO_CATEGORIA,
    DESCRICAO_COR,
    DESCRICAO_DESTINO,
    PROMPT_SISTEMA,
    ConsultaTraducao,
    descricao_acao,
    montar_mensagem_usuario,
)
from helmcare_cyber.ia.reserva import MENSAGEM_SEM_EVENTOS, SELO_RESERVA, frase_reserva
from helmcare_cyber.ia.tradutor import validar_mensagem
from helmcare_cyber.pii import MotorPII
from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.risco.risco_cidadao import CorSemaforo
from helmcare_cyber.taint.modelo import Acao, Destino, Estado

PROMPT_ESPERADO = (
    "Você é o assistente de comunicação de segurança do HelmCare, um capacete inteligente para entregadores "
    "de moto. Explique um evento de segurança de dados para uma pessoa leiga de 60 anos, em português do "
    "Brasil, em no máximo 2 frases curtas, em tom calmo e acolhedor. Não use termos técnicos como PII, log, "
    "payload, MQTT ou API. Não invente fatos além do evento. Diga o que poderia ter acontecido com os dados "
    'da pessoa e o que o sistema fez. Não use travessões. Responda só com JSON no formato {"mensagem": "..."}.'
)


def consulta(categoria=Categoria.SENHA, destino=Destino.LOG, acao=Acao.REDIGIR, estado=Estado.NEUTRALIZADO,
             cor=CorSemaforo.VERDE) -> ConsultaTraducao:
    return ConsultaTraducao(categoria, destino, acao, estado, cor)


class TestDescricoes:
    def test_cobrem_todos_os_enums(self):
        assert set(DESCRICAO_CATEGORIA) == {*Categoria, None}
        assert set(DESCRICAO_DESTINO) == set(Destino)
        assert set(DESCRICAO_COR) == set(CorSemaforo)

    def test_temas_em_minusculas(self):
        assert all(tema[0].islower() for tema in DESCRICAO_CATEGORIA.values())

    def test_destinos_comecam_com_preposicao(self):
        preposicoes = ("no ", "na ", "nos ", "nas ", "num ", "numa ")
        assert all(onde.startswith(preposicoes) for onde in DESCRICAO_DESTINO.values())

    @pytest.mark.parametrize(
        "acao, estado, esperado",
        [
            (Acao.BLOQUEAR, Estado.EXPOSTO, "não conseguiu impedir, só registrou o ocorrido"),
            (Acao.REDIGIR, Estado.PERMITIDO, "liberou, porque o canal é autorizado"),
            (Acao.PERMITIR, Estado.NEUTRALIZADO, "liberou, porque o canal é autorizado"),
            (Acao.BLOQUEAR, Estado.NEUTRALIZADO, "bloqueou a informação"),
            (Acao.REDIGIR, Estado.NEUTRALIZADO, "escondeu a informação"),
            (Acao.MASCARAR, Estado.NEUTRALIZADO, "escondeu parte da informação"),
            (Acao.ARREDONDAR, Estado.NEUTRALIZADO, "deixou o local apenas aproximado"),
        ],
    )
    def test_descricao_acao(self, acao, estado, esperado):
        assert descricao_acao(acao, estado) == esperado

    def test_toda_combinacao_de_acao_e_estado_tem_texto(self):
        assert all(descricao_acao(acao, estado) for acao, estado in product(Acao, Estado))


class TestPrompt:
    def test_prompt_do_sistema_exato(self):
        assert PROMPT_SISTEMA == PROMPT_ESPERADO

    def test_mensagem_do_usuario(self):
        texto = montar_mensagem_usuario(consulta(Categoria.QR_TOKEN, Destino.DOCUMENTACAO_PUBLICA, Acao.BLOQUEAR,
                                                 Estado.EXPOSTO, CorSemaforo.AMARELO))
        assert texto.splitlines() == [
            "Em risco: o código do QR do seu capacete",
            "Onde poderia aparecer: num documento público",
            "O que o sistema fez: não conseguiu impedir, só registrou o ocorrido",
            "Cor do semáforo: amarelo, atenção",
        ]

    def test_sem_categoria(self):
        assert montar_mensagem_usuario(consulta(None)).startswith("Em risco: uma informação do sistema")

    def test_consulta_hashable(self):
        assert consulta() == consulta() and len({consulta(), consulta()}) == 1


class TestReserva:
    def test_todas_as_frases_passam_na_validacao(self):
        motor = MotorPII()
        falhas = []
        for categoria, destino, estado, acao in product([*Categoria, None], Destino, Estado, Acao):
            frase = frase_reserva(consulta(categoria, destino, acao, estado))
            try:
                validar_mensagem(json.dumps({"mensagem": frase}, ensure_ascii=False), motor)
            except ValueError as erro:
                falhas.append((categoria, destino, estado, acao, str(erro)))
        assert falhas == []

    @pytest.mark.parametrize(
        "estado, acao, esperado",
        [
            (
                Estado.EXPOSTO,
                Acao.BLOQUEAR,
                "Sua senha apareceu nos registros internos do sistema, e o sistema não conseguiu impedir, só "
                "registrou o ocorrido. A equipe precisa corrigir isso o quanto antes.",
            ),
            (
                Estado.NEUTRALIZADO,
                Acao.REDIGIR,
                "Sua senha poderia ter aparecido nos registros internos do sistema, mas o sistema escondeu a "
                "informação antes que saísse. Seus dados continuam protegidos, e a equipe só precisa corrigir o código.",
            ),
            (
                Estado.PERMITIDO,
                Acao.PERMITIR,
                "O sistema usou sua senha nos registros internos do sistema, porque esse é um caminho autorizado. "
                "Não há nada a fazer.",
            ),
            (
                Estado.NEUTRALIZADO,
                Acao.PERMITIR,
                "O sistema usou sua senha nos registros internos do sistema, porque esse é um caminho autorizado. "
                "Não há nada a fazer.",
            ),
        ],
    )
    def test_modelos(self, estado, acao, esperado):
        assert frase_reserva(consulta(Categoria.SENHA, Destino.LOG, acao, estado)) == esperado

    def test_concordancia_no_plural(self):
        exposto = frase_reserva(consulta(Categoria.SAUDE, Destino.RESPOSTA_API_PUBLICA, Acao.BLOQUEAR, Estado.EXPOSTO))
        neutralizado = frase_reserva(consulta(Categoria.SAUDE, Destino.LOG, Acao.REDIGIR, Estado.NEUTRALIZADO))
        assert exposto.startswith("Seus dados de saúde apareceram numa resposta aberta do sistema")
        assert neutralizado.startswith("Seus dados de saúde poderiam ter aparecido")

    def test_deterministica(self):
        assert frase_reserva(consulta()) == frase_reserva(consulta())

    def test_mensagem_sem_eventos_e_selo(self):
        assert validar_mensagem(json.dumps({"mensagem": MENSAGEM_SEM_EVENTOS})) == MENSAGEM_SEM_EVENTOS
        assert SELO_RESERVA == "modelo de reserva"
