import copy
import json
import logging

import pytest

from helmcare_cyber.config import Config
from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.simulacao import dados_simulados as dados
from helmcare_cyber.taint import criar_guarda
from helmcare_cyber.taint.guarda import MARCADOR_ERRO, GuardaTaint, tratar
from helmcare_cyber.taint.modelo import Acao, ContextoSaida, Destinatario, Destino, Estado, Fonte, Modo
from helmcare_cyber.taint.rastro import Rastro, TipoPasso

LOG = ContextoSaida(Destino.LOG)
PUBLICO = ContextoSaida(Destino.RESPOSTA_API_PUBLICA)
PROPRIO = ContextoSaida(Destino.RESPOSTA_PROPRIO_ENTREGADOR, Destinatario.PROPRIO_ENTREGADOR)
VOLUNTARIO_ANTES = ContextoSaida(Destino.NOTIFICACAO_PUSH, Destinatario.VOLUNTARIO_ANTES_ACEITE)


class TestModos:
    def test_protecao_neutraliza_numa_copia(self, guarda):
        entrada = dados.cadastro_entregador()
        original = copy.deepcopy(entrada)
        resultado = guarda.verificar_saida(entrada, PUBLICO)
        assert entrada == original
        assert resultado.payload_seguro is not entrada
        assert resultado.decisao_geral is Estado.NEUTRALIZADO
        categorias = ("NOME", "CPF", "CNH", "TELEFONE", "EMAIL", "SENHA")
        assert set(resultado.payload_seguro.values()) == {f"[BLOQUEADO:{c}]" for c in categorias}

    def test_auditoria_deixa_passar_intacto(self, guarda_auditoria):
        entrada = dados.cadastro_entregador()
        resultado = guarda_auditoria.verificar_saida(entrada, PUBLICO)
        assert resultado.payload_seguro == entrada
        assert resultado.payload_seguro is not entrada
        assert resultado.decisao_geral is Estado.EXPOSTO
        assert {e.estado for e in resultado.eventos} == {Estado.EXPOSTO}

    def test_modo_em_texto(self):
        assert GuardaTaint(modo="AUDITORIA").modo is Modo.AUDITORIA


class TestAcoes:
    def test_arredonda_meio_para_cima(self, guarda):
        resultado = guarda.verificar_saida({"latitude": -23.565, "longitude": -46.655}, VOLUNTARIO_ANTES)
        assert resultado.payload_seguro == {"latitude": -23.57, "longitude": -46.66}

    def test_arredonda_coordenada_em_texto(self, guarda):
        resultado = guarda.verificar_saida("SOS em -23.5641, -46.6525", VOLUNTARIO_ANTES)
        assert resultado.payload_seguro == "SOS em -23.56, -46.65"

    def test_arredonda_coordenada_textual_em_campo(self, guarda):
        resultado = guarda.verificar_saida({"latitude": "-23.5641"}, VOLUNTARIO_ANTES)
        assert resultado.payload_seguro == {"latitude": "-23.56"}

    def test_mascarar_na_resposta_ao_proprio(self, guarda):
        resultado = guarda.verificar_saida({"cpf": dados.CPF, "email": dados.EMAIL}, PROPRIO)
        assert resultado.payload_seguro == {"cpf": "***.***.***-37", "email": "f***@exemplo.com"}
        assert resultado.decisao_geral is Estado.NEUTRALIZADO

    def test_ja_mascarado_fica_permitido(self, guarda_auditoria):
        # GET /entregadores/{id} já devolve CPF e e-mail mascarados: nada a fazer, nada exposto.
        resultado = guarda_auditoria.verificar_saida({"cpf": "***.***.***-37", "email": "f***@exemplo.com"}, PROPRIO)
        assert resultado.decisao_geral is Estado.PERMITIDO
        assert all("já estava na forma exigida" in d.motivo for d in resultado.decisoes)

    def test_redigido_passa(self, guarda):
        resultado = guarda.verificar_saida({"cpf": "[REDIGIDO:CPF]"}, PUBLICO)
        assert [d.regra_id for d in resultado.decisoes] == ["TA-MASCARADO-01"]
        assert resultado.decisao_geral is Estado.PERMITIDO

    def test_redigir_no_log(self, guarda):
        resultado = guarda.verificar_saida(f"falha no cadastro do CPF {dados.CPF}", LOG)
        assert resultado.payload_seguro == "falha no cadastro do CPF [REDIGIDO:CPF]"

    def test_credencial_no_login(self, guarda):
        contexto = ContextoSaida(Destino.RESPOSTA_PROPRIO_ENTREGADOR, Destinatario.PROPRIO_ENTREGADOR, True)
        corpo = {"tokenType": "Bearer", "accessToken": "TokenSimulado" * 3, "refreshToken": "RefreshSimulado" * 3}
        resultado = guarda.verificar_saida(corpo, contexto)
        assert resultado.payload_seguro == corpo
        assert {d.regra_id for d in resultado.decisoes} == {"TA-PROPRIO-02"}

    @pytest.mark.parametrize(
        "acao, valor, esperado",
        [
            (Acao.PERMITIR, "x", "x"),
            (Acao.BLOQUEAR, "x", "[BLOQUEADO:CPF]"),
            (Acao.REDIGIR, "x", "[REDIGIDO:CPF]"),
            (Acao.MASCARAR, "48291736537", "***.***.***-37"),
        ],
    )
    def test_tratar(self, acao, valor, esperado):
        assert tratar(acao, Categoria.CPF, valor) == esperado

    def test_tratar_arredondar(self):
        assert tratar(Acao.ARREDONDAR, Categoria.LOCALIZACAO_EXATA, -23.5641) == -23.56


