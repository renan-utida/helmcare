"""Demonstração do Taint Analysis: os cenários C1 a C11 em AUDITORIA e depois em PROTECAO.

Uso, dentro de cyber/ com o venv ativo:

    python scripts/demo.py                         # todos os cenários, nos dois modos
    python scripts/demo.py --cenario C1            # só um cenário
    python scripts/demo.py --modo protecao         # só um modo
    python scripts/demo.py --cenario C1 --cenario C6 --modo auditoria

Todos os dados são simulados. A saída emitida em AUDITORIA mostra os valores
simulados sem proteção, de propósito: é o "antes" do cenário.
"""

import argparse
import json
import sys
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(RAIZ))

from helmcare_cyber.eventos.repositorio import RepositorioEventos  # noqa: E402
from helmcare_cyber.simulacao.cenarios import CENARIOS_POR_ID, ResultadoCenario, executar_todos  # noqa: E402
from helmcare_cyber.taint.guarda import GuardaTaint  # noqa: E402
from helmcare_cyber.taint.modelo import Modo  # noqa: E402

LARGURA = 78


def formatar_saida(saida) -> str:
    if isinstance(saida, str):
        return saida
    return json.dumps(saida, ensure_ascii=False)


def recuar(texto: str, margem: str = "  ") -> str:
    return "\n".join(margem + linha for linha in texto.splitlines())


def imprimir_cenario(resultado: ResultadoCenario) -> None:
    cenario = resultado.cenario
    print("=" * LARGURA)
    print(f"{cenario.id}  {cenario.titulo}  [{cenario.rotulo}]")
    print(
        f"Modo: {resultado.modo.value} | Fonte: {cenario.fonte.value} | "
        f"Destino: {cenario.destino.value} | Destinatário: {cenario.destinatario.value}"
    )
    print(f"Descrição: {cenario.descricao}")
    print("-" * LARGURA)

    saidas = resultado.saida_emitida if isinstance(resultado.saida_emitida, list) else [resultado.saida_emitida]
    print("Saída emitida:")
    for indice, saida in enumerate(saidas, start=1):
        prefixo = f"[{indice}] " if len(saidas) > 1 else ""
        print(recuar(prefixo + formatar_saida(saida)))

    esperado = cenario.esperado(resultado.modo).value
    situacao = "conforme o esperado" if resultado.passou else f"DIFERENTE do esperado ({esperado})"
    print(f"Decisão geral: {resultado.decisao_geral.value} ({situacao})")
    print(f"Regras aplicadas: {', '.join(resultado.regras_aplicadas()) or 'nenhuma'}")

    for indice, guarda in enumerate(resultado.resultados, start=1):
        titulo = f"Rastro da saída {indice}:" if len(resultado.resultados) > 1 else "Rastro:"
        print(titulo)
        print(recuar(guarda.rastro.para_texto()))


def rodar_modo(modo: Modo, ids: list[str] | None, repositorio: RepositorioEventos) -> list[ResultadoCenario]:
    repositorio.limpar()
    guarda = GuardaTaint(repositorio=repositorio, modo=modo)
    resultados = executar_todos(guarda, ids)
    print()
    print("#" * LARGURA)
    print(f"# MODO {modo.value}")
    print("#" * LARGURA)
    for resultado in resultados:
        imprimir_cenario(resultado)
    print("=" * LARGURA)
    print(f"Resumo do modo {modo.value}: {len(repositorio)} eventos de segurança gerados")
    for resultado in resultados:
        marca = "ok" if resultado.passou else "FALHOU"
        print(f"  {resultado.cenario.id:<4} {resultado.decisao_geral.value:<13} {marca}")
    return resultados


def main(argv: list[str] | None = None) -> int:
    # O terminal do Windows nem sempre usa UTF-8; sem isso, os acentos saem trocados.
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    parser = argparse.ArgumentParser(description="Cenários do Taint Analysis do HelmCare.")
    parser.add_argument("--cenario", action="append", help="id do cenário (C1 a C11); pode repetir")
    parser.add_argument("--modo", choices=["auditoria", "protecao"], help="só um modo (padrão: os dois)")
    argumentos = parser.parse_args(argv)

    ids = [c.upper() for c in argumentos.cenario] if argumentos.cenario else None
    desconhecidos = [c for c in ids or [] if c not in CENARIOS_POR_ID]
    if desconhecidos:
        parser.error(f"cenário desconhecido: {', '.join(desconhecidos)} (use C1 a C11)")

    modos = [Modo(argumentos.modo.upper())] if argumentos.modo else [Modo.AUDITORIA, Modo.PROTECAO]
    repositorio = RepositorioEventos()
    todos = [resultado for modo in modos for resultado in rodar_modo(modo, ids, repositorio)]
    return 0 if all(resultado.passou for resultado in todos) else 1


if __name__ == "__main__":
    sys.exit(main())
