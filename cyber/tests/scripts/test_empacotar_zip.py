"""scripts/empacotar_zip.py numa pasta temporária com arquivos falsos (nenhum segredo real)."""

import importlib.util
import zipfile
from pathlib import Path, PurePosixPath

import pytest

SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "empacotar_zip.py"

# Chave falsa montada por concatenação, só para ver a conferência apontar o padrão.
CHAVE_FALSA_GOOGLE = "AIza" + "X" * 35


def carregar():
    especificacao = importlib.util.spec_from_file_location("script_empacotar_zip", SCRIPT)
    modulo = importlib.util.module_from_spec(especificacao)
    especificacao.loader.exec_module(modulo)
    return modulo


@pytest.fixture(scope="module")
def ez():
    return carregar()


@pytest.fixture
def cyber_falso(tmp_path):
    raiz = tmp_path / "cyber"
    arquivos = {
        ".env": "GEMINI_API_KEY=valor-falso",
        ".env.example": "GEMINI_API_KEY=",
        ".gitignore": ".env",
        ".coverage": "x",
        "README.md": '<p align="center"><img src="assets/marca/logo-horizontal.png" alt="Logo" width="420"></p>\n# cyber',
        "docs/relatorio-cyber.md": "# relatório\n\n![Figura 4](../evidencias/prints/P03-pytest-pii.png)",
        "docs/relatorio-cyber.pdf": "%PDF",
        "auditoria/auditoria-prototipo.md": "# auditoria",
        "assets/marca/logo-horizontal.png": "png",
        "evidencias/prints/P03-pytest-pii.png": "png",
        "evidencias/video/video-dashboard.mp4": "mp4",
        "helmcare_cyber/pii/motor.py": "print('ok')",
        "helmcare_cyber/pii/__pycache__/motor.cpython-313.pyc": "pyc",
        "scripts/solto.pyc": "pyc",
        ".venv/Lib/site.py": "venv",
        ".pytest_cache/v/cache": "x",
        "dados/eventos.jsonl": "{}",
        "dist/antigo.zip": "zip",
    }
    for relativo, conteudo in arquivos.items():
        caminho = raiz / relativo
        caminho.parent.mkdir(parents=True, exist_ok=True)
        caminho.write_text(conteudo, encoding="utf-8")
    return raiz


def nomes(destino: Path) -> set[str]:
    with zipfile.ZipFile(destino) as zipado:
        return set(zipado.namelist())


class TestRegras:
    @pytest.mark.parametrize(
        "relativo, excluir",
        [
            (".env", True),
            (".env.example", False),
            (".coverage", True),
            (".venv/Lib/x.py", True),
            ("helmcare_cyber/__pycache__/a.pyc", True),
            ("scripts/a.pyc", True),
            (".pytest_cache/v", True),
            ("dados/eventos.jsonl", True),
            ("dist/x.zip", True),
            ("evidencias/video/video-dashboard.mp4", True),
            ("evidencias/video-dashboard-original.mp4", True),
            ("evidencias/prints/P03-pytest-pii.png", False),
            ("assets/marca/logo-horizontal.png", False),
            ("docs/relatorio-cyber.pdf", False),
            ("auditoria/auditoria-prototipo.md", False),
        ],
    )
    def test_deve_excluir(self, ez, relativo, excluir):
        assert ez.deve_excluir(PurePosixPath(relativo)) is excluir

    def test_video_com_a_flag(self, ez):
        assert ez.deve_excluir(PurePosixPath("evidencias/video/video-dashboard.mp4"), incluir_video=True) is False

    def test_pasta_excluida_como_arquivo(self, ez):
        assert ez.deve_excluir(PurePosixPath("dist")) is True


