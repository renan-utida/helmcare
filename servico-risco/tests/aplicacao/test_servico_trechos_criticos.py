"""Testes do caso de uso de listagem dos trechos críticos."""

import pytest

from app.aplicacao.excecoes import ServicoIndisponivelError
from app.aplicacao.servico_trechos_criticos import ServicoTrechosCriticos
from app.dominio.excecoes import ValidacaoDominioError
from tests.fakes.repositorios_memoria import RepositorioTrechosMemoria, criar_conjunto_trechos


def test_listar_limite_10_devolve_10_trechos_em_ordem_decrescente() -> None:
    # Arrange
    servico = ServicoTrechosCriticos(RepositorioTrechosMemoria(criar_conjunto_trechos()))

    # Act
    conjunto = servico.listar(10)

    # Assert
    quantidades = [t.quantidade_sinistros_graves for t in conjunto.trechos]
    assert len(quantidades) == 10
    assert quantidades == sorted(quantidades, reverse=True)


@pytest.mark.parametrize("limite", [0, 51])
def test_listar_limite_invalido_lanca_validacao_sem_ir_ao_banco(limite: int) -> None:
    # Arrange
    repositorio = RepositorioTrechosMemoria(criar_conjunto_trechos())
    servico = ServicoTrechosCriticos(repositorio)

    # Act e Assert
    with pytest.raises(ValidacaoDominioError):
        servico.listar(limite)
    assert repositorio.limites_pedidos == []


def test_listar_repositorio_falha_lanca_servico_indisponivel() -> None:
    # Arrange
    servico = ServicoTrechosCriticos(RepositorioTrechosMemoria(None, falhar=True))

    # Act e Assert
    with pytest.raises(ServicoIndisponivelError):
        servico.listar(10)


def test_listar_sem_conjunto_carregado_lanca_servico_indisponivel() -> None:
    # Arrange
    servico = ServicoTrechosCriticos(RepositorioTrechosMemoria(None))

    # Act e Assert
    with pytest.raises(ServicoIndisponivelError, match="Nenhum conjunto"):
        servico.listar(10)


def test_listar_sem_banco_configurado_lanca_servico_indisponivel() -> None:
    # Arrange
    servico = ServicoTrechosCriticos(None)

    # Act e Assert
    with pytest.raises(ServicoIndisponivelError, match="não está configurado"):
        servico.listar(10)
