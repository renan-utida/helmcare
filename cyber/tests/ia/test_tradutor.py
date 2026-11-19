import json
from dataclasses import asdict

import pytest

from helmcare_cyber.eventos.modelo import EventoSeguranca
from helmcare_cyber.ia.cliente_llm import ErroLLM
from helmcare_cyber.ia.prompt import ConsultaTraducao
from helmcare_cyber.ia.reserva import MENSAGEM_SEM_EVENTOS, MotivoReserva, OrigemMensagem, frase_reserva
from helmcare_cyber.ia.tradutor import TradutorAlertas, Traducao, criar_tradutor, validar_mensagem
from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.risco.risco_cidadao import CorSemaforo, calcular_risco
from helmcare_cyber.simulacao import dados_simulados as dados
from helmcare_cyber.taint.modelo import Acao, Destinatario, Destino, Estado, Fonte, Modo
from tests.ia.conftest import (
    CHAVE_FALSA,
    MODELO_FALSO,
    TransporteFalso,
    cliente_com,
    erro_google,
    mensagem_json,
    resposta_gemini,
)

CONSULTA = ConsultaTraducao(Categoria.SENHA, Destino.LOG, Acao.REDIGIR, Estado.NEUTRALIZADO, CorSemaforo.VERDE)
OUTRA = ConsultaTraducao(Categoria.SAUDE, Destino.RESPOSTA_API_PUBLICA, Acao.BLOQUEAR, Estado.EXPOSTO, CorSemaforo.VERMELHO)
BOA = "Sua senha quase apareceu nos registros, mas o sistema a escondeu a tempo."


def evento(categoria=Categoria.SAUDE, destino=Destino.CHAMADO_VOLUNTARIO, estado=Estado.NEUTRALIZADO) -> EventoSeguranca:
    return EventoSeguranca(
        cenario="C4",
        fonte=Fonte.FICHA_MEDICA,
        destino=destino,
        destinatario=Destinatario.VOLUNTARIO_ANTES_ACEITE,
        categoria=categoria,
        acao=Acao.BLOQUEAR,
        estado=estado,
        regra_id="TA-VOLUNT-01",
        modo=Modo.PROTECAO,
        quantidade=2,
        caminhos=("$.ficha.alergias",),
    )


class Relogio:
    def __init__(self) -> None:
        self.agora = 1000.0

    def __call__(self) -> float:
        return self.agora


class TestValidarMensagem:
    def test_valida(self):
        assert validar_mensagem(mensagem_json(f"  {BOA}  ")) == BOA

    def test_cerca_de_codigo(self):
        assert validar_mensagem(f"```json\n{mensagem_json(BOA)}\n```") == BOA
        assert validar_mensagem(f"```{mensagem_json(BOA)}```") == BOA

    def test_duas_frases_e_numero_decimal(self):
        texto = "Seu local ficou a 1.2 km do pedido. Está tudo bem?"
        assert validar_mensagem(mensagem_json(texto)) == texto

    @pytest.mark.parametrize(
        "texto",
        [
            None,
            "não é json",
            json.dumps(["lista"]),
            json.dumps({"texto": BOA}),
            json.dumps({"mensagem": 42}),
            mensagem_json("   "),
            mensagem_json("a" * 281),
            mensagem_json("Uma frase. Outra frase. Mais uma."),
            mensagem_json("Uma! Duas? Três."),
            mensagem_json("Seus dados \u2014 protegidos."),
            mensagem_json("Seus dados \u2013 protegidos."),
            mensagem_json("Seu CPF 482.917.365-37 ficou protegido."),
            mensagem_json(f"O sistema protegeu {dados.EMAIL}."),
        ],
        ids=[
            "nao-texto",
            "nao-json",
            "lista",
            "sem-chave",
            "nao-str",
            "vazia",
            "281-caracteres",
            "3-frases",
            "3-terminadores",
            "travessao-em",
            "travessao-en",
            "cpf-valido",
            "email",
        ],
    )
    def test_invalida(self, texto):
        with pytest.raises(ValueError):
            validar_mensagem(texto)

    def test_280_caracteres_passa(self):
        assert len(validar_mensagem(mensagem_json("a" * 279 + "."))) == 280

    @pytest.mark.parametrize("termo", ["PII", "log", "Logs", "payload", "MQTT", "api", "Token", "hash", "endpoint", "JSON", "taint"])
    def test_jargao(self, termo):
        with pytest.raises(ValueError):
            validar_mensagem(mensagem_json(f"O sistema protegeu o {termo} com cuidado."))

    def test_jargao_so_palavra_inteira(self):
        texto = "Faça o login de novo com calma e use a capital como referência."
        assert validar_mensagem(mensagem_json(texto)) == texto


