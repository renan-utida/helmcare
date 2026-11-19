"""scripts/gerar_pdf.py sem abrir navegador: o Edge é trocado por uma função que escreve um PDF falso."""

import base64
import importlib.util
import io
from pathlib import Path

import pytest

# O Pillow não está no requirements.txt (o gerar_pdf funciona sem ele, só não reduz as imagens).
Image = pytest.importorskip("PIL.Image")

SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "gerar_pdf.py"


def carregar():
    especificacao = importlib.util.spec_from_file_location("script_gerar_pdf", SCRIPT)
    modulo = importlib.util.module_from_spec(especificacao)
    especificacao.loader.exec_module(modulo)
    return modulo


@pytest.fixture(scope="module")
def gp():
    return carregar()


@pytest.fixture
def relatorio(tmp_path):
    """Estrutura parecida com a real: docs/relatorio.md, evidencias/prints/ com uma imagem e a logo."""
    (tmp_path / "evidencias" / "prints").mkdir(parents=True)
    Image.new("RGB", (40, 20), (0, 29, 72)).save(tmp_path / "evidencias" / "prints" / "existe.png")
    (tmp_path / "assets" / "marca").mkdir(parents=True)
    Image.new("RGB", (60, 10), (254, 120, 1)).save(tmp_path / "assets" / "marca" / "logo-horizontal.png")
    docs = tmp_path / "docs"
    docs.mkdir()
    texto = (
        '<div class="capa">\n'
        '<p align="center"><img src="../assets/marca/logo-horizontal.png" alt="Logo" width="420"></p>\n'
        "<h1>Capa</h1>\n</div>\n\n"
        "# Título\n\n"
        "![Figura 1: imagem que existe](../evidencias/prints/existe.png)\n\n"
        "![Figura 2: imagem que falta](../evidencias/prints/P18-pytest-cobertura.png)\n\n"
        "| a | b |\n|---|---|\n| 1 | 2 |\n\n"
        "```python\nprint('ok')\n```\n"
    )
    arquivo = docs / "relatorio.md"
    arquivo.write_text(texto, encoding="utf-8")
    return arquivo