class TestSemPii:
    def test_telemetria_sem_evento(self, guarda, repositorio):
        resultado = guarda.verificar_saida(dados.telemetria(), ContextoSaida(Destino.MQTT_PAYLOAD))
        assert resultado.decisao_geral is Estado.PERMITIDO
        assert resultado.decisoes == [] and resultado.eventos == []
        assert len(repositorio) == 0
        assert resultado.payload_seguro == dados.telemetria()
        assert "nenhum dado pessoal" in resultado.rastro.para_texto()

    def test_texto_sem_pii(self, guarda):
        assert guarda.verificar_saida("turno iniciado", LOG).payload_seguro == "turno iniciado"


class TestFormatos:
    def test_json_em_texto(self, guarda):
        resultado = guarda.verificar_saida(json.dumps({"cpf": dados.CPF, "estado": "ARMADO"}), PUBLICO)
        assert json.loads(resultado.payload_seguro) == {"cpf": "[BLOQUEADO:CPF]", "estado": "ARMADO"}

    def test_json_sem_mudanca_volta_igual(self, guarda):
        texto = json.dumps(dados.evento_mqtt_sos(), indent=2)
        resultado = guarda.verificar_saida(texto, ContextoSaida(Destino.MQTT_PAYLOAD))
        assert resultado.payload_seguro is texto
        assert resultado.decisao_geral is Estado.PERMITIDO

    def test_bytes(self, guarda):
        resultado = guarda.verificar_saida(f"cpf {dados.CPF}".encode(), LOG)
        assert resultado.payload_seguro == "cpf [REDIGIDO:CPF]"

    def test_tupla(self, guarda):
        resultado = guarda.verificar_saida(({"cpf": dados.CPF},), PUBLICO)
        assert resultado.payload_seguro == ({"cpf": "[BLOQUEADO:CPF]"},)

    def test_inteiro_longo(self, guarda):
        resultado = guarda.verificar_saida({"documento": int(dados.calcular_dv_cpf("482917365"))}, LOG)
        assert resultado.payload_seguro == {"documento": "[REDIGIDO:CPF]"}

    def test_objeto_sem_texto(self, guarda):
        assert guarda.verificar_saida(None, LOG).decisao_geral is Estado.PERMITIDO


