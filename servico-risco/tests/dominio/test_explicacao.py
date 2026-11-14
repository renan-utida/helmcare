"""Testes do texto da explicação."""

import itertools
import re

import pytest

from app.dominio.calculadora_indice import calcular_indice
from app.dominio.componentes import Componentes
from app.dominio.explicacao import formatar_uma_casa, gerar_explicacao, juntar_trechos
from app.dominio.faixa_risco import FaixaRisco, classificar_faixa

# Palavras que ligariam a chuva a mais sinistros ou acidentes (proibido pelo contrato).
PADRAO_PROIBIDO = re.compile(r"aument|elev|caus|acident|provoc|agrav", re.IGNORECASE)

# Travessão (U+2014) e meia-risca (U+2013), proibidos nos textos do serviço.
TRAVESSAO = chr(0x2014)
MEIA_RISCA = chr(0x2013)


def test_gerar_explicacao_exemplo_referencia_texto_completo() -> None:
    # Arrange
    componentes = Componentes(sinistro=84, chuva=70, calor=0)

    # Act
    texto = gerar_explicacao(FaixaRisco.ALTO, componentes, hora_pico=19, indice_calor_c=24.67)

    # Assert
    assert texto == (
        "Risco alto: o seu horário tem histórico de sinistros graves (pico às 19h) "
        "e chuva prevista no turno, atenção redobrada na pista"
    )


def test_gerar_explicacao_com_chuva_cita_chuva_prevista_no_turno() -> None:
    # Arrange
    componentes = Componentes(sinistro=10, chuva=1, calor=0)

    # Act
    texto = gerar_explicacao(FaixaRisco.BAIXO, componentes, hora_pico=3, indice_calor_c=20.0)

    # Assert
    assert "chuva prevista no turno" in texto


def test_gerar_explicacao_sem_chuva_nao_cita_chuva() -> None:
    # Arrange
    componentes = Componentes(sinistro=10, chuva=0, calor=0)

    # Act
    texto = gerar_explicacao(FaixaRisco.BAIXO, componentes, hora_pico=3, indice_calor_c=20.0)

    # Assert
    assert texto == "Risco baixo: o seu horário tem poucos sinistros graves no histórico"


def test_gerar_explicacao_tres_trechos_virgula_e_calor_com_uma_casa() -> None:
    # Arrange
    componentes = Componentes(sinistro=40, chuva=50, calor=91)

    # Act
    texto = gerar_explicacao(
        FaixaRisco.MUITO_ALTO, componentes, hora_pico=8, indice_calor_c=39.8049
    )

    # Assert
    assert texto == (
        "Risco muito alto: o seu horário tem histórico moderado de sinistros graves, "
        "chuva prevista no turno, atenção redobrada na pista e "
        "calor previsto no turno (índice de calor de 39,8 °C), faça pausas e hidrate-se"
    )


@pytest.mark.parametrize(
    ("sinistro", "trecho"),
    [
        (25, "poucos sinistros graves no histórico"),
        (26, "histórico moderado de sinistros graves"),
        (50, "histórico moderado de sinistros graves"),
        (51, "histórico de sinistros graves (pico às 7h)"),
    ],
)
def test_gerar_explicacao_fronteiras_do_sinistro_frase_esperada(sinistro: int, trecho: str) -> None:
    # Arrange
    componentes = Componentes(sinistro=sinistro, chuva=0, calor=0)

    # Act
    texto = gerar_explicacao(FaixaRisco.MODERADO, componentes, hora_pico=7, indice_calor_c=20.0)

    # Assert
    assert trecho in texto


def test_gerar_explicacao_todas_combinacoes_nunca_relaciona_chuva_e_sinistros() -> None:
    # Arrange: varre os componentes de 0 a 100 de 5 em 5 (mais o 1) e todas as horas.
    valores = sorted({*range(0, 101, 5), 1})

    for sinistro, chuva, calor in itertools.product(valores, repeat=3):
        componentes = Componentes(sinistro=sinistro, chuva=chuva, calor=calor)
        faixa = classificar_faixa(calcular_indice(componentes))

        # Act
        texto = gerar_explicacao(faixa, componentes, hora_pico=sinistro % 24, indice_calor_c=35.0)

        # Assert
        assert PADRAO_PROIBIDO.search(texto) is None, texto
        assert TRAVESSAO not in texto and MEIA_RISCA not in texto


@pytest.mark.parametrize(("valor", "esperado"), [(24.67, "24,7"), (39.75, "39,8"), (30.04, "30,0")])
def test_formatar_uma_casa_arredonda_meio_para_cima_com_virgula(
    valor: float, esperado: str
) -> None:
    # Act e Assert
    assert formatar_uma_casa(valor) == esperado


def test_juntar_trechos_um_dois_e_tres_trechos() -> None:
    # Act e Assert
    assert juntar_trechos(["a"]) == "a"
    assert juntar_trechos(["a", "b"]) == "a e b"
    assert juntar_trechos(["a", "b", "c"]) == "a, b e c"
