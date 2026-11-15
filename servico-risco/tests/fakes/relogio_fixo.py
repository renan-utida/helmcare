"""Relógio fixo: sempre o mesmo instante, para os testes serem repetíveis."""

from datetime import UTC, datetime

INSTANTE_PADRAO = datetime(2026, 11, 12, 19, 30, tzinfo=UTC)


class RelogioFixo:
    """Implementa a porta Relogio devolvendo um instante fixo."""

    def __init__(self, instante: datetime = INSTANTE_PADRAO) -> None:
        """Guarda o instante que agora() vai devolver."""
        self.instante = instante

    def agora(self) -> datetime:
        """O instante fixo."""
        return self.instante