class TestTraduzir:
    def test_ia_e_cache(self):
        falso = TransporteFalso(resposta=resposta_gemini(mensagem_json(BOA)))
        tradutor = TradutorAlertas(cliente_com(falso))
        primeira = tradutor.traduzir(CONSULTA)
        segunda = tradutor.traduzir(CONSULTA)
        assert primeira == segunda == Traducao(BOA, OrigemMensagem.IA, None)
        assert primeira.selo is None
        assert len(falso.requisicoes) == 1

    def test_cache_do_exposto_com_o_texto_novo(self):
        # A chave do cache é a ConsultaTraducao: o texto novo da mensagem não muda nada nela.
        falso = TransporteFalso(resposta=resposta_gemini(mensagem_json(BOA)))
        tradutor = TradutorAlertas(cliente_com(falso))
        assert tradutor.traduzir(OUTRA) == tradutor.traduzir(OUTRA) == Traducao(BOA, OrigemMensagem.IA)
        assert len(falso.requisicoes) == 1
        enviado = falso.corpos()[0]["contents"][0]["parts"][0]["text"]
        assert "Onde apareceu de verdade: numa resposta aberta do sistema" in enviado
        assert "o dado chegou a aparecer de verdade" in enviado

    def test_cache_por_consulta(self):
        falso = TransporteFalso(resposta=resposta_gemini(mensagem_json(BOA)))
        tradutor = TradutorAlertas(cliente_com(falso))
        tradutor.traduzir(CONSULTA)
        tradutor.traduzir(OUTRA)
        assert len(falso.requisicoes) == 2

    @pytest.mark.parametrize(
        "texto",
        [
            "não é json",
            mensagem_json("a" * 281),
            mensagem_json("Uma frase. Outra frase. Mais uma."),
            mensagem_json("Seus dados \u2014 protegidos."),
            mensagem_json("O log foi protegido."),
            mensagem_json("Seu CPF 482.917.365-37 ficou protegido."),
        ],
        ids=["nao-json", "281", "3-frases", "travessao", "jargao", "cpf"],
    )
    def test_saida_invalida_vira_reserva_sem_cache(self, texto):
        falso = TransporteFalso(resposta=resposta_gemini(texto))
        tradutor = TradutorAlertas(cliente_com(falso))
        traducao = tradutor.traduzir(CONSULTA)
        assert traducao == Traducao(frase_reserva(CONSULTA), OrigemMensagem.RESERVA, MotivoReserva.RESPOSTA_INVALIDA)
        assert traducao.selo == "modelo de reserva"
        tradutor.traduzir(CONSULTA)
        assert len(falso.requisicoes) == 2  # não entrou no cache
        assert not tradutor.disjuntor_aberto

    def test_resposta_fora_do_formato_nao_abre_o_disjuntor(self):
        falso = TransporteFalso(resposta={"candidates": []})
        tradutor = TradutorAlertas(cliente_com(falso))
        assert tradutor.traduzir(CONSULTA).motivo_reserva is MotivoReserva.RESPOSTA_INVALIDA
        assert not tradutor.disjuntor_aberto

    def test_sem_cliente(self):
        traducao = TradutorAlertas().traduzir(CONSULTA)
        assert (traducao.origem, traducao.motivo_reserva) == (OrigemMensagem.RESERVA, MotivoReserva.SEM_CHAVE)


