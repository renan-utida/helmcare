import json
import time

import httpx
import pytest

from helmcare_cyber.ia.cliente_llm import (
    ESPERA_RETENTATIVA_S,
    MAXIMO_TENTATIVAS,
    STATUS_REPETIVEIS,
    ClienteGemini,
    ErroLLM,
    limpar_detalhe,
)
from helmcare_cyber.ia.reserva import MotivoReserva
from tests.ia.conftest import (
    CHAVE_FALSA,
    MODELO_FALSO,
    Dorminhoco,
    TransporteFalso,
    cliente_com,
    erro_google,
    mensagem_json,
    resposta_gemini,
)


class TestRequisicao:
    def test_sucesso(self, falso):
        assert cliente_com(falso).gerar_json("sistema", "usuario") == mensagem_json("Mensagem simulada.")

    def test_url_cabecalho_e_corpo(self, falso):
        cliente_com(falso).gerar_json("instrução do sistema", "mensagem do usuário")
        requisicao = falso.requisicoes[0]
        assert requisicao.method == "POST"
        assert str(requisicao.url) == (
            f"https://generativelanguage.googleapis.com/v1beta/models/{MODELO_FALSO}:generateContent"
        )
        assert requisicao.headers["x-goog-api-key"] == CHAVE_FALSA
        assert json.loads(requisicao.content) == {
            "systemInstruction": {"parts": [{"text": "instrução do sistema"}]},
            "contents": [{"role": "user", "parts": [{"text": "mensagem do usuário"}]}],
            "generationConfig": {"responseMimeType": "application/json", "temperature": 0.3, "maxOutputTokens": 512},
        }

    def test_chave_so_no_cabecalho(self, falso):
        cliente_com(falso).gerar_json("s", "u")
        requisicao = falso.requisicoes[0]
        assert CHAVE_FALSA not in str(requisicao.url)
        assert CHAVE_FALSA.encode() not in requisicao.content
        assert "key=" not in str(requisicao.url)

    def test_modelo_com_caractere_especial_e_escapado(self, falso):
        ClienteGemini(CHAVE_FALSA, "modelo/../x", transporte=falso.transporte, dormir=Dorminhoco()).gerar_json("s", "u")
        assert "/models/modelo%2F..%2Fx:generateContent" in str(falso.requisicoes[0].url)


class TestNovaTentativa:
    def test_sucesso_na_segunda_tentativa_depois_de_503(self):
        falso = TransporteFalso(sequencia=[(503, erro_google("The model is overloaded.")), (200, resposta_gemini(mensagem_json("Ok.")))])
        dormir = Dorminhoco()
        assert cliente_com(falso, dormir).gerar_json("s", "u") == mensagem_json("Ok.")
        assert len(falso.requisicoes) == 2
        assert dormir.esperas == [ESPERA_RETENTATIVA_S] == [1.0]

    @pytest.mark.parametrize("status", sorted(STATUS_REPETIVEIS))
    def test_falha_passageira_repete_uma_vez(self, status):
        falso = TransporteFalso(resposta=erro_google("Tente de novo."), status=status)
        dormir = Dorminhoco()
        with pytest.raises(ErroLLM) as erro:
            cliente_com(falso, dormir).gerar_json("s", "u")
        assert len(falso.requisicoes) == MAXIMO_TENTATIVAS == 2
        assert dormir.esperas == [1.0]
        assert (erro.value.motivo, erro.value.status) == (MotivoReserva.ERRO_HTTP, status)
        assert erro.value.detalhe == f"{status}: Tente de novo."
        assert str(erro.value) == f"falha na chamada ao Gemini (HTTP {status}: Tente de novo.)"

    def test_429_repete(self):
        falso = TransporteFalso(sequencia=[(429, erro_google("Resource exhausted.")), (200, resposta_gemini(mensagem_json("Ok.")))])
        assert cliente_com(falso).gerar_json("s", "u") == mensagem_json("Ok.")
        assert len(falso.requisicoes) == 2

    @pytest.mark.parametrize("status", [400, 401, 403, 404])
    def test_erro_do_pedido_nao_repete(self, status):
        falso = TransporteFalso(resposta=erro_google("models/x is not found."), status=status)
        dormir = Dorminhoco()
        with pytest.raises(ErroLLM) as erro:
            cliente_com(falso, dormir).gerar_json("s", "u")
        assert len(falso.requisicoes) == 1 and dormir.esperas == []
        assert erro.value.detalhe == f"{status}: models/x is not found."

    @pytest.mark.parametrize("excecao", [httpx.ReadTimeout, httpx.ConnectError])
    def test_tempo_e_rede_nao_repetem(self, excecao):
        falso = TransporteFalso(excecao=excecao)
        with pytest.raises(ErroLLM):
            cliente_com(falso).gerar_json("s", "u")
        assert len(falso.requisicoes) == 1

    def test_padroes(self):
        cliente = ClienteGemini(CHAVE_FALSA, MODELO_FALSO)
        assert cliente.timeout == 8.0 and cliente.espera_retentativa_s == 1.0
        assert cliente._dormir is time.sleep
        assert STATUS_REPETIVEIS == {429, 500, 502, 503, 504}

    def test_espera_configuravel(self):
        falso = TransporteFalso(status=503)
        dormir = Dorminhoco()
        with pytest.raises(ErroLLM):
            cliente_com(falso, dormir, espera_retentativa_s=0.25).gerar_json("s", "u")
        assert dormir.esperas == [0.25]


