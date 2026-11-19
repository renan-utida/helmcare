"""Sanitizadores reconhecidos pelo Taint Analysis.

Um sanitizador é uma função que transforma um dado contaminado numa forma
segura. Estas funções reaproveitam as máscaras do motor de PII e, quando
recebem um Rastro, registram o passo SANITIZADOR com o resultado (que já é
seguro). Elas nunca registram o valor de entrada.
"""

import base64
import hashlib
import hmac
import re
import secrets
from collections.abc import Iterable

from helmcare_cyber.pii import mascaras
from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.taint.rastro import Rastro

# PBKDF2-HMAC-SHA256 com 600 mil iterações, o mínimo recomendado pela OWASP
# para esse algoritmo. O formato segue o do Django: algoritmo$iteracoes$sal$hash.
ALGORITMO_HASH = "pbkdf2_sha256"
ITERACOES_PADRAO = 600_000
TAMANHO_SAL = 16

_PADRAO_HASH_PBKDF2 = re.compile(r"pbkdf2_sha256\$\d{4,}\$[A-Za-z0-9+/=_-]{8,}\$[A-Za-z0-9+/=_-]{16,}")
# Hash do ASP.NET Core Identity (formato v3): base64 que começa em AQAAAA.
_PADRAO_HASH_IDENTITY = re.compile(r"AQAAAA[A-Za-z0-9+/]{40,}={0,2}")


def mascarar(categoria: Categoria, valor, rastro: Rastro | None = None):
    resultado = mascaras.mascarar_valor(categoria, valor)
    if rastro is not None:
        rastro.adicionar_sanitizador("mascarar", [categoria], str(resultado))
    return resultado


def bloquear(categoria: Categoria, rastro: Rastro | None = None) -> str:
    """Marcador [BLOQUEADO:CPF]: o dado foi impedido de sair por aquele destino."""
    resultado = f"[BLOQUEADO:{Categoria(categoria).value}]"
    if rastro is not None:
        rastro.adicionar_sanitizador("bloquear", [categoria], resultado)
    return resultado


def redigir(categoria: Categoria, rastro: Rastro | None = None) -> str:
    resultado = mascaras.redigir(categoria)
    if rastro is not None:
        rastro.adicionar_sanitizador("redigir", [categoria], resultado)
    return resultado


def arredondar_localizacao(valor, rastro: Rastro | None = None) -> float:
    resultado = mascaras.arredondar_localizacao(valor)
    if rastro is not None:
        rastro.adicionar_sanitizador(
            "arredondar_localizacao (2 casas)", [Categoria.LOCALIZACAO_EXATA], str(resultado)
        )
    return resultado


def hash_senha(
    senha: str, rastro: Rastro | None = None, iteracoes: int = ITERACOES_PADRAO, sal: bytes | None = None
) -> str:
    """PBKDF2-HMAC-SHA256 da biblioteca padrão, no formato pbkdf2_sha256$iteracoes$sal$hash.

    O sal é aleatório a cada chamada (secrets), então a mesma senha dá hashes
    diferentes. O parâmetro sal existe só para testes determinísticos.
    """
    sal = sal if sal is not None else secrets.token_bytes(TAMANHO_SAL)
    derivado = hashlib.pbkdf2_hmac("sha256", senha.encode("utf-8"), sal, iteracoes)
    resultado = "$".join(
        (ALGORITMO_HASH, str(iteracoes), base64.b64encode(sal).decode(), base64.b64encode(derivado).decode())
    )
    if rastro is not None:
        rastro.adicionar_sanitizador("hash_senha (PBKDF2-HMAC-SHA256)", [Categoria.SENHA], f"{ALGORITMO_HASH}$...")
    return resultado


def conferir_senha(senha: str, hash_gravado: str) -> bool:
    """Confere uma senha contra um hash gerado por hash_senha (comparação em tempo constante)."""
    try:
        algoritmo, iteracoes, sal, esperado = hash_gravado.split("$")
        if algoritmo != ALGORITMO_HASH:
            return False
        calculado = hashlib.pbkdf2_hmac("sha256", senha.encode("utf-8"), base64.b64decode(sal), int(iteracoes))
    except (ValueError, TypeError):
        return False
    return hmac.compare_digest(base64.b64encode(calculado).decode(), esperado)


def eh_hash_senha(valor) -> bool:
    """Reconhece o hash de hash_senha e o hash do ASP.NET Core Identity (prefixo AQAAAA)."""
    if not isinstance(valor, str):
        return False
    texto = valor.strip()
    return bool(_PADRAO_HASH_PBKDF2.fullmatch(texto) or _PADRAO_HASH_IDENTITY.fullmatch(texto))


def projetar_campos(payload: dict, permitidos: Iterable[str], rastro: Rastro | None = None) -> dict:
    """Devolve um dict novo só com os campos de primeiro nível permitidos.

    É o sanitizador por lista branca: em vez de procurar o que tirar, diz o que
    pode ficar. O rastro registra os nomes dos campos removidos (nomes de campo
    não são dado pessoal), nunca os valores.
    """
    permitidos = list(permitidos)
    projetado = {chave: valor for chave, valor in payload.items() if chave in permitidos}
    if rastro is not None:
        removidos = [str(chave) for chave in payload if chave not in permitidos]
        descricao = f"projetar_campos (mantidos: {', '.join(permitidos) or 'nenhum'}"
        descricao += f"; removidos: {', '.join(removidos)})" if removidos else ")"
        rastro.adicionar_sanitizador(descricao)
    return projetado
