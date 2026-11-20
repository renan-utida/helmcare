"""Gera o docs/HelmCare_Testing_GS_RM558540.pdf a partir do docs/relatorio-testing.md com o Edge headless.

Segue o mesmo pipeline do cyber/scripts/gerar_pdf.py e do ml/relatorio/gerar_pdf.py, com a mesma
identidade visual (azul-marinho #001D48, laranja #FE7801, Segoe UI, tabelas com cabeçalho azul e
linhas alternadas, capa com a logo e a faixa laranja). O que muda:

- a página é A4 retrato e cada print do Azure DevOps ocupa a largura útil da página, sem distorcer
  a proporção; o conteúdo flui, então um print pode dividir a página com a tabela que vem depois;
- os prints entram em base64 na resolução original (só recomprimidos em PNG), sem redução, para o
  texto continuar nítido quando o PDF é ampliado;
- só as seções marcadas com {.pagina} (Detalhe das Features, Test Cases e Cobertura) começam numa
  página nova; título e print ficam sempre juntos;
- os links relativos (test-cases/, evidencias/, ../prompts.md) viram links do GitHub;
- o rodapé de cada página mostra o nome do relatório e o número da página;
- antes de imprimir, o script confere que não há travessão (U+2014 nem U+2013) no Markdown nem no HTML.

O Markdown é a fonte única: o PDF sai dele, sem nenhum texto a mais, e não depende de nenhum arquivo
fora de docs/. O link do pitch fica como o texto {{LINK_PITCH}} até ser trocado no Markdown.

Uso, a partir da raiz do repositório, com um Python que tenha markdown, Pygments e Pillow
(por exemplo o venv do cyber):

    cyber/.venv/Scripts/python.exe docs/scripts/gerar_pdf_testing.py
    cyber/.venv/Scripts/python.exe docs/scripts/gerar_pdf_testing.py --html saida.html   # guarda o HTML

Sem Edge nem Chrome, o script grava o HTML ao lado do PDF e orienta a imprimir com Ctrl+P.
"""

import argparse
import base64
import html
import io
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path
from urllib.parse import urljoin

import markdown

PASTA_DOCS = Path(__file__).resolve().parents[1]
RELATORIO_MD = PASTA_DOCS / "relatorio-testing.md"
RELATORIO_PDF = PASTA_DOCS / "HelmCare_Testing_GS_RM558540.pdf"
URL_GITHUB_DOCS = "https://github.com/renan-utida/helmcare/blob/main/docs/"
URL_GITHUB_PASTA = "https://github.com/renan-utida/helmcare/tree/main/docs/"
TITULO = "HelmCare | Testing, Compliance e Quality Assurance"
LIMITE_MB = 25
TRAVESSOES = ("\u2014", "\u2013")

