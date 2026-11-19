import re
from pathlib import Path

import pytest

from helmcare_cyber.api.app import PASTA_PAINEL

ARQUIVOS = ["index.html", "painel.js", "painel.css"]


@pytest.mark.parametrize("arquivo", ARQUIVOS)
def test_arquivos_servidos(cliente, arquivo):
    resposta = cliente.get(f"/painel/{arquivo}")
    assert resposta.status_code == 200
    assert "http://" not in resposta.text and "https://" not in resposta.text
    assert "innerHTML" not in resposta.text and "outerHTML" not in resposta.text
    assert "insertAdjacentHTML" not in resposta.text and "document.write" not in resposta.text


def test_index_sem_script_nem_estilo_inline():
    html = (PASTA_PAINEL / "index.html").read_text(encoding="utf-8")
    assert re.findall(r"<script[^>]*>", html) == ['<script src="painel.js" defer>']
    assert re.findall(r"<script[^>]*>(.*?)</script>", html, re.DOTALL) == [""]  # nenhum script inline
    assert "<style" not in html and " style=" not in html
    assert re.search(r"\son[a-z]+=", html) is None  # nada de onclick e afins


def test_painel_acessivel():
    html = (PASTA_PAINEL / "index.html").read_text(encoding="utf-8")
    assert '<html lang="pt-BR">' in html
    assert 'aria-live="polite"' in html and 'role="status"' in html
    assert "Pular para o conteúdo" in html


def test_js_so_com_text_content():
    js = (PASTA_PAINEL / "painel.js").read_text(encoding="utf-8")
    assert "textContent" in js
    assert re.search(r"fetch\(\s*API", js)  # só chamadas relativas à própria API


def test_rastro_volta_ao_inicio_quando_a_tabela_e_redesenhada():
    # Sem navegador nos testes: confere no código que desenharEventos limpa o rastro antes de redesenhar.
    js = (PASTA_PAINEL / "painel.js").read_text(encoding="utf-8")
    html = (PASTA_PAINEL / "index.html").read_text(encoding="utf-8")
    corpo = js.split("function desenharEventos", 1)[1].split("function ", 1)[0]
    assert corpo.index("limparRastro()") < corpo.index("replaceChildren")
    inicial = re.search(r'TEXTO_RASTRO_INICIAL = "([^"]+)"', js).group(1)
    assert f'<p id="rastro-titulo" class="ajuda">{inicial}</p>' in html


def test_sem_travessao_nos_arquivos():
    for arquivo in ARQUIVOS:
        texto = Path(PASTA_PAINEL / arquivo).read_text(encoding="utf-8")
        assert "\u2014" not in texto and "\u2013" not in texto
