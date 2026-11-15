"""Relógio do sistema (implementa a porta Relogio do domínio).

É o único lugar do serviço que chama datetime.now(). Os testes usam um relógio fixo.
"""

from datetime import UTC, datetime


class RelogioSistema:
    """Devolve o instante atual em UTC."""

    def agora(self) -> datetime:
        """Instante atual com fuso UTC."""
        return datetime.now(UTC)