NAVEGADORES = (
    Path(r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"),
    Path(r"C:\Program Files\Microsoft\Edge\Application\msedge.exe"),
    Path(r"C:\Program Files\Google\Chrome\Application\chrome.exe"),
    Path(r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"),
)

TIPOS_IMAGEM = {".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".svg": "image/svg+xml"}

# Mesmas cores, fonte e estilo de títulos, tabelas e capa do CSS do Cyber, em A4 retrato.
CSS = """
@page {
  size: A4;
  margin: 12mm 13mm 13mm 13mm;
  @bottom-left { content: "HelmCare | Testing, Compliance e Quality Assurance";
    font-family: "Segoe UI", Arial, sans-serif; font-size: 7.5pt; color: #555; }
  @bottom-right { content: "página " counter(page) " de " counter(pages);
    font-family: "Segoe UI", Arial, sans-serif; font-size: 7.5pt; color: #555; }
}
@page :first { @bottom-left { content: none; } @bottom-right { content: none; } }
html { font-size: 9.8pt; }
body { font-family: "Segoe UI", Arial, sans-serif; color: #111; line-height: 1.38; margin: 0; }
h1, h2, h3 { color: #001D48; line-height: 1.2; page-break-after: avoid; break-after: avoid; }
h2 { font-size: 1.4rem; margin: 1.1rem 0 0.45rem; border-bottom: 3px solid #FE7801; padding-bottom: 0.12rem; }
h2.pagina { page-break-before: always; break-before: page; margin-top: 0; }
h3 { font-size: 1.08rem; margin: 0.8rem 0 0.3rem; border-bottom: 1px solid #c8ccd2; padding-bottom: 0.08rem; }
h2 + h3 { margin-top: 0.4rem; }
h3 + p { page-break-after: avoid; break-after: avoid; }
p { margin: 0.25rem 0 0.4rem; }
ul { margin: 0.2rem 0 0.4rem; padding-left: 1.2rem; }
li { margin: 0.1rem 0; }
p, li { orphans: 3; widows: 3; }
a { color: #0b4aa8; word-break: break-word; }
figure { margin: 0.3rem 0 0.45rem; text-align: center; page-break-inside: avoid; break-inside: avoid; }
figure img { width: 100%; max-height: 130mm; object-fit: contain; height: auto; border: 1px solid #c8ccd2; }
figcaption { font-size: 0.84rem; color: #333; margin-top: 0.15rem; font-style: italic; }
.pendente { border: 3px dashed #a4001d; color: #a4001d; font-weight: 700; padding: 1.2rem; margin: 0.8rem 0;
  text-align: center; page-break-inside: avoid; }
table { border-collapse: collapse; width: 100%; margin: 0.3rem 0 0.5rem; font-size: 0.84rem; page-break-inside: auto; }
tr { page-break-inside: avoid; break-inside: avoid; }
thead { display: table-header-group; }
th, td { border: 1px solid #9aa0a8; padding: 0.22rem 0.4rem; text-align: left; vertical-align: top;
  overflow-wrap: anywhere; }
th { background: #001D48; color: #fff; }
th:first-child { white-space: nowrap; overflow-wrap: normal; }
/* Tabela-resumo dos Test Cases (a única com 6 colunas): TC, ID, Feature e Polaridade sem quebra. */
table:has(th:nth-child(6)) th:nth-child(-n+4), table:has(th:nth-child(6)) td:nth-child(-n+4) {
  white-space: nowrap; overflow-wrap: normal; }
tr:nth-child(even) td { background: #f3f5f8; }
code { font-family: Consolas, "Courier New", monospace; font-size: 0.88em; background: #f1f2f4; padding: 0 0.15rem; }
.capa { page-break-after: always; break-after: page; text-align: center; padding-top: 28mm; }
.capa img { width: 120mm; max-width: 100%; height: auto; margin-bottom: 8mm; }
.capa h1 { border: none; font-size: 2.3rem; margin: 0 0 0.4rem; }
.capa .subtitulo { font-size: 1.2rem; color: #333; margin: 0 0 0.4rem; }
.capa .slogan { font-size: 1.12rem; color: #B45300; font-style: italic; font-weight: 600; margin: 0 0 12mm; }
.capa .faixa { width: 60mm; height: 4px; background: #FE7801; margin: 0 auto 12mm; }
.capa p { margin: 0.25rem 0; font-size: 1.05rem; }
.capa .links { margin: 14mm auto 0; display: inline-block; text-align: left; border-left: 4px solid #FE7801;
  background: #F2F5FA; padding: 0.5rem 0.9rem; }
.capa .links p { font-size: 0.9rem; }
.capa .rodape-capa { margin-top: 22mm; font-size: 0.95rem; color: #333; }
.sumario h2 { margin-top: 0; }
.sumario ol { font-size: 1.05rem; line-height: 1.7; padding-left: 1.6rem; margin: 0.3rem 0 0.6rem; }
.sumario a { color: #001D48; text-decoration: none; }
"""

# Uma passada só: a imagem sozinha num parágrafo (o parágrafo some) ou solta no texto.
_IMAGEM = re.compile(
    r'<p>\s*<img alt="(?P<alt_p>[^"]*)" src="(?P<src_p>[^"]+)"\s*/?>\s*</p>'
    r'|<img alt="(?P<alt>[^"]*)" src="(?P<src>[^"]+)"\s*/?>'
)
# Imagem escrita em HTML no próprio Markdown (a logo da capa: src antes do alt). Só o src muda.
_SRC_HTML = re.compile(r'(<img\b[^>]*?\bsrc=")([^"]+)(")')
# Links relativos do Markdown, que no PDF viram links do GitHub.
_HREF_RELATIVO = re.compile(r'href="(?!https?://|mailto:|#)([^"]+)"')
LIMIAR_QUASE_BRANCO = 240


def _ler_imagem(caminho: Path, fundo_branco: bool = False) -> tuple[str, bytes]:
    """Tipo e bytes da imagem, recomprimida em PNG se o Pillow existir (sem reduzir a resolução).

    Com fundo_branco (a logo), o pixel quase branco vira branco puro, como nos PDFs do Cyber e do ML:
    o fundo do arquivo da marca varia de 246 a 254 e apareceria como uma caixa na página.
    Só a cópia embutida muda; o arquivo em docs/marca/ continua igual.
    """
    tipo = TIPOS_IMAGEM.get(caminho.suffix.lower(), "application/octet-stream")
    dados = caminho.read_bytes()
    if tipo != "image/png":
        return tipo, dados
    try:
        from PIL import Image, ImageChops
    except ImportError:  # o Pillow é opcional
        return tipo, dados
    with Image.open(io.BytesIO(dados)) as imagem:
        trabalhada = imagem.convert("RGB")
    if fundo_branco:
        vermelho, verde, azul = (c.point(lambda v: 255 if v >= LIMIAR_QUASE_BRANCO else 0) for c in trabalhada.split())
        mascara = ImageChops.multiply(ImageChops.multiply(vermelho, verde), azul)
        trabalhada.paste((255, 255, 255), mask=mascara)
    saida = io.BytesIO()
    trabalhada.save(saida, format="PNG", optimize=True)
    return ("image/png", saida.getvalue()) if len(saida.getvalue()) < len(dados) or fundo_branco else (tipo, dados)


def _figura(alt: str, src: str, pasta_base: Path, pendentes: list[str]) -> str:
    caminho = (pasta_base / src).resolve()
    if not caminho.is_file():
        pendentes.append(src)
        return f'<div class="pendente">{html.escape("IMAGEM PENDENTE: " + src)}</div>'
    tipo, dados = _ler_imagem(caminho)
    embutida = base64.b64encode(dados).decode("ascii")
    return f'<figure><img alt="{alt}" src="data:{tipo};base64,{embutida}"><figcaption>{alt}</figcaption></figure>'


def embutir_imagens(corpo_html: str, pasta_base: Path) -> tuple[str, list[str]]:
    """Troca cada imagem do Markdown por uma figura em base64 com legenda (a logo só troca o src)."""
    pendentes: list[str] = []

    def trocar(m: re.Match) -> str:
        alt = m.group("alt_p") if m.group("src_p") else m.group("alt")
        return _figura(alt, m.group("src_p") or m.group("src"), pasta_base, pendentes)

    def trocar_src(m: re.Match) -> str:
        src = m.group(2)
        if src.startswith(("data:", "http://", "https://")):
            return m.group(0)
        caminho = (pasta_base / src).resolve()
        if not caminho.is_file():
            pendentes.append(src)
            return m.group(0)
        tipo, dados = _ler_imagem(caminho, fundo_branco=True)
        return f"{m.group(1)}data:{tipo};base64,{base64.b64encode(dados).decode('ascii')}{m.group(3)}"

    corpo = _IMAGEM.sub(trocar, corpo_html)
    return _SRC_HTML.sub(trocar_src, corpo), pendentes


def link_github(relativo: str) -> str:
    """Link do GitHub para um caminho relativo a docs/ (pasta termina em /, arquivo não)."""
    base = URL_GITHUB_PASTA if relativo.endswith("/") else URL_GITHUB_DOCS
    return urljoin(base, relativo)


def montar_html(texto_md: str, pasta_base: Path) -> tuple[str, list[str]]:
    """HTML completo (com CSS e imagens embutidas) e a lista de imagens pendentes."""
    corpo = markdown.markdown(
        texto_md,
        extensions=["tables", "fenced_code", "codehilite", "attr_list"],
        extension_configs={"codehilite": {"guess_lang": False, "noclasses": True}},
        output_format="html",
    )
    corpo = _HREF_RELATIVO.sub(lambda m: f'href="{link_github(m.group(1))}"', corpo)
    corpo, pendentes = embutir_imagens(corpo, pasta_base)
    documento = (
        '<!doctype html>\n<html lang="pt-BR">\n<head>\n<meta charset="utf-8">\n'
        f"<title>{html.escape(TITULO)}</title>\n<style>{CSS}</style>\n</head>\n<body>\n{corpo}\n</body>\n</html>\n"
    )
    return documento, pendentes


def achar_travessoes(texto: str) -> list[int]:
    """Números das linhas que têm travessão (longo ou médio)."""
    return [n for n, linha in enumerate(texto.splitlines(), 1) if any(t in linha for t in TRAVESSOES)]


def encontrar_navegador() -> Path | None:
    """O primeiro Edge (ou Chrome) instalado, ou None."""
    return next((c for c in NAVEGADORES if c.is_file()), None)


def gerar_pdf(navegador: Path, html_entrada: Path, pdf_saida: Path) -> bool:
    """Imprime o HTML em PDF com o navegador. Devolve se o PDF saiu."""
    if pdf_saida.exists():
        pdf_saida.unlink()
    with tempfile.TemporaryDirectory(prefix="helmcare-pdf-") as perfil:
        comando = [
            str(navegador), "--headless=new", "--disable-gpu", "--no-first-run", "--no-default-browser-check",
            f"--user-data-dir={perfil}", "--no-pdf-header-footer", "--print-to-pdf-no-header",
            f"--print-to-pdf={pdf_saida}", html_entrada.resolve().as_uri(),
        ]
        subprocess.run(comando, timeout=300, check=False, capture_output=True)
    return pdf_saida.is_file() and pdf_saida.stat().st_size > 0


def contar_paginas(pdf: Path) -> int:
    """Páginas do PDF: pelo pypdf, se existir, ou pelos objetos /Type /Page."""
    try:
        from pypdf import PdfReader
    except ImportError:
        return len(re.findall(rb"/Type\s*/Page(?!s)", pdf.read_bytes()))
    return len(PdfReader(pdf).pages)


def main(argv: list[str] | None = None) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description="Gera o PDF de Testing, Compliance e QA do HelmCare.")
    parser.add_argument("--html", type=Path, help="guarda o HTML intermediário neste caminho")
    argumentos = parser.parse_args(argv)

    texto_md = RELATORIO_MD.read_text(encoding="utf-8")
    documento, pendentes = montar_html(texto_md, RELATORIO_MD.parent)
    if pendentes:
        print("Imagens pendentes: " + ", ".join(pendentes))
        return 2
    linhas_md, linhas_html = achar_travessoes(texto_md), achar_travessoes(documento)
    if linhas_md or linhas_html:
        print(f"Travessão encontrado. Linhas do Markdown: {linhas_md}. Linhas do HTML: {linhas_html}.")
        return 4

    navegador = encontrar_navegador()
    if navegador is None:
        html_saida = argumentos.html or RELATORIO_PDF.with_suffix(".html")
        html_saida.write_text(documento, encoding="utf-8")
        print(f"Edge e Chrome não encontrados: abra {html_saida} no navegador e use Ctrl+P para salvar em PDF.")
        return 0

    with tempfile.TemporaryDirectory(prefix="helmcare-testing-") as pasta:
        html_saida = argumentos.html or Path(pasta) / "relatorio-testing.html"
        html_saida.write_text(documento, encoding="utf-8")
        if not gerar_pdf(navegador, html_saida, RELATORIO_PDF):
            print(f"O navegador não gerou o PDF. Abra o HTML e use Ctrl+P. Navegador: {navegador}")
            return 1

    tamanho_mb = RELATORIO_PDF.stat().st_size / (1024 * 1024)
    print(f"PDF: {RELATORIO_PDF}")
    print(f"Tamanho: {tamanho_mb:.1f} MB, {contar_paginas(RELATORIO_PDF)} páginas")
    if tamanho_mb >= LIMITE_MB:
        print(f"ATENÇÃO: o PDF passou de {LIMITE_MB} MB.")
        return 3
    return 0


if __name__ == "__main__":
    os.chdir(PASTA_DOCS)
    sys.exit(main())
