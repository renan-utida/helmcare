"""Sobe a API do módulo e o painel Risco Cidadão.

Uso, dentro de cyber/ com o venv ativo:

    python scripts/servir.py                  # carrega cyber/.env e sobe em 127.0.0.1:8100
    python scripts/servir.py --sem-dotenv     # só o ambiente atual (sem chave, mensagens da reserva)
    python scripts/servir.py --porta 8200

O padrão escuta só na própria máquina (127.0.0.1). Nunca imprime a chave nem
as variáveis de ambiente: só diz se a chave existe e qual é o modelo.
"""

import argparse
import ipaddress
import os
import sys
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(RAIZ))

from helmcare_cyber.api.app import criar_app  # noqa: E402
from helmcare_cyber.config import carregar_dotenv  # noqa: E402

PORTA_PADRAO = 8100
HOST_PADRAO = "127.0.0.1"


def eh_local(host: str) -> bool:
    if host == "localhost":
        return True
    try:
        return ipaddress.ip_address(host).is_loopback
    except ValueError:
        return False


def main(argv: list[str] | None = None) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description="Sobe a API e o painel Risco Cidadão.")
    parser.add_argument("--sem-dotenv", action="store_true", help="não carrega o arquivo cyber/.env")
    parser.add_argument("--host", default=HOST_PADRAO, help="endereço de escuta (padrão 127.0.0.1)")
    parser.add_argument("--porta", type=int, default=PORTA_PADRAO, help="porta (padrão 8100)")
    argumentos = parser.parse_args(argv)

    if argumentos.sem_dotenv:
        print("arquivo .env: não carregado (--sem-dotenv)")
    else:
        print(f"arquivo .env: {'carregado' if carregar_dotenv() else 'não encontrado'}")

    app = criar_app()
    cliente = app.state.cyber.tradutor.cliente
    print(f"chave configurada: {'sim' if (os.environ.get('GEMINI_API_KEY') or '').strip() else 'não'}")
    print(f"modelo: {cliente.modelo if cliente else '(não definido)'}")
    print(f"modo inicial: {app.state.cyber.modo.value}")
    if not eh_local(argumentos.host):
        print(f"atenção: escutando em {argumentos.host}, a API fica visível fora desta máquina")

    base = f"http://{argumentos.host}:{argumentos.porta}"
    print(f"painel:  {base}/painel/")
    print(f"swagger: {base}/docs")

    import uvicorn

    uvicorn.run(app, host=argumentos.host, port=argumentos.porta, log_level="info")
    return 0


if __name__ == "__main__":
    sys.exit(main())
