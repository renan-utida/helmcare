"""carregar_dotenv só com arquivos temporários e valores falsos (o cyber/.env real nunca é aberto)."""

import os
from pathlib import Path

import pytest

import helmcare_cyber.config as config
from helmcare_cyber.config import CAMINHO_DOTENV_PADRAO, Config, carregar_dotenv

VARIAVEIS = ("CYBER_TESTE_A", "CYBER_TESTE_B", "GEMINI_MODEL")


@pytest.fixture
def ambiente_limpo(monkeypatch):
    """Remove as variáveis e garante que voltam ao estado original no fim do teste.

    O setenv antes do delenv faz o monkeypatch anotar o estado original, então o
    que o load_dotenv gravar em os.environ também é desfeito.
    """
    for nome in VARIAVEIS:
        monkeypatch.setenv(nome, "provisorio")
        monkeypatch.delenv(nome)
    return monkeypatch


def escrever(tmp_path: Path, conteudo: str) -> Path:
    arquivo = tmp_path / ".env"
    arquivo.write_text(conteudo, encoding="utf-8")
    return arquivo


def test_carrega_arquivo(tmp_path, ambiente_limpo):
    arquivo = escrever(tmp_path, "CYBER_TESTE_A=valor-falso-a\nGEMINI_MODEL=modelo-falso\n")
    assert carregar_dotenv(arquivo) is True
    assert os.environ["CYBER_TESTE_A"] == "valor-falso-a"
    assert os.environ["GEMINI_MODEL"] == "modelo-falso"


def test_aceita_texto(tmp_path, ambiente_limpo):
    assert carregar_dotenv(str(escrever(tmp_path, "CYBER_TESTE_B=b\n"))) is True


def test_ambiente_vale_mais_que_o_arquivo(tmp_path, ambiente_limpo):
    ambiente_limpo.setenv("CYBER_TESTE_A", "do-ambiente")
    carregar_dotenv(escrever(tmp_path, "CYBER_TESTE_A=do-arquivo\nCYBER_TESTE_B=b\n"))
    assert os.environ["CYBER_TESTE_A"] == "do-ambiente"
    assert os.environ["CYBER_TESTE_B"] == "b"


def test_arquivo_inexistente(tmp_path, ambiente_limpo):
    assert carregar_dotenv(tmp_path / "nao-existe.env") is False


def test_arquivo_vazio(tmp_path, ambiente_limpo):
    assert carregar_dotenv(escrever(tmp_path, "# só comentário\n")) is False


def test_nao_imprime_nada(tmp_path, ambiente_limpo, capsys):
    carregar_dotenv(escrever(tmp_path, "CYBER_TESTE_A=valor-falso-a\n"))
    saida = capsys.readouterr()
    assert saida.out == "" and saida.err == ""


def test_caminho_padrao_e_cyber_env(tmp_path, ambiente_limpo, monkeypatch):
    # Confere o caminho sem abrir o arquivo real: o padrão é trocado por um temporário.
    assert CAMINHO_DOTENV_PADRAO == Path(config.__file__).resolve().parents[1] / ".env"
    assert CAMINHO_DOTENV_PADRAO.parent.name == "cyber"
    monkeypatch.setattr(config, "CAMINHO_DOTENV_PADRAO", escrever(tmp_path, "CYBER_TESTE_B=padrao\n"))
    assert config.carregar_dotenv() is True
    assert os.environ["CYBER_TESTE_B"] == "padrao"


def test_a_chave_nao_esta_na_config():
    assert "chave" not in " ".join(Config.__dataclass_fields__).lower()
    assert "gemini" not in " ".join(Config.__dataclass_fields__).lower()
