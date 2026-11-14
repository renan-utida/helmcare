"""Previsor de sinistros graves com o modelo XGBoost (implementa a porta PrevisorSinistros).

O modelo é carregado uma única vez (no startup, na Rodada 2) e depois só é lido. Todas
as horas do turno vão numa única chamada predict, e o numpy.float32 do XGBoost vira
float do Python aqui, na fronteira, para o domínio não depender do numpy.
"""

from collections.abc import Sequence
from pathlib import Path

import numpy as np
import xgboost

from app.dominio.horas_turno import ORDEM_VARIAVEIS, HoraTurno
from app.infraestrutura.ml.artefatos import ArtefatoModeloError, ArtefatosModelo, carregar_artefatos


def montar_dmatrix(horas: Sequence[HoraTurno]) -> xgboost.DMatrix:
    """Monta a matriz de entrada: uma linha por hora, colunas na ordem de ORDEM_VARIAVEIS.

    Os nomes das colunas são obrigatórios: o modelo foi salvo com nomes de variáveis,
    e sem eles o XGBoost recusa a previsão.
    """
    linhas = [hora.variaveis_modelo() for hora in horas]
    matriz = np.array(linhas, dtype=np.float32).reshape(len(linhas), len(ORDEM_VARIAVEIS))
    return xgboost.DMatrix(matriz, feature_names=list(ORDEM_VARIAVEIS))


class PrevisorXgboost:
    """Previsor que usa o modelo_sinistros_xgb.json de ml/modelo/."""

    def __init__(self, artefatos: ArtefatosModelo) -> None:
        """Carrega o modelo do caminho indicado nos artefatos e confere os nomes das variáveis."""
        self._artefatos = artefatos
        self._booster = xgboost.Booster()
        try:
            self._booster.load_model(str(artefatos.caminho_modelo))
        except xgboost.core.XGBoostError as erro:
            raise ArtefatoModeloError(
                f"Não foi possível carregar o modelo {artefatos.caminho_modelo}: {erro}"
            ) from erro
        if tuple(self._booster.feature_names or ()) != ORDEM_VARIAVEIS:
            raise ArtefatoModeloError(
                f"O modelo tem as variáveis {self._booster.feature_names}, "
                f"esperadas {list(ORDEM_VARIAVEIS)}."
            )

    @classmethod
    def carregar(cls, diretorio: Path) -> "PrevisorXgboost":
        """Lê os artefatos do diretório e devolve o previsor pronto para uso."""
        return cls(carregar_artefatos(diretorio))

    @property
    def artefatos(self) -> ArtefatosModelo:
        """Artefatos lidos no carregamento (inclui o calendário de feriados)."""
        return self._artefatos

    @property
    def p95(self) -> float:
        """P95 das previsões do modelo (p95_sinistros_graves.json)."""
        return self._artefatos.p95

    @property
    def versao_modelo(self) -> str:
        """Versão do modelo (versaoModelo de metadados_modelo.json)."""
        return self._artefatos.versao_modelo

    def prever(self, horas: Sequence[HoraTurno]) -> list[float]:
        """Prevê os sinistros graves esperados em cada hora, numa única chamada predict."""
        if not horas:
            return []
        saida = self._booster.predict(montar_dmatrix(horas))
        return [float(valor) for valor in saida]
