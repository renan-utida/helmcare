import pytest

from helmcare_cyber.api.interceptador import _resumo_saida, mascarar_resposta
from helmcare_cyber.pii import MotorPII
from helmcare_cyber.simulacao import dados_simulados as dados
from helmcare_cyber.taint.guarda import MARCADOR_ERRO, ResultadoGuarda
from helmcare_cyber.taint.modelo import Destino, Estado
from helmcare_cyber.taint.rastro import Rastro
from helmcare_cyber.taint.sanitizadores import conferir_senha, eh_hash_senha
from tests.api.conftest import API, cadastro, capturar, estado_de, novo_cliente

CPF_DIGITOS = dados.calcular_dv_cpf("482917365")
VALORES_DO_CADASTRO = (
    dados.CPF,
    CPF_DIGITOS,
    dados.CNH,
    dados.TELEFONE,
    "11987654321",
    dados.EMAIL,
    dados.SENHA,
    dados.NOME_ENTREGADOR,
)


def sem_valores(texto: str) -> list[str]:
    return [valor for valor in VALORES_DO_CADASTRO if valor in texto]


class TestProtecao:
    def test_log_e_resposta_sem_valores(self, cliente, captura):
        resposta = cliente.post(f"{API}/demo/entregadores", json=cadastro())
        assert resposta.status_code == 201
        assert sem_valores(captura.texto) == []
        assert sem_valores(resposta.text) == []
        assert captura.texto.startswith("cadastro recebido {")
        assert "[REDIGIDO:NOME]" in captura.texto and "[REDIGIDO:SENHA]" in captura.texto

    def test_resposta_mostra_o_que_o_interceptador_achou(self, cliente):
        corpo = cliente.post(f"{API}/demo/entregadores", json=cadastro()).json()
        interceptador = corpo["interceptador"]
        assert interceptador["entregueAoHandler"] == "COPIA_MASCARADA"
        assert interceptador["quantidade"] == 6
        assert interceptador["categorias"] == ["CPF", "CNH", "TELEFONE", "EMAIL", "NOME", "SENHA"]
        valores = {c["caminho"]: c["valorMascarado"] for c in interceptador["campos"]}
        assert valores["$.cpf"] == "***.***.***-37" and valores["$.telefone"] == "(11) *****-4321"
        assert corpo["entregador"]["senha"] == "[BLOQUEADO:SENHA]"
        assert corpo["entregador"]["cpf"] == "***.***.***-37"
        assert [s["destino"] for s in corpo["saidas"]] == ["LOG", "PERSISTENCIA", "RESPOSTA_PROPRIO_ENTREGADOR"]
        assert corpo["saidas"][0]["decisaoGeral"] == "NEUTRALIZADO"
        assert "SANITIZADOR  interceptador de entrada" in corpo["rastro"]
        assert corpo["eventos"] > 0

    def test_banco_guarda_so_o_hash_e_as_mascaras(self, cliente):
        corpo = cliente.post(f"{API}/demo/entregadores", json=cadastro()).json()
        gravado = estado_de(cliente).banco.obter(corpo["id"])
        assert eh_hash_senha(gravado["senha"]) and conferir_senha(dados.SENHA, gravado["senha"])
        assert sem_valores(str(gravado)) == []

    def test_registro_de_taint_recebe_a_fonte(self, cliente):
        cliente.post(f"{API}/demo/entregadores", json=cadastro())
        assert estado_de(cliente).guarda.registro.procurar(f"oi {dados.NOME_ENTREGADOR}")

    def test_logger_nao_propaga(self, cliente):
        assert estado_de(cliente).logger_demo.propagate is False


