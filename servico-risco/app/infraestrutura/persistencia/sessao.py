"""Engine e fábrica de sessões do SQLAlchemy, criadas a partir da configuração.

A URL do banco chega como SecretStr e só é aberta aqui, na chamada ao create_engine.
Ela nunca é registrada no log: os erros são registrados só pelo tipo.
"""

import logging

from pydantic import SecretStr
from sqlalchemy import Engine, create_engine, text
from sqlalchemy.exc import SQLAlchemyError
from sqlalchemy.orm import Session, sessionmaker

logger = logging.getLogger(__name__)

# Tempo máximo, em segundos, para abrir a conexão: com o banco fora do ar, a requisição
# falha em 3 s em vez de ficar presa.
TEMPO_LIMITE_CONEXAO_S = 3


def criar_engine(url: SecretStr) -> Engine:
    """Cria a engine síncrona com psycopg 3.

    pool_pre_ping testa a conexão antes de usá-la (o banco pode ter reiniciado), e
    hide_parameters tira os valores dos parâmetros das mensagens de erro, para o log
    nunca levar dados da requisição.
    """
    return create_engine(
        url.get_secret_value(),
        pool_pre_ping=True,
        hide_parameters=True,
        connect_args={"connect_timeout": TEMPO_LIMITE_CONEXAO_S},
    )


def criar_fabrica_sessoes(engine: Engine) -> sessionmaker[Session]:
    """Cria a fábrica de sessões; cada requisição abre e fecha a sua própria sessão.

    expire_on_commit=False mantém os objetos legíveis depois do commit, sem nova consulta.
    """
    return sessionmaker(bind=engine, expire_on_commit=False)


def verificar_conexao(engine: Engine) -> bool:
    """Executa SELECT 1 e devolve True se o banco respondeu (usado pela /saude)."""
    try:
        with engine.connect() as conexao:
            conexao.execute(text("SELECT 1"))
        return True
    except SQLAlchemyError as erro:
        logger.warning("Banco indisponível na verificação de saúde (%s).", type(erro).__name__)
        return False
