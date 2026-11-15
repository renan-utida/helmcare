"""Ambiente do Alembic: de onde vem a URL do banco e quais tabelas ele compara.

A URL nunca fica no alembic.ini. Ela vem, nesta ordem:
1. de config.attributes["url"], quando os testes chamam o Alembic pelo código;
2. de RISCO_TESTES_DATABASE_URL, com "alembic -x banco=teste ...";
3. de RISCO_DATABASE_URL, no uso normal.
A URL não é gravada na configuração do Alembic nem impressa.
"""

from logging.config import fileConfig

from alembic import context
from sqlalchemy import create_engine
from sqlalchemy.pool import NullPool

from app.config import obter_configuracoes

# A Base vem pelo módulo modelos de propósito: importar modelos registra as quatro
# tabelas em Base.metadata, e sem isso o "alembic check" não veria nenhuma tabela.
from app.infraestrutura.persistencia.modelos import Base

config = context.config

# Os testes passam configure_logger=False para o Alembic não trocar a configuração dos logs.
if config.config_file_name is not None and config.attributes.get("configure_logger", True):
    fileConfig(config.config_file_name)

# Metadados dos modelos: usados pelo "alembic check" para comparar modelos e migrações.
target_metadata = Base.metadata


def obter_url() -> str:
    """Escolhe a URL do banco sem nunca registrá-la no log."""
    url_dos_testes = config.attributes.get("url")
    if url_dos_testes:
        return url_dos_testes

    configuracoes = obter_configuracoes()
    banco = context.get_x_argument(as_dictionary=True).get("banco")
    if banco == "teste":
        segredo = configuracoes.testes_database_url
        nome_variavel = "RISCO_TESTES_DATABASE_URL"
    else:
        segredo = configuracoes.database_url
        nome_variavel = "RISCO_DATABASE_URL"
    if segredo is None:
        raise RuntimeError(f"Configure {nome_variavel} no servico-risco/.env.")
    return segredo.get_secret_value()


def executar_offline() -> None:
    """Gera o SQL das migrações sem conectar ao banco (alembic upgrade head --sql)."""
    context.configure(url=obter_url(), target_metadata=target_metadata, literal_binds=True)
    with context.begin_transaction():
        context.run_migrations()


def executar_online() -> None:
    """Conecta ao banco e aplica as migrações numa transação."""
    # NullPool: o Alembic roda uma vez e sai, então não vale a pena manter conexões abertas.
    engine = create_engine(obter_url(), poolclass=NullPool, hide_parameters=True)
    with engine.connect() as conexao:
        context.configure(connection=conexao, target_metadata=target_metadata, compare_type=True)
        with context.begin_transaction():
            context.run_migrations()
    engine.dispose()


if context.is_offline_mode():
    executar_offline()
else:
    executar_online()