class TestRegistro:
    def test_nome_achado_pelo_registro(self, guarda):
        guarda.registrar_fonte(Fonte.CADASTRO_ENTREGADOR, dados.cadastro_entregador())
        resultado = guarda.verificar_saida({"observacao": f"cliente {dados.NOME_ENTREGADOR} ligou"}, LOG)
        assert resultado.payload_seguro == {"observacao": "cliente [REDIGIDO:NOME] ligou"}
        assert [d.categoria for d in resultado.decisoes] == [Categoria.NOME]

    def test_campo_com_nome_nao_duplica(self, guarda):
        guarda.registrar_fonte(Fonte.CADASTRO_ENTREGADOR, dados.cadastro_entregador())
        resultado = guarda.verificar_saida(dados.cadastro_entregador(), PUBLICO)
        assert len(resultado.decisoes) == 6

    def test_trecho_do_motor_nao_duplica(self, guarda):
        guarda.registrar_fonte(Fonte.CADASTRO_ENTREGADOR, dados.cadastro_entregador())
        resultado = guarda.verificar_saida(f"cpf {dados.CPF}", LOG)
        assert [d.categoria for d in resultado.decisoes] == [Categoria.CPF]

    def test_nome_dentro_de_lista(self, guarda):
        guarda.registrar_fonte(Fonte.CADASTRO_ENTREGADOR, dados.cadastro_entregador())
        resultado = guarda.verificar_saida({"linhas": ["ok", f"falou com {dados.NOME_ENTREGADOR}"]}, LOG)
        assert resultado.payload_seguro == {"linhas": ["ok", "falou com [REDIGIDO:NOME]"]}

    def test_numero_fora_de_campo_conhecido(self, guarda):
        guarda.registrar_fonte(Fonte.OCORRENCIA_SOS, dados.ocorrencia_sos())
        resultado = guarda.verificar_saida({"posicao": dados.LATITUDE_EXATA}, LOG)
        assert resultado.payload_seguro == {"posicao": "[REDIGIDO:LOCALIZACAO_EXATA]"}

    def test_nome_registrado_identifica_payload_mqtt(self, guarda):
        guarda.registrar_fonte(Fonte.CADASTRO_ENTREGADOR, dados.cadastro_entregador())
        evento = dados.evento_mqtt_sos() | {"observacao": dados.NOME_ENTREGADOR}
        resultado = guarda.verificar_saida(evento, ContextoSaida(Destino.MQTT_PAYLOAD))
        assert resultado.payload_seguro["latitude"] == "[BLOQUEADO:LOCALIZACAO_EXATA]"

    def test_achado_permitido_do_registro_nao_muda(self, guarda):
        guarda.registrar_fonte(Fonte.CONTATO_EMERGENCIA, dados.contato_emergencia())
        resposta = {"contatos": [{"nome": "x", "parentesco": f"irmã ({dados.NOME_CONTATO})"}]}
        contexto = ContextoSaida(Destino.COFRE_SOCORRISTA, Destinatario.SOCORRISTA_VERIFICADO_NA_JANELA)
        assert guarda.verificar_saida(resposta, contexto).payload_seguro == resposta

    def test_registrar_fonte_anota_o_rastro(self, guarda):
        rastro = Rastro()
        achados = guarda.registrar_fonte(Fonte.LOGIN, dados.login(), rastro, "POST /entregadores/login")
        assert len(achados) == 2 and len(guarda.registro) >= 2
        assert [p.tipo for p in rastro.passos] == [TipoPasso.FONTE, TipoPasso.FONTE]


