"""O texto impresso pelos scripts mostra o código HTTP da falha, sem a chave e sem rede."""

import importlib.util
from pathlib import Path

import pytest

from helmcare_cyber.ia.tradutor import TradutorAlertas
from tests.ia.conftest import CHAVE_FALSA, TransporteFalso, cliente_com, erro_google

PASTA_SCRIPTS = Path(__file__).resolve().parents[2] / "scripts"


def carregar_script(nome: str):
    """Importa scripts/<nome>.py como módulo (a pasta scripts não é um pacote)."""
    especificacao = importlib.util.spec_from_file_location(f"script_{nome}", PASTA_SCRIPTS / f"{nome}.py")
    modulo = importlib.util.module_from_spec(especificacao)
    especificacao.loader.exec_module(modulo)
    return modulo


@pytest.fixture
def tradutor_com_503() -> tuple[TradutorAlertas, TransporteFalso]:
    mensagem = f"The model is overloaded. Key {CHAVE_FALSA} ignored."
    falso = TransporteFalso(resposta=erro_google(mensagem), status=503)
    return TradutorAlertas(cliente_com(falso)), falso


def test_testar_ia_mostra_o_codigo_http(tradutor_com_503, capsys, monkeypatch):
    monkeypatch.delenv("GEMINI_API_KEY", raising=False)
    tradutor, falso = tradutor_com_503
    codigo = carregar_script("testar_ia").main(["--sem-dotenv"], tradutor=tradutor)
    saida = capsys.readouterr().out
    assert codigo == 0
    assert "motivo da reserva: ERRO_HTTP (503: The model is overloaded. Key *** ignored.)" in saida
    assert "motivo da reserva: DISJUNTOR" in saida
    assert CHAVE_FALSA not in saida
    assert len(falso.requisicoes) == 2  # uma consulta com a nova tentativa; o disjuntor segurou o resto


def test_demo_risco_mostra_o_codigo_http(tradutor_com_503, capsys):
    tradutor, _ = tradutor_com_503
    codigo = carregar_script("demo_risco").main([], tradutor=tradutor)
    saida = capsys.readouterr().out
    assert codigo == 0
    assert "motivo da reserva: ERRO_HTTP (503: The model is overloaded. Key *** ignored.)" in saida
    assert CHAVE_FALSA not in saida


def test_demo_risco_sem_ia_continua_sem_rede(capsys):
    assert carregar_script("demo_risco").main([]) == 0
    assert "motivo da reserva: SEM_CHAVE" in capsys.readouterr().out
