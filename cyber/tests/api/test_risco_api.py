import pytest

from tests.api.conftest import API, C1_A_C10, C1_A_C11, novo_cliente, tradutor_com_ia, tradutor_falhando


def risco_depois(modo: str, ids: list[str], tradutor=None) -> dict:
    cliente = novo_cliente(modo, tradutor)
    for identificador in ids:
        assert cliente.post(f"{API}/simulacoes/{identificador}").status_code == 200
    return cliente.get(f"{API}/risco-cidadao").json()


@pytest.mark.parametrize(
    "modo, ids, indice, cor",
    [
        ("PROTECAO", [], 0, "VERDE"),
        ("PROTECAO", ["C1"], 15, "VERDE"),
        ("PROTECAO", C1_A_C10, 38, "AMARELO"),
        ("AUDITORIA", ["C11"], 40, "AMARELO"),
        ("AUDITORIA", C1_A_C11, 97, "VERMELHO"),
    ],
    ids=["sem-eventos", "c1-protecao", "c1-a-c10-protecao", "c11-auditoria", "c1-a-c11-auditoria"],
)
def test_calibracao(modo, ids, indice, cor):
    risco = risco_depois(modo, ids)
    assert (risco["indice"], risco["cor"]) == (indice, cor)
    assert risco["rotulo"] and risco["icone"] in {"OK", "ATENCAO", "PERIGO"}


def test_sem_eventos_mensagem_de_tudo_certo():
    mensagem = risco_depois("PROTECAO", [])["mensagem"]
    assert mensagem == {
        "texto": "Tudo certo: seus dados estão protegidos e nada importante aconteceu.",
        "origem": "RESERVA",
        "selo": "modelo de reserva",
        "motivo": None,
    }


def test_reserva_sem_chave():
    risco = risco_depois("PROTECAO", ["C1"])
    assert risco["mensagem"]["origem"] == "RESERVA"
    assert risco["mensagem"]["selo"] == "modelo de reserva"
    assert risco["mensagem"]["motivo"] == "SEM_CHAVE"
    assert risco["mensagemCidadao"] == risco["mensagem"]["texto"]
    assert risco["origemMensagem"] == "RESERVA"


def test_com_ia():
    risco = risco_depois("PROTECAO", ["C1"], tradutor_com_ia("Sua senha ficou protegida pelo sistema."))
    assert risco["mensagem"] == {
        "texto": "Sua senha ficou protegida pelo sistema.",
        "origem": "IA",
        "selo": "",
        "motivo": None,
    }


def test_ia_falhando_cai_na_reserva_com_diagnostico():
    risco = risco_depois("AUDITORIA", ["C11"], tradutor_falhando(503, "The model is overloaded."))
    mensagem = risco["mensagem"]
    assert mensagem["origem"] == "RESERVA" and mensagem["selo"] == "modelo de reserva"
    assert mensagem["motivo"] == "ERRO_HTTP (503: The model is overloaded.)"
    assert mensagem["texto"].startswith("O código do QR do seu capacete apareceu")


def test_sem_traducao_responde_sem_mensagem():
    cliente = novo_cliente("PROTECAO", tradutor_falhando())
    cliente.post(f"{API}/simulacoes/C1")
    risco = cliente.get(f"{API}/risco-cidadao?traduzir=false").json()
    assert risco["mensagem"] is None and risco["indice"] == 15
    assert cliente.app.state.cyber.tradutor.disjuntor_aberto is False  # nem chegou a chamar o LLM


def test_por_que_e_contadores():
    risco = risco_depois("PROTECAO", C1_A_C10)
    assert len(risco["porQue"]) == 3
    assert risco["contadores"] == {"eventos": 30, "neutralizados": 21, "expostos": 0, "permitidos": 9}
    assert risco["piorEventoPontos"] == 24.3 and risco["volumePontos"] == 14
