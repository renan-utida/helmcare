"""Confere a tradução dos alertas: três consultas, uma de cada cor, e a repetição da primeira.

Uso, dentro de cyber/ com o venv ativo:

    python scripts/testar_ia.py                # carrega cyber/.env e usa o Gemini, se houver chave
    python scripts/testar_ia.py --sem-dotenv   # só o ambiente atual (sem chave, usa a reserva)

Nunca imprime a chave nem as variáveis de ambiente: só diz se a chave existe e
qual é o modelo. Sai com código 0 também quando cai na reserva, porque a
reserva é o comportamento esperado sem chave ou sem rede.
"""

import argparse
import os
import sys
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(RAIZ))

from helmcare_cyber.config import carregar_dotenv  # noqa: E402
from helmcare_cyber.ia.prompt import ConsultaTraducao  # noqa: E402
from helmcare_cyber.ia.tradutor import TradutorAlertas, criar_tradutor  # noqa: E402
from helmcare_cyber.pii.categorias import Categoria  # noqa: E402
from helmcare_cyber.risco.risco_cidadao import CorSemaforo  # noqa: E402
from helmcare_cyber.taint.modelo import Acao, Destino, Estado  # noqa: E402

CONSULTAS = (
    ConsultaTraducao(Categoria.SENHA, Destino.LOG, Acao.REDIGIR, Estado.NEUTRALIZADO, CorSemaforo.VERDE),
    ConsultaTraducao(
        Categoria.QR_TOKEN, Destino.DOCUMENTACAO_PUBLICA, Acao.BLOQUEAR, Estado.EXPOSTO, CorSemaforo.AMARELO
    ),
    ConsultaTraducao(
        Categoria.SAUDE, Destino.RESPOSTA_API_PUBLICA, Acao.BLOQUEAR, Estado.EXPOSTO, CorSemaforo.VERMELHO
    ),
)


def main(argv: list[str] | None = None, tradutor: TradutorAlertas | None = None) -> int:
    """tradutor existe para os testes injetarem um cliente com transporte falso (sem rede)."""
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description="Testa a tradução dos alertas por IA.")
    parser.add_argument("--sem-dotenv", action="store_true", help="não carrega o arquivo cyber/.env")
    argumentos = parser.parse_args(argv)

    if argumentos.sem_dotenv:
        print("arquivo .env: não carregado (--sem-dotenv)")
    else:
        print(f"arquivo .env: {'carregado' if carregar_dotenv() else 'não encontrado'}")

    tradutor = tradutor if tradutor is not None else criar_tradutor()
    print(f"chave configurada: {'sim' if (os.environ.get('GEMINI_API_KEY') or '').strip() else 'não'}")
    print(f"modelo: {tradutor.cliente.modelo if tradutor.cliente else '(não definido)'}")
    print()

    for rotulo, consulta in [*((c.cor.value, c) for c in CONSULTAS), ("repetição (cache)", CONSULTAS[0])]:
        traducao = tradutor.traduzir(consulta)
        selo = f" [{traducao.selo}]" if traducao.selo else ""
        print(f"{rotulo}: {consulta.categoria.value} em {consulta.destino.value}, {consulta.estado.value}")
        print(f"  origem: {traducao.origem.value}{selo} | motivo da reserva: {traducao.motivo_descrito()}")
        print(f"  mensagem: {traducao.mensagem}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
