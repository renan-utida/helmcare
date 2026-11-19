"""Proteção do log: filtro do logging e saída protegida para print.

FiltroLogGuarda é um logging.Filter: formata a mensagem (msg % args), passa o
texto pelo guarda com destino LOG e troca a mensagem pela versão segura. Cobre
os erros mais comuns de código:

    logger.info(f"cadastro {dados}")     # dict inteiro numa f-string
    logger.info("login %s", senha)       # senha como argumento
    logger.info(ficha_medica)            # dict de saúde direto no log

SaidaProtegida faz o mesmo com print, quando usada com redirect_stdout (ou com
o atalho proteger_print), para um print(f"senha={senha}") esquecido no código.

As duas falham fechando: se algo der errado, a linha vira
"[LOG BLOQUEADO: erro no filtro]". O filtro nunca registra nada pelo próprio
logging, para não entrar em recursão; um registro gerado enquanto o filtro
trabalha (por exemplo, o aviso de erro do guarda, que não tem conteúdo) passa
sem ser verificado de novo.
"""

import contextlib
import io
import logging
import sys
import threading

from helmcare_cyber.taint.guarda import GuardaTaint, ResultadoGuarda
from helmcare_cyber.taint.modelo import ContextoSaida, Destinatario, Destino, Fonte
from helmcare_cyber.taint.rastro import Rastro

MENSAGEM_ERRO_FILTRO = "[LOG BLOQUEADO: erro no filtro]"


class FiltroLogGuarda(logging.Filter):
    """Filtro que passa toda mensagem de log pelo guarda (destino LOG, destinatário SISTEMA)."""

    def __init__(
        self,
        guarda: GuardaTaint,
        fonte: Fonte | None = None,
        cenario: str | None = None,
        rastro: Rastro | None = None,
    ) -> None:
        super().__init__()
        self.guarda = guarda
        self.fonte = fonte
        self.cenario = cenario
        self.rastro = rastro
        self.contexto = ContextoSaida(Destino.LOG, Destinatario.SISTEMA)
        self.ultimo_resultado: ResultadoGuarda | None = None
        self._local = threading.local()

    def filter(self, record: logging.LogRecord) -> bool:
        if getattr(self._local, "ativo", False):
            return True
        self._local.ativo = True
        try:
            # Um dict passado direto (logger.info(dados)) vai como estrutura, para
            # os nomes de campo ajudarem; o resto vai como o texto final do log.
            if isinstance(record.msg, (dict, list)) and not record.args:
                payload = record.msg
            else:
                payload = record.getMessage()
            resultado = self.guarda.verificar_saida(payload, self.contexto, self.fonte, self.cenario, self.rastro)
            self.ultimo_resultado = resultado
            # Em AUDITORIA, payload_seguro é o próprio conteúdo: o log sai intacto.
            record.msg = resultado.payload_seguro
            record.args = ()
        except Exception:  # noqa: BLE001 (falha fechada)
            record.msg = MENSAGEM_ERRO_FILTRO
            record.args = ()
        finally:
            self._local.ativo = False
        return True


def instalar_filtro(alvo: logging.Logger | logging.Handler, guarda: GuardaTaint, **opcoes) -> FiltroLogGuarda:
    """Instala o filtro num logger (só as mensagens dele) ou num handler (tudo que passa por ele)."""
    filtro = FiltroLogGuarda(guarda, **opcoes)
    alvo.addFilter(filtro)
    return filtro


class SaidaProtegida(io.TextIOBase):
    """Fluxo de texto que verifica cada linha no guarda antes de escrever no destino real."""

    def __init__(
        self,
        guarda: GuardaTaint,
        destino_real,
        fonte: Fonte | None = None,
        cenario: str | None = None,
        rastro: Rastro | None = None,
    ) -> None:
        super().__init__()
        self.guarda = guarda
        self.destino_real = destino_real
        self.fonte = fonte
        self.cenario = cenario
        self.rastro = rastro
        self.ultimo_resultado: ResultadoGuarda | None = None
        self._pendente = ""

    def writable(self) -> bool:
        return True

    def write(self, texto: str) -> int:
        # print chama write várias vezes por linha; a verificação é feita por linha completa.
        self._pendente += texto
        while "\n" in self._pendente:
            linha, self._pendente = self._pendente.split("\n", 1)
            self._emitir(linha, "\n")
        return len(texto)

    def flush(self) -> None:
        if self._pendente:
            linha, self._pendente = self._pendente, ""
            self._emitir(linha, "")
        self.destino_real.flush()

    def _emitir(self, linha: str, fim: str) -> None:
        try:
            resultado = self.guarda.verificar_saida(
                linha, ContextoSaida(Destino.LOG, Destinatario.SISTEMA), self.fonte, self.cenario, self.rastro
            )
            self.ultimo_resultado = resultado
            texto = str(resultado.payload_seguro)
        except Exception:  # noqa: BLE001 (falha fechada)
            texto = MENSAGEM_ERRO_FILTRO
        self.destino_real.write(texto + fim)


@contextlib.contextmanager
def proteger_print(guarda: GuardaTaint, destino_real=None, **opcoes):
    """Dentro do bloco, todo print passa pelo guarda antes de chegar ao destino real."""
    saida = SaidaProtegida(guarda, destino_real if destino_real is not None else sys.stdout, **opcoes)
    with contextlib.redirect_stdout(saida):
        yield saida
    saida.flush()