class TestDisjuntor:
    def test_timeout_abre_e_depois_tenta_de_novo(self):
        import httpx

        relogio = Relogio()
        falso = TransporteFalso(excecao=httpx.ReadTimeout)
        tradutor = TradutorAlertas(cliente_com(falso), disjuntor_segundos=60, relogio=relogio)

        assert tradutor.traduzir(CONSULTA).motivo_reserva is MotivoReserva.TIMEOUT
        assert tradutor.disjuntor_aberto
        assert tradutor.traduzir(OUTRA).motivo_reserva is MotivoReserva.DISJUNTOR
        assert len(falso.requisicoes) == 1  # o disjuntor não deixou chamar

        relogio.agora += 59
        assert tradutor.traduzir(OUTRA).motivo_reserva is MotivoReserva.DISJUNTOR
        relogio.agora += 2
        falso.excecao = None
        falso.resposta = resposta_gemini(mensagem_json(BOA))
        assert tradutor.traduzir(OUTRA).origem is OrigemMensagem.IA
        assert len(falso.requisicoes) == 2 and not tradutor.disjuntor_aberto

    @pytest.mark.parametrize("status", [429, 500])
    def test_erro_http_abre(self, status):
        tradutor = TradutorAlertas(cliente_com(TransporteFalso(status=status)), relogio=Relogio())
        assert tradutor.traduzir(CONSULTA).motivo_reserva is MotivoReserva.ERRO_HTTP
        assert tradutor.disjuntor_aberto

    def test_erro_de_rede_abre(self):
        import httpx

        tradutor = TradutorAlertas(cliente_com(TransporteFalso(excecao=httpx.ConnectError)), relogio=Relogio())
        assert tradutor.traduzir(CONSULTA).motivo_reserva is MotivoReserva.ERRO_REDE
        assert tradutor.disjuntor_aberto

    def test_falha_inesperada_do_cliente(self):
        class ClienteQuebrado:
            def gerar_json(self, sistema, usuario):
                raise RuntimeError("defeito simulado")

        tradutor = TradutorAlertas(ClienteQuebrado(), relogio=Relogio())
        assert tradutor.traduzir(CONSULTA).motivo_reserva is MotivoReserva.ERRO_REDE
        assert tradutor.disjuntor_aberto

    def test_erro_llm_de_formato_vindo_do_cliente(self):
        class ClienteFormato:
            def gerar_json(self, sistema, usuario):
                raise ErroLLM(MotivoReserva.RESPOSTA_INVALIDA, "resposta fora do formato")

        tradutor = TradutorAlertas(ClienteFormato())
        assert tradutor.traduzir(CONSULTA).motivo_reserva is MotivoReserva.RESPOSTA_INVALIDA
        assert not tradutor.disjuntor_aberto


class TestNovaTentativaEDiagnostico:
    def test_segunda_tentativa_salva_e_o_disjuntor_fica_fechado(self):
        falso = TransporteFalso(sequencia=[(503, erro_google("overloaded")), (200, resposta_gemini(mensagem_json(BOA)))])
        tradutor = TradutorAlertas(cliente_com(falso), relogio=Relogio())
        traducao = tradutor.traduzir(CONSULTA)
        assert traducao == Traducao(BOA, OrigemMensagem.IA)
        assert traducao.motivo_descrito() == "-"
        assert len(falso.requisicoes) == 2 and not tradutor.disjuntor_aberto

    def test_falha_nas_duas_tentativas_abre_o_disjuntor(self):
        falso = TransporteFalso(resposta=erro_google("The model is overloaded."), status=503)
        tradutor = TradutorAlertas(cliente_com(falso), relogio=Relogio())
        traducao = tradutor.traduzir(CONSULTA)
        assert len(falso.requisicoes) == 2
        assert tradutor.disjuntor_aberto
        assert traducao.motivo_reserva is MotivoReserva.ERRO_HTTP
        assert traducao.detalhe_reserva == "503: The model is overloaded."
        assert traducao.motivo_descrito() == "ERRO_HTTP (503: The model is overloaded.)"
        # Com o disjuntor aberto, a consulta seguinte nem chega ao transporte.
        assert tradutor.traduzir(OUTRA).motivo_descrito() == "DISJUNTOR"
        assert len(falso.requisicoes) == 2

    def test_404_nao_repete_e_abre_o_disjuntor(self):
        falso = TransporteFalso(resposta=erro_google("models/x is not found."), status=404)
        tradutor = TradutorAlertas(cliente_com(falso), relogio=Relogio())
        assert tradutor.traduzir(CONSULTA).motivo_descrito() == "ERRO_HTTP (404: models/x is not found.)"
        assert len(falso.requisicoes) == 1 and tradutor.disjuntor_aberto

    def test_detalhe_sem_a_chave(self):
        falso = TransporteFalso(resposta=erro_google(f"API key {CHAVE_FALSA} not valid."), status=400)
        traducao = TradutorAlertas(cliente_com(falso)).traduzir(CONSULTA)
        assert CHAVE_FALSA not in traducao.motivo_descrito() + repr(traducao)
        assert "***" in traducao.detalhe_reserva

    def test_falha_inesperada_so_com_o_tipo(self):
        class ClienteQuebrado:
            def gerar_json(self, sistema, usuario):
                raise RuntimeError(f"conteúdo sigiloso {CHAVE_FALSA}")

        traducao = TradutorAlertas(ClienteQuebrado(), relogio=Relogio()).traduzir(CONSULTA)
        assert traducao.motivo_descrito() == "ERRO_REDE (falha inesperada (RuntimeError))"
        assert CHAVE_FALSA not in traducao.motivo_descrito()

    def test_motivo_sem_detalhe(self):
        assert TradutorAlertas().traduzir(CONSULTA).motivo_descrito() == "SEM_CHAVE"


