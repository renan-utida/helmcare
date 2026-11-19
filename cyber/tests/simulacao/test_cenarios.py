import pytest

from helmcare_cyber.eventos.repositorio import RepositorioEventos
from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.simulacao import dados_simulados as dados
from helmcare_cyber.simulacao.cenarios import (
    CENARIOS,
    CENARIOS_POR_ID,
    ROTULO_REAL_SIMULADO,
    ROTULO_SIMULADO,
    executar_cenario,
    executar_todos,
)
from helmcare_cyber.taint.guarda import GuardaTaint
from helmcare_cyber.taint.modelo import Acao, ContextoSaida, Destino, Estado, Modo
from helmcare_cyber.taint.rastro import TipoPasso
from helmcare_cyber.taint.sanitizadores import conferir_senha


def rodar(id_cenario: str, modo: Modo):
    return executar_cenario(id_cenario, GuardaTaint(repositorio=RepositorioEventos(), modo=modo))


def por_categoria(resultado_guarda) -> dict:
    agrupado: dict = {}
    for decisao in resultado_guarda.decisoes:
        agrupado.setdefault(decisao.categoria, []).append(decisao)
    return agrupado


class TestTodos:
    def test_onze_cenarios(self):
        assert [c.id for c in CENARIOS] == [f"C{n}" for n in range(1, 12)]
        assert set(CENARIOS_POR_ID) == {c.id for c in CENARIOS}

    @pytest.mark.parametrize("modo", list(Modo))
    @pytest.mark.parametrize("cenario", CENARIOS, ids=[c.id for c in CENARIOS])
    def test_resultado_esperado(self, cenario, modo):
        resultado = rodar(cenario.id, modo)
        assert resultado.passou, (cenario.id, modo, resultado.decisao_geral)
        assert resultado.decisao_geral is cenario.esperado(modo)
        assert resultado.eventos
        assert all(e.cenario == cenario.id and e.modo is modo for e in resultado.eventos)

    @pytest.mark.parametrize("cenario", CENARIOS, ids=[c.id for c in CENARIOS])
    def test_rastro_completo(self, cenario):
        for guarda in rodar(cenario.id, Modo.PROTECAO).resultados:
            tipos = [p.tipo for p in guarda.rastro.passos]
            assert tipos[0] is TipoPasso.FONTE
            assert TipoPasso.PROPAGACAO in tipos
            assert tipos[-1] is TipoPasso.DESTINO

    def test_rotulos(self):
        assert {c.rotulo for c in CENARIOS if c.id != "C11"} == {ROTULO_SIMULADO}
        assert CENARIOS_POR_ID["C11"].rotulo == ROTULO_REAL_SIMULADO

    def test_executar_todos_com_ids(self):
        guarda = GuardaTaint(modo=Modo.PROTECAO)
        assert [r.cenario.id for r in executar_todos(guarda, ["c2", "C6"])] == ["C2", "C6"]

    def test_registro_limpo_entre_cenarios(self):
        guarda = GuardaTaint()
        executar_todos(guarda, ["C1", "C11"])
        assert guarda.registro.procurar(dados.NOME_ENTREGADOR) == []

    def test_repr_nao_mostra_a_saida(self):
        resultado = rodar("C1", Modo.AUDITORIA)
        assert dados.CPF in resultado.saida_emitida
        assert dados.CPF not in repr(resultado)


