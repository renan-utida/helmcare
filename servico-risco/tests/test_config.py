"""Testes da configuração.

Todos usam _env_file=None para não ler o servico-risco/.env real: o teste não
depende de segredos e nunca os exibe numa falha.
"""

from pathlib import Path

import pytest

from app.config import DIRETORIO_SERVICO, MODELO_DIR_PADRAO, Configuracoes


def test_configuracoes_sem_variaveis_usa_padroes(monkeypatch: pytest.MonkeyPatch) -> None:
    # Arrange
    for nome in (
        "RISCO_DATABASE_URL",
        "RISCO_MODELO_DIR",
        "RISCO_CARREGAR_TRECHOS_NA_INICIALIZACAO",
    ):
        monkeypatch.delenv(nome, raising=False)

    # Act
    configuracoes = Configuracoes(_env_file=None)

    # Assert
    assert configuracoes.database_url is None
    assert configuracoes.modelo_dir == MODELO_DIR_PADRAO
    assert configuracoes.carregar_trechos_na_inicializacao is True


def test_configuracoes_modelo_dir_padrao_resolvido_pelo_arquivo_e_existe() -> None:
    # Assert: aponta para ml/modelo do repositório, independente do diretório atual.
    assert MODELO_DIR_PADRAO == DIRETORIO_SERVICO.parent / "ml" / "modelo"
    assert (MODELO_DIR_PADRAO / "metadados_modelo.json").is_file()


def test_configuracoes_variaveis_risco_sobrescrevem_padroes(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    # Arrange: URL fictícia, sem senha real.
    monkeypatch.setenv("RISCO_DATABASE_URL", "postgresql+psycopg://usuario@localhost:5433/banco")
    monkeypatch.setenv("RISCO_MODELO_DIR", str(tmp_path))
    monkeypatch.setenv("RISCO_CARREGAR_TRECHOS_NA_INICIALIZACAO", "false")

    # Act
    configuracoes = Configuracoes(_env_file=None)

    # Assert
    assert configuracoes.database_url is not None
    assert configuracoes.database_url.get_secret_value().endswith("/banco")
    assert configuracoes.modelo_dir == tmp_path
    assert configuracoes.carregar_trechos_na_inicializacao is False


def test_configuracoes_url_do_banco_nao_aparece_ao_imprimir(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    # Arrange
    monkeypatch.setenv("RISCO_DATABASE_URL", "postgresql+psycopg://u:segredo-ficticio@h/b")

    # Act
    texto = repr(Configuracoes(_env_file=None))

    # Assert
    assert "segredo-ficticio" not in texto


def test_configuracoes_chave_sem_prefixo_e_ignorada(monkeypatch: pytest.MonkeyPatch) -> None:
    # Arrange: como a POSTGRES_PASSWORD que o compose lê do mesmo .env.
    monkeypatch.setenv("POSTGRES_PASSWORD", "valor-ficticio")

    # Act
    configuracoes = Configuracoes(_env_file=None)

    # Assert
    assert not hasattr(configuracoes, "postgres_password")
