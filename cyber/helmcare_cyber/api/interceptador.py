"""Interceptador de entrada: varre o dado que chega, marca o que é PII e mascara antes do log e do banco.

É uma dependência do FastAPI (interceptar_cadastro), usada pelo
POST /cyber/v1/demo/entregadores. O fluxo:

1. O FastAPI lê e valida o JSON (CadastroEntregador, regras da seção 3 do Contrato).
2. MotorPII.varrer marca cada campo pessoal.
3. Os valores vão para o registro de taint (fonte CADASTRO_ENTREGADOR), para
   serem reconhecidos depois mesmo concatenados num texto.
4. Em PROTECAO, o handler recebe uma cópia com as máscaras do Contrato e a
   senha já como hash. Em AUDITORIA, recebe o original: o modo existe para
   mostrar o vazamento acontecendo.

O handler (registrar_cadastro) faz o que um código descuidado faria: loga o
cadastro numa f-string, grava no banco e responde. Cada uma dessas saídas
passa pelo guarda, e a resposta ainda passa pelas máscaras do motor no fim:
a resposta HTTP nunca leva o valor original, em nenhum modo.
"""

import re
from dataclasses import dataclass, field

from fastapi import Depends
from pydantic import BaseModel, ConfigDict, Field, field_validator

from helmcare_cyber.api.estado import EstadoApp, obter_estado
from helmcare_cyber.pii.categorias import Categoria
from helmcare_cyber.pii.mascaras import mascarar_valor
from helmcare_cyber.pii.motor import Achado, MotorPII
from helmcare_cyber.pii.validadores import somente_digitos
from helmcare_cyber.taint.guarda import ResultadoGuarda
from helmcare_cyber.taint.politica import esta_redigido
from helmcare_cyber.taint.modelo import ContextoSaida, Destinatario, Destino, Estado, Fonte, Modo
from helmcare_cyber.taint.rastro import Rastro, TipoPasso
from helmcare_cyber.taint.sanitizadores import eh_hash_senha, hash_senha

CENARIO_DEMO = "DEMO"
ROTA_DEMO = "POST /cyber/v1/demo/entregadores"
_EMAIL = re.compile(r"[^@\s]+@[^@\s]+\.[^@\s]+")


class CadastroEntregador(BaseModel):
    """Corpo do cadastro do entregador (seção 3 do Contrato). Campo a mais é recusado."""

    model_config = ConfigDict(extra="forbid")

    nome: str = Field(min_length=1, max_length=120)
    cpf: str = Field(max_length=20)
    cnh: str = Field(max_length=20)
    telefone: str = Field(max_length=25)
    email: str = Field(max_length=254)
    senha: str = Field(min_length=8, max_length=64)

    @field_validator("cpf", "cnh")
    @classmethod
    def _onze_digitos(cls, valor: str) -> str:
        if len(somente_digitos(valor)) != 11:
            raise ValueError("precisa ter 11 digitos")
        return valor

    @field_validator("telefone")
    @classmethod
    def _telefone(cls, valor: str) -> str:
        if len(somente_digitos(valor)) not in (10, 11):
            raise ValueError("precisa ter 10 ou 11 digitos, com DDD")
        return valor

    @field_validator("email")
    @classmethod
    def _email(cls, valor: str) -> str:
        if not _EMAIL.fullmatch(valor):
            raise ValueError("formato de e-mail invalido")
        return valor

    @field_validator("senha")
    @classmethod
    def _senha(cls, valor: str) -> str:
        if not (re.search(r"[A-Z]", valor) and re.search(r"[a-z]", valor) and re.search(r"\d", valor)):
            raise ValueError("precisa de letra maiuscula, minuscula e digito")
        return valor


@dataclass
class EntradaInterceptada:
    """O que o interceptador entrega ao handler. dados fica fora do repr (em AUDITORIA, é o original)."""

    modo: Modo
    dados: dict = field(repr=False)
    achados: list[Achado] = field(repr=False)
    rastro: Rastro

    def resumo(self) -> dict:
        """O que o interceptador achou, só com valores mascarados."""
        vistas = {a.categoria for a in self.achados}
        return {
            "modo": self.modo.value,
            "entregueAoHandler": "COPIA_MASCARADA" if self.modo is Modo.PROTECAO else "ORIGINAL",
            "quantidade": len(self.achados),
            "categorias": [c.value for c in Categoria if c in vistas],
            "campos": [a.para_dict() for a in self.achados],
        }