class TestCadaCenario:
    def test_c1_log_do_cadastro(self):
        protecao = rodar("C1", Modo.PROTECAO)
        assert {d.categoria for d in protecao.resultado.decisoes} == {
            Categoria.NOME,
            Categoria.CPF,
            Categoria.CNH,
            Categoria.TELEFONE,
            Categoria.EMAIL,
            Categoria.SENHA,
        }
        assert {d.acao for d in protecao.resultado.decisoes} == {Acao.REDIGIR}
        for valor in dados.cadastro_entregador().values():
            assert valor not in protecao.saida_emitida
        assert protecao.saida_emitida.startswith("INFO helmcare.api.cadastro: cadastro {")
        auditoria = rodar("C1", Modo.AUDITORIA)
        assert dados.NOME_ENTREGADOR in auditoria.saida_emitida

    def test_c2_senha_no_print(self):
        resultado = rodar("C2", Modo.PROTECAO)
        assert resultado.saida_emitida == "login recebido: email=[REDIGIDO:EMAIL] senha=[REDIGIDO:SENHA]"
        assert rodar("C2", Modo.AUDITORIA).saida_emitida.endswith(f"senha={dados.SENHA}")

    def test_c3_erro_de_validacao(self):
        saida = rodar("C3", Modo.PROTECAO).saida_emitida
        assert saida["detail"] == "O CPF [BLOQUEADO:CPF] não confere com o telefone [BLOQUEADO:TELEFONE]."
        assert saida["errors"] == {"cpf": ["[BLOQUEADO:CPF]"]}
        assert saida["status"] == 400

    def test_c4_ficha_no_chamado(self):
        resultado = rodar("C4", Modo.PROTECAO)
        decisoes = por_categoria(resultado.resultado)
        assert {d.estado for d in decisoes[Categoria.SAUDE]} == {Estado.NEUTRALIZADO}
        assert {d.estado for d in decisoes[Categoria.LOCALIZACAO_APROXIMADA]} == {Estado.PERMITIDO}
        assert set(resultado.saida_emitida["fichaMedica"].values()) >= {"[BLOQUEADO:SAUDE]"}
        assert dados.ALERGIAS not in str(resultado.saida_emitida)

    def test_c5_mqtt_com_identificacao(self):
        resultado = rodar("C5", Modo.PROTECAO)
        assert {d.regra_id for d in resultado.resultado.decisoes} == {"TA-MQTT-02"}
        assert resultado.saida_emitida["latitude"] == "[BLOQUEADO:LOCALIZACAO_EXATA]"
        assert resultado.saida_emitida["dispositivo"] == "HC-0001"

    def test_c5_controle_mqtt_sem_identificacao(self):
        resultado = GuardaTaint().verificar_saida(dados.evento_mqtt_sos(), ContextoSaida(Destino.MQTT_PAYLOAD))
        assert resultado.decisao_geral is Estado.PERMITIDO
        assert resultado.regras_aplicadas() == ["TA-MQTT-01"]

    def test_c6_push_arredondado(self):
        saida = rodar("C6", Modo.PROTECAO).saida_emitida
        assert (saida["latitude"], saida["longitude"]) == (-23.56, -46.65)
        auditoria = rodar("C6", Modo.AUDITORIA).saida_emitida
        assert auditoria["latitude"] == dados.LATITUDE_EXATA

    @pytest.mark.parametrize("modo", list(Modo))
    def test_c7_canais_autorizados(self, modo):
        resultado = rodar("C7", modo)
        voluntario, contato = resultado.saida_emitida
        assert voluntario["latitude"] == contato["latitude"] == dados.LATITUDE_EXATA
        assert {e.estado for e in resultado.eventos} == {Estado.PERMITIDO}
        assert resultado.regras_aplicadas() == ["TA-ACEITOU-01", "TA-CONTATO-01"]

    def test_c8_cofre_por_caminho(self):
        resultado = rodar("C8", Modo.PROTECAO)
        regras = {d.caminho: (d.regra_id, d.estado) for d in resultado.resultado.decisoes}
        assert regras == {
            "$.tipoSanguineo": ("TA-COFRE-01", Estado.PERMITIDO),
            "$.alergias": ("TA-COFRE-01", Estado.PERMITIDO),
            "$.condicoesCriticas": ("TA-COFRE-01", Estado.PERMITIDO),
            "$.contatos[0].nome": ("TA-COFRE-02", Estado.PERMITIDO),
            "$.contatos[0].telefone": ("TA-COFRE-02", Estado.PERMITIDO),
            "$.medicamentos": ("TA-COFRE-03", Estado.NEUTRALIZADO),
        }
        saida = resultado.saida_emitida
        assert saida["alergias"] == dados.ALERGIAS
        assert saida["contatos"][0]["telefone"] == dados.TELEFONE_CONTATO
        assert saida["medicamentos"] == "[BLOQUEADO:SAUDE]"
        assert rodar("C8", Modo.AUDITORIA).saida_emitida["medicamentos"] == dados.MEDICAMENTOS

    def test_c9_cofre_negado(self):
        resultado = rodar("C9", Modo.PROTECAO)
        assert {d.regra_id for d in resultado.resultado.decisoes} == {"TA-NEGADO-01"}
        assert {d.estado for d in resultado.resultado.decisoes} == {Estado.NEUTRALIZADO}
        assert dados.TELEFONE_CONTATO not in str(resultado.saida_emitida)

    def test_c10_senha_pura_e_hash(self):
        resultado = rodar("C10", Modo.PROTECAO)
        pura, com_hash = resultado.resultados
        assert pura.regras_aplicadas() == ["TA-PERS-01"]
        assert por_categoria(pura)[Categoria.SENHA][0].estado is Estado.NEUTRALIZADO
        assert por_categoria(com_hash)[Categoria.SENHA][0].regra_id == "TA-PERS-02"
        assert com_hash.decisao_geral is Estado.PERMITIDO
        saida_pura, saida_hash = resultado.saida_emitida
        assert saida_pura["senha"] == "[BLOQUEADO:SENHA]"
        assert conferir_senha(dados.SENHA, saida_hash["senha"])
        assert TipoPasso.SANITIZADOR in [p.tipo for p in com_hash.rastro.passos]

    def test_c11_qr_token_na_documentacao(self):
        protecao = rodar("C11", Modo.PROTECAO)
        assert dados.QR_TOKEN not in protecao.saida_emitida
        assert "/cofre/[BLOQUEADO:QR_TOKEN]" in protecao.saida_emitida
        auditoria = rodar("C11", Modo.AUDITORIA)
        assert dados.QR_TOKEN in auditoria.saida_emitida
        assert {e.categoria for e in auditoria.eventos} == {Categoria.QR_TOKEN}
        assert auditoria.decisao_geral is Estado.EXPOSTO


