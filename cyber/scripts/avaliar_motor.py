"""Avalia o motor de PII sobre o corpus rotulado e imprime as métricas por categoria.

Uso, dentro de cyber/ com o venv ativo:

    python scripts/avaliar_motor.py

Sai com código 1 se houver falso positivo nos negativos de telemetria, GUID,
timestamp ou versão, que nunca podem ser marcados.
"""

import sys
from pathlib import Path

# Permite rodar o script direto, sem instalar o pacote.
RAIZ = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(RAIZ))

from helmcare_cyber.pii.avaliacao import GRUPOS_SEM_FALSO_POSITIVO, avaliar, carregar_corpus  # noqa: E402
from helmcare_cyber.pii.categorias import Categoria  # noqa: E402

CORPUS = RAIZ / "tests" / "corpus" / "corpus_pii.json"


def porcentagem(valor: float | None) -> str:
    """Formato brasileiro (vírgula decimal); "-" quando não há o que medir."""
    return "-" if valor is None else f"{valor * 100:.1f}%".replace(".", ",")


def main() -> int:
    # O console do Windows nem sempre usa UTF-8; sem isso, os acentos saem trocados.
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    casos = carregar_corpus(CORPUS)
    resultado = avaliar(casos)

    cabecalho = f"{'Categoria':<24}{'VP':>5}{'FP':>5}{'FN':>5}{'Precisão':>11}{'Recall':>9}"
    linha = "-" * len(cabecalho)
    print("Avaliação do motor de PII do HelmCare")
    print(f"Corpus: {CORPUS.relative_to(RAIZ).as_posix()} ({resultado.quantidade_casos} casos)")
    print()
    print(cabecalho)
    print(linha)
    for categoria in Categoria:
        m = resultado.por_categoria[categoria]
        print(
            f"{categoria.value:<24}{m.vp:>5}{m.fp:>5}{m.fn:>5}"
            f"{porcentagem(m.precisao):>11}{porcentagem(m.recall):>9}"
        )
    print(linha)
    t = resultado.total
    print(f"{'TOTAL':<24}{t.vp:>5}{t.fp:>5}{t.fn:>5}{porcentagem(t.precisao):>11}{porcentagem(t.recall):>9}")
    print()

    negativos = [c for c in casos if c.get("grupo") in GRUPOS_SEM_FALSO_POSITIVO]
    grupos = ", ".join(sorted(GRUPOS_SEM_FALSO_POSITIVO))
    print(f"Negativos protegidos ({grupos}): {len(negativos)} casos, "
          f"{len(resultado.falsos_positivos_protegidos)} com falso positivo")
    if resultado.limitacoes:
        print(f"Limitações conhecidas (nome em texto livre, sem NER): {', '.join(resultado.limitacoes)}")
    outras = [c for c in resultado.divergentes if c not in resultado.limitacoes]
    print(f"Casos divergentes fora das limitações: {', '.join(outras) if outras else 'nenhum'}")

    if resultado.falsos_positivos_protegidos:
        print(f"FALHA: falso positivo em {', '.join(resultado.falsos_positivos_protegidos)}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
