"""Caso de uso: listar os trechos críticos com mais sinistros graves."""

import logging

from app.aplicacao.excecoes import ServicoIndisponivelError
from app.dominio.portas import RepositorioTrechosCriticos
from app.dominio.trecho_critico import ConjuntoTrechosCriticos, validar_limite

logger = logging.getLogger(__name__)


class ServicoTrechosCriticos:
    """Devolve os primeiros trechos do conjunto mais recente, em ordem decrescente."""

    def __init__(self, repositorio: RepositorioTrechosCriticos | None) -> None:
        """Recebe o repositório; None quando o banco não está configurado."""
        self._repositorio = repositorio

    def listar(self, limite: int) -> ConjuntoTrechosCriticos:
        """Lista até "limite" trechos (1 a 50).

        Lança ValidacaoDominioError (400) para limite inválido e ServicoIndisponivelError
        (503) se o banco falhar ou se nenhum conjunto tiver sido carregado.
        """
        validar_limite(limite)
        if self._repositorio is None:
            raise ServicoIndisponivelError("O banco de dados não está configurado.")
        try:
            conjunto = self._repositorio.obter_mais_recentes(limite)
        except Exception as erro:
            # A aplicação não conhece o SQLAlchemy: qualquer falha do repositório é 503.
            logger.exception("Falha ao ler os trechos críticos (%s).", type(erro).__name__)
            raise ServicoIndisponivelError("Não foi possível ler os trechos críticos.") from erro
        if conjunto is None:
            raise ServicoIndisponivelError("Nenhum conjunto de trechos críticos foi carregado.")
        return conjunto
