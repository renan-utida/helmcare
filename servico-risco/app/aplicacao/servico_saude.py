"""Caso de uso: verificar a saúde do serviço (modelo carregado e banco respondendo)."""

from collections.abc import Callable


class ServicoSaude:
    """Saudável só quando o modelo está carregado e o banco responde ao SELECT 1."""

    def __init__(self, modelo_carregado: bool, verificar_banco: Callable[[], bool]) -> None:
        """Recebe o estado do modelo e uma função que testa o banco (True se respondeu)."""
        self._modelo_carregado = modelo_carregado
        self._verificar_banco = verificar_banco

    def saudavel(self) -> bool:
        """True se o serviço pode atender; o banco só é consultado se o modelo carregou."""
        return self._modelo_carregado and self._verificar_banco()
