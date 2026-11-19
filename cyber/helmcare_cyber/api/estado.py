"""Estado de uma instância da API: o que as rotas compartilham.

Fica num módulo próprio para as rotas e o interceptador importarem o tipo sem
importar a fábrica (app.py), que importa os dois.
"""

import logging
import threading
from dataclasses import dataclass, field

from fastapi import Request

from helmcare_cyber.api.banco import BancoSimulado
from helmcare_cyber.ia.tradutor import TradutorAlertas
from helmcare_cyber.taint.filtro_log import FiltroLogGuarda
from helmcare_cyber.taint.guarda import GuardaTaint
from helmcare_cyber.taint.modelo import Modo


@dataclass
class EstadoApp:
    guarda: GuardaTaint
    tradutor: TradutorAlertas
    banco: BancoSimulado
    logger_demo: logging.Logger
    filtro_log: FiltroLogGuarda
    # Serializa o que muda o estado (modo, cenários, demonstração, reinício):
    # um cenário limpa o registro de taint e não pode trocar de modo no meio.
    lock: threading.RLock = field(default_factory=threading.RLock)

    @property
    def modo(self) -> Modo:
        return self.guarda.modo

    def trocar_modo(self, modo: Modo) -> None:
        with self.lock:
            self.guarda.modo = Modo(modo)

    def __repr__(self) -> str:
        return f"EstadoApp(modo={self.modo.value}, eventos={len(self.guarda.repositorio)})"


def obter_estado(request: Request) -> EstadoApp:
    """Dependência do FastAPI: o estado da instância que recebeu a requisição."""
    return request.app.state.cyber
