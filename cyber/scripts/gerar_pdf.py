"""Gera o PDF do relatório (docs/relatorio-cyber.md) com o Edge em modo headless.

Uso, dentro de cyber/ com o venv ativo:

    python scripts/gerar_pdf.py              # imagem que falta vira o aviso "IMAGEM PENDENTE"
    python scripts/gerar_pdf.py --estrito    # imagem que falta faz o script falhar

O Markdown vira HTML (extensões tables, fenced_code e codehilite com Pygments),
as imagens entram em base64 (reduzidas, se o Pillow estiver instalado, para o
PDF não ficar enorme) e um CSS para A4 cuida de margens, figuras, tabelas e
código. A logo da capa (assets/marca/) também entra em base64, então o PDF e o
HTML não dependem de nenhum arquivo externo. O HTML intermediário fica em dist/.
Sem Edge nem Chrome, o script gera só o HTML e explica como imprimir com Ctrl+P.

Os prints ficam em evidencias/prints/. No modo --estrito, além das imagens
citadas no relatório, os prints de PRINTS_OBRIGATORIOS precisam existir.
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
from collections.abc import Callable
from pathlib import Path, PurePosixPath

import markdown

RAIZ = Path(__file__).resolve().parents[1]
RELATORIO_MD = RAIZ / "docs" / "relatorio-cyber.md"
RELATORIO_PDF = RAIZ / "docs" / "relatorio-cyber.pdf"
PASTA_DIST = RAIZ / "dist"
LARGURA_MAXIMA_IMAGEM = 1600

# Prints que o relatório espera em evidencias/prints/ (caminhos relativos a cyber/).
PRINTS_OBRIGATORIOS = (
    "evidencias/prints/P01-estrutura.png",
    "evidencias/prints/P18-pytest-cobertura.png",
)

NAVEGADORES = (
    Path(r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"),
    Path(r"C:\Program Files\Microsoft\Edge\Application\msedge.exe"),
    Path(r"C:\Program Files\Google\Chrome\Application\chrome.exe"),
    Path(r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"),
)

TIPOS_IMAGEM = {".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".svg": "image/svg+xml"}

CSS = """
@page { size: A4; margin: 18mm 16mm 18mm 16mm; }
html { font-size: 10.5pt; }
body { font-family: "Segoe UI", Arial, sans-serif; color: #111; line-height: 1.45; }
h1, h2, h3 { color: #001D48; line-height: 1.2; page-break-after: avoid; break-after: avoid; }
h1 { font-size: 1.9rem; border-bottom: 3px solid #FE7801; padding-bottom: 0.2rem; }
h2 { font-size: 1.45rem; margin-top: 1.6rem; border-bottom: 1px solid #c8ccd2; padding-bottom: 0.15rem; }
h2 { page-break-before: always; break-before: page; }
h1 + h2 { page-break-before: auto; break-before: auto; }
h3 { font-size: 1.15rem; margin-top: 1.1rem; }
p, li { orphans: 3; widows: 3; }
a { color: #0b4aa8; word-break: break-all; }
figure { margin: 0.8rem 0; text-align: center; page-break-inside: avoid; break-inside: avoid; }
figure img { max-width: 100%; max-height: 205mm; height: auto; border: 1px solid #c8ccd2; }
figcaption { font-size: 0.9rem; color: #333; margin-top: 0.3rem; font-style: italic; }
.pendente { border: 3px dashed #a4001d; color: #a4001d; font-weight: 700; padding: 1.2rem; margin: 0.8rem 0;
  text-align: center; page-break-inside: avoid; }
table { border-collapse: collapse; width: 100%; margin: 0.6rem 0; font-size: 0.85rem; page-break-inside: auto; }
tr { page-break-inside: avoid; break-inside: avoid; }
thead { display: table-header-group; }
th, td { border: 1px solid #9aa0a8; padding: 0.25rem 0.4rem; text-align: left; vertical-align: top; }
th { background: #001D48; color: #fff; }
tr:nth-child(even) td { background: #f3f5f8; }
code { font-family: Consolas, "Courier New", monospace; font-size: 0.88em; background: #f1f2f4; padding: 0 0.15rem; }
pre, .codehilite { white-space: pre-wrap; word-break: break-word; overflow-wrap: anywhere; background: #f6f8fa;
  border: 1px solid #d0d7de; border-radius: 4px; padding: 0.5rem 0.7rem; font-size: 0.82rem; page-break-inside: avoid; }
pre code { background: none; padding: 0; }
.capa { page-break-after: always; break-after: page; text-align: center; padding-top: 30mm; }
.capa img { width: 120mm; max-width: 100%; height: auto; margin-bottom: 10mm; }
.capa h1 { border: none; font-size: 2.4rem; margin-bottom: 0.4rem; }
.capa .subtitulo { font-size: 1.3rem; color: #333; margin-bottom: 18mm; }
.capa .faixa { width: 60mm; height: 4px; background: #FE7801; margin: 0 auto 14mm; }
.capa p { margin: 0.25rem 0; font-size: 1.05rem; }
.capa .rodape-capa { margin-top: 30mm; font-size: 0.95rem; color: #333; }
"""

# Uma passada só: a imagem sozinha num parágrafo (o parágrafo some) ou solta no texto.
# Duas passadas embrulhariam de novo a imagem que a primeira já pôs na figura.
_IMAGEM = re.compile(
    r'<p>\s*<img alt="(?P<alt_p>[^"]*)" src="(?P<src_p>[^"]+)"\s*/?>\s*</p>'
    r'|<img alt="(?P<alt>[^"]*)" src="(?P<src>[^"]+)"\s*/?>'
)
# Imagem escrita em HTML no próprio Markdown (como a logo: src antes do alt). Só o src muda.
_SRC_HTML = re.compile(r'(<img\b[^>]*?\bsrc=")([^"]+)(")')


def rotulo_pendente(src: str) -> str:
    """Caminho da imagem sem os "../" do começo: ../evidencias/prints/P01.png vira evidencias/prints/P01.png."""
    partes = list(PurePosixPath(src.replace("\\", "/")).parts)
    while partes and partes[0] in ("..", "."):
        partes.pop(0)
    return "/".join(partes)


LIMIAR_QUASE_BRANCO = 240


def _ler_imagem(caminho: Path, fundo_branco: bool = False) -> tuple[str, bytes]:
    """Tipo e bytes da imagem; PNG e JPEG grandes são reduzidos se o Pillow existir.

    Com fundo_branco (a logo da capa), o pixel quase branco vira branco puro: o fundo do
    arquivo da marca varia de 246 a 254 e apareceria como uma caixa na página branca.
    Só a cópia embutida muda; o arquivo continua igual ao de docs/marca/.
    """
    tipo = TIPOS_IMAGEM.get(caminho.suffix.lower(), "application/octet-stream")
    dados = caminho.read_bytes()
    if tipo not in ("image/png", "image/jpeg"):
        return tipo, dados
    try:
        from PIL import Image
    except ImportError:  # pragma: no cover (o Pillow é opcional)
        return tipo, dados
    with Image.open(io.BytesIO(dados)) as imagem:
        if imagem.width <= LARGURA_MAXIMA_IMAGEM and not fundo_branco:
            return tipo, dados
        trabalhada = imagem.convert("RGB")
        if trabalhada.width > LARGURA_MAXIMA_IMAGEM:
            altura = round(trabalhada.height * LARGURA_MAXIMA_IMAGEM / trabalhada.width)
            trabalhada = trabalhada.resize((LARGURA_MAXIMA_IMAGEM, altura), Image.LANCZOS)
        if fundo_branco:
            from PIL import ImageChops

            # 255 só onde os três canais passam do limiar (multiplicar máscaras 0/255 é um "e").
            vermelho, verde, azul = (c.point(lambda v: 255 if v >= LIMIAR_QUASE_BRANCO else 0) for c in trabalhada.split())
            mascara = ImageChops.multiply(ImageChops.multiply(vermelho, verde), azul)
            trabalhada.paste((255, 255, 255), mask=mascara)
        saida = io.BytesIO()
        trabalhada.save(saida, format="PNG", optimize=True)
        return "image/png", saida.getvalue()


def _figura(alt: str, src: str, pasta_base: Path, pendentes: list[str]) -> str:
    if src.startswith("data:"):
        return f'<figure><img alt="{alt}" src="{src}"><figcaption>{alt}</figcaption></figure>'
    caminho = (pasta_base / src).resolve()
    if not caminho.is_file():
        pendentes.append(rotulo_pendente(src))
        aviso = html.escape(f"IMAGEM PENDENTE: {rotulo_pendente(src)}")
        return f'<div class="pendente">{aviso}</div><p class="legenda-pendente"><em>{alt}</em></p>'
    tipo, dados = _ler_imagem(caminho)
    embutida = base64.b64encode(dados).decode("ascii")
    return f'<figure><img alt="{alt}" src="data:{tipo};base64,{embutida}"><figcaption>{alt}</figcaption></figure>'


def embutir_imagens(corpo_html: str, pasta_base: Path) -> tuple[str, list[str]]:
    """Troca cada imagem do Markdown por uma figura em base64 com legenda, ou pelo aviso de pendência.

    Imagens escritas em HTML (a logo) ficam no lugar, só com o src trocado pelo base64.
    Devolve o HTML e a lista dos caminhos das imagens que não existem.
    """
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
            pendentes.append(rotulo_pendente(src))
            return m.group(0)
        tipo, dados = _ler_imagem(caminho, fundo_branco=True)
        return f"{m.group(1)}data:{tipo};base64,{base64.b64encode(dados).decode('ascii')}{m.group(3)}"

    corpo = _IMAGEM.sub(trocar, corpo_html)
    return _SRC_HTML.sub(trocar_src, corpo), pendentes


def montar_html(texto_md: str, pasta_base: Path, titulo: str = "Relatório de Cybersecurity") -> tuple[str, list[str]]:
    """HTML completo (com CSS e imagens embutidas) e a lista de imagens pendentes."""
    corpo = markdown.markdown(
        texto_md,
        extensions=["tables", "fenced_code", "codehilite"],
        extension_configs={"codehilite": {"guess_lang": False, "noclasses": True}},
        output_format="html",
    )
    corpo, pendentes = embutir_imagens(corpo, pasta_base)
    documento = (
        '<!doctype html>\n<html lang="pt-BR">\n<head>\n<meta charset="utf-8">\n'
        f"<title>{html.escape(titulo)}</title>\n<style>{CSS}</style>\n</head>\n<body>\n{corpo}\n</body>\n</html>\n"
    )
    return documento, pendentes


def encontrar_navegador(candidatos=NAVEGADORES) -> Path | None:
    """O primeiro Edge (ou Chrome) instalado, ou None."""
    return next((Path(c) for c in candidatos if Path(c).is_file()), None)


def comando_pdf(navegador: Path, html_entrada: Path, pdf_saida: Path, perfil: Path) -> list[str]:
    return [
        str(navegador),
        "--headless=new",
        "--disable-gpu",
        "--no-first-run",
        "--no-default-browser-check",
        f"--user-data-dir={perfil}",
        "--no-pdf-header-footer",
        "--print-to-pdf-no-header",
        f"--print-to-pdf={pdf_saida}",
        html_entrada.resolve().as_uri(),
    ]


def contar_paginas(pdf: bytes) -> int:
    """Páginas do PDF pelos objetos /Type /Page (sem contar o /Pages da árvore)."""
    return len(re.findall(rb"/Type\s*/Page(?!s)", pdf))


def gerar_pdf(
    navegador: Path,
    html_entrada: Path,
    pdf_saida: Path,
    executar: Callable[..., subprocess.CompletedProcess] = subprocess.run,
) -> bool:
    """Imprime o HTML em PDF com o navegador. Devolve se o PDF saiu."""
    if pdf_saida.exists():
        pdf_saida.unlink()
    with tempfile.TemporaryDirectory(prefix="helmcare-pdf-") as perfil:
        executar(comando_pdf(navegador, html_entrada, pdf_saida, Path(perfil)), timeout=180, check=False,
                 capture_output=True)
    return pdf_saida.is_file() and pdf_saida.stat().st_size > 0


def main(
    argv: list[str] | None = None,
    navegador: Path | None = None,
    executar: Callable[..., subprocess.CompletedProcess] = subprocess.run,
    entrada: Path = RELATORIO_MD,
    saida: Path = RELATORIO_PDF,
    pasta_dist: Path = PASTA_DIST,
    raiz: Path = RAIZ,
    obrigatorios: tuple[str, ...] = PRINTS_OBRIGATORIOS,
) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description="Gera o PDF do relatório de Cybersecurity.")
    parser.add_argument("--estrito", action="store_true",
                        help="falha se faltar alguma imagem citada ou algum print de evidencias/prints/ esperado")
    argumentos = parser.parse_args(argv)

    documento, pendentes = montar_html(entrada.read_text(encoding="utf-8"), entrada.parent)
    pendentes += [p for p in obrigatorios if not (raiz / p).is_file() and p not in pendentes]
    if pendentes:
        print("Imagens pendentes:")
        for nome in pendentes:
            print(f"  IMAGEM PENDENTE: {nome}")
        if argumentos.estrito:
            print("Modo estrito: o PDF não foi gerado.")
            return 2

    pasta_dist.mkdir(parents=True, exist_ok=True)
    html_saida = pasta_dist / (entrada.stem + ".html")
    html_saida.write_text(documento, encoding="utf-8")
    print(f"HTML: {html_saida} ({html_saida.stat().st_size / 1024:.0f} KB)")

    navegador = navegador or encontrar_navegador()
    if navegador is None:
        print("Edge e Chrome não encontrados: abra o HTML acima no navegador e use Ctrl+P para salvar em PDF.")
        return 0
    if not gerar_pdf(navegador, html_saida, saida, executar):
        print(f"O navegador não gerou o PDF. Abra o HTML e use Ctrl+P. Navegador: {navegador}")
        return 1
    dados = saida.read_bytes()
    print(f"PDF: {saida}")
    print(f"Tamanho: {len(dados) / (1024 * 1024):.1f} MB, {contar_paginas(dados)} páginas")
    return 0


if __name__ == "__main__":
    os.chdir(RAIZ)
    sys.exit(main())
