"""Integração: semeador dos trechos, repositórios e CHECK do banco."""

import json
import uuid
from datetime import UTC, datetime, timedelta
from pathlib import Path

import pytest
from pydantic import SecretStr
from sqlalchemy import func, select
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from app.config import Configuracoes
from app.dominio.calculadora_indice import (
    PrevisaoTempo,
    RegistroCalculoIndice,
    calcular_resultado_indice,
)
from app.dominio.calendario import CalendarioFeriados
from app.dominio.horas_turno import gerar_horas_turno
from app.infraestrutura.persistencia import semeador_trechos
from app.infraestrutura.persistencia.modelos import (
    CalculoIndiceHoraModelo,
    CalculoIndiceModelo,
    ConjuntoTrechosCriticosModelo,
    TrechoCriticoModelo,
)
from app.infraestrutura.persistencia.repositorios import (
    RepositorioCalculosSql,
    RepositorioTrechosCriticosSql,
)
from app.infraestrutura.persistencia.semeador_trechos import ARQUIVO_TRECHOS, carregar_trechos
from tests.conftest import FIM_REFERENCIA, INICIO_REFERENCIA, P95_REFERENCIA, PREVISOES_REFERENCIA
from tests.fakes.relogio_fixo import RelogioFixo

pytestmark = pytest.mark.integracao


def contar(sessao: Session, modelo: type) -> int:
    """Quantidade de linhas de uma tabela."""
    return sessao.scalar(select(func.count()).select_from(modelo)) or 0


def criar_registro(calendario: CalendarioFeriados) -> RegistroCalculoIndice:
    """Registro do exemplo de referência, com as previsões do contrato."""
    horas = gerar_horas_turno(INICIO_REFERENCIA, FIM_REFERENCIA, calendario)
    tempo = PrevisaoTempo(chuva_mm_hora=7.0, temperatura_c=24.0, umidade_pct=85)
    resultado = calcular_resultado_indice(horas, PREVISOES_REFERENCIA, P95_REFERENCIA, tempo)
    return RegistroCalculoIndice(
        id=uuid.uuid4(),
        calculado_em=datetime(2026, 11, 12, 19, 30, tzinfo=UTC),
        inicio=INICIO_REFERENCIA,
        fim=FIM_REFERENCIA,
        previsao_tempo=tempo,
        resultado=resultado,
        versao_modelo="1.0.0",
    )


def test_semeador_primeira_carga_100_trechos_segunda_nao_duplica(
    sessao: Session, diretorio_modelo: Path
) -> None:
    # Arrange
    caminho = diretorio_modelo / ARQUIVO_TRECHOS

    # Act
    primeira = carregar_trechos(sessao, caminho, RelogioFixo())
    segunda = carregar_trechos(sessao, caminho, RelogioFixo())

    # Assert
    assert primeira.carregado is True and segunda.carregado is False
    assert primeira.hash_arquivo == segunda.hash_arquivo
    assert contar(sessao, ConjuntoTrechosCriticosModelo) == 1
    assert contar(sessao, TrechoCriticoModelo) == 100


def test_semeador_arquivo_novo_vira_conjunto_mais_recente(
    sessao: Session, diretorio_modelo: Path, tmp_path: Path
) -> None:
    # Arrange: o arquivo real e uma versão alterada, carregada depois.
    carregar_trechos(sessao, diretorio_modelo / ARQUIVO_TRECHOS, RelogioFixo())
    dados = json.loads((diretorio_modelo / ARQUIVO_TRECHOS).read_text(encoding="utf-8"))
    dados["periodo"] = "2019 a 2026"
    novo = tmp_path / ARQUIVO_TRECHOS
    novo.write_text(json.dumps(dados, ensure_ascii=False), encoding="utf-8")
    depois = RelogioFixo(datetime(2026, 11, 13, tzinfo=UTC))

    # Act
    resultado = carregar_trechos(sessao, novo, depois)
    conjunto = RepositorioTrechosCriticosSql(sessao).obter_mais_recentes(5)

    # Assert
    assert resultado.carregado is True
    assert contar(sessao, ConjuntoTrechosCriticosModelo) == 2
    assert conjunto is not None and conjunto.periodo == "2019 a 2026"


