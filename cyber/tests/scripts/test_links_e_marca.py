"""Links de imagem dos .md do módulo, o ZIP gerado a partir do módulo real e a cópia da marca.

Os únicos arquivos que podem faltar são os prints de PRINTS_OBRIGATORIOS do gerar_pdf
(tirados por último, depois da contagem final de testes); qualquer outro link quebrado falha.
"""

import importlib.util
import os
import re
import zipfile
from pathlib import Path

import pytest

RAIZ = Path(__file__).resolve().parents[2]
MARCA_DO_MODULO = RAIZ / "assets" / "marca"
MARCA_DO_REPOSITORIO = RAIZ.parent / "docs" / "marca"
LOGO = MARCA_DO_MODULO / "logo-horizontal.png"
ALT_LOGO = "Logo do HelmCare: um capacete de moto com uma linha de batimento cardíaco na viseira"
DOCUMENTOS_COM_LOGO = ("README.md", "docs/relatorio-cyber.md", "auditoria/auditoria-prototipo.md", "evidencias/README.md")

_LINK_MD = re.compile(r"(?<!!)\[[^\]]*\]\(([^)\s]+)\)")


def carregar(nome: str):
    especificacao = importlib.util.spec_from_file_location(f"script_{nome}", RAIZ / "scripts" / f"{nome}.py")
    modulo = importlib.util.module_from_spec(especificacao)
    especificacao.loader.exec_module(modulo)
    return modulo


@pytest.fixture(scope="module")
def ez():
    return carregar("empacotar_zip")


@pytest.fixture(scope="module")
def pendentes_aceitos():
    return set(carregar("gerar_pdf").PRINTS_OBRIGATORIOS)


def arquivos_md(ez) -> list[Path]:
    return [p for p in ez.listar_arquivos(RAIZ) if p.suffix.lower() == ".md"]


def relativo_ao_modulo(caminho: Path) -> str:
    return Path(os.path.relpath(caminho, RAIZ)).as_posix()


def conferir_link(origem: Path, link: str, pendentes_aceitos: set[str]) -> str | None:
    """None se o link resolve (ou é um print pendente aceito); senão, a descrição do problema."""
    alvo = (origem.parent / link.split("#")[0]).resolve()
    if alvo.exists():
        return None
    if relativo_ao_modulo(alvo) in pendentes_aceitos:
        return None
    return f"{relativo_ao_modulo(origem)}: {link}"


class TestLinksNoRepositorio:
    def test_todo_link_de_imagem_resolve(self, ez, pendentes_aceitos):
        problemas, total = [], 0
        for md in arquivos_md(ez):
            for link in ez.links_de_imagem(md.read_text(encoding="utf-8")):
                total += 1
                problema = conferir_link(md, link, pendentes_aceitos)
                if problema:
                    problemas.append(problema)
        assert problemas == []
        assert total >= 25  # 21 figuras do relatório e as 4 logos

    def test_links_do_readme_de_evidencias_resolvem(self, pendentes_aceitos):
        readme = RAIZ / "evidencias" / "README.md"
        links = [l for l in _LINK_MD.findall(readme.read_text(encoding="utf-8")) if not l.startswith("http")]
        assert len(links) >= 22
        assert [p for l in links if (p := conferir_link(readme, l, pendentes_aceitos))] == []

    def test_pendentes_ficam_em_evidencias_prints(self, pendentes_aceitos):
        assert pendentes_aceitos == {"evidencias/prints/P01-estrutura.png", "evidencias/prints/P18-pytest-cobertura.png"}

    @pytest.mark.parametrize("documento", DOCUMENTOS_COM_LOGO)
    def test_logo_no_topo_centralizada(self, documento):
        arquivo = RAIZ / documento
        texto = arquivo.read_text(encoding="utf-8")
        caminho = Path(os.path.relpath(LOGO, arquivo.parent)).as_posix()
        esperado = f'<p align="center"><img src="{caminho}" alt="{ALT_LOGO}" width="420"></p>'
        assert esperado in texto
        primeiro_titulo = min(texto.find("\n# "), texto.find("<h1>") if "<h1>" in texto else len(texto))
        assert texto.find(esperado) < primeiro_titulo
        assert "docs/marca" not in caminho


class TestZipGerado:
    def test_links_de_imagem_resolvem_dentro_do_zip(self, ez, pendentes_aceitos, tmp_path):
        destino = tmp_path / "helmcare-cyber-558540.zip"
        ez.empacotar(RAIZ, destino, compressao=zipfile.ZIP_STORED)
        faltando = set(ez.imagens_fora_do_zip(destino))
        assert faltando <= {f"{ez.PASTA_NO_ZIP}/{p}" for p in pendentes_aceitos}
        with zipfile.ZipFile(destino) as zipado:
            nomes = set(zipado.namelist())
        assert "cyber/assets/marca/logo-horizontal.png" in nomes
        assert all(n.startswith("cyber/") for n in nomes)
        assert not any(n.endswith(".mp4") for n in nomes)
        assert ez.conferir_zip(destino) == []


class TestMarca:
    def test_copia_da_logo_existe(self):
        assert LOGO.is_file()
        assert sorted(p.name for p in MARCA_DO_MODULO.iterdir()) == ["logo-horizontal-escuro.png", "logo-horizontal.png"]

    def test_copia_igual_a_marca_do_repositorio(self):
        if not MARCA_DO_REPOSITORIO.is_dir():
            pytest.skip("docs/marca/ do repositório não existe aqui (por exemplo, no ZIP extraído)")
        for copia in MARCA_DO_MODULO.iterdir():
            original = MARCA_DO_REPOSITORIO / copia.name
            assert original.is_file(), f"{copia.name} não existe em docs/marca/"
            assert copia.read_bytes() == original.read_bytes(), f"{copia.name} diverge de docs/marca/"
