"""Gera ml/apresentacao/APRESENTACAO_ML.pdf: slides 16:9 da entrega de IA e ML, com o Edge em modo headless.

Adaptado do ml/relatorio/gerar_pdf.py, com a mesma identidade visual do RELATORIO_ML.pdf
(azul #001D48, laranja #FE7801, Segoe UI) e a logo de ml/marca/logo-horizontal.png.
Cada slide é uma página de 1280 px por 720 px (@page), e as imagens entram em base64,
então o PDF não depende de nenhum arquivo externo. Os números são os das saídas do notebook.

Uso, a partir da raiz do repositório (Pillow e pypdf são opcionais):

    python ml/apresentacao/gerar_slides.py
    python ml/apresentacao/gerar_slides.py --html slides.html   # guarda também o HTML
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

PASTA_ML = Path(__file__).resolve().parents[1]
PASTA_FIGURAS = PASTA_ML / "figuras"
LOGO = PASTA_ML / "marca" / "logo-horizontal.png"
SLIDES_PDF = Path(__file__).resolve().parent / "APRESENTACAO_ML.pdf"
LARGURA_MAXIMA_IMAGEM = 2000
LIMIAR_QUASE_BRANCO = 240
RODAPE = "HelmCare | Inteligência Artificial e Machine Learning | Renan Dias Utida"

NAVEGADORES = (
    Path(r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"),
    Path(r"C:\Program Files\Microsoft\Edge\Application\msedge.exe"),
    Path(r"C:\Program Files\Google\Chrome\Application\chrome.exe"),
    Path(r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"),
)

CSS = """
@page { size: 1280px 720px; margin: 0; }
* { box-sizing: border-box; }
html, body { margin: 0; padding: 0; }
body { font-family: "Segoe UI", Arial, sans-serif; color: #111; font-size: 26px; line-height: 1.32;
  -webkit-print-color-adjust: exact; print-color-adjust: exact; }
.slide { width: 1280px; height: 720px; position: relative; overflow: hidden; background: #fff;
  page-break-after: always; break-after: page; display: flex; flex-direction: column; }
