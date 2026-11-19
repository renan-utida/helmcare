"""Cliente do Gemini (generateContent), com httpx e sem nenhum segredo à vista.

A chave vai só no cabeçalho x-goog-api-key, nunca na URL (URLs aparecem em
logs de proxy e de servidor). As mensagens de erro nunca levam a chave nem a
URL. Do corpo de uma resposta de erro, só entra a mensagem do Google
(error.message), para o diagnóstico, cortada em 150 caracteres e com a chave
trocada por ***, como em "falha na chamada ao Gemini (HTTP 503: The model is
overloaded)".

Falhas passageiras do serviço (429, 500, 502, 503 e 504) ganham uma nova
tentativa depois de 1 s. Erros do pedido (400, 401, 403, 404) não repetem,
porque a segunda tentativa daria o mesmo erro. Tempo esgotado e falha de
conexão também não repetem, para o tempo de uma consulta continuar limitado.
O transporte e a espera são injetáveis, para os testes usarem
httpx.MockTransport sem rede e sem dormir de verdade.
"""

import os
import re
import time
from collections.abc import Callable, Mapping
from urllib.parse import quote

import httpx

from helmcare_cyber.ia.reserva import MotivoReserva

URL_BASE = "https://generativelanguage.googleapis.com/v1beta/models"
TIMEOUT_PADRAO_S = 8.0
TEMPERATURA = 0.3
MAXIMO_TOKENS_SAIDA = 512

# Nova tentativa: no máximo 2 chamadas por consulta, com 1 s entre elas. No
# pior caso, uma consulta leva 2 x 8 s + 1 s.
MAXIMO_TENTATIVAS = 2
ESPERA_RETENTATIVA_S = 1.0
STATUS_REPETIVEIS = frozenset({429, 500, 502, 503, 504})

LIMITE_DETALHE = 150
MASCARA_CHAVE = "***"
# Qualquer coisa com cara de chave do Google também é escondida, mesmo que não seja a nossa.
_PARECE_CHAVE_GOOGLE = re.compile(r"AIza[0-9A-Za-z_-]{10,}")


def limpar_detalhe(texto, chave: str | None = None) -> str:
    """Texto de erro seguro para mostrar: sem a chave, numa linha só e com até 150 caracteres.

    A chave é trocada antes do corte, para não sobrar um pedaço dela no fim.
    """
    if not isinstance(texto, str):
        return ""
    limpo = " ".join(texto.split())
    if chave:
        limpo = limpo.replace(chave, MASCARA_CHAVE)
    limpo = _PARECE_CHAVE_GOOGLE.sub(MASCARA_CHAVE, limpo)
    return limpo[:LIMITE_DETALHE]


class ErroLLM(Exception):
    """Falha ao falar com o LLM.

    motivo diz qual reserva usar; status é o código HTTP (quando houver) e
    detalhe é o texto curto para diagnóstico, já sem a chave.
    """

    def __init__(self, motivo: MotivoReserva, detalhe: str, status: int | None = None) -> None:
        prefixo = "HTTP " if status is not None else ""
        super().__init__(f"falha na chamada ao Gemini ({prefixo}{detalhe})")
        self.motivo = motivo
        self.detalhe = detalhe
        self.status = status


class ClienteGemini:
    def __init__(
        self,
        chave: str,
        modelo: str,
        timeout: float = TIMEOUT_PADRAO_S,
        transporte: httpx.BaseTransport | None = None,
        espera_retentativa_s: float = ESPERA_RETENTATIVA_S,
        dormir: Callable[[float], None] = time.sleep,
    ) -> None:
        if not chave or not modelo:
            raise ValueError("chave e modelo do Gemini são obrigatórios")
        self._chave = chave
        self.modelo = modelo
        self.timeout = timeout
        self._transporte = transporte
        self.espera_retentativa_s = espera_retentativa_s
        self._dormir = dormir

    @classmethod
    def de_ambiente(cls, ambiente: Mapping[str, str] | None = None, **opcoes) -> "ClienteGemini | None":
        """Cliente com GEMINI_API_KEY e GEMINI_MODEL do ambiente, ou None se faltar um dos dois.

        O modelo vem só do ambiente, sem valor padrão no código, para a escolha
        do modelo ficar com quem opera o sistema.
        """
        ambiente = os.environ if ambiente is None else ambiente
        chave = (ambiente.get("GEMINI_API_KEY") or "").strip()
        modelo = (ambiente.get("GEMINI_MODEL") or "").strip()
        if not chave or not modelo:
            return None
        return cls(chave, modelo, **opcoes)

    @property
    def url(self) -> str:
        return f"{URL_BASE}/{quote(self.modelo, safe='-._')}:generateContent"

    def corpo(self, sistema: str, usuario: str) -> dict:
        return {
            "systemInstruction": {"parts": [{"text": sistema}]},
            "contents": [{"role": "user", "parts": [{"text": usuario}]}],
            "generationConfig": {
                "responseMimeType": "application/json",
                "temperature": TEMPERATURA,
                "maxOutputTokens": MAXIMO_TOKENS_SAIDA,
            },
        }

    def _enviar(self, cliente: httpx.Client, corpo: dict) -> httpx.Response:
        try:
            return cliente.post(self.url, json=corpo, headers={"x-goog-api-key": self._chave})
        except httpx.TimeoutException:
            # from None: a exceção original do httpx leva a URL e não deve subir junto.
            raise ErroLLM(MotivoReserva.TIMEOUT, "tempo esgotado") from None
        except httpx.HTTPError:
            raise ErroLLM(MotivoReserva.ERRO_REDE, "falha de conexão") from None

    def _erro_http(self, resposta: httpx.Response) -> ErroLLM:
        """ErroLLM com o código e, se o Google mandou, a mensagem dele (já limpa)."""
        try:
            mensagem = resposta.json()["error"]["message"]
        except (ValueError, KeyError, IndexError, TypeError):
            mensagem = ""
        mensagem = limpar_detalhe(mensagem, self._chave)
        detalhe = f"{resposta.status_code}: {mensagem}" if mensagem else str(resposta.status_code)
        return ErroLLM(MotivoReserva.ERRO_HTTP, detalhe, status=resposta.status_code)

    def gerar_json(self, sistema: str, usuario: str) -> str:
        """Devolve o texto do primeiro candidato. Qualquer falha vira ErroLLM com o motivo."""
        corpo = self.corpo(sistema, usuario)
        with httpx.Client(transport=self._transporte, timeout=self.timeout) as cliente:
            for tentativa in range(1, MAXIMO_TENTATIVAS + 1):
                resposta = self._enviar(cliente, corpo)
                if resposta.status_code in STATUS_REPETIVEIS and tentativa < MAXIMO_TENTATIVAS:
                    self._dormir(self.espera_retentativa_s)
                    continue
                break

        if resposta.status_code != 200:
            raise self._erro_http(resposta)
        try:
            texto = resposta.json()["candidates"][0]["content"]["parts"][0]["text"]
        except (ValueError, KeyError, IndexError, TypeError):
            raise ErroLLM(MotivoReserva.RESPOSTA_INVALIDA, "resposta fora do formato") from None
        if not isinstance(texto, str):
            raise ErroLLM(MotivoReserva.RESPOSTA_INVALIDA, "resposta fora do formato")
        return texto

    def __repr__(self) -> str:
        return f"ClienteGemini(modelo={self.modelo!r}, chave=<oculta>)"

    __str__ = __repr__
