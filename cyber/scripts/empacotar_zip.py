"""Gera dist/helmcare-cyber-558540.zip com a pasta cyber/ dentro, sem segredos nem arquivos de ambiente.

Uso, dentro de cyber/ com o venv ativo:

    python scripts/empacotar_zip.py                   # sem o vídeo (evidencias/video/*.mp4)
    python scripts/empacotar_zip.py --incluir-video   # com o vídeo

Ficam de fora: .env, .venv/, __pycache__/, .pytest_cache/, .coverage, dados/,
dist/, *.pyc e, sem a flag, todo .mp4 (o vídeo do painel fica em evidencias/video/).
Depois de zipar, o script reabre o ZIP e confere que não entrou .env, nem .mp4 sem
a flag, nem nada com cara de chave de API. Também lista as imagens citadas nos .md
que não estão no ZIP (por exemplo, um print ainda não tirado), só como aviso.

A logo dos documentos fica em assets/marca/, dentro do módulo, então os links de
imagem dos .md resolvem dentro do ZIP sem nenhum arquivo de fora de cyber/.
"""

import argparse
import posixpath
import re
import sys
import zipfile
from pathlib import Path, PurePosixPath

RAIZ = Path(__file__).resolve().parents[1]
DESTINO = RAIZ / "dist" / "helmcare-cyber-558540.zip"
PASTA_NO_ZIP = "cyber"

PASTAS_EXCLUIDAS = frozenset({".venv", "__pycache__", ".pytest_cache", "dados", "dist", "htmlcov"})
ARQUIVOS_EXCLUIDOS = frozenset({".env", ".coverage"})
EXTENSOES_EXCLUIDAS = frozenset({".pyc"})
EXTENSAO_VIDEO = ".mp4"

# Os mesmos padrões do grep de segredos (chave do Google e chave sk- longa).
PADROES_CHAVE = (re.compile(rb"AIza[0-9A-Za-z_-]{35}"), re.compile(rb"sk-[A-Za-z0-9_-]{20,}"))

# Links de imagem num .md: ![texto](caminho) e <img ... src="caminho">.
_IMAGEM_MD = re.compile(r"!\[[^\]]*\]\(([^)\s]+)\)")
_IMAGEM_HTML = re.compile(r'<img\b[^>]*?\bsrc="([^"]+)"')


def links_de_imagem(texto_md: str) -> list[str]:
    """Caminhos relativos das imagens citadas num Markdown (sem http, https nem data:)."""
    links = _IMAGEM_MD.findall(texto_md) + _IMAGEM_HTML.findall(texto_md)
    return [link for link in links if not link.startswith(("http://", "https://", "data:"))]


def deve_excluir(relativo: PurePosixPath, incluir_video: bool = False) -> bool:
    """Diz se um caminho relativo a cyber/ fica fora do ZIP."""
    if any(parte in PASTAS_EXCLUIDAS for parte in relativo.parts[:-1]):
        return True
    nome = relativo.name
    if nome in ARQUIVOS_EXCLUIDOS or relativo.suffix.lower() in EXTENSOES_EXCLUIDAS:
        return True
    if nome in PASTAS_EXCLUIDAS:
        return True
    return relativo.suffix.lower() == EXTENSAO_VIDEO and not incluir_video


def listar_arquivos(raiz: Path, incluir_video: bool = False) -> list[Path]:
    """Arquivos de raiz que entram no ZIP, em ordem."""
    arquivos = []
    for caminho in sorted(raiz.rglob("*")):
        if caminho.is_file() and not deve_excluir(PurePosixPath(caminho.relative_to(raiz).as_posix()), incluir_video):
            arquivos.append(caminho)
    return arquivos


def empacotar(raiz: Path, destino: Path, incluir_video: bool = False, compressao: int = zipfile.ZIP_DEFLATED) -> int:
    """Escreve o ZIP com os arquivos dentro de cyber/. Devolve quantos arquivos entraram."""
    arquivos = listar_arquivos(raiz, incluir_video)
    destino.parent.mkdir(parents=True, exist_ok=True)
    nivel = 9 if compressao == zipfile.ZIP_DEFLATED else None
    with zipfile.ZipFile(destino, "w", compression=compressao, compresslevel=nivel) as zipado:
        for caminho in arquivos:
            zipado.write(caminho, f"{PASTA_NO_ZIP}/{caminho.relative_to(raiz).as_posix()}")
    return len(arquivos)


def conferir_zip(destino: Path, incluir_video: bool = False) -> list[str]:
    """Reabre o ZIP e devolve os problemas encontrados (lista vazia: tudo certo)."""
    problemas = []
    with zipfile.ZipFile(destino) as zipado:
        for info in zipado.infolist():
            caminho = PurePosixPath(info.filename)
            if caminho.name == ".env":
                problemas.append(f"arquivo .env no ZIP: {info.filename}")
            if caminho.suffix.lower() == EXTENSAO_VIDEO and not incluir_video:
                problemas.append(f"vídeo no ZIP sem --incluir-video: {info.filename}")
            if caminho.suffix.lower() in (".png", ".jpg", ".jpeg", ".mp4", ".pdf"):
                continue
            conteudo = zipado.read(info)
            if any(padrao.search(conteudo) for padrao in PADROES_CHAVE):
                problemas.append(f"padrão de chave de API em {info.filename}")
    return problemas


def imagens_fora_do_zip(destino: Path) -> list[str]:
    """Imagens citadas pelos .md do ZIP que não estão dentro dele (caminhos do ZIP, em ordem)."""
    faltando = set()
    with zipfile.ZipFile(destino) as zipado:
        nomes = set(zipado.namelist())
        for nome in nomes:
            if not nome.lower().endswith(".md"):
                continue
            pasta = posixpath.dirname(nome)
            for link in links_de_imagem(zipado.read(nome).decode("utf-8")):
                alvo = posixpath.normpath(posixpath.join(pasta, link))
                if alvo not in nomes:
                    faltando.add(alvo)
    return sorted(faltando)


def main(argv: list[str] | None = None, raiz: Path = RAIZ, destino: Path = DESTINO) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description="Empacota a pasta cyber/ para a entrega.")
    parser.add_argument("--incluir-video", action="store_true", help="inclui o vídeo .mp4 de evidencias/video/")
    argumentos = parser.parse_args(argv)

    quantidade = empacotar(raiz, destino, argumentos.incluir_video)
    problemas = conferir_zip(destino, argumentos.incluir_video)
    print(f"ZIP: {destino}")
    print(f"Arquivos: {quantidade}, tamanho: {destino.stat().st_size / (1024 * 1024):.1f} MB")
    print(f"Vídeo: {'incluído' if argumentos.incluir_video else 'fora do ZIP (use --incluir-video)'}")
    for imagem in imagens_fora_do_zip(destino):
        print(f"Aviso: imagem citada num .md e ausente do ZIP: {imagem}")
    if problemas:
        print("Problemas encontrados:")
        for problema in problemas:
            print(f"  {problema}")
        return 1
    print("Conferência: sem .env, sem vídeo indevido e sem padrão de chave de API.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