.slide:last-child { page-break-after: auto; break-after: auto; }
.topo { display: flex; align-items: center; justify-content: space-between; padding: 26px 56px 10px;
  border-bottom: 4px solid #FE7801; margin: 0 0 0; }
.topo h2 { margin: 0; color: #001D48; font-size: 40px; line-height: 1.15; }
.topo img { height: 34px; width: auto; }
.corpo { flex: 1; padding: 18px 56px 0; display: flex; flex-direction: column; gap: 14px; min-height: 0; }
.colunas { display: flex; gap: 32px; flex: 1; min-height: 0; align-items: center; }
.colunas .texto { flex: 0 0 auto; }
.colunas .figura { flex: 1; min-width: 0; height: 100%; }
ul { margin: 0; padding-left: 30px; }
li { margin: 0 0 12px; }
li::marker { color: #FE7801; }
p { margin: 0; }
b, strong { color: #001D48; }
.figura { display: flex; align-items: center; justify-content: center; min-height: 0; }
.figura img { max-width: 100%; max-height: 100%; object-fit: contain; border: 1px solid #c8ccd2; }
.corpo > .figura { flex: 1 1 0; padding-bottom: 12px; }
.slide.centrado .corpo { justify-content: center; gap: 30px; padding-bottom: 20px; }
.chips { display: flex; flex-wrap: wrap; gap: 10px 12px; }
.chip { background: #f3f5f8; border-left: 6px solid #FE7801; padding: 6px 14px; font-size: 23px; }
.rodape { height: 38px; display: flex; align-items: center; justify-content: space-between; padding: 0 56px;
  font-size: 15px; color: #555; border-top: 1px solid #e1e4e8; }
.pergunta { border-left: 8px solid #FE7801; background: #fff6ee; padding: 18px 26px; font-size: 30px; }
.pergunta .rotulo { display: block; font-size: 22px; font-weight: 700; color: #001D48; margin-bottom: 6px; }
.funil { display: flex; align-items: stretch; gap: 14px; }
.funil .etapa { flex: 1; background: #001D48; color: #fff; border-radius: 10px; padding: 12px 18px; text-align: center; }
.funil .etapa:last-child { background: #FE7801; }
.funil .numero { font-size: 38px; font-weight: 700; display: block; line-height: 1.1; }
.funil .legenda { font-size: 22px; }
.funil .seta { align-self: center; font-size: 40px; color: #FE7801; font-weight: 700; }
.cartoes { display: flex; gap: 22px; }
.cartao { flex: 1; border: 2px solid #001D48; border-radius: 12px; padding: 14px 20px; }
.cartao.destaque { border-color: #FE7801; background: #fff6ee; }
.cartao .titulo { font-size: 22px; font-weight: 700; color: #001D48; text-transform: uppercase; letter-spacing: 0.5px; }
.cartao .valor { font-size: 34px; font-weight: 700; color: #001D48; line-height: 1.2; }
.cartao .detalhe { font-size: 22px; color: #333; }
table { border-collapse: collapse; font-size: 26px; width: 100%; }
th, td { border: 1px solid #9aa0a8; padding: 6px 16px; text-align: left; }
th { background: #001D48; color: #fff; }
td.num, th.num { text-align: right; }
tr:nth-child(even) td { background: #f3f5f8; }
tr.destaque td { font-weight: 700; color: #001D48; background: #fff6ee; }
.nota { font-size: 22px; color: #333; }
.capa { justify-content: center; align-items: center; text-align: center; }
.capa img.logo { width: 560px; height: auto; margin-bottom: 30px; }
.capa h1 { color: #001D48; font-size: 50px; line-height: 1.15; margin: 0 120px 18px; }
.capa .faixa { width: 220px; height: 6px; background: #FE7801; margin: 0 auto 28px; }
.capa p { font-size: 28px; margin: 4px 0; color: #222; }
.capa .slogan { font-size: 40px; font-weight: 700; color: #001D48; margin-bottom: 26px; }
.capa a { color: #0b4aa8; text-decoration: none; }
"""


def _ler_imagem(caminho: Path, fundo_branco: bool = False) -> tuple[str, bytes]:
    """Tipo e bytes da imagem, reduzida se o Pillow existir (a logo fica com o fundo branco puro)."""
    dados = caminho.read_bytes()
    try:
        from PIL import Image, ImageChops
    except ImportError:  # o Pillow é opcional
        return "image/png", dados
    with Image.open(io.BytesIO(dados)) as imagem:
        if imagem.width <= LARGURA_MAXIMA_IMAGEM and not fundo_branco:
            return "image/png", dados
        trabalhada = imagem.convert("RGB")
        if trabalhada.width > LARGURA_MAXIMA_IMAGEM:
            altura = round(trabalhada.height * LARGURA_MAXIMA_IMAGEM / trabalhada.width)
            trabalhada = trabalhada.resize((LARGURA_MAXIMA_IMAGEM, altura), Image.LANCZOS)
        if fundo_branco:
            vermelho, verde, azul = (c.point(lambda v: 255 if v >= LIMIAR_QUASE_BRANCO else 0)
                                     for c in trabalhada.split())
            mascara = ImageChops.multiply(ImageChops.multiply(vermelho, verde), azul)
            trabalhada.paste((255, 255, 255), mask=mascara)
        saida = io.BytesIO()
        trabalhada.save(saida, format="PNG", optimize=True)
        return "image/png", saida.getvalue()


def imagem(caminho: Path, alt: str, classe: str = "", fundo_branco: bool = False) -> str:
    if not caminho.is_file():
        raise FileNotFoundError(f"imagem não encontrada: {caminho}")
    tipo, dados = _ler_imagem(caminho, fundo_branco)
    atributo = f' class="{classe}"' if classe else ""
    return f'<img{atributo} alt="{html.escape(alt)}" src="data:{tipo};base64,{base64.b64encode(dados).decode()}">'


def figura(nome: str, alt: str, altura: int | None = None) -> str:
    estilo = f' style="height: {altura}px; flex: 0 0 auto;"' if altura else ""
    return f'<div class="figura"{estilo}>{imagem(PASTA_FIGURAS / nome, alt)}</div>'


def lista(itens: list[str]) -> str:
    return "<ul>" + "".join(f"<li>{i}</li>" for i in itens) + "</ul>"


def montar_slides() -> list[tuple[str, str, str]]:
    """Lista de (título, classe extra, HTML do corpo). Título vazio = slide de capa."""
    logo = imagem(LOGO, "Logo do HelmCare", "logo", fundo_branco=True)
    slides = []

    slides.append(("", "capa", (
        f"{logo}<h1>Risco de sinistros graves com moto por hora em São Paulo</h1><div class='faixa'></div>"
        "<p><b>Renan Dias Utida</b>, RM 558540, turma 3ESPW</p>"
        "<p>Inteligência Artificial e Machine Learning</p>"
        "<p>Prof. Danilo Rodrigues de Assis Elias</p>"
        "<p class='nota' style='margin-top: 22px;'>FIAP, Global Solution 2026.2 (CitizenTech)</p>")))

    slides.append(("O problema", "", (
        "<div class='cartoes'>"
        "<div class='cartao'><div class='titulo'>Mortes no trânsito da capital em 2025</div>"
        "<div class='valor'>1.034</div><div class='detalhe'>o maior número desde 2015</div></div>"
        "<div class='cartao destaque'><div class='titulo'>Motociclistas entre elas</div>"
        "<div class='valor'>475</div><div class='detalhe'>o maior grupo de vítimas</div></div></div>"
        + lista(["O entregador sai para o turno <b>sem saber quanto o horário pesa</b> no risco.",
                 "O HelmCare mostra antes do turno o <b>Índice de Risco do Turno</b> (0 a 100): "
                 "50% vêm deste modelo, 25% da chuva e 25% do calor."])
        + "<div class='pergunta'><span class='rotulo'>Pergunta de ML</span>Quantos sinistros graves com moto são "
        "esperados em cada hora local da cidade de São Paulo, segundo o calendário? E as condições do tempo "
        "medidas pelo INMET mudam essa expectativa?</div>")))

    slides.append(("Dados reais: Infosiga SP e INMET A701", "", (
        "<div class='funil'>"
        "<div class='etapa'><span class='numero'>1.450.961</span><span class='legenda'>registros do Infosiga</span></div>"
        "<div class='seta'>&#8594;</div>"
        "<div class='etapa'><span class='numero'>120.621</span><span class='legenda'>com moto na capital</span></div>"
        "<div class='seta'>&#8594;</div>"
        "<div class='etapa'><span class='numero'>13.028</span><span class='legenda'>sinistros graves com hora</span></div>"
        "</div>"
        + lista(["De <b>2019 a 2025</b>. Grave: pelo menos 1 moto e pelo menos 1 vítima grave ou fatal.",
                 "Validação: <b>1.038 mortes</b> em 2025 na base, contra <b>1.034 divulgadas</b>."])
        + figura("item2_sinistros_por_hora.png", "Sinistros com moto por hora do dia"))))

    slides.append(("O modelo: XGBoost com objetivo count:poisson", "centrado", (
        lista(["O alvo é uma <b>contagem</b> pequena por hora: 0,212 sinistro grave por hora, em média.",
               "As árvores captam a interação entre hora e dia da semana, e o modelo sai em JSON "
               "para o Serviço de Risco.",
               "A métrica principal é a <b>deviance de Poisson</b>: quanto menor, melhor.",
               "Comparação sempre contra a linha de base: média por hora e dia da semana."])
        + "<div class='cartoes'>"
        "<div class='cartao destaque'><div class='titulo'>A armadilha do MAE</div>"
        "<div class='valor'>Prever sempre 0</div><div class='detalhe'>tem o menor MAE no teste (0,2325, contra 0,3487 "
        "da linha de base), mas deviance infinita e viés de -100%.</div></div>"
        "<div class='cartao'><div class='titulo'>Na grade</div>"
        "<div class='valor'>Menor MAE = pior modelo</div><div class='detalhe'>a combinação com o menor MAE (0,2905) "
        "foi a de pior deviance (0,9648).</div></div></div>")))

    slides.append(("Divisão treino e teste e grade de hiperparâmetros", "", (
        "<div class='chips'>"
        + "".join(f"<span class='chip'>{t}</span>" for t in [
            "Treino: <b>2019 a 2024</b>", "Teste: <b>2025</b>, uma única vez", "<b>5 dobras anuais</b> só no treino",
            "<b>27 combinações</b> de 3 hiperparâmetros", "Melhor: <b>5, 0,03 e 200 árvores</b> (validação 0,6990)"])
        + "</div>" + figura("item8_grade_hiperparametros.png", "Grade de hiperparâmetros"))))

    slides.append(("Treino contra teste: a deviance de Poisson", "", (
        "<table><tr><th>Deviance de Poisson</th><th class='num'>Treino</th><th class='num'>Validação</th>"
        "<th class='num'>Teste (2025)</th></tr>"
        "<tr class='destaque'><td>Modelo exportado (só calendário)</td><td class='num'>0,6852</td>"
        "<td class='num'>0,6977</td><td class='num'>0,7429</td></tr>"
        "<tr><td>Linha de base (média por hora e dia da semana)</td><td class='num'>0,6880</td>"
        "<td class='num'>0,7081</td><td class='num'>0,7485</td></tr></table>"
        "<p class='nota'>Overfitting da combinação complexa (7, 0,3 e 800 árvores): a validação sobe de 0,7236 "
        "para 0,8962 enquanto o treino cai a 0,2902.</p>"
        + figura("item9_curva_aprendizado.png", "Curva de aprendizado"))))

    slides.append(("A decisão: o modelo usa só o calendário", "", (
        "<div class='colunas'><div class='texto' style='width: 600px;'>"
        + lista(["O tempo do INMET <b>não melhorou</b> a previsão: na ablação, -0,18% na validação.",
                 "Permutação da chuva no teste: <b>aumento zero</b> da deviance.",
                 "Grade só com calendário: melhor em <b>21 de 27</b> combinações e <b>gap menor em todas</b>.",
                 "Chuva e calor entram no Índice só como <b>exposição</b> do entregador."])
        + "</div>" + figura("item10_grades_7_variaveis_calendario.png", "As duas grades lado a lado") + "</div>")))

    slides.append(("Para o cidadão: quanto o horário pesa", "", (
        figura("item11_horario_para_o_entregador.png", "Componente sinistro ao longo do dia", altura=380)
        + "<div class='cartoes'>"
        "<div class='cartao'><div class='titulo'>Quinta-feira de novembro</div>"
        "<div class='valor'>18h: 4,0 vezes o risco das 3h</div>"
        "<div class='detalhe'>no fim de semana, a madrugada também fica alta</div></div>"
        "<div class='cartao destaque'><div class='titulo'>Turno de quinta, das 17h às 21h</div>"
        "<div class='valor'>Índice 60, ALTO</div><div class='detalhe'>pico às 19h</div></div></div>")))

    slides.append(("Trechos críticos: a Raposo Tavares em destaque", "", (
        "<div class='colunas'><div class='texto' style='width: 330px;'>"
        + lista(["<b>9 dos 11</b> primeiros trechos na SP 270",
                 "Até 46 sinistros graves em cerca de 550 m",
                 "Registro <b>por quilômetro</b>: contagem real, com precisão de cerca de 1 km"])
        + "</div>" + figura("item10_trechos_criticos.png", "Trechos críticos") + "</div>")))

    slides.append(("", "capa", (
        f"{logo}<p class='slogan'>Do risco ao resgate, cuidado em cada corrida.</p><div class='faixa'></div>"
        "<p><a href='https://github.com/renan-utida/helmcare/tree/main/ml'>"
        "https://github.com/renan-utida/helmcare/tree/main/ml</a></p>"
        "<p class='nota' style='margin-top: 18px;'>Renan Dias Utida, RM 558540, turma 3ESPW</p>")))
    return slides


def montar_html() -> str:
    logo_topo = imagem(LOGO, "Logo do HelmCare", fundo_branco=True)
    slides = montar_slides()
    partes = []
    for numero, (titulo, classe, corpo) in enumerate(slides, start=1):
        if not titulo:
            partes.append(f"<section class='slide {classe}'>{corpo}</section>")
            continue
        partes.append(
            f"<section class='slide {classe}'><div class='topo'><h2>{titulo}</h2>{logo_topo}</div>"
            f"<div class='corpo'>{corpo}</div>"
            f"<div class='rodape'><span>{RODAPE}</span><span>{numero} / {len(slides)}</span></div></section>")
    return ('<!doctype html>\n<html lang="pt-BR">\n<head>\n<meta charset="utf-8">\n'
            f"<title>Apresentação de IA e ML do HelmCare</title>\n<style>{CSS}</style>\n</head>\n<body>\n"
            + "\n".join(partes) + "\n</body>\n</html>\n")


def encontrar_navegador() -> Path | None:
    return next((c for c in NAVEGADORES if c.is_file()), None)


def gerar_pdf(navegador: Path, html_entrada: Path, pdf_saida: Path) -> bool:
    if pdf_saida.exists():
        pdf_saida.unlink()
    with tempfile.TemporaryDirectory(prefix="helmcare-slides-") as perfil:
        comando = [
            str(navegador), "--headless=new", "--disable-gpu", "--no-first-run", "--no-default-browser-check",
            f"--user-data-dir={perfil}", "--no-pdf-header-footer", "--print-to-pdf-no-header",
            f"--print-to-pdf={pdf_saida}", html_entrada.resolve().as_uri(),
        ]
        subprocess.run(comando, timeout=180, check=False, capture_output=True)
    return pdf_saida.is_file() and pdf_saida.stat().st_size > 0


def contar_paginas(pdf: Path) -> int:
    try:
        from pypdf import PdfReader
    except ImportError:
        return len(re.findall(rb"/Type\s*/Page(?!s)", pdf.read_bytes()))
    return len(PdfReader(pdf).pages)


def main(argv: list[str] | None = None) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description="Gera os slides de IA e ML em PDF.")
    parser.add_argument("--html", type=Path, help="guarda o HTML intermediário neste caminho")
    argumentos = parser.parse_args(argv)
    documento = montar_html()

    navegador = encontrar_navegador()
    if navegador is None:
        html_saida = argumentos.html or SLIDES_PDF.with_suffix(".html")
        html_saida.write_text(documento, encoding="utf-8")
        print(f"Edge e Chrome não encontrados: abra {html_saida} e use Ctrl+P (paisagem, sem margens).")
        return 0
    with tempfile.TemporaryDirectory(prefix="helmcare-ml-") as pasta:
        html_saida = argumentos.html or Path(pasta) / "APRESENTACAO_ML.html"
        html_saida.write_text(documento, encoding="utf-8")
        if not gerar_pdf(navegador, html_saida, SLIDES_PDF):
            print(f"O navegador não gerou o PDF. Navegador: {navegador}")
            return 1
    print(f"PDF: {SLIDES_PDF}")
    print(f"Tamanho: {SLIDES_PDF.stat().st_size / 1024:.0f} KB, {contar_paginas(SLIDES_PDF)} páginas")
    return 0


if __name__ == "__main__":
    os.chdir(PASTA_ML)
    sys.exit(main())