class TestDadosSimulados:
    def test_documentos_validos(self):
        from helmcare_cyber.pii.validadores import cnh_valida, cpf_valido

        assert cpf_valido(dados.CPF) and cnh_valida(dados.CNH)
        assert dados.CPF == "482.917.365-37"

    def test_cnh_com_desconto(self):
        from helmcare_cyber.pii.validadores import cnh_valida

        # Bases com primeiro resto 10 (desconto no segundo DV), inclusive a que fica negativa.
        for base in ("950248613", "478245040"):
            assert cnh_valida(dados.calcular_dv_cnh(base))
        # Segundo resto 10: o DV vira 0.
        assert dados.calcular_dv_cnh("139582746") == "13958274600"

    @pytest.mark.parametrize("funcao", [dados.calcular_dv_cpf, dados.calcular_dv_cnh])
    def test_base_invalida(self, funcao):
        with pytest.raises(ValueError):
            funcao("123")

    def test_payloads_novos_a_cada_chamada(self):
        assert dados.cadastro_entregador() is not dados.cadastro_entregador()
        primeira = dados.resposta_cofre()
        primeira["contatos"].append({})
        assert len(dados.resposta_cofre()["contatos"]) == 1

    def test_cofre_com_e_sem_medicamentos(self):
        assert "medicamentos" not in dados.resposta_cofre()
        assert dados.resposta_cofre(True)["medicamentos"] == dados.MEDICAMENTOS

    def test_telemetria_sem_pii(self):
        from helmcare_cyber.pii import MotorPII

        assert MotorPII().varrer(dados.telemetria()) == []

    def test_qr_token_e_url(self):
        resposta = dados.resposta_dispositivo()
        assert resposta["urlCofre"].endswith(f"/cofre/{dados.QR_TOKEN}")
        assert len(dados.QR_TOKEN) >= 16 and "Simulado" in dados.QR_TOKEN
