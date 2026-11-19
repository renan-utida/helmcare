"""Demonstração do Risco Cidadão: o índice, o semáforo e a mensagem de cada situação.

Uso, dentro de cyber/ com o venv ativo:

    python scripts/demo_risco.py            # só a reserva por tabela, sem rede
    python scripts/demo_risco.py --com-ia   # carrega cyber/.env e usa o Gemini

Cada situação roda os cenários num guarda novo, com repositório próprio, e
calcula o índice sobre os eventos gerados. Sai com código 1 se alguma
situação não der a cor (e o índice, quando fixado) esperada.
"""

import argparse
import sys
from dataclasses import replace
from decimal import Decimal
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(RAIZ))

from helmcare_cyber.config import carregar_dotenv  # noqa: E402
from helmcare_cyber.eventos.repositorio import RepositorioEventos  # noqa: E402
from helmcare_cyber.ia.tradutor import TradutorAlertas, criar_tradutor  # noqa: E402
from helmcare_cyber.risco.risco_cidadao import CorSemaforo, calcular_risco  # noqa: E402
from helmcare_cyber.simulacao.cenarios import executar_todos  # noqa: E402
from helmcare_cyber.taint.guarda import GuardaTaint  # noqa: E402
from helmcare_cyber.taint.modelo import Modo  # noqa: E402

LARGURA = 78
C1_A_C10 = [f"C{n}" for n in range(1, 11)]
C1_A_C11 = [*C1_A_C10, "C11"]

# (título, modo, cenários, cor esperada, índice esperado ou None)
SITUACOES = (
    ("Sem eventos", Modo.PROTECAO, [], CorSemaforo.VERDE, 0),
    ("C1 em PROTECAO", Modo.PROTECAO, ["C1"], CorSemaforo.VERDE, 15),
    ("C1 a C10 em PROTECAO", Modo.PROTECAO, C1_A_C10, CorSemaforo.AMARELO, 38),
    ("C11 em AUDITORIA", Modo.AUDITORIA, ["C11"], CorSemaforo.AMARELO, 40),
    ("C1 a C11 em AUDITORIA", Modo.AUDITORIA, C1_A_C11, CorSemaforo.VERMELHO, None),
)


def pontos(valor: Decimal) -> str:
    """24.300 vira "24,3" e 15.00 vira "15", no formato brasileiro."""
    texto = f"{valor.quantize(Decimal('0.1'))}"
    return texto.removesuffix(".0").replace(".", ",")


def mostrar(titulo: str, modo: Modo, ids: list[str], tradutor: TradutorAlertas) -> tuple[CorSemaforo, int]:
    repositorio = RepositorioEventos()
    if ids:
        executar_todos(GuardaTaint(repositorio=repositorio, modo=modo), ids)
    eventos = repositorio.listar()
    risco = calcular_risco(eventos)
    traducao = tradutor.traduzir_risco(risco, eventos)
    risco = replace(risco, mensagem_cidadao=traducao.mensagem, origem_mensagem=traducao.origem.value)

    print("=" * LARGURA)
    print(f"{titulo}  ({len(eventos)} {'evento' if len(eventos) == 1 else 'eventos'})")
    print(f"Índice: {risco.indice}  Cor: {risco.cor.value}  Ícone: {risco.icone}")
    print(f"Rótulo: {risco.rotulo}")
    print(f"Pior evento: {pontos(risco.pior_evento_pontos)}  Volume: {risco.volume_pontos}")
    c = risco.contadores
    print(f"Eventos: {c.eventos} (expostos {c.expostos}, neutralizados {c.neutralizados}, permitidos {c.permitidos})")
    print("Por quê:")
    if not risco.por_que:
        print("  nenhum evento pesou")
    for item in risco.por_que:
        categoria = item.categoria.value if item.categoria else "SEM_CATEGORIA"
        print(f"  {pontos(item.pontos):>5}  {categoria} em {item.destino.value}, {item.estado.value}")
    selo = f", {traducao.selo}" if traducao.selo else ""
    motivo = f", motivo da reserva: {traducao.motivo_descrito()}" if traducao.motivo_reserva else ""
    print(f"Mensagem ({risco.origem_mensagem}{selo}{motivo}):")
    print(f"  {risco.mensagem_cidadao}")
    return risco.cor, risco.indice


def main(argv: list[str] | None = None, tradutor: TradutorAlertas | None = None) -> int:
    """tradutor existe para os testes injetarem um cliente com transporte falso (sem rede)."""
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description="Demonstração do Risco Cidadão.")
    parser.add_argument("--com-ia", action="store_true", help="carrega cyber/.env e usa o Gemini")
    argumentos = parser.parse_args(argv)

    if tradutor is None and argumentos.com_ia:
        carregar_dotenv()
        tradutor = criar_tradutor()
    elif tradutor is None:
        # Sem cliente: só a reserva por tabela, nenhuma chamada de rede.
        tradutor = TradutorAlertas(cliente=None)

    divergencias = []
    for titulo, modo, ids, cor_esperada, indice_esperado in SITUACOES:
        cor, indice = mostrar(titulo, modo, ids, tradutor)
        if cor is not cor_esperada or (indice_esperado is not None and indice != indice_esperado):
            divergencias.append(titulo)
    print("=" * LARGURA)
    print(f"Situações diferentes do esperado: {', '.join(divergencias) if divergencias else 'nenhuma'}")
    return 1 if divergencias else 0


if __name__ == "__main__":
    sys.exit(main())
