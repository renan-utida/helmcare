"""Banco simulado da demonstração: uma lista em memória, com lock.

Guarda só o que o guarda deixou passar para o destino PERSISTENCIA. Nada daqui
é devolvido por rota nenhuma; o repr mostra só a quantidade de registros.
"""

import threading
import uuid


class BancoSimulado:
    def __init__(self) -> None:
        self._registros: dict[str, dict] = {}
        self._lock = threading.Lock()

    def salvar(self, registro: dict) -> str:
        identificador = str(uuid.uuid4())
        with self._lock:
            self._registros[identificador] = dict(registro)
        return identificador

    def obter(self, identificador: str) -> dict | None:
        """Uso interno e dos testes (por exemplo, conferir que a senha foi gravada como hash)."""
        with self._lock:
            registro = self._registros.get(identificador)
            return dict(registro) if registro is not None else None

    def limpar(self) -> None:
        with self._lock:
            self._registros.clear()

    def __len__(self) -> int:
        with self._lock:
            return len(self._registros)

    def __repr__(self) -> str:
        return f"BancoSimulado({len(self)} registros)"
