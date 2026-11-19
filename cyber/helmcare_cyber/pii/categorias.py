"""Vocabulário do motor de PII: categorias, níveis de confiança e métodos de detecção.

Os enums herdam de StrEnum para virarem texto puro no JSON (por exemplo, "CPF"),
no mesmo padrão UPPER_SNAKE_CASE que o Contrato usa para os valores de enum.
"""

from enum import StrEnum


class Categoria(StrEnum):
    """Tipo de dado pessoal ou credencial encontrado.

    Segue as classificações da seção 9 do Contrato: PII (CPF, CNH, RG, telefone,
    e-mail, nome), PII sensível (saúde), localização (exata ou aproximada) e
    credenciais (senha, token de acesso, qrToken e segredo de API).
    """

    CPF = "CPF"
    CNH = "CNH"
    RG = "RG"
    TELEFONE = "TELEFONE"
    EMAIL = "EMAIL"
    NOME = "NOME"
    SAUDE = "SAUDE"
    LOCALIZACAO_EXATA = "LOCALIZACAO_EXATA"
    LOCALIZACAO_APROXIMADA = "LOCALIZACAO_APROXIMADA"
    SENHA = "SENHA"
    TOKEN_ACESSO = "TOKEN_ACESSO"
    QR_TOKEN = "QR_TOKEN"
    SEGREDO_API = "SEGREDO_API"


class Confianca(StrEnum):
    """Quanto o motor confia no achado.

    ALTA: nome de campo conhecido, dígito verificador conferido ou padrão
    inequívoco. MEDIA: só uma das evidências (por exemplo, 11 dígitos que passam
    no DV do CPF sem nenhuma palavra de contexto por perto).
    """

    ALTA = "ALTA"
    MEDIA = "MEDIA"


class Metodo(StrEnum):
    """Como o achado foi encontrado: pelo nome do campo ou pelo valor (texto)."""

    NOME_CAMPO = "NOME_CAMPO"
    VALOR = "VALOR"