def interceptar_cadastro(
    cadastro: CadastroEntregador, estado: EstadoApp = Depends(obter_estado)
) -> EntradaInterceptada:
    """Passos 2 a 4 do fluxo: marca, registra a fonte e decide o que o handler recebe."""
    original = cadastro.model_dump()
    rastro = Rastro()
    modo = estado.modo
    achados = estado.guarda.registrar_fonte(Fonte.CADASTRO_ENTREGADOR, original, rastro, ROTA_DEMO)

    if modo is Modo.PROTECAO:
        copia, _ = estado.guarda.motor.mascarar(original)
        # A senha não vira [REDIGIDO]: o banco precisa do hash dela.
        copia["senha"] = hash_senha(original["senha"], rastro)
        rastro.adicionar_sanitizador(
            "interceptador de entrada: máscaras do Contrato antes do log e do banco",
            [a.categoria for a in achados],
        )
        return EntradaInterceptada(modo, copia, achados, rastro)

    rastro.adicionar_propagacao("interceptador em AUDITORIA: o original segue para o handler")
    return EntradaInterceptada(modo, dict(original), achados, rastro)


def mascarar_resposta(motor: MotorPII, payload):
    """Barreira final da resposta HTTP: as máscaras do Contrato em tudo que ainda for dado pessoal.

    Os marcadores do guarda ([BLOQUEADO:SENHA], [REDIGIDO:NOME]) ficam como
    estão, para a resposta mostrar o que o guarda fez.
    """

    def substituir(achado, valor_atual):
        if isinstance(valor_atual, str) and esta_redigido(valor_atual):
            return None
        return mascarar_valor(achado.categoria, valor_atual)

    copia, _ = motor.transformar(payload, substituir)
    return copia


def _resumo_saida(destino: Destino, resultado: ResultadoGuarda | None) -> dict:
    if resultado is None:
        return {"destino": destino.value, "decisaoGeral": None, "regras": [], "estados": {}}
    estados = {estado.value: 0 for estado in Estado}
    for decisao in resultado.decisoes:
        estados[decisao.estado.value] += 1
    return {
        "destino": destino.value,
        "decisaoGeral": resultado.decisao_geral.value,
        "regras": resultado.regras_aplicadas(),
        "estados": estados,
    }


def registrar_cadastro(entrada: EntradaInterceptada, estado: EstadoApp) -> dict:
    """O handler ingênuo da demonstração: loga, grava e responde, cada saída pelo guarda."""
    with estado.lock:
        guarda = estado.guarda
        dados, rastro = entrada.dados, entrada.rastro
        eventos_antes = len(guarda.repositorio)

        # Log: a f-string que todo mundo escreve. O filtro do logger passa a linha pelo guarda.
        rastro.adicionar_propagacao('f-string no log do servidor: logger.info(f"cadastro recebido {dados}")')
        estado.filtro_log.ultimo_resultado = None
        estado.logger_demo.info(f"cadastro recebido {dados}")
        resultado_log = estado.filtro_log.ultimo_resultado
        if resultado_log is not None:
            rastro.passos.extend(p for p in resultado_log.rastro.passos if p.tipo is TipoPasso.DESTINO)

        # Banco: só o hash da senha é gravado, nos dois modos.
        para_banco = dict(dados)
        if not eh_hash_senha(para_banco["senha"]):
            para_banco["senha"] = hash_senha(para_banco["senha"], rastro)
        rastro.adicionar_propagacao("cadastro gravado no banco simulado")
        resultado_banco = guarda.verificar_saida(
            para_banco, ContextoSaida(Destino.PERSISTENCIA), Fonte.CADASTRO_ENTREGADOR, CENARIO_DEMO, rastro
        )
        gravado = resultado_banco.payload_seguro
        identificador = estado.banco.salvar(gravado if isinstance(gravado, dict) else {"bloqueado": True})

        # Resposta ao próprio entregador, pelo guarda e depois pelas máscaras do motor.
        rastro.adicionar_propagacao("cadastro devolvido na resposta do POST")
        resultado_resposta = guarda.verificar_saida(
            dados,
            ContextoSaida(Destino.RESPOSTA_PROPRIO_ENTREGADOR, Destinatario.PROPRIO_ENTREGADOR),
            Fonte.CADASTRO_ENTREGADOR,
            CENARIO_DEMO,
            rastro,
        )
        entregador = mascarar_resposta(guarda.motor, resultado_resposta.payload_seguro)

        return {
            "id": identificador,
            "modo": guarda.modo.value,
            "interceptador": entrada.resumo(),
            "saidas": [
                _resumo_saida(Destino.LOG, resultado_log),
                _resumo_saida(Destino.PERSISTENCIA, resultado_banco),
                _resumo_saida(Destino.RESPOSTA_PROPRIO_ENTREGADOR, resultado_resposta),
            ],
            "entregador": entregador,
            "rastro": rastro.para_texto(),
            "eventos": len(guarda.repositorio) - eventos_antes,
        }
