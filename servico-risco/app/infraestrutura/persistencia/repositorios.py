"""Repositórios SQL que implementam as portas do domínio.

Cada repositório recebe a sessão da requisição e faz o commit explícito quando grava.
Eles convertem entre os modelos de tabela e os objetos do domínio, para o domínio
nunca ver o SQLAlchemy.
"""

from sqlalchemy import select
from sqlalchemy.orm import Session

from app.dominio.calculadora_indice import RegistroCalculoIndice
from app.dominio.trecho_critico import ConjuntoTrechosCriticos, TrechoCritico, validar_limite
from app.infraestrutura.persistencia.modelos import (
    CalculoIndiceHoraModelo,
    CalculoIndiceModelo,
    ConjuntoTrechosCriticosModelo,
    TrechoCriticoModelo,
)

SEGUNDOS_POR_MINUTO = 60


class RepositorioTrechosCriticosSql:
    """Lê os trechos do conjunto carregado mais recentemente (porta RepositorioTrechosCriticos)."""

    def __init__(self, sessao: Session) -> None:
        """Guarda a sessão da requisição."""
        self._sessao = sessao

    def obter_mais_recentes(self, limite: int) -> ConjuntoTrechosCriticos | None:
        """Devolve os primeiros trechos do conjunto mais recente, ou None se não houver carga."""
        validar_limite(limite)
        conjunto = self._sessao.scalars(
            select(ConjuntoTrechosCriticosModelo)
            .order_by(ConjuntoTrechosCriticosModelo.carregado_em.desc())
            .limit(1)
        ).first()
        if conjunto is None:
            return None

        # O LIMIT vai para o banco: só os trechos pedidos saem da tabela.
        trechos = self._sessao.scalars(
            select(TrechoCriticoModelo)
            .where(TrechoCriticoModelo.conjunto_id == conjunto.id)
            .order_by(TrechoCriticoModelo.posicao)
            .limit(limite)
        ).all()
        return ConjuntoTrechosCriticos(
            periodo=conjunto.periodo,
            fonte=conjunto.fonte,
            criterio=conjunto.criterio,
            trechos=tuple(
                TrechoCritico(
                    posicao=t.posicao,
                    logradouro=t.logradouro,
                    latitude=t.latitude,
                    longitude=t.longitude,
                    quantidade_sinistros_graves=t.quantidade_sinistros_graves,
                )
                for t in trechos
            ),
        )


class RepositorioCalculosSql:
    """Grava o histórico dos cálculos do índice (porta RepositorioCalculos)."""

    def __init__(self, sessao: Session) -> None:
        """Guarda a sessão da requisição."""
        self._sessao = sessao

    def salvar(self, registro: RegistroCalculoIndice) -> None:
        """Grava o cálculo e as suas horas numa única transação (tudo ou nada)."""
        resultado = registro.resultado
        previsao = registro.previsao_tempo
        modelo = CalculoIndiceModelo(
            id=registro.id,
            calculado_em=registro.calculado_em,
            inicio=registro.inicio,
            fim=registro.fim,
            chuva_mm_hora=previsao.chuva_mm_hora,
            temperatura_c=previsao.temperatura_c,
            umidade_pct=previsao.umidade_pct,
            indice_calor_c=resultado.indice_calor_c,
            componente_sinistro=resultado.componentes.sinistro,
            componente_chuva=resultado.componentes.chuva,
            componente_calor=resultado.componentes.calor,
            indice_risco=resultado.indice_risco,
            faixa_risco=resultado.faixa_risco.value,
            explicacao=resultado.explicacao,
            versao_modelo=registro.versao_modelo,
            regra_agregacao=registro.regra_agregacao,
            horas=[
                CalculoIndiceHoraModelo(
                    hora_inicio_utc=h.hora.inicio_utc,
                    hora_local=h.hora.hora,
                    dia_semana=h.hora.dia_semana,
                    mes=h.hora.mes,
                    feriado=bool(h.hora.feriado),
                    previsao_sinistros=h.previsao,
                    componente_sinistro_hora=h.componente,
                    minutos_no_turno=h.hora.segundos_no_turno / SEGUNDOS_POR_MINUTO,
                )
                for h in resultado.sinistro.horas
            ],
        )
        try:
            self._sessao.add(modelo)
            self._sessao.commit()
        except Exception:
            # Desfaz a transação para a sessão não ficar num estado quebrado; quem chama
            # (o serviço de aplicação) decide o que fazer com o erro.
            self._sessao.rollback()
            raise