class TestAuditoria:
    def test_log_sai_como_esta_e_gera_exposto(self, cliente_auditoria):
        captura = capturar(cliente_auditoria)
        resposta = cliente_auditoria.post(f"{API}/demo/entregadores", json=cadastro())
        assert dados.CPF in captura.texto and dados.SENHA in captura.texto and dados.NOME_ENTREGADOR in captura.texto
        eventos = estado_de(cliente_auditoria).guarda.repositorio.listar()
        expostos_no_log = [e for e in eventos if e.destino is Destino.LOG and e.estado is Estado.EXPOSTO]
        assert expostos_no_log and all(e.cenario == "DEMO" for e in expostos_no_log)
        assert resposta.json()["interceptador"]["entregueAoHandler"] == "ORIGINAL"
        assert resposta.json()["saidas"][0]["decisaoGeral"] == "EXPOSTO"

    def test_resposta_continua_sem_valores(self, cliente_auditoria):
        resposta = cliente_auditoria.post(f"{API}/demo/entregadores", json=cadastro())
        assert sem_valores(resposta.text) == []
        assert resposta.json()["entregador"]["cpf"] == "***.***.***-37"

    def test_senha_vai_para_o_banco_como_hash(self, cliente_auditoria):
        corpo = cliente_auditoria.post(f"{API}/demo/entregadores", json=cadastro()).json()
        gravado = estado_de(cliente_auditoria).banco.obter(corpo["id"])
        assert eh_hash_senha(gravado["senha"]) and dados.SENHA not in str(gravado)
        assert "SANITIZADOR  hash_senha" in corpo["rastro"]


class TestValidacao:
    @pytest.mark.parametrize(
        "campo, valor",
        [
            ("cpf", "123"),
            ("cnh", "abc"),
            ("telefone", "1234"),
            ("email", "sem-arroba"),
            ("senha", "curta"),
            ("senha", "semmaiuscula123"),
            ("nome", ""),
        ],
    )
    def test_campo_invalido_sem_eco(self, cliente, campo, valor):
        resposta = cliente.post(f"{API}/demo/entregadores", json=cadastro() | {campo: valor})
        assert resposta.status_code == 422
        assert resposta.json()["campos"] == [f"body.{campo}"]
        assert sem_valores(resposta.text) == []

    def test_campo_a_mais(self, cliente):
        resposta = cliente.post(f"{API}/demo/entregadores", json=cadastro() | {"observacao": dados.CPF})
        assert resposta.status_code == 422 and dados.CPF not in resposta.text

    def test_nada_e_gravado_com_erro(self, cliente):
        cliente.post(f"{API}/demo/entregadores", json={"cpf": dados.CPF})
        assert len(estado_de(cliente).banco) == 0


class TestCasosDeBorda:
    def test_banco_bloqueado_e_log_sem_filtro(self):
        cliente = novo_cliente()
        estado = estado_de(cliente)
        # Sem o filtro, o log não gera resultado; com o guarda falhando na persistência, nada vai ao banco.
        estado.logger_demo.removeFilter(estado.filtro_log)
        original = estado.guarda.verificar_saida

        def verificar(payload, contexto, *args, **kwargs):
            if contexto.destino is Destino.PERSISTENCIA:
                return ResultadoGuarda(Estado.NEUTRALIZADO, MARCADOR_ERRO, [], [], Rastro())
            return original(payload, contexto, *args, **kwargs)

        estado.guarda.verificar_saida = verificar
        try:
            corpo = cliente.post(f"{API}/demo/entregadores", json=cadastro()).json()
        finally:
            estado.logger_demo.addFilter(estado.filtro_log)
        assert estado.banco.obter(corpo["id"]) == {"bloqueado": True}
        assert corpo["saidas"][0] == {"destino": "LOG", "decisaoGeral": None, "regras": [], "estados": {}}

    def test_resumo_sem_resultado(self):
        assert _resumo_saida(Destino.LOG, None)["decisaoGeral"] is None

    def test_mascarar_resposta_mantem_os_marcadores(self):
        payload = {"senha": "[BLOQUEADO:SENHA]", "nome": dados.NOME_ENTREGADOR, "cpf": dados.CPF}
        assert mascarar_resposta(MotorPII(), payload) == {
            "senha": "[BLOQUEADO:SENHA]",
            "nome": "[REDIGIDO:NOME]",
            "cpf": "***.***.***-37",
        }
