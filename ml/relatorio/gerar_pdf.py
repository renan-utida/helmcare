"""Gera o ml/RELATORIO_ML.pdf a partir do ml/RELATORIO_ML.md com o Edge em modo headless.

Cópia adaptada do cyber/scripts/gerar_pdf.py, com a mesma identidade visual (azul #001D48,
laranja #FE7801, Segoe UI, tabelas com cabeçalho azul e linhas alternadas). O que muda:

- em vez de uma capa inteira, a primeira página começa com um cabeçalho compacto (tudo o
  que vem antes do primeiro título "## ": logo, título, nome, RM, turma, disciplina e
  professor), e o relatório começa logo abaixo;
- as seções não forçam quebra de página, para o relatório caber em até 4 páginas A4;
- as figuras ficam com largura reduzida e centralizadas;
- os links relativos (notebook, dados/README.md) viram links do GitHub, para funcionarem no PDF;
- o rodapé de cada página mostra o nome do relatório e o número da página.

O Markdown é a fonte única: o PDF sai dele, sem nenhum texto a mais. As imagens entram em
base64 (reduzidas, se o Pillow estiver instalado), então o PDF não depende de arquivo externo.

Uso, a partir da raiz do repositório, num ambiente com o pacote markdown (Pillow e pypdf
são opcionais):

    python ml/relatorio/gerar_pdf.py                # gera ml/RELATORIO_ML.pdf
    python ml/relatorio/gerar_pdf.py --html saida.html   # guarda também o HTML intermediário

Sem Edge nem Chrome, o script grava o HTML em ml/relatorio/ e orienta a imprimir com Ctrl+P.
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

import markdown

PASTA_ML = Path(__file__).resolve().parents[1]
RELATORIO_MD = PASTA_ML / "RELATORIO_ML.md"
RELATORIO_PDF = PASTA_ML / "RELATORIO_ML.pdf"
URL_GITHUB_ML = "https://github.com/renan-utida/helmcare/blob/main/ml/"
LARGURA_MAXIMA_IMAGEM = 1600
LIMITE_PAGINAS = 4
TITULO = "Relatório de IA e ML do HelmCare"

NAVEGADORES = (
    Path(r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"),
    Path(r"C:\Program Files\Microsoft\Edge\Application\msedge.exe"),
    Path(r"C:\Program Files\Google\Chrome\Application\chrome.exe"),
    Path(r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"),
)

TIPOS_IMAGEM = {".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".svg": "image/svg+xml"}

# Mesmas cores, fonte e estilo de títulos e tabelas do CSS do Cyber, mais compacto.
CSS = """
@page {
  size: A4;
  margin: 13mm 15mm 14mm 15mm;
  @bottom-left { content: "HelmCare | Relatório de Inteligência Artificial e Machine Learning";
    font-family: "Segoe UI", Arial, sans-serif; font-size: 7.5pt; color: #555; }
  @bottom-right { content: "página " counter(page) " de " counter(pages);
    font-family: "Segoe UI", Arial, sans-serif; font-size: 7.5pt; color: #555; }
}
html { font-size: 9.8pt; }
body { font-family: "Segoe UI", Arial, sans-serif; color: #111; line-height: 1.38; margin: 0; }
h1, h2, h3 { color: #001D48; line-height: 1.2; page-break-after: avoid; break-after: avoid; }
h2 { font-size: 1.28rem; margin: 0.9rem 0 0.35rem; border-bottom: 1px solid #c8ccd2; padding-bottom: 0.1rem; }
h3 { font-size: 1.08rem; margin: 0.7rem 0 0.3rem; }
p { margin: 0.3rem 0 0.45rem; }
ul { margin: 0.25rem 0 0.45rem; padding-left: 1.3rem; }
li { margin: 0.12rem 0; }
p, li { orphans: 3; widows: 3; }
a { color: #0b4aa8; word-break: break-word; }
blockquote { margin: 0.5rem 0; padding: 0.35rem 0.7rem; border-left: 4px solid #FE7801; background: #fff6ee;
  page-break-inside: avoid; break-inside: avoid; }
blockquote p { margin: 0.1rem 0; }
figure { margin: 0.45rem 0 0.55rem; text-align: center; page-break-inside: avoid; break-inside: avoid; }
figure img { width: 84%; max-height: 72mm; object-fit: contain; height: auto; border: 1px solid #c8ccd2; }
figcaption { font-size: 0.85rem; color: #333; margin-top: 0.15rem; font-style: italic; }
.pendente { border: 3px dashed #a4001d; color: #a4001d; font-weight: 700; padding: 1rem; margin: 0.6rem 0;
  text-align: center; page-break-inside: avoid; }
table { border-collapse: collapse; width: 100%; margin: 0.4rem 0 0.55rem; font-size: 0.86rem; table-layout: auto;
  page-break-inside: avoid; break-inside: avoid; }
tr { page-break-inside: avoid; break-inside: avoid; }
thead { display: table-header-group; }
th, td { border: 1px solid #9aa0a8; padding: 0.18rem 0.4rem; text-align: left; vertical-align: top;
  overflow-wrap: anywhere; }
th { background: #001D48; color: #fff; }
td a, td:last-child, th:last-child { white-space: nowrap; }
tr:nth-child(even) td { background: #f3f5f8; }
code { font-family: Consolas, "Courier New", monospace; font-size: 0.88em; background: #f1f2f4; padding: 0 0.15rem; }
.cabecalho { text-align: center; margin: 0 0 0.6rem; padding-bottom: 0.5rem; border-bottom: 3px solid #FE7801; }
.cabecalho p { margin: 0.12rem 0; }
.cabecalho img { width: 62mm; max-width: 100%; height: auto; margin: 0 auto 0.2rem; }
.cabecalho h1 { font-size: 1.55rem; margin: 0.15rem 0 0.1rem; border: none; padding: 0; }
.cabecalho p:nth-of-type(2) { font-size: 1.08rem; color: #333; }
.cabecalho p:nth-of-type(n+3) { font-size: 0.92rem; color: #333; }
"""

# Uma passada só: a imagem sozinha num parágrafo (o parágrafo some) ou solta no texto.
_IMAGEM = re.compile(
    r'<p>\s*<img alt="(?P<alt_p>[^"]*)" src="(?P<src_p>[^"]+)"\s*/?>\s*</p>'
    r'|<img alt="(?P<alt>[^"]*)" src="(?P<src>[^"]+)"\s*/?>'
)
# Imagem escrita em HTML no próprio Markdown (a logo: src antes do alt). Só o src muda.
_SRC_HTML = re.compile(r'(<img\b[^>]*?\bsrc=")([^"]+)(")')
# Links relativos do Markdown (notebook, dados/README.md), que no PDF viram links do GitHub.
_HREF_RELATIVO = re.compile(r'href="(?!https?://|mailto:|#)([^"]+)"')
LIMIAR_QUASE_BRANCO = 240


def _ler_imagem(caminho: Path, fundo_branco: bool = False) -> tuple[str, bytes]:
    """Tipo e bytes da imagem; PNG e JPEG grandes são reduzidos se o Pillow existir.

    Com fundo_branco (a logo), o pixel quase branco vira branco puro, como no PDF do Cyber:
    o fundo do arquivo da marca varia de 246 a 254 e apareceria como uma caixa na página.
    Só a cópia embutida muda; o arquivo em ml/marca/ continua igual ao de docs/marca/.
    """
    tipo = TIPOS_IMAGEM.get(caminho.suffix.lower(), "application/octet-stream")
    dados = caminho.read_bytes()
    if tipo not in ("image/png", "image/jpeg"):
        return tipo, dados
    try:
        from PIL import Image, ImageChops
    except ImportError:  # o Pillow é opcional
        return tipo, dados
    with Image.open(io.BytesIO(dados)) as imagem:
        if imagem.width <= LARGURA_MAXIMA_IMAGEM and not fundo_branco:
            return tipo, dados
        trabalhada = imagem.convert("RGB")
        if trabalhada.width > LARGURA_MAXIMA_IMAGEM:
            altura = round(trabalhada.height * LARGURA_MAXIMA_IMAGEM / trabalhada.width)
            trabalhada = trabalhada.resize((LARGURA_MAXIMA_IMAGEM, altura), Image.LANCZOS)
        if fundo_branco:
            canais = (c.point(lambda v: 255 if v >= LIMIAR_QUASE_BRANCO else 0) for c in trabalhada.split())
            vermelho, verde, azul = canais
            mascara = ImageChops.multiply(ImageChops.multiply(vermelho, verde), azul)
            trabalhada.paste((255, 255, 255), mask=mascara)
        saida = io.BytesIO()
        trabalhada.save(saida, format="PNG", optimize=True)
        return "image/png", saida.getvalue()


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


def separar_cabecalho(corpo_html: str) -> str:
    """Embrulha tudo o que vem antes do primeiro <h2> no cabeçalho compacto da primeira página."""
    posicao = corpo_html.find("<h2")
    if posicao <= 0:
        return corpo_html
    return f'<header class="cabecalho">\n{corpo_html[:posicao]}</header>\n{corpo_html[posicao:]}'


def montar_html(texto_md: str, pasta_base: Path) -> tuple[str, list[str]]:
    """HTML completo (com CSS e imagens embutidas) e a lista de imagens pendentes."""
    corpo = markdown.markdown(texto_md, extensions=["tables", "fenced_code"], output_format="html")
    corpo = _HREF_RELATIVO.sub(lambda m: f'href="{URL_GITHUB_ML}{m.group(1)}"', corpo)
    corpo, pendentes = embutir_imagens(corpo, pasta_base)
    corpo = separar_cabecalho(corpo)
    documento = (
        '<!doctype html>\n<html lang="pt-BR">\n<head>\n<meta charset="utf-8">\n'
        f"<title>{html.escape(TITULO)}</title>\n<style>{CSS}</style>\n</head>\n<body>\n{corpo}\n</body>\n</html>\n"
    )
    return documento, pendentes


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
        subprocess.run(comando, timeout=180, check=False, capture_output=True)
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
    parser = argparse.ArgumentParser(description="Gera o PDF do relatório de IA e ML.")
    parser.add_argument("--html", type=Path, help="guarda o HTML intermediário neste caminho")
    argumentos = parser.parse_args(argv)

    documento, pendentes = montar_html(RELATORIO_MD.read_text(encoding="utf-8"), RELATORIO_MD.parent)
    if pendentes:
        print("Imagens pendentes: " + ", ".join(pendentes))
        return 2

    navegador = encontrar_navegador()
    if navegador is None:
        html_saida = argumentos.html or Path(__file__).with_name("RELATORIO_ML.html")
        html_saida.write_text(documento, encoding="utf-8")
        print(f"Edge e Chrome não encontrados: abra {html_saida} no navegador e use Ctrl+P para salvar em PDF.")
        return 0

    with tempfile.TemporaryDirectory(prefix="helmcare-ml-") as pasta:
        html_saida = argumentos.html or Path(pasta) / "RELATORIO_ML.html"
        html_saida.write_text(documento, encoding="utf-8")
        if not gerar_pdf(navegador, html_saida, RELATORIO_PDF):
            print(f"O navegador não gerou o PDF. Navegador: {navegador}")
            return 1

    paginas = contar_paginas(RELATORIO_PDF)
    print(f"PDF: {RELATORIO_PDF}")
    print(f"Tamanho: {RELATORIO_PDF.stat().st_size / 1024:.0f} KB, {paginas} páginas")
    if paginas > LIMITE_PAGINAS:
        print(f"ATENÇÃO: passou do limite de {LIMITE_PAGINAS} páginas.")
        return 3
    return 0


if __name__ == "__main__":
    os.chdir(PASTA_ML)
    sys.exit(main())