class TestEventosERastro:
    def test_um_evento_por_categoria_e_estado(self, guarda, repositorio):
        payload = {"telefone": dados.TELEFONE, "contatos": [{"telefone": dados.TELEFONE_CONTATO}], "cpf": dados.CPF}
        resultado = guarda.verificar_saida(payload, PUBLICO, Fonte.CADASTRO_ENTREGADOR, "TESTE")
        por_categoria = {e.categoria: e for e in resultado.eventos}
        assert set(por_categoria) == {Categoria.TELEFONE, Categoria.CPF}
        assert por_categoria[Categoria.TELEFONE].quantidade == 2
        assert por_categoria[Categoria.TELEFONE].caminhos == ("$.telefone", "$.contatos[0].telefone")
        assert len(repositorio) == 2
        assert all(e.cenario == "TESTE" and e.fonte is Fonte.CADASTRO_ENTREGADOR for e in resultado.eventos)

    def test_permitido_tambem_gera_evento(self, guarda):
        contexto = ContextoSaida(Destino.CHAMADO_VOLUNTARIO, Destinatario.VOLUNTARIO_QUE_ACEITOU)
        resultado = guarda.verificar_saida(dados.ocorrencia_sos(), contexto)
        assert [e.estado for e in resultado.eventos] == [Estado.PERMITIDO]
        assert resultado.eventos[0].quantidade == 2

    def test_rastro_recebe_fonte_e_destino(self, guarda):
        resultado = guarda.verificar_saida({"cpf": dados.CPF}, PUBLICO, Fonte.CADASTRO_ENTREGADOR)
        tipos = [p.tipo for p in resultado.rastro.passos]
        assert tipos == [TipoPasso.FONTE, TipoPasso.DESTINO]
        assert "REGRA TA-PUBLICO-01, ACAO BLOQUEAR, NEUTRALIZADO" in resultado.rastro.para_texto()
        assert resultado.rastro.passos[-1].valor_mascarado == "[BLOQUEADO:CPF]"

    def test_evento_leva_o_rastro(self, guarda):
        resultado = guarda.verificar_saida({"cpf": dados.CPF}, PUBLICO)
        assert resultado.eventos[0].rastro[-1]["tipo"] == "DESTINO"

    def test_regras_aplicadas(self, guarda):
        resultado = guarda.verificar_saida({"cpf": dados.CPF, "cnh": dados.CNH}, PUBLICO)
        assert resultado.regras_aplicadas() == ["TA-PUBLICO-01"]

    def test_repr_nao_mostra_payload(self, guarda_auditoria):
        resultado = guarda_auditoria.verificar_saida({"cpf": dados.CPF}, PUBLICO)
        assert dados.CPF not in repr(resultado)


class _MotorQueFalha:
    def varrer(self, dado):
        raise RuntimeError(f"conteúdo sigiloso {dados.CPF}")


class TestFalhaFechada:
    @pytest.mark.parametrize("modo", list(Modo))
    def test_erro_interno_bloqueia_tudo(self, modo, repositorio, caplog):
        guarda = GuardaTaint(motor=_MotorQueFalha(), repositorio=repositorio, modo=modo)
        with caplog.at_level(logging.ERROR, logger="helmcare_cyber.taint.guarda"):
            resultado = guarda.verificar_saida({"cpf": dados.CPF}, PUBLICO, Fonte.CADASTRO_ENTREGADOR, "C0")
        assert resultado.payload_seguro == MARCADOR_ERRO
        assert resultado.decisao_geral is Estado.NEUTRALIZADO
        evento = resultado.eventos[0]
        assert (evento.regra_id, evento.categoria, evento.acao) == ("TA-ERRO-01", None, Acao.BLOQUEAR)
        assert len(repositorio) == 1
        assert "RuntimeError" in caplog.text
        assert dados.CPF not in caplog.text

    def test_contexto_invalido_falha_fechado(self, guarda):
        resultado = guarda.verificar_saida({"cpf": dados.CPF}, "LOG")
        assert resultado.payload_seguro == MARCADOR_ERRO
        assert resultado.eventos == []

    def test_conteudo_estranho_nao_levanta(self, guarda):
        estranho = {"a": [1, None, True, 2.5, {"b": object()}], 3: "x", "texto": "\x00�"}
        resultado = guarda.verificar_saida(estranho, LOG)
        assert resultado.decisao_geral is Estado.PERMITIDO


def test_exportacoes_do_pacote():
    import helmcare_cyber.taint as taint

    assert taint.GuardaTaint is GuardaTaint and taint.Modo is Modo
    assert set(taint.__all__) >= {"GuardaTaint", "TaintRegistry", "Rastro", "criar_guarda"}
    with pytest.raises(AttributeError):
        taint.NaoExiste  # noqa: B018


def test_criar_guarda_pela_config(tmp_path):
    arquivo = tmp_path / "eventos.jsonl"
    guarda = criar_guarda(Config(modo=Modo.AUDITORIA, eventos_jsonl=str(arquivo)))
    assert guarda.modo is Modo.AUDITORIA
    guarda.verificar_saida({"cpf": dados.CPF}, PUBLICO)
    assert arquivo.read_text(encoding="utf-8").count("\n") == 1


def test_criar_guarda_padrao(monkeypatch):
    monkeypatch.delenv("CYBER_MODO", raising=False)
    monkeypatch.delenv("CYBER_EVENTOS_JSONL", raising=False)
    guarda = criar_guarda()
    assert guarda.modo is Modo.PROTECAO and guarda.repositorio.caminho_jsonl is None
