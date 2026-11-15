"""Testes do previsor XGBoost com o modelo real e do exemplo de referência completo."""

from dataclasses import replace
from pathlib import Path

import pytest

from app.dominio.calculadora_indice import PrevisaoTempo, calcular_resultado_indice
from app.dominio.componentes import Componentes
from app.dominio.faixa_risco import FaixaRisco
from app.dominio.horas_turno import gerar_horas_turno
from app.dominio.portas import PrevisorSinistros
from app.infraestrutura.ml.artefatos import ArtefatoModeloError
from app.infraestrutura.ml.previsor_xgboost import PrevisorXgboost, montar_dmatrix
from tests.conftest import FIM_REFERENCIA, INICIO_REFERENCIA, PREVISOES_REFERENCIA


@pytest.fixture(scope="module")
def previsor(diretorio_modelo: Path) -> PrevisorXgboost:
    """Previsor com o modelo real, carregado uma vez para o módulo inteiro."""
    return PrevisorXgboost.carregar(diretorio_modelo)


def test_montar_dmatrix_colunas_na_ordem_do_contrato(previsor: PrevisorXgboost) -> None:
    # Arrange
    horas = gerar_horas_turno(INICIO_REFERENCIA, FIM_REFERENCIA, previsor.artefatos.calendario)

    # Act
    matriz = montar_dmatrix(horas)

    # Assert
    assert matriz.feature_names == ["hora", "dia_semana", "mes", "feriado"]
    assert matriz.num_row() == 4
    assert matriz.num_col() == 4


def test_prever_exemplo_referencia_paridade_com_notebook(previsor: PrevisorXgboost) -> None:
    # Arrange
    horas = gerar_horas_turno(INICIO_REFERENCIA, FIM_REFERENCIA, previsor.artefatos.calendario)

    # Act
    previsoes = previsor.prever(horas)

    # Assert
    assert previsoes == pytest.approx(PREVISOES_REFERENCIA, abs=1e-5)
    assert all(type(p) is float for p in previsoes)  # float do Python, não numpy.float32


def test_prever_ordem_das_variaveis_importa(previsor: PrevisorXgboost) -> None:
    # Arrange: troca hora e dia_semana de lugar; a previsão precisa mudar.
    horas = gerar_horas_turno(INICIO_REFERENCIA, FIM_REFERENCIA, previsor.artefatos.calendario)
    trocadas = [replace(h, hora=h.dia_semana, dia_semana=h.hora % 7) for h in horas]

    # Act
    certas = previsor.prever(horas)
    erradas = previsor.prever(trocadas)

    # Assert
    assert certas != pytest.approx(erradas, abs=1e-5)


def test_prever_sem_horas_devolve_lista_vazia(previsor: PrevisorXgboost) -> None:
    # Act e Assert
    assert previsor.prever([]) == []


def test_previsor_xgboost_membros_da_porta_previsor_sinistros_presentes(
    previsor: PrevisorXgboost,
) -> None:
    # Arrange: os membros que a porta do domínio declara.
    membros_porta = {"p95", "versao_modelo", "prever"}

    # Assert: o Protocol declara esses membros e o previsor real tem todos eles.
    assert membros_porta <= set(dir(PrevisorSinistros))
    assert all(hasattr(previsor, membro) for membro in membros_porta)


def test_previsor_expoe_p95_e_versao_modelo(previsor: PrevisorXgboost) -> None:
    # Assert
    assert previsor.p95 == pytest.approx(0.322927)
    assert previsor.versao_modelo == "1.0.0"


def test_previsor_modelo_corrompido_erro_claro(diretorio_modelo: Path, tmp_path: Path) -> None:
    # Arrange: artefatos válidos apontando para um arquivo de modelo inválido.
    previsor_real = PrevisorXgboost.carregar(diretorio_modelo)
    corrompido = tmp_path / "modelo_sinistros_xgb.json"
    corrompido.write_text("{}", encoding="utf-8")
    artefatos = replace(previsor_real.artefatos, caminho_modelo=corrompido)

    # Act e Assert
    with pytest.raises(ArtefatoModeloError, match="modelo"):
        PrevisorXgboost(artefatos)


def test_exemplo_referencia_previsor_real_e_dominio_84_70_0_60_alto(
    previsor: PrevisorXgboost,
) -> None:
    # Arrange: requisição de exemplo da seção 8 do contrato.
    horas = gerar_horas_turno(INICIO_REFERENCIA, FIM_REFERENCIA, previsor.artefatos.calendario)
    tempo = PrevisaoTempo(chuva_mm_hora=7.0, temperatura_c=24.0, umidade_pct=85)

    # Act
    previsoes = previsor.prever(horas)
    resultado = calcular_resultado_indice(horas, previsoes, previsor.p95, tempo)

    # Assert
    assert resultado.sinistro.media == pytest.approx(83.62, abs=0.01)
    assert resultado.componentes == Componentes(sinistro=84, chuva=70, calor=0)
    assert resultado.indice_risco == 60
    assert resultado.faixa_risco is FaixaRisco.ALTO
    assert resultado.explicacao == (
        "Risco alto: chuva prevista no turno e o seu horário tem histórico de sinistros "
        "graves (pico às 19h)."
    )
