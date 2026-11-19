"""Repositório dos eventos de segurança: em memória, com gravação opcional em JSONL.

Em memória com lock para o guarda poder ser usado por várias threads (por
exemplo, o filtro de log de uma API). Com caminho_jsonl, cada evento também
vira uma linha no arquivo, que sobrevive ao fim do processo. O evento já não
tem valor original, então o arquivo também não tem.
"""

import threading
from datetime import datetime
from pathlib import Path

from helmcare_cyber.eventos.modelo import EventoSeguranca, ler_iso


class RepositorioEventos:
    def __init__(self, caminho_jsonl: str | Path | None = None) -> None:
        self._eventos: list[EventoSeguranca] = []
        self._lock = threading.Lock()
        self.caminho_jsonl = Path(caminho_jsonl) if caminho_jsonl else None

    def adicionar(self, evento: EventoSeguranca) -> None:
        with self._lock:
            self._eventos.append(evento)
            if self.caminho_jsonl is not None:
                self.caminho_jsonl.parent.mkdir(parents=True, exist_ok=True)
                with self.caminho_jsonl.open("a", encoding="utf-8") as arquivo:
                    arquivo.write(evento.para_json() + "\n")

    def listar(self, desde: str | datetime | None = None) -> list[EventoSeguranca]:
        """Todos os eventos, ou só os de desde em diante (texto ISO 8601 ou datetime com fuso)."""
        with self._lock:
            eventos = list(self._eventos)
        if desde is None:
            return eventos
        limite = ler_iso(desde) if isinstance(desde, str) else desde
        return [e for e in eventos if ler_iso(e.ts) >= limite]

    def limpar(self) -> None:
        """Esvazia a memória. O arquivo JSONL, se houver, é histórico e fica como está."""
        with self._lock:
            self._eventos.clear()

    def __len__(self) -> int:
        with self._lock:
            return len(self._eventos)
