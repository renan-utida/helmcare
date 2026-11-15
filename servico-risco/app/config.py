"""Configuração do Serviço de Risco, lida das variáveis de ambiente RISCO_*.

O pydantic-settings lê primeiro as variáveis de ambiente e depois o arquivo
servico-risco/.env (que nunca é versionado). Este módulo não abre conexão com o
banco: ele só guarda os valores para as outras camadas usarem.
"""

from functools import lru_cache
from pathlib import Path

from pydantic import SecretStr
from pydantic_settings import BaseSettings, SettingsConfigDict

# Os caminhos saem da posição deste arquivo, nunca do diretório atual, para o serviço
# funcionar igual quando é iniciado da raiz do repositório ou de servico-risco/.
DIRETORIO_SERVICO = Path(__file__).resolve().parent.parent
DIRETORIO_REPOSITORIO = DIRETORIO_SERVICO.parent
ARQUIVO_ENV = DIRETORIO_SERVICO / ".env"
MODELO_DIR_PADRAO = DIRETORIO_REPOSITORIO / "ml" / "modelo"


class Configuracoes(BaseSettings):
    """Valores de configuração do serviço.

    Cada atributo corresponde a uma variável com o prefixo RISCO_ (por exemplo,
    database_url vem de RISCO_DATABASE_URL). As URLs do banco são SecretStr para
    a senha não aparecer se o objeto for impresso ou registrado no log.
    """

    model_config = SettingsConfigDict(
        env_prefix="RISCO_",
        env_file=ARQUIVO_ENV,
        env_file_encoding="utf-8",
        # Obrigatório: o .env também guarda a POSTGRES_PASSWORD do compose, sem o
        # prefixo RISCO_, e sem "ignore" o pydantic-settings recusaria essa chave.
        extra="ignore",
    )

    database_url: SecretStr | None = None
    testes_database_url: SecretStr | None = None
    modelo_dir: Path = MODELO_DIR_PADRAO
    carregar_trechos_na_inicializacao: bool = True


@lru_cache
def obter_configuracoes() -> Configuracoes:
    """Devolve a configuração do serviço, lida uma única vez e reaproveitada."""
    return Configuracoes()
