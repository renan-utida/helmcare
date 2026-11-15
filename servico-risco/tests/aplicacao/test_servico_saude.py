"""Testes do caso de uso de saúde."""

from app.aplicacao.servico_saude import ServicoSaude


def test_saudavel_modelo_e_banco_ok_verdadeiro() -> None:
    # Act e Assert
    assert ServicoSaude(True, lambda: True).saudavel() is True


def test_saudavel_banco_fora_falso() -> None:
    # Act e Assert
    assert ServicoSaude(True, lambda: False).saudavel() is False


def test_saudavel_modelo_nao_carregado_falso_sem_consultar_banco() -> None:
    # Arrange
    consultas: list[bool] = []

    def verificar_banco() -> bool:
        consultas.append(True)
        return True

    # Act
    resultado = ServicoSaude(False, verificar_banco).saudavel()

    # Assert
    assert resultado is False
    assert consultas == []
