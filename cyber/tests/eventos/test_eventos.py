import json
import re
import uuid
from datetime import UTC, datetime, timedelta

import pytest

from helmcare_cyber.config import RISCO_JANELA_MIN_PADRAO, carregar_config
from helmcare_cyber.eventos import EventoSeguranca, RepositorioEventos
from helmcare_cyber.eventos.modelo import agora_iso, ler_iso
from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.taint.modelo import Acao, Destinatario, Destino, Estado, Fonte, Modo


def novo_evento(**campos) -> EventoSeguranca:
    base = dict(
        cenario="C1",
        fonte=Fonte.CADASTRO_ENTREGADOR,
        destino=Destino.LOG,
        destinatario=Destinatario.SISTEMA,
        categoria=Categoria.CPF,
        acao=Acao.REDIGIR,
        estado=Estado.NEUTRALIZADO,
        regra_id="TA-LOG-01",
        modo=Modo.PROTECAO,
        quantidade=1,
        caminhos=("$",),
        rastro=({"tipo": "DESTINO", "descricao": "LOG", "categorias": ["CPF"], "valorMascarado": None},),
    )
    return EventoSeguranca(**(base | campos))


class TestEvento:
    def test_campos_padrao(self):
        evento = novo_evento()
        assert uuid.UUID(evento.id).version == 4
        assert re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z", evento.ts)
        assert evento.mensagem_cidadao is None and evento.origem_mensagem is None

    def test_para_dict(self):
        dicionario = novo_evento().para_dict()
        assert list(dicionario) == [
            "id",
            "ts",
            "cenario",
            "fonte",
            "destino",
            "destinatario",
            "categoria",
            "acao",
            "estado",
            "regraId",
            "modo",
            "quantidade",
            "caminhos",
            "rastro",
            "mensagemCidadao",
            "origemMensagem",
        ]
        assert dicionario["categoria"] == "CPF" and dicionario["estado"] == "NEUTRALIZADO"
        assert "peso" not in dicionario

    def test_sem_fonte_e_sem_categoria(self):
        dicionario = novo_evento(fonte=None, categoria=None, regra_id="TA-ERRO-01").para_dict()
        assert dicionario["fonte"] is None and dicionario["categoria"] is None

    def test_para_json(self):
        assert json.loads(novo_evento().para_json())["regraId"] == "TA-LOG-01"

    def test_ids_diferentes(self):
        assert novo_evento().id != novo_evento().id

    def test_ler_iso(self):
        assert ler_iso(agora_iso()).tzinfo is not None


class TestRepositorio:
    def test_adicionar_listar_limpar(self):
        repositorio = RepositorioEventos()
        repositorio.adicionar(novo_evento())
        repositorio.adicionar(novo_evento(categoria=Categoria.NOME))
        assert len(repositorio) == 2
        assert [e.categoria for e in repositorio.listar()] == [Categoria.CPF, Categoria.NOME]
        repositorio.limpar()
        assert repositorio.listar() == []

    def test_listar_desde(self):
        repositorio = RepositorioEventos()
        antigo = novo_evento(ts="2026-11-01T10:00:00.000Z")
        recente = novo_evento(ts="2026-11-19T10:00:00.000Z")
        repositorio.adicionar(antigo)
        repositorio.adicionar(recente)
        assert repositorio.listar(desde="2026-11-10T00:00:00Z") == [recente]
        assert repositorio.listar(desde=datetime(2026, 11, 1, tzinfo=UTC)) == [antigo, recente]
        futuro = datetime.now(UTC) + timedelta(days=1)
        assert repositorio.listar(desde=futuro) == []

    def test_jsonl(self, tmp_path):
        arquivo = tmp_path / "sub" / "eventos.jsonl"
        repositorio = RepositorioEventos(arquivo)
        repositorio.adicionar(novo_evento())
        repositorio.adicionar(novo_evento(categoria=Categoria.EMAIL))
        linhas = arquivo.read_text(encoding="utf-8").splitlines()
        assert [json.loads(linha)["categoria"] for linha in linhas] == ["CPF", "EMAIL"]
        repositorio.limpar()
        assert len(arquivo.read_text(encoding="utf-8").splitlines()) == 2

    def test_sem_jsonl(self):
        assert RepositorioEventos().caminho_jsonl is None
        assert RepositorioEventos("").caminho_jsonl is None


class TestConfig:
    def test_padroes(self):
        config = carregar_config({})
        assert config.modo is Modo.PROTECAO
        assert config.risco_janela_min == RISCO_JANELA_MIN_PADRAO == 1440
        assert config.eventos_jsonl is None

    def test_valores(self):
        config = carregar_config(
            {"CYBER_MODO": " auditoria ", "RISCO_JANELA_MIN": "60", "CYBER_EVENTOS_JSONL": "dados/eventos.jsonl"}
        )
        assert (config.modo, config.risco_janela_min, config.eventos_jsonl) == (
            Modo.AUDITORIA,
            60,
            "dados/eventos.jsonl",
        )

    @pytest.mark.parametrize("modo", ["", "DESLIGADO", "123"])
    def test_modo_desconhecido_protege(self, modo):
        assert carregar_config({"CYBER_MODO": modo}).modo is Modo.PROTECAO

    @pytest.mark.parametrize("janela", ["abc", "0", "-5", ""])
    def test_janela_invalida_usa_padrao(self, janela):
        assert carregar_config({"RISCO_JANELA_MIN": janela}).risco_janela_min == 1440

    def test_le_os_environ(self, monkeypatch):
        monkeypatch.setenv("CYBER_MODO", "AUDITORIA")
        assert carregar_config().modo is Modo.AUDITORIA
