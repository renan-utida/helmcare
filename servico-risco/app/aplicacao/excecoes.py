"""Exceções da camada de aplicação.

ServicoIndisponivelError não é regra de negócio: indica que uma dependência do serviço
(o modelo de ML ou o banco) não está disponível. A API a traduz para 503.
"""


class ServicoIndisponivelError(Exception):
    """Dependência indisponível (modelo não carregado, banco fora do ar ou sem dados)."""
