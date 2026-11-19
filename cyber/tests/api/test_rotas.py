import json

import pytest

from helmcare_cyber.ia.tradutor import TradutorAlertas
from helmcare_cyber.simulacao import dados_simulados as dados
from helmcare_cyber.simulacao.cenarios import CENARIOS
from tests.api.conftest import API, estado_de, novo_cliente
from tests.ia.conftest import CHAVE_FALSA, MODELO_FALSO, TransporteFalso, cliente_com


class TestSaude:
    def test_sem_ia(self, cliente):
        resposta = cliente.get(f"{API}/saude")
        assert resposta.status_code == 200
        assert resposta.json() == {
            "status": "ok",
            "modo": "PROTECAO",
            "ia": {"configurada": False, "modelo": None, "disjuntorAberto": False},
            "eventos": 0,
        }

    def test_com_ia_sem_mostrar_a_chave(self):
        cliente = novo_cliente(tradutor=TradutorAlertas(cliente_com(TransporteFalso())))
        resposta = cliente.get(f"{API}/saude")
        assert resposta.json()["ia"] == {"configurada": True, "modelo": MODELO_FALSO, "disjuntorAberto": False}
        assert CHAVE_FALSA not in resposta.text


class TestAnalisarPii:
    def test_objeto(self, cliente):
        resposta = cliente.post(f"{API}/pii/analisar", json=dados.cadastro_entregador())
        corpo = resposta.json()
        assert resposta.status_code == 200 and corpo["quantidade"] == 6
        primeiro = corpo["achados"][1]
        assert {"caminho", "categoria", "metodo", "confianca", "regraId", "valorMascarado"} <= set(primeiro)
        assert primeiro["valorMascarado"] == "***.***.***-37"
        assert dados.CPF not in resposta.text

    def test_texto(self, cliente):
        resposta = cliente.post(f"{API}/pii/analisar", json=f"cliente com cpf {dados.CPF}")
        assert [a["categoria"] for a in resposta.json()["achados"]] == ["CPF"]

    def test_sem_pii(self, cliente):
        assert cliente.post(f"{API}/pii/analisar", json=dados.telemetria()).json() == {"quantidade": 0, "achados": []}

    def test_json_malformado_sem_eco(self, cliente):
        resposta = cliente.post(
            f"{API}/pii/analisar", content=f'{{"cpf": "{dados.CPF}"'.encode(), headers={"content-type": "application/json"}
        )
        assert resposta.status_code == 422
        assert resposta.json()["erro"] == "requisicao invalida"
        assert dados.CPF not in resposta.text

    def test_sem_corpo(self, cliente):
        assert cliente.post(f"{API}/pii/analisar").status_code == 422


class TestSimulacoes:
    def test_lista(self, cliente):
        cenarios = cliente.get(f"{API}/simulacoes").json()["cenarios"]
        assert [c["id"] for c in cenarios] == [c.id for c in CENARIOS]
        assert cenarios[10]["rotulo"].startswith("Real")

    @pytest.mark.parametrize("modo, esperado", [("PROTECAO", "NEUTRALIZADO"), ("AUDITORIA", "EXPOSTO")])
    def test_um_cenario(self, modo, esperado):
        resposta = novo_cliente(modo).post(f"{API}/simulacoes/C1")
        corpo = resposta.json()
        assert resposta.status_code == 200
        assert (corpo["id"], corpo["modo"], corpo["decisaoGeral"], corpo["passou"]) == ("C1", modo, esperado, True)
        assert corpo["regras"] == ["TA-LOG-01"] and corpo["eventos"] == 6
        assert corpo["rastro"][0].startswith("FONTE")
        assert "saidaEmitida" not in corpo and "saida_emitida" not in corpo

    def test_id_em_minusculas(self, cliente):
        assert cliente.post(f"{API}/simulacoes/c6").json()["id"] == "C6"

    def test_cenario_inexistente(self, cliente):
        resposta = cliente.post(f"{API}/simulacoes/C99")
        assert resposta.status_code == 404
        assert resposta.json() == {"erro": "nao encontrado"}

    def test_todas(self, cliente):
        corpo = cliente.post(f"{API}/simulacoes/todas").json()
        assert [c["id"] for c in corpo["cenarios"]] == [c.id for c in CENARIOS]
        assert all(c["passou"] for c in corpo["cenarios"])
        assert corpo["eventos"] == len(estado_de(cliente).guarda.repositorio) == 31

    def test_metodo_errado(self, cliente):
        resposta = cliente.get(f"{API}/simulacoes/todas")
        assert resposta.status_code == 405 and resposta.json() == {"erro": "metodo nao permitido"}


