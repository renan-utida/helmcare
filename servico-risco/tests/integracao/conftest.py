"""Fixtures dos testes de integração com o PostgreSQL de testes.

Só rodam com RISCO_TESTES_DATABASE_URL definida (no ambiente ou no servico-risco/.env,
lido pelo próprio app). Sem ela, os testes são ignorados com o motivo. Por segurança,
o banco precisa se chamar *_teste e ser diferente do RISCO_DATABASE_URL, porque estes
testes rodam "alembic downgrade base" e apagam as tabelas.
"""

from collections.abc import Iterator

import pytest
from alembic import command
from alembic.config import Config as ConfigAlembic
from pydantic import SecretStr
from sqlalchemy import Engine, make_url, text
from sqlalchemy.orm import Session

from app.config import DIRETORIO_SERVICO, Configuracoes
from app.infraestrutura.persistencia.sessao import criar_engine, criar_fabrica_sessoes

SUFIXO_BANCO_TESTE = "_teste"


@pytest.fixture(scope="session")
def url_testes() -> str:
    """URL do banco de testes, ou pula os testes de integração com o motivo."""
    configuracoes = Configuracoes()
    if configuracoes.testes_database_url is None:
        pytest.skip("RISCO_TESTES_DATABASE_URL não definida: testes de integração ignorados.")
    url = configuracoes.testes_database_url.get_secret_value()
    nome_banco = make_url(url).database or ""
    if not nome_banco.endswith(SUFIXO_BANCO_TESTE):
        pytest.skip(f"Por segurança, o banco de testes precisa terminar em {SUFIXO_BANCO_TESTE}.")
    if (
        configuracoes.database_url is not None
        and configuracoes.database_url.get_secret_value() == url
    ):
        pytest.skip("RISCO_TESTES_DATABASE_URL não pode ser igual a RISCO_DATABASE_URL.")
    return url


def configuracao_alembic(url: str) -> ConfigAlembic:
    """Configuração do Alembic apontando para o banco de testes, sem gravar a URL no .ini."""
    configuracao = ConfigAlembic(str(DIRETORIO_SERVICO / "alembic.ini"))
    configuracao.attributes["url"] = url
    configuracao.attributes["configure_logger"] = False
    return configuracao


@pytest.fixture(scope="session")
def alembic_teste(url_testes: str) -> ConfigAlembic:
    """Configuração do Alembic do banco de testes."""
    return configuracao_alembic(url_testes)


@pytest.fixture(scope="session")
def engine_teste(url_testes: str, alembic_teste: ConfigAlembic) -> Iterator[Engine]:
    """Engine do banco de testes, com o esquema recriado do zero pelas migrações.

    Começa com downgrade base (banco vazio) e upgrade head, e termina com downgrade base,
    deixando o banco de testes vazio de novo.
    """
    command.downgrade(alembic_teste, "base")
    command.upgrade(alembic_teste, "head")
    engine = criar_engine(SecretStr(url_testes))
    yield engine
    engine.dispose()
    command.downgrade(alembic_teste, "base")


def limpar_tabelas(engine: Engine) -> None:
    """Apaga os dados das quatro tabelas, para cada teste começar do zero."""
    with engine.begin() as conexao:
        conexao.execute(
            text(
                "TRUNCATE calculo_indice_hora, calculo_indice, trecho_critico, "
                "conjunto_trechos_criticos"
            )
        )


@pytest.fixture
def sessao(engine_teste: Engine) -> Iterator[Session]:
    """Sessão num banco de testes com as tabelas vazias."""
    limpar_tabelas(engine_teste)
    with criar_fabrica_sessoes(engine_teste)() as sessao:
        yield sessao
