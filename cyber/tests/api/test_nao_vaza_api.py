"""Nenhuma resposta de nenhuma rota leva valor simulado original, em nenhum cenário e em nenhum modo."""

import pytest

from helmcare_cyber.simulacao import dados_simulados as dados
from helmcare_cyber.simulacao.cenarios import CENARIOS
from tests.api.conftest import API, novo_cliente, tradutor_com_ia


def respostas_de_tudo(modo: str, tradutor) -> list[str]:
    cliente = novo_cliente(modo, tradutor)
    textos = []

    def guardar(resposta):
        assert resposta.status_code < 500, resposta.text
        textos.append(resposta.text)
        textos.append(str(dict(resposta.headers)))

    guardar(cliente.post(f"{API}/demo/entregadores", json=dados.cadastro_entregador()))
    guardar(cliente.post(f"{API}/pii/analisar", json=dados.cadastro_entregador()))
    guardar(cliente.post(f"{API}/pii/analisar", json=dados.resposta_cofre(True)))
    guardar(cliente.post(f"{API}/pii/analisar", json=dados.resposta_dispositivo()))
    guardar(cliente.post(f"{API}/pii/analisar", json=dados.ocorrencia_sos()))
    guardar(cliente.post(f"{API}/demo/entregadores", json=dados.cadastro_entregador() | {"cpf": "1"}))
    for cenario in CENARIOS:
        guardar(cliente.post(f"{API}/simulacoes/{cenario.id}"))
    guardar(cliente.post(f"{API}/simulacoes/todas"))
    guardar(cliente.get(f"{API}/eventos?limite=200"))
    guardar(cliente.get(f"{API}/eventos?limite=200&traduzir=true"))
    guardar(cliente.get(f"{API}/risco-cidadao"))
    guardar(cliente.get(f"{API}/risco-cidadao?traduzir=false"))
    guardar(cliente.get(f"{API}/saude"))
    guardar(cliente.get(f"{API}/modo"))
    guardar(cliente.get(f"{API}/simulacoes"))
    guardar(cliente.post(f"{API}/reiniciar"))
    return textos


@pytest.mark.parametrize("modo", ["PROTECAO", "AUDITORIA"])
@pytest.mark.parametrize("com_ia", [False, True], ids=["reserva", "ia-simulada"])
def test_nenhum_valor_original_nas_respostas(modo, com_ia):
    tradutor = tradutor_com_ia() if com_ia else None
    tudo = "\n".join(respostas_de_tudo(modo, tradutor))
    vazados = [valor for valor in dados.VALORES_SENSIVEIS if valor in tudo]
    assert vazados == []