def test_semeador_main_carrega_no_banco_configurado(
    sessao: Session, url_testes: str, diretorio_modelo: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    # Arrange: a linha de comando com a configuração apontando para o banco de testes.
    monkeypatch.setattr(
        "app.config.obter_configuracoes",
        lambda: Configuracoes(
            _env_file=None, database_url=SecretStr(url_testes), modelo_dir=diretorio_modelo
        ),
    )

    # Act
    codigo = semeador_trechos.main()

    # Assert
    assert codigo == 0
    assert contar(sessao, TrechoCriticoModelo) == 100


def test_repositorio_trechos_limite_10_em_ordem_decrescente(
    sessao: Session, diretorio_modelo: Path
) -> None:
    # Arrange
    carregar_trechos(sessao, diretorio_modelo / ARQUIVO_TRECHOS, RelogioFixo())

    # Act
    conjunto = RepositorioTrechosCriticosSql(sessao).obter_mais_recentes(10)

    # Assert
    assert conjunto is not None
    assert [t.posicao for t in conjunto.trechos] == list(range(1, 11))
    quantidades = [t.quantidade_sinistros_graves for t in conjunto.trechos]
    assert quantidades == sorted(quantidades, reverse=True)
    assert conjunto.trechos[0].quantidade_sinistros_graves == 46


def test_repositorio_trechos_banco_vazio_devolve_none(sessao: Session) -> None:
    # Act e Assert
    assert RepositorioTrechosCriticosSql(sessao).obter_mais_recentes(10) is None


def test_repositorio_calculos_salvar_grava_calculo_e_4_horas(
    sessao: Session, calendario: CalendarioFeriados
) -> None:
    # Arrange
    registro = criar_registro(calendario)

    # Act
    RepositorioCalculosSql(sessao).salvar(registro)

    # Assert
    calculo = sessao.get(CalculoIndiceModelo, registro.id)
    assert calculo is not None
    assert (calculo.componente_sinistro, calculo.componente_chuva, calculo.componente_calor) == (
        84,
        70,
        0,
    )
    assert (calculo.indice_risco, calculo.faixa_risco) == (60, "ALTO")
    assert calculo.regra_agregacao == "MEDIA_PONDERADA_MINUTOS_V1"
    horas = sorted(calculo.horas, key=lambda h: h.hora_inicio_utc)
    assert [h.hora_local for h in horas] == [17, 18, 19, 20]
    assert all(h.minutos_no_turno == 60 and h.feriado is False for h in horas)
    assert horas[0].hora_inicio_utc == INICIO_REFERENCIA


def test_repositorio_calculos_falha_desfaz_e_sessao_continua_usavel(
    sessao: Session, calendario: CalendarioFeriados
) -> None:
    # Arrange: o mesmo id duas vezes viola a chave primária.
    registro = criar_registro(calendario)
    repositorio = RepositorioCalculosSql(sessao)
    repositorio.salvar(registro)

    # Tira o objeto já gravado da sessão, para o erro vir do banco (chave primária repetida).
    sessao.expunge_all()

    # Act e Assert
    with pytest.raises(IntegrityError):
        repositorio.salvar(registro)
    assert contar(sessao, CalculoIndiceModelo) == 1
    assert contar(sessao, CalculoIndiceHoraModelo) == 4


def test_check_do_banco_rejeita_componente_101(sessao: Session) -> None:
    # Arrange: grava direto no modelo, passando por cima das validações do domínio.
    agora = datetime(2026, 11, 12, 20, tzinfo=UTC)
    calculo = CalculoIndiceModelo(
        id=uuid.uuid4(),
        calculado_em=agora,
        inicio=agora,
        fim=agora + timedelta(hours=1),
        chuva_mm_hora=0.0,
        temperatura_c=24.0,
        umidade_pct=50.0,
        indice_calor_c=24.0,
        componente_sinistro=101,
        componente_chuva=0,
        componente_calor=0,
        indice_risco=51,
        faixa_risco="ALTO",
        explicacao="teste",
        versao_modelo="1.0.0",
        regra_agregacao="MEDIA_PONDERADA_MINUTOS_V1",
    )

    # Act e Assert
    sessao.add(calculo)
    with pytest.raises(IntegrityError, match="ck_calculo_indice_componente_sinistro_0_a_100"):
        sessao.commit()
    sessao.rollback()