class TestEventosERisco:
    def test_consulta_do_evento(self):
        assert TradutorAlertas.consulta_do_evento(evento(), CorSemaforo.AMARELO) == ConsultaTraducao(
            Categoria.SAUDE, Destino.CHAMADO_VOLUNTARIO, Acao.BLOQUEAR, Estado.NEUTRALIZADO, CorSemaforo.AMARELO
        )

    def test_traduzir_evento_usa_a_cor_dos_pontos(self, falso):
        TradutorAlertas(cliente_com(falso)).traduzir_evento(evento(estado=Estado.EXPOSTO))
        usuario = falso.corpos()[0]["contents"][0]["parts"][0]["text"]
        assert "Cor do semáforo: vermelho, perigo" in usuario.splitlines()  # 81 pontos

    def test_traduzir_evento_com_cor(self, falso):
        TradutorAlertas(cliente_com(falso)).traduzir_evento(evento(), CorSemaforo.AMARELO)
        assert falso.corpos()[0]["contents"][0]["parts"][0]["text"].endswith("amarelo, atenção")

    @pytest.mark.parametrize("origem", list(OrigemMensagem))
    def test_com_traducao_mantem_os_outros_campos(self, origem):
        original = evento()
        traduzido = TradutorAlertas.com_traducao(original, Traducao("Mensagem simulada.", origem))
        assert (traduzido.mensagem_cidadao, traduzido.origem_mensagem) == ("Mensagem simulada.", origem.value)
        assert original.mensagem_cidadao is None
        sem_mensagem = asdict(traduzido) | {"mensagem_cidadao": None, "origem_mensagem": None}
        assert sem_mensagem == asdict(original)

    def test_traduzir_risco_sem_eventos(self):
        traducao = TradutorAlertas().traduzir_risco(calcular_risco([]), [])
        assert traducao == Traducao(MENSAGEM_SEM_EVENTOS, OrigemMensagem.RESERVA, None)

    def test_traduzir_risco_so_com_permitidos(self):
        permitido = evento(estado=Estado.PERMITIDO)
        traducao = TradutorAlertas().traduzir_risco(calcular_risco([permitido]), [permitido])
        assert traducao.mensagem == MENSAGEM_SEM_EVENTOS

    def test_traduzir_risco_usa_o_pior_evento_e_a_cor_geral(self, falso):
        # O pior evento sozinho vale 24,3 (verde), mas 4 pares somam 6 de volume: o índice é 30 (amarelo).
        pior = evento(Categoria.SAUDE, Destino.CHAMADO_VOLUNTARIO)
        leves = [evento(Categoria.NOME, destino) for destino in (Destino.LOG, Destino.MENSAGEM_ERRO, Destino.PERSISTENCIA)]
        risco = calcular_risco([pior, *leves])
        assert (risco.indice, risco.cor) == (30, CorSemaforo.AMARELO)
        TradutorAlertas(cliente_com(falso)).traduzir_risco(risco, [*leves, pior])
        usuario = falso.corpos()[0]["contents"][0]["parts"][0]["text"]
        assert usuario.startswith("Em risco: seus dados de saúde")
        assert usuario.endswith("Cor do semáforo: amarelo, atenção")

    def test_traduzir_risco_com_lista_diferente(self):
        # O id do por_que não está na lista recebida: usa o que mais pesa nela.
        risco = calcular_risco([evento()])
        outro = evento(Categoria.SENHA, Destino.LOG)
        traducao = TradutorAlertas().traduzir_risco(risco, [outro])
        assert traducao.mensagem.startswith("Sua senha")


class TestCriarTradutor:
    def test_com_ambiente(self):
        tradutor = criar_tradutor({"GEMINI_API_KEY": CHAVE_FALSA, "GEMINI_MODEL": MODELO_FALSO})
        assert tradutor.cliente.modelo == MODELO_FALSO

    def test_sem_ambiente(self):
        assert criar_tradutor({}).cliente is None