class TestDetalheDoErro:
    def test_sem_mensagem_do_google(self):
        with pytest.raises(ErroLLM) as erro:
            cliente_com(TransporteFalso(resposta=b"<html>erro</html>", status=404)).gerar_json("s", "u")
        assert erro.value.detalhe == "404"
        assert str(erro.value) == "falha na chamada ao Gemini (HTTP 404)"

    def test_mensagem_que_nao_e_texto(self):
        with pytest.raises(ErroLLM) as erro:
            cliente_com(TransporteFalso(resposta={"error": {"message": 42}}, status=400)).gerar_json("s", "u")
        assert erro.value.detalhe == "400"

    def test_chave_nunca_aparece_no_detalhe(self):
        mensagem = f"API key {CHAVE_FALSA} not valid. Please pass a valid API key."
        with pytest.raises(ErroLLM) as erro:
            cliente_com(TransporteFalso(resposta=erro_google(mensagem), status=400)).gerar_json("s", "u")
        texto = erro.value.detalhe + str(erro.value) + repr(erro.value)
        assert CHAVE_FALSA not in texto
        assert erro.value.detalhe == "400: API key *** not valid. Please pass a valid API key."

    def test_detalhe_cortado_em_150_caracteres(self):
        with pytest.raises(ErroLLM) as erro:
            cliente_com(TransporteFalso(resposta=erro_google("x" * 400), status=400)).gerar_json("s", "u")
        assert erro.value.detalhe == "400: " + "x" * 150

    def test_chave_cortada_no_limite_nao_sobra(self):
        # A chave fica na posição em que o corte de 150 a dividiria: a troca vem antes do corte.
        mensagem = "a" * 140 + CHAVE_FALSA
        assert CHAVE_FALSA[:10] not in limpar_detalhe(mensagem, CHAVE_FALSA)

    def test_limpar_detalhe(self):
        parece_chave = "AIza" + "ExemploFalso01234567"
        assert limpar_detalhe(f"linha 1\n  linha 2 {parece_chave}") == "linha 1 linha 2 ***"
        assert limpar_detalhe(None) == ""
        assert len(limpar_detalhe("y" * 500)) == 150


class TestFalhas:
    @pytest.mark.parametrize(
        "excecao, motivo",
        [
            (httpx.ReadTimeout, MotivoReserva.TIMEOUT),
            (httpx.ConnectTimeout, MotivoReserva.TIMEOUT),
            (httpx.ConnectError, MotivoReserva.ERRO_REDE),
            (httpx.RemoteProtocolError, MotivoReserva.ERRO_REDE),
        ],
    )
    def test_tempo_e_rede(self, excecao, motivo):
        with pytest.raises(ErroLLM) as erro:
            cliente_com(TransporteFalso(excecao=excecao)).gerar_json("s", "u")
        assert erro.value.motivo is motivo
        assert erro.value.__cause__ is None and erro.value.__suppress_context__

    @pytest.mark.parametrize(
        "resposta",
        [
            b"isto nao e json",
            {"candidates": []},
            {"candidates": [{"content": {"parts": []}}]},
            {"semCandidatos": True},
            resposta_gemini(123),
            ["lista"],
        ],
        ids=["json-malformado", "sem-candidatos", "sem-partes", "sem-chave", "texto-nao-str", "lista"],
    )
    def test_resposta_fora_do_formato(self, resposta):
        with pytest.raises(ErroLLM) as erro:
            cliente_com(TransporteFalso(resposta=resposta)).gerar_json("s", "u")
        assert erro.value.motivo is MotivoReserva.RESPOSTA_INVALIDA

    def test_erro_nunca_mostra_chave_url_ou_corpo(self):
        falso = TransporteFalso(resposta={"segredo": "corpo-sigiloso"}, status=500)
        with pytest.raises(ErroLLM) as erro:
            cliente_com(falso).gerar_json("s", "u")
        texto = str(erro.value) + repr(erro.value)
        assert CHAVE_FALSA not in texto and "googleapis" not in texto and "corpo-sigiloso" not in texto


class TestConstrucao:
    def test_repr_sem_chave(self, falso):
        cliente = cliente_com(falso)
        assert CHAVE_FALSA not in repr(cliente) and CHAVE_FALSA not in str(cliente)
        assert MODELO_FALSO in repr(cliente)

    @pytest.mark.parametrize("chave, modelo", [("", MODELO_FALSO), (CHAVE_FALSA, ""), (None, None)])
    def test_exige_chave_e_modelo(self, chave, modelo):
        with pytest.raises(ValueError):
            ClienteGemini(chave, modelo)

    def test_timeout_padrao(self):
        assert ClienteGemini(CHAVE_FALSA, MODELO_FALSO).timeout == 8.0

    def test_de_ambiente(self):
        cliente = ClienteGemini.de_ambiente({"GEMINI_API_KEY": CHAVE_FALSA, "GEMINI_MODEL": f" {MODELO_FALSO} "})
        assert cliente.modelo == MODELO_FALSO

    @pytest.mark.parametrize(
        "ambiente",
        [{}, {"GEMINI_API_KEY": CHAVE_FALSA}, {"GEMINI_MODEL": MODELO_FALSO}, {"GEMINI_API_KEY": " ", "GEMINI_MODEL": "x"}],
    )
    def test_de_ambiente_incompleto(self, ambiente):
        assert ClienteGemini.de_ambiente(ambiente) is None

    def test_de_ambiente_le_os_environ(self, monkeypatch):
        monkeypatch.setenv("GEMINI_API_KEY", CHAVE_FALSA)
        monkeypatch.setenv("GEMINI_MODEL", MODELO_FALSO)
        assert ClienteGemini.de_ambiente(timeout=2.0).timeout == 2.0
