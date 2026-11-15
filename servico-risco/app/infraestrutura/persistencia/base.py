"""Base declarativa do SQLAlchemy com a convenção de nomes das constraints.

Com a naming_convention, toda chave primária, única, estrangeira e CHECK ganha um nome
previsível (por exemplo, uq_trecho_critico_conjunto_id_posicao). As migrações do
Alembic usam esses mesmos nomes, escritos à mão, e o teste de integração confere que
modelos e migrações batem.
"""

from sqlalchemy import MetaData
from sqlalchemy.orm import DeclarativeBase

NAMING_CONVENTION = {
    "ix": "ix_%(column_0_label)s",
    "uq": "uq_%(table_name)s_%(column_0_N_name)s",
    "ck": "ck_%(table_name)s_%(constraint_name)s",
    "fk": "fk_%(table_name)s_%(column_0_name)s_%(referred_table_name)s",
    "pk": "pk_%(table_name)s",
}


class Base(DeclarativeBase):
    """Classe base de todos os modelos de tabela do serviço."""

    metadata = MetaData(naming_convention=NAMING_CONVENTION)