class TestMontarHtml:
    def test_figura_embutida_e_pendencia(self, gp, relatorio):
        documento, pendentes = gp.montar_html(relatorio.read_text(encoding="utf-8"), relatorio.parent)
        assert pendentes == ["evidencias/prints/P18-pytest-cobertura.png"]
        assert "IMAGEM PENDENTE: evidencias/prints/P18-pytest-cobertura.png" in documento
        assert '<figure><img alt="Figura 1: imagem que existe" src="data:image/png;base64,' in documento
        assert "<figcaption>Figura 1: imagem que existe</figcaption>" in documento
        assert "../evidencias/prints/existe.png" not in documento

    def test_acha_as_imagens_em_prints(self, gp, relatorio):
        dados = (relatorio.parent.parent / "evidencias" / "prints" / "existe.png").read_bytes()
        documento, _ = gp.montar_html(relatorio.read_text(encoding="utf-8"), relatorio.parent)
        assert f"data:image/png;base64,{base64.b64encode(dados).decode('ascii')}" in documento

    def test_logo_da_capa_embutida(self, gp, relatorio):
        documento, pendentes = gp.montar_html(relatorio.read_text(encoding="utf-8"), relatorio.parent)
        capa = documento.split('<div class="capa">')[1].split("</div>")[0]
        assert '<img src="data:image/png;base64,' in capa and 'width="420"' in capa
        assert "assets/marca" not in documento
        assert "assets/marca/logo-horizontal.png" not in pendentes

    def test_logo_com_fundo_quase_branco_vira_branco(self, gp, tmp_path):
        logo = Image.new("RGB", (20, 10), (246, 250, 253))
        logo.paste((0, 29, 72), (5, 2, 15, 8))
        logo.save(tmp_path / "logo.png")
        original = (tmp_path / "logo.png").read_bytes()
        corpo, _ = gp.embutir_imagens('<img src="logo.png" alt="Logo">', tmp_path)
        embutida = Image.open(io.BytesIO(base64.b64decode(corpo.split("base64,")[1].split('"')[0])))
        assert embutida.getpixel((0, 0)) == (255, 255, 255)
        assert embutida.getpixel((10, 5)) == (0, 29, 72)
        assert (tmp_path / "logo.png").read_bytes() == original  # o arquivo não muda

    def test_figura_nao_tem_o_fundo_trocado(self, gp, tmp_path):
        Image.new("RGB", (4, 4), (246, 246, 246)).save(tmp_path / "f.png")
        corpo, _ = gp.embutir_imagens('<p><img alt="f" src="f.png" /></p>', tmp_path)
        assert corpo.split("base64,")[1].split('"')[0] == base64.b64encode((tmp_path / "f.png").read_bytes()).decode()

    def test_logo_que_falta_vira_pendencia(self, gp, tmp_path):
        corpo, pendentes = gp.embutir_imagens('<img src="../assets/marca/logo.png" alt="Logo">', tmp_path)
        assert pendentes == ["assets/marca/logo.png"] and 'src="../assets/marca/logo.png"' in corpo

    @pytest.mark.parametrize(
        "src, rotulo",
        [
            ("../evidencias/prints/P01-estrutura.png", "evidencias/prints/P01-estrutura.png"),
            ("./prints/P18.png", "prints/P18.png"),
            ("..\\evidencias\\prints\\P01.png", "evidencias/prints/P01.png"),
            ("P02a.png", "P02a.png"),
        ],
    )
    def test_rotulo_pendente(self, gp, src, rotulo):
        assert gp.rotulo_pendente(src) == rotulo

    def test_cada_figura_tem_uma_legenda_so(self, gp, relatorio):
        documento, _ = gp.montar_html(relatorio.read_text(encoding="utf-8"), relatorio.parent)
        assert documento.count("<figcaption>Figura 1: imagem que existe</figcaption>") == 1
        assert documento.count("<figure>") == documento.count("</figure>") == 1
        assert "<p><figure>" not in documento

    def test_tabela_codigo_capa_e_css(self, gp, relatorio):
        documento, _ = gp.montar_html(relatorio.read_text(encoding="utf-8"), relatorio.parent)
        assert "<table>" in documento and "<th>a</th>" in documento
        assert 'class="codehilite"' in documento
        assert '<div class="capa">' in documento
        assert "@page { size: A4" in documento and "page-break-inside: avoid" in documento
        assert documento.startswith('<!doctype html>\n<html lang="pt-BR">')

    def test_imagem_grande_e_reduzida(self, gp, tmp_path):
        Image.new("RGB", (4000, 1000), (255, 255, 255)).save(tmp_path / "grande.png")
        corpo, pendentes = gp.embutir_imagens('<p><img alt="g" src="grande.png" /></p>', tmp_path)
        dados = base64.b64decode(corpo.split("base64,")[1].split('"')[0])
        assert pendentes == []
        assert dados.startswith(b"\x89PNG")
        largura = int.from_bytes(dados[16:20], "big")
        assert largura == gp.LARGURA_MAXIMA_IMAGEM

    def test_imagem_pequena_fica_como_esta(self, gp, tmp_path):
        Image.new("RGB", (10, 10)).save(tmp_path / "p.jpg")
        corpo, _ = gp.embutir_imagens('<img alt="p" src="p.jpg" />', tmp_path)
        assert "data:image/jpeg;base64," in corpo

    def test_svg_e_data_uri(self, gp, tmp_path):
        (tmp_path / "s.svg").write_text("<svg xmlns='http://www.w3.org/2000/svg'/>", encoding="utf-8")
        corpo, _ = gp.embutir_imagens('<img alt="s" src="s.svg" /><img alt="d" src="data:image/png;base64,AA" />', tmp_path)
        assert "data:image/svg+xml;base64," in corpo and 'src="data:image/png;base64,AA"' in corpo


class TestNavegador:
    def test_encontrar(self, gp, tmp_path):
        falso = tmp_path / "msedge.exe"
        falso.write_text("x")
        assert gp.encontrar_navegador([tmp_path / "nao.exe", falso]) == falso
        assert gp.encontrar_navegador([tmp_path / "nao.exe"]) is None

    def test_comando(self, gp, tmp_path):
        comando = gp.comando_pdf(Path("edge.exe"), tmp_path / "r.html", tmp_path / "r.pdf", tmp_path / "perfil")
        assert "--headless=new" in comando and any(c.startswith("--print-to-pdf=") for c in comando)
        assert comando[-1].startswith("file:///")

    def test_contar_paginas(self, gp):
        assert gp.contar_paginas(b"<< /Type /Pages >> << /Type /Page >> << /Type/Page >>") == 2