class TestModo:
    def test_ler_e_trocar(self, cliente):
        assert cliente.get(f"{API}/modo").json() == {"modo": "PROTECAO"}
        assert cliente.put(f"{API}/modo", json={"modo": "AUDITORIA"}).json() == {"modo": "AUDITORIA"}
        assert estado_de(cliente).guarda.modo.value == "AUDITORIA"
        assert cliente.post(f"{API}/simulacoes/C2").json()["decisaoGeral"] == "EXPOSTO"

    @pytest.mark.parametrize(
        "corpo",
        [{"modo": "DESLIGADO"}, {"modo": "protecao"}, {"modo": dados.CPF}, {}, {"modo": "PROTECAO", "extra": 1}],
    )
    def test_valor_invalido_sem_eco(self, cliente, corpo):
        resposta = cliente.put(f"{API}/modo", json=corpo)
        assert resposta.status_code == 422
        assert dados.CPF not in resposta.text and "DESLIGADO" not in resposta.text
        assert cliente.get(f"{API}/modo").json() == {"modo": "PROTECAO"}


class TestEventos:
    def test_mais_recentes_primeiro_e_limite(self, cliente):
        cliente.post(f"{API}/simulacoes/C1")
        cliente.post(f"{API}/simulacoes/C11")
        eventos = cliente.get(f"{API}/eventos?limite=3").json()
        assert eventos["quantidade"] == 3
        assert eventos["eventos"][0]["cenario"] == "C11"
        assert eventos["eventos"][0]["rastro"][0]["tipo"] == "FONTE"
        assert eventos["eventos"][0]["mensagemCidadao"] is None

    def test_padrao_50(self, cliente):
        cliente.post(f"{API}/simulacoes/todas")
        assert cliente.get(f"{API}/eventos").json()["quantidade"] == 31

    @pytest.mark.parametrize("limite", ["0", "201", "abc"])
    def test_limite_invalido(self, cliente, limite):
        resposta = cliente.get(f"{API}/eventos?limite={limite}")
        assert resposta.status_code == 422 and resposta.json()["campos"] == ["query.limite"]

    def test_com_traducao(self, cliente):
        cliente.post(f"{API}/simulacoes/C11")
        evento = cliente.get(f"{API}/eventos?traduzir=true").json()["eventos"][0]
        assert evento["origemMensagem"] == "RESERVA" and evento["mensagemCidadao"]


class TestReiniciar:
    def test_limpa_eventos_registro_e_banco_e_mantem_o_cache(self):
        tradutor = TradutorAlertas(cliente_com(TransporteFalso()))
        cliente = novo_cliente(tradutor=tradutor)
        cliente.post(f"{API}/simulacoes/C1")
        cliente.post(f"{API}/demo/entregadores", json=dados.cadastro_entregador())
        cliente.get(f"{API}/risco-cidadao")
        cache_antes = dict(tradutor._cache)
        assert cache_antes

        resposta = cliente.post(f"{API}/reiniciar")
        estado = estado_de(cliente)
        assert resposta.json() == {"eventos": 0, "modo": "PROTECAO"}
        assert len(estado.guarda.repositorio) == 0 and len(estado.guarda.registro) == 0 and len(estado.banco) == 0
        assert tradutor._cache == cache_antes
        assert cliente.get(f"{API}/risco-cidadao?traduzir=false").json()["indice"] == 0


