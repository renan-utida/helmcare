"""Texto da explicação devolvida ao entregador (campo explicacao).

Função pura, com frases fixas, para o texto ser testável. Regra do contrato: a
explicação nunca diz que a chuva aumenta, eleva ou causa sinistros ou acidentes,
porque o histórico não mostra isso. Ela só avisa que há chuva prevista.
"""

from app.dominio.componentes import Componentes, arredondar_meio_para_cima
from app.dominio.faixa_risco import LIMITE_BAIXO, LIMITE_MODERADO, FaixaRisco

# A partir deste valor, chuva e calor entram na explicação.
COMPONENTE_MINIMO_PARA_CITAR = 1

FRASE_SINISTRO_ALTO = "o seu horário tem histórico de sinistros graves (pico às {hora}h)"
FRASE_SINISTRO_MODERADO = "o seu horário tem histórico moderado de sinistros graves"
FRASE_SINISTRO_BAIXO = "o seu horário tem poucos sinistros graves no histórico"
FRASE_CHUVA = "chuva prevista no turno, atenção redobrada na pista"
FRASE_CALOR = "calor previsto no turno (índice de calor de {indice} °C), faça pausas e hidrate-se"


def formatar_uma_casa(valor: float) -> str:
    """Formata com uma casa decimal e vírgula (24,7), arredondando 0,5 para cima."""
    decimos = arredondar_meio_para_cima(valor * 10)
    return f"{decimos / 10:.1f}".replace(".", ",")


def juntar_trechos(trechos: list[str]) -> str:
    """Junta os trechos com vírgula e " e " antes do último ("a, b e c")."""
    if len(trechos) == 1:
        return trechos[0]
    return ", ".join(trechos[:-1]) + " e " + trechos[-1]


def frase_sinistro(componente_sinistro: int, hora_pico: int) -> str:
    """Escolhe a frase do sinistro pela mesma escala das faixas (até 25, 26 a 50, 51 ou mais)."""
    if componente_sinistro > LIMITE_MODERADO:
        return FRASE_SINISTRO_ALTO.format(hora=hora_pico)
    if componente_sinistro > LIMITE_BAIXO:
        return FRASE_SINISTRO_MODERADO
    return FRASE_SINISTRO_BAIXO


def gerar_explicacao(
    faixa: FaixaRisco, componentes: Componentes, hora_pico: int, indice_calor_c: float
) -> str:
    """Monta a explicação: "Risco {faixa}: " seguido dos trechos de sinistro, chuva e calor.

    hora_pico é a hora local (0 a 23) de maior valor de sinistro no turno. O trecho
    do sinistro sempre aparece; chuva e calor só quando o componente é 1 ou mais.
    """
    trechos = [frase_sinistro(componentes.sinistro, hora_pico)]
    if componentes.chuva >= COMPONENTE_MINIMO_PARA_CITAR:
        trechos.append(FRASE_CHUVA)
    if componentes.calor >= COMPONENTE_MINIMO_PARA_CITAR:
        trechos.append(FRASE_CALOR.format(indice=formatar_uma_casa(indice_calor_c)))
    return f"Risco {faixa.rotulo}: {juntar_trechos(trechos)}"
