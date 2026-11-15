"""Testes do startup (inicializar_recursos) e das dependências reais, sem banco de verdade.

Nenhum teste aqui lê o servico-risco/.env: as configurações são montadas com
_env_file=None e URLs fictícias.
"""

import logging
from pathlib import Path

import pytest
from pydantic import SecretStr
from sqlalchemy import create_engine
from sqlalchemy.orm import Session

from app.api.dependencias import (
    RecursosServico,
    obter_servico_indice_turno,
    obter_servico_saude,
    obter_servico_trechos_criticos,
    obter_sessao,
)
from app.config import Configuracoes
from app.infraestrutura.persistencia.repositorios import (
    RepositorioCalculosSql,
    RepositorioTrechosCriticosSql,
)
from app.main import criar_app, gerar_openapi, inicializar_recursos

SENHA_FICTICIA = "senha-ficticia-nao-logar"


def configuracoes(**valores: object) -> Configuracoes:
    """Configuração de teste, sem ler o .env real."""
    return Configuracoes(_env_file=None, **valores)


def test_inicializar_modelo_ausente_e_sem_banco_sobe_degradado(
    tmp_path: Path, caplog: pytest.LogCaptureFixture
) -> None:
    # Arrange: pasta de modelo vazia e nenhuma URL de banco.
    config = configuracoes(modelo_dir=tmp_path, database_url=None)

    # Act
    with caplog.at_level(logging.INFO):
        recursos = inicializar_recursos(config)

    # Assert: nada lançou exceção; o que falhou ficou None e foi registrado.
    assert recursos.previsor is None and recursos.engine is None
    assert "Modelo de ML não carregado" in caplog.text
    assert "RISCO_DATABASE_URL não configurada" in caplog.text


def test_inicializar_modelo_real_carrega_previsor_e_calendario(
    diretorio_modelo: Path,
) -> None:
    # Act
    recursos = inicializar_recursos(configuracoes(modelo_dir=diretorio_modelo, database_url=None))

    # Assert
    assert recursos.previsor is not None
    assert recursos.calendario is not None


def test_inicializar_url_invalida_registra_tipo_sem_vazar_url(
    tmp_path: Path, caplog: pytest.LogCaptureFixture
) -> None:
    # Arrange: texto que o SQLAlchemy não consegue ler como URL.
    config = configuracoes(
        modelo_dir=tmp_path, database_url=SecretStr(f"isso-nao-e-url-{SENHA_FICTICIA}")
    )

    # Act
    with caplog.at_level(logging.INFO):
        recursos = inicializar_recursos(config)

    # Assert
    assert recursos.engine is None
    assert "URL do banco inválida (ArgumentError)" in caplog.text
    assert SENHA_FICTICIA not in caplog.text


def test_inicializar_banco_fora_do_ar_sobe_e_carga_falha_sem_vazar_senha(
    diretorio_modelo: Path, caplog: pytest.LogCaptureFixture
) -> None:
    # Arrange: porta 1 em localhost, onde nenhum PostgreSQL atende.
    url = f"postgresql+psycopg://helmcare:{SENHA_FICTICIA}@127.0.0.1:1/inexistente"
    config = configuracoes(modelo_dir=diretorio_modelo, database_url=SecretStr(url))

    # Act
    with caplog.at_level(logging.INFO):
        recursos = inicializar_recursos(config)

    # Assert: o serviço sobe com engine (o banco pode voltar depois) e registra a falha.
    assert recursos.engine is not None and recursos.previsor is not None
    assert "Carga dos trechos críticos falhou (OperationalError)" in caplog.text
    assert SENHA_FICTICIA not in caplog.text
    assert obter_servico_saude(recursos).saudavel() is False
    recursos.fechar()


def test_inicializar_sem_carga_de_trechos_nao_conecta(
    tmp_path: Path, caplog: pytest.LogCaptureFixture
) -> None:
    # Arrange
    url = SecretStr("postgresql+psycopg://helmcare:x@127.0.0.1:1/inexistente")
    config = configuracoes(
        modelo_dir=tmp_path, database_url=url, carregar_trechos_na_inicializacao=False
    )

    # Act
    with caplog.at_level(logging.INFO):
        recursos = inicializar_recursos(config)

    # Assert
    assert recursos.fabrica_sessoes is not None
    assert "Carga dos trechos" not in caplog.text
    recursos.fechar()


def test_obter_sessao_sem_banco_devolve_none_e_servicos_sem_repositorio() -> None:
    # Arrange
    recursos = RecursosServico()

    # Act
    sessoes = list(obter_sessao(recursos))

    # Assert
    assert sessoes == [None]
    assert obter_servico_indice_turno(recursos, None)._repositorio is None
    assert obter_servico_trechos_criticos(None)._repositorio is None


def test_obter_sessao_com_fabrica_abre_sessao_e_monta_repositorios_sql() -> None:
    # Arrange: engine SQLite em memória só para criar a sessão (nenhuma consulta é feita).
    from sqlalchemy.orm import sessionmaker

    engine = create_engine("sqlite://")
    recursos = RecursosServico(engine=engine, fabrica_sessoes=sessionmaker(bind=engine))

    # Act
    gerador = obter_sessao(recursos)
    sessao = next(gerador)

    # Assert
    assert isinstance(sessao, Session)
    assert isinstance(
        obter_servico_indice_turno(recursos, sessao)._repositorio, RepositorioCalculosSql
    )
    assert isinstance(
        obter_servico_trechos_criticos(sessao)._repositorio, RepositorioTrechosCriticosSql
    )
    gerador.close()
    recursos.fechar()


def test_gerar_openapi_segunda_chamada_reaproveita_esquema() -> None:
    # Arrange
    app = criar_app(inicializar=RecursosServico)

    # Act e Assert
    assert gerar_openapi(app) is gerar_openapi(app)