class TestControlesDeSeguranca:
    def test_cabecalhos_da_api(self, cliente):
        resposta = cliente.get(f"{API}/saude")
        assert resposta.headers["x-content-type-options"] == "nosniff"
        assert resposta.headers["cache-control"] == "no-store"
        assert resposta.headers["referrer-policy"] == "no-referrer"
        assert resposta.headers["x-frame-options"] == "DENY"
        assert "access-control-allow-origin" not in resposta.headers

    def test_cabecalhos_do_painel(self, cliente):
        resposta = cliente.get("/painel/")
        politica = resposta.headers["content-security-policy"]
        assert "default-src 'self'" in politica and "script-src 'self'" in politica
        assert "unsafe-inline" not in politica
        assert resposta.headers["x-content-type-options"] == "nosniff"

    def test_cabecalhos_nos_erros(self, cliente):
        for resposta in (cliente.get(f"{API}/nao-existe"), cliente.put(f"{API}/modo", json={})):
            assert resposta.headers["x-content-type-options"] == "nosniff"
            assert resposta.headers["cache-control"] == "no-store"

    def test_rota_inexistente(self, cliente):
        resposta = cliente.get(f"{API}/nao-existe")
        assert resposta.status_code == 404 and resposta.json() == {"erro": "nao encontrado"}

    def test_corpo_grande_com_tamanho_declarado(self, cliente):
        corpo = json.dumps({"texto": "x" * (64 * 1024)})
        resposta = cliente.post(f"{API}/pii/analisar", content=corpo, headers={"content-type": "application/json"})
        assert resposta.status_code == 413 and resposta.json() == {"erro": "corpo grande demais"}
        assert resposta.headers["x-content-type-options"] == "nosniff"

    def test_corpo_grande_em_partes(self, cliente):
        def partes():
            for _ in range(80):
                yield b"x" * 1024

        resposta = cliente.post(f"{API}/pii/analisar", content=partes(), headers={"content-type": "application/json"})
        assert resposta.status_code == 413

    def test_corpo_no_limite_passa(self, cliente):
        corpo = json.dumps("x" * (64 * 1024 - 2))
        assert len(corpo) == 64 * 1024
        resposta = cliente.post(f"{API}/pii/analisar", content=corpo, headers={"content-type": "application/json"})
        assert resposta.status_code == 200

    def test_falha_interna_sem_detalhe(self):
        class TradutorQuebrado(TradutorAlertas):
            def traduzir_risco(self, risco, eventos):
                raise RuntimeError(f"segredo {dados.CPF}")

        cliente = novo_cliente(tradutor=TradutorQuebrado())
        resposta = cliente.get(f"{API}/risco-cidadao")
        assert resposta.status_code == 500
        assert resposta.json() == {"erro": "falha interna"}
        assert resposta.headers["x-content-type-options"] == "nosniff"
        assert dados.CPF not in resposta.text

    def test_chave_do_json_com_cpf_nao_volta_no_erro(self, cliente):
        corpo = dados.cadastro_entregador() | {dados.calcular_dv_cpf("482917365"): "x"}
        resposta = cliente.post(f"{API}/demo/entregadores", json=corpo)
        assert resposta.status_code == 422
        assert dados.calcular_dv_cpf("482917365") not in resposta.text
        assert "body.***.***.***-37" in resposta.json()["campos"]


class TestPainelEInicio:
    def test_inicio_redireciona(self, cliente):
        resposta = cliente.get("/", follow_redirects=False)
        assert resposta.status_code in (302, 307) and resposta.headers["location"] == "/painel/"

    def test_painel_sem_barra(self, cliente):
        resposta = cliente.get("/painel")
        assert resposta.status_code == 200 and "Risco Cidadão" in resposta.text

    def test_favicon_sem_corpo(self, cliente):
        resposta = cliente.get("/favicon.ico")
        assert resposta.status_code == 204
        assert resposta.content == b""
        assert resposta.headers["x-content-type-options"] == "nosniff"
        assert resposta.headers["referrer-policy"] == "no-referrer"
        assert resposta.headers["x-frame-options"] == "DENY"
        assert "/favicon.ico" not in cliente.get("/openapi.json").json()["paths"]

    def test_docs(self, cliente):
        assert cliente.get("/docs").status_code == 200
        assert "/cyber/v1/risco-cidadao" in cliente.get("/openapi.json").json()["paths"]
