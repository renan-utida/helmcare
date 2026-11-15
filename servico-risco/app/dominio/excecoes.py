"""Exceções do domínio do Serviço de Risco.

A camada de API (app/api/erros.py) traduz cada uma para um status HTTP em ProblemDetails:
ValidacaoDominioError vira 400 e RegraNegocioError vira 409.
"""


class DominioError(Exception):
    """Base de todas as exceções do domínio."""


class ValidacaoDominioError(DominioError):
    """Entrada inválida para uma regra do domínio (vira 400 na API)."""


class RegraNegocioError(DominioError):
    """Entrada válida, mas que viola uma regra de negócio (vira 409 na API, se precisar)."""