def edge_falso(paginas: int = 3, escrever: bool = True):
    """Substitui o subprocess.run: escreve um PDF mínimo no caminho do --print-to-pdf."""
    chamadas = []

    def executar(comando, **opcoes):
        chamadas.append(comando)
        if escrever:
            destino = next(c.split("=", 1)[1] for c in comando if c.startswith("--print-to-pdf="))
            Path(destino).write_bytes(b"%PDF-1.4 " + b"<< /Type /Page >> " * paginas + b"<< /Type /Pages >>")

    return executar, chamadas


class TestMain:
    def test_gera_pdf_com_pendencia(self, gp, relatorio, tmp_path, capsys):
        executar, chamadas = edge_falso(4)
        saida = tmp_path / "docs" / "r.pdf"
        codigo = gp.main([], navegador=Path("edge.exe"), executar=executar, entrada=relatorio, saida=saida,
                         pasta_dist=tmp_path / "dist", raiz=tmp_path)
        texto = capsys.readouterr().out
        assert codigo == 0 and saida.is_file() and len(chamadas) == 1
        assert "IMAGEM PENDENTE: evidencias/prints/P18-pytest-cobertura.png" in texto and "4 páginas" in texto
        assert "IMAGEM PENDENTE: evidencias/prints/P01-estrutura.png" in texto  # obrigatório, mesmo sem citação
        assert texto.count("P18-pytest-cobertura.png") == 1  # citado e obrigatório: aparece uma vez só
        assert (tmp_path / "dist" / "relatorio.html").is_file()

    def test_estrito_falha_sem_chamar_o_navegador(self, gp, relatorio, tmp_path):
        executar, chamadas = edge_falso()
        codigo = gp.main(["--estrito"], navegador=Path("edge.exe"), executar=executar, entrada=relatorio,
                         saida=tmp_path / "r.pdf", pasta_dist=tmp_path / "dist", raiz=tmp_path)
        assert codigo == 2 and chamadas == []

    def test_estrito_passa_sem_pendencia(self, gp, tmp_path):
        arquivo = tmp_path / "r.md"
        arquivo.write_text("# Só texto\n", encoding="utf-8")
        executar, _ = edge_falso(1)
        assert gp.main(["--estrito"], navegador=Path("edge.exe"), executar=executar, entrada=arquivo,
                       saida=tmp_path / "r.pdf", pasta_dist=tmp_path / "dist", raiz=tmp_path, obrigatorios=()) == 0

    def test_estrito_exige_os_prints_de_evidencias_prints(self, gp, tmp_path, capsys):
        arquivo = tmp_path / "r.md"
        arquivo.write_text("# Só texto\n", encoding="utf-8")
        executar, chamadas = edge_falso(1)
        argumentos = dict(navegador=Path("edge.exe"), executar=executar, entrada=arquivo, saida=tmp_path / "r.pdf",
                          pasta_dist=tmp_path / "dist", raiz=tmp_path)
        assert gp.main(["--estrito"], **argumentos) == 2 and chamadas == []
        saida = capsys.readouterr().out
        assert all(f"IMAGEM PENDENTE: {p}" in saida for p in gp.PRINTS_OBRIGATORIOS)
        assert all(p.startswith("evidencias/prints/") for p in gp.PRINTS_OBRIGATORIOS)
        for p in gp.PRINTS_OBRIGATORIOS:
            (tmp_path / p).parent.mkdir(parents=True, exist_ok=True)
            Image.new("RGB", (4, 4)).save(tmp_path / p)
        assert gp.main(["--estrito"], **argumentos) == 0 and len(chamadas) == 1

    def test_navegador_nao_gerou(self, gp, relatorio, tmp_path):
        executar, _ = edge_falso(escrever=False)
        saida = tmp_path / "r.pdf"
        saida.write_bytes(b"antigo")
        assert gp.main([], navegador=Path("edge.exe"), executar=executar, entrada=relatorio, saida=saida,
                       pasta_dist=tmp_path / "dist", raiz=tmp_path) == 1
        assert not saida.exists()

    def test_sem_navegador_gera_so_o_html(self, gp, relatorio, tmp_path, monkeypatch, capsys):
        monkeypatch.setattr(gp, "encontrar_navegador", lambda: None)
        assert gp.main([], entrada=relatorio, saida=tmp_path / "r.pdf", pasta_dist=tmp_path / "dist",
                       raiz=tmp_path) == 0
        assert "Ctrl+P" in capsys.readouterr().out
