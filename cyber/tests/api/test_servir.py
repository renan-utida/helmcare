"""scripts/servir.py sem subir servidor de verdade: o uvicorn.run é trocado por um registro."""

import importlib.util
from pathlib import Path

import pytest
import uvicorn

from tests.ia.conftest import CHAVE_FALSA, MODELO_FALSO

SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "servir.py"


def carregar():
    especificacao = importlib.util.spec_from_file_location("script_servir", SCRIPT)
    modulo = importlib.util.module_from_spec(especificacao)
    especificacao.loader.exec_module(modulo)
    return modulo


@pytest.fixture
def chamadas(monkeypatch):
    registro = []
    monkeypatch.setattr(uvicorn, "run", lambda app, **opcoes: registro.append((app, opcoes)))
    return registro


def test_padrao_local_sem_chave(chamadas, monkeypatch, capsys):
    monkeypatch.delenv("GEMINI_API_KEY", raising=False)
    monkeypatch.delenv("GEMINI_MODEL", raising=False)
    assert carregar().main(["--sem-dotenv"]) == 0
    _, opcoes = chamadas[0]
    assert (opcoes["host"], opcoes["port"]) == ("127.0.0.1", 8100)
    saida = capsys.readouterr().out
    assert "chave configurada: não" in saida and "/painel/" in saida and "/docs" in saida
    assert "atenção" not in saida


def test_com_chave_nunca_imprime_a_chave(chamadas, monkeypatch, capsys):
    monkeypatch.setenv("GEMINI_API_KEY", CHAVE_FALSA)
    monkeypatch.setenv("GEMINI_MODEL", MODELO_FALSO)
    carregar().main(["--sem-dotenv", "--porta", "8123", "--host", "0.0.0.0"])
    saida = capsys.readouterr().out
    assert CHAVE_FALSA not in saida
    assert "chave configurada: sim" in saida and f"modelo: {MODELO_FALSO}" in saida
    assert "atenção" in saida and chamadas[0][1]["port"] == 8123


@pytest.mark.parametrize("host, local", [("127.0.0.1", True), ("localhost", True), ("::1", True), ("0.0.0.0", False), ("meu-pc", False)])
def test_eh_local(host, local):
    assert carregar().eh_local(host) is local