class TestEmpacotar:
    def test_padrao_sem_env_sem_video(self, ez, cyber_falso, tmp_path):
        destino = tmp_path / "saida" / "helmcare-cyber-558540.zip"
        quantidade = ez.empacotar(cyber_falso, destino)
        conteudo = nomes(destino)
        assert quantidade == len(conteudo) == 9
        assert "cyber/.env.example" in conteudo and "cyber/docs/relatorio-cyber.pdf" in conteudo
        assert "cyber/auditoria/auditoria-prototipo.md" in conteudo
        assert "cyber/evidencias/prints/P03-pytest-pii.png" in conteudo and "cyber/assets/marca/logo-horizontal.png" in conteudo
        assert not any(n.endswith("/.env") or n.endswith(".mp4") or ".venv" in n or n.endswith(".pyc") for n in conteudo)
        assert ez.conferir_zip(destino) == []

    def test_padrao_exclui_o_video_de_evidencias_video(self, ez, cyber_falso, tmp_path):
        destino = tmp_path / "z.zip"
        ez.empacotar(cyber_falso, destino)
        assert "cyber/evidencias/video/video-dashboard.mp4" not in nomes(destino)
        assert "cyber/evidencias/prints/P03-pytest-pii.png" in nomes(destino)

    def test_com_video(self, ez, cyber_falso, tmp_path):
        destino = tmp_path / "z.zip"
        ez.empacotar(cyber_falso, destino, incluir_video=True)
        assert "cyber/evidencias/video/video-dashboard.mp4" in nomes(destino)
        assert ez.conferir_zip(destino, incluir_video=True) == []
        assert ez.conferir_zip(destino) == [
            "vídeo no ZIP sem --incluir-video: cyber/evidencias/video/video-dashboard.mp4"
        ]

    def test_sem_compressao(self, ez, cyber_falso, tmp_path):
        destino = tmp_path / "z.zip"
        ez.empacotar(cyber_falso, destino, compressao=zipfile.ZIP_STORED)
        with zipfile.ZipFile(destino) as zipado:
            assert {i.compress_type for i in zipado.infolist()} == {zipfile.ZIP_STORED}

    def test_conferencia_aponta_env_e_chave(self, ez, tmp_path):
        destino = tmp_path / "ruim.zip"
        with zipfile.ZipFile(destino, "w") as zipado:
            zipado.writestr("cyber/.env", "x")
            zipado.writestr("cyber/vazou.txt", f"chave={CHAVE_FALSA_GOOGLE}")
            zipado.writestr("cyber/imagem.png", CHAVE_FALSA_GOOGLE)  # binário não é varrido
        problemas = ez.conferir_zip(destino)
        assert "arquivo .env no ZIP: cyber/.env" in problemas
        assert "padrão de chave de API em cyber/vazou.txt" in problemas
        assert len(problemas) == 2


class TestLinksDeImagem:
    def test_markdown_e_html(self, ez):
        texto = (
            '<p align="center"><img src="../assets/marca/logo-horizontal.png" alt="Logo" width="420"></p>\n'
            "![Figura 1](../evidencias/prints/P01-estrutura.png)\n"
            "![externa](https://exemplo.com/a.png) <img src=\"data:image/png;base64,AA\">\n"
            "[link comum](../README.md)\n"
        )
        assert ez.links_de_imagem(texto) == ["../evidencias/prints/P01-estrutura.png", "../assets/marca/logo-horizontal.png"]

    def test_imagens_fora_do_zip(self, ez, tmp_path):
        destino = tmp_path / "z.zip"
        with zipfile.ZipFile(destino, "w") as zipado:
            zipado.writestr("cyber/README.md", '<img src="assets/marca/logo.png" alt="x">')
            zipado.writestr("cyber/assets/marca/logo.png", "png")
            zipado.writestr("cyber/docs/r.md", "![a](../evidencias/prints/P02.png) ![b](../evidencias/prints/P01.png)")
            zipado.writestr("cyber/evidencias/prints/P02.png", "png")
        assert ez.imagens_fora_do_zip(destino) == ["cyber/evidencias/prints/P01.png"]


class TestMain:
    def test_main_ok(self, ez, cyber_falso, tmp_path, capsys):
        assert ez.main([], raiz=cyber_falso, destino=tmp_path / "z.zip") == 0
        saida = capsys.readouterr().out
        assert "Arquivos: 9" in saida and "fora do ZIP" in saida and "Aviso" not in saida

    def test_main_avisa_imagem_ausente(self, ez, cyber_falso, tmp_path, capsys):
        (cyber_falso / "evidencias" / "prints" / "P03-pytest-pii.png").unlink()
        assert ez.main([], raiz=cyber_falso, destino=tmp_path / "z.zip") == 0
        assert "Aviso: imagem citada num .md e ausente do ZIP: cyber/evidencias/prints/P03-pytest-pii.png" in (
            capsys.readouterr().out
        )

    def test_main_aponta_problema(self, ez, cyber_falso, tmp_path, capsys):
        (cyber_falso / "docs" / "vazou.md").write_text(CHAVE_FALSA_GOOGLE, encoding="utf-8")
        assert ez.main(["--incluir-video"], raiz=cyber_falso, destino=tmp_path / "z.zip") == 1
        assert "padrão de chave de API em cyber/docs/vazou.md" in capsys.readouterr().out
