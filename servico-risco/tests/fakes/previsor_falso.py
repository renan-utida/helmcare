"""Previsor falso: devolve previsões fixas, sem carregar o XGBoost."""

from collections.abc import Sequence

from app.dominio.horas_turno import HoraTurno

P95_PADRAO = 0.322927


class PrevisorFalso:
    """Implementa a porta PrevisorSinistros com uma previsão fixa por hora.

    Guarda as horas recebidas, para o teste conferir o que o serviço pediu.
    """

    def __init__(self, previsoes: Sequence[float] | None = None, valor_padrao: float = 0.2) -> None:
        """previsoes: lista usada na ordem das horas; sem ela, todas recebem valor_padrao."""
        self._previsoes = list(previsoes) if previsoes is not None else None
        self._valor_padrao = valor_padrao
        self.horas_recebidas: list[HoraTurno] = []

    @property
    def p95(self) -> float:
        """Mesmo P95 do artefato real."""
        return P95_PADRAO

    @property
    def versao_modelo(self) -> str:
        """Versão fictícia, para o teste reconhecer que veio do fake."""
        return "falso-1.0"

    def prever(self, horas: Sequence[HoraTurno]) -> list[float]:
        """Devolve uma previsão para cada hora."""
        self.horas_recebidas = list(horas)
        if self._previsoes is not None:
            return self._previsoes[: len(horas)]
        return [self._valor_padrao] * len(horas)
