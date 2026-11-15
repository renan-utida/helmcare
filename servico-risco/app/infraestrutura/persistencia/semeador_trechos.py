"""Carga idempotente dos trechos críticos de ml/modelo/trechos_criticos.json no banco.

Idempotente pelo sha256 do arquivo: se o mesmo arquivo já foi carregado, não faz nada.
Se o arquivo mudar, o hash muda e entra um conjunto novo; o repositório sempre lê o mais
recente (carregado_em). Roda no startup do serviço (se a configuração permitir) e
também pela linha de comando:

    .venv/Scripts/python -m app.infraestrutura.persistencia.semeador_trechos
"""

import hashlib
import json
import logging
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from sqlalchemy import select
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from app.dominio.excecoes import ValidacaoDominioError
from app.dominio.portas import Relogio
from app.dominio.trecho_critico import ConjuntoTrechosCriticos, TrechoCritico, validar_carga
from app.infraestrutura.persistencia.modelos import (
    ConjuntoTrechosCriticosModelo,
    TrechoCriticoModelo,
)

logger = logging.getLogger(__name__)

ARQUIVO_TRECHOS = "trechos_criticos.json"


@dataclass(frozen=True)
class ResultadoCarga:
    """Resultado da carga: se gravou um conjunto novo e quantos trechos o arquivo tem."""

    carregado: bool
    quantidade_trechos: int
    hash_arquivo: str


def calcular_hash(conteudo: bytes) -> str:
    """sha256 do conteúdo do arquivo, em hexadecimal (64 caracteres)."""
    return hashlib.sha256(conteudo).hexdigest()


def converter_conjunto(dados: dict[str, Any]) -> ConjuntoTrechosCriticos:
    """Converte o JSON num conjunto do domínio e aplica as regras da carga.

    O domínio valida posições de 1 a N, ordem decrescente, quantidadeTrechos igual ao
    tamanho da lista e no mínimo 50 trechos. Campo ausente vira ValidacaoDominioError.
    """
    try:
        conjunto = ConjuntoTrechosCriticos(
            periodo=dados["periodo"],
            fonte=dados["fonte"],
            criterio=dados["criterio"],
            trechos=tuple(
                TrechoCritico(
                    posicao=int(t["posicao"]),
                    logradouro=str(t["logradouro"]),
                    latitude=float(t["latitude"]),
                    longitude=float(t["longitude"]),
                    quantidade_sinistros_graves=int(t["quantidadeSinistrosGraves"]),
                )
                for t in dados["trechos"]
            ),
        )
        quantidade_declarada = int(dados["quantidadeTrechos"])
    except (KeyError, TypeError, ValueError) as erro:
        raise ValidacaoDominioError(f"trechos_criticos.json com campo inválido: {erro!r}") from erro
    validar_carga(conjunto, quantidade_declarada)
    return conjunto


def carregar_trechos(sessao: Session, caminho: Path, relogio: Relogio) -> ResultadoCarga:
    """Carrega o arquivo no banco numa única transação, se o hash ainda não existir."""
    conteudo = caminho.read_bytes()
    hash_arquivo = calcular_hash(conteudo)
    conjunto = converter_conjunto(json.loads(conteudo.decode("utf-8")))

    ja_existe = sessao.scalar(
        select(ConjuntoTrechosCriticosModelo.id).where(
            ConjuntoTrechosCriticosModelo.hash_arquivo == hash_arquivo
        )
    )
    if ja_existe is not None:
        logger.info("Trechos críticos já carregados (hash %s).", hash_arquivo[:12])
        return ResultadoCarga(False, len(conjunto.trechos), hash_arquivo)

    modelo = ConjuntoTrechosCriticosModelo(
        periodo=conjunto.periodo,
        fonte=conjunto.fonte,
        criterio=conjunto.criterio,
        hash_arquivo=hash_arquivo,
        carregado_em=relogio.agora(),
        trechos=[
            TrechoCriticoModelo(
                posicao=t.posicao,
                logradouro=t.logradouro,
                latitude=t.latitude,
                longitude=t.longitude,
                quantidade_sinistros_graves=t.quantidade_sinistros_graves,
            )
            for t in conjunto.trechos
        ],
    )
    try:
        sessao.add(modelo)
        sessao.commit()
    except IntegrityError:
        # Duas instâncias subindo juntas podem tentar a mesma carga: a constraint única
        # do hash barra a segunda, e isso conta como "já carregado".
        sessao.rollback()
        logger.info("Outra instância carregou os trechos primeiro (hash %s).", hash_arquivo[:12])
        return ResultadoCarga(False, len(conjunto.trechos), hash_arquivo)

    logger.info(
        "Carregados %d trechos críticos (hash %s).", len(conjunto.trechos), hash_arquivo[:12]
    )
    return ResultadoCarga(True, len(conjunto.trechos), hash_arquivo)


def main() -> int:
    """Executa a carga pela linha de comando, com a configuração do serviço."""
    # Imports aqui dentro para o módulo poder ser importado sem ler a configuração.
    from app.config import obter_configuracoes
    from app.infraestrutura.persistencia.sessao import criar_engine, criar_fabrica_sessoes
    from app.infraestrutura.relogio import RelogioSistema

    logging.basicConfig(level=logging.INFO, format="%(levelname)s %(name)s: %(message)s")
    configuracoes = obter_configuracoes()
    if configuracoes.database_url is None:
        logger.error("Configure RISCO_DATABASE_URL no servico-risco/.env.")
        return 1
    engine = criar_engine(configuracoes.database_url)
    try:
        with criar_fabrica_sessoes(engine)() as sessao:
            resultado = carregar_trechos(
                sessao, configuracoes.modelo_dir / ARQUIVO_TRECHOS, RelogioSistema()
            )
    finally:
        engine.dispose()
    situacao = "carregados" if resultado.carregado else "já estavam carregados"
    logger.info("%d trechos %s.", resultado.quantidade_trechos, situacao)
    return 0


if __name__ == "__main__":
    sys.exit(main())
