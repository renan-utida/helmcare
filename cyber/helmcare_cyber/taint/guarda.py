"""Guarda de saída: detecta dado pessoal numa saída, decide pela política e aplica a ação.

Fluxo de verificar_saida:
1. Detecção: o motor de PII (formatos e nomes de campo) e o registro de taint
   (valores que entraram por uma fonte, como o nome do entregador).
2. Decisão: a política decide cada achado. A regra do MQTT depende de haver
   identificação pessoal em qualquer ponto do payload, por isso todos os
   achados são reunidos antes de decidir.
3. Aplicação: em PROTECAO, a ação é aplicada numa cópia (o motor coloca o novo
   valor no lugar exato de cada achado). Em AUDITORIA, a saída passa intacta.
4. Registro: um evento por categoria e estado, inclusive PERMITIDO, para provar
   o uso legítimo dos canais autorizados.

Falha fechada: qualquer erro interno bloqueia a saída inteira e gera um evento
TA-ERRO-01, sem o conteúdo e sem a mensagem da exceção.
"""

import copy
import json
import logging
from collections import defaultdict
from dataclasses import dataclass, field

from helmcare_cyber.eventos.modelo import EventoSeguranca
from helmcare_cyber.eventos.repositorio import RepositorioEventos
from helmcare_cyber.pii.categorias import Confianca, Metodo
from helmcare_cyber.pii.mascaras import mascarar_localizacao, mascarar_valor, redigir
from helmcare_cyber.pii.motor import Achado, MotorPII, ValorProtegido, caminho_filho
from helmcare_cyber.taint.modelo import (
    Acao,
    ContextoSaida,
    DecisaoAchado,
    Estado,
    Fonte,
    Modo,
    pior_estado,
)
from helmcare_cyber.taint.politica import (
    IDENTIFICACAO_PESSOAL,
    TA_ERRO_01,
    TA_REG_01,
    decidir,
    esta_redigido,
)
from helmcare_cyber.taint.rastro import Rastro
from helmcare_cyber.taint.registro import TaintRegistry
from helmcare_cyber.taint.sanitizadores import arredondar_localizacao, bloquear

_LOG = logging.getLogger("helmcare_cyber.taint.guarda")

MARCADOR_ERRO = "[BLOQUEADO:ERRO_INTERNO]"


@dataclass
class ResultadoGuarda:
    """Resultado de uma verificação. payload_seguro fica fora do repr: em AUDITORIA é o original."""

    decisao_geral: Estado
    payload_seguro: object = field(repr=False)
    decisoes: list[DecisaoAchado]
    eventos: list[EventoSeguranca]
    rastro: Rastro

    def regras_aplicadas(self) -> list[str]:
        """Ids de regra na ordem em que apareceram, sem repetição."""
        return list(dict.fromkeys(d.regra_id for d in self.decisoes))


@dataclass
class _Item:
    """Um achado com a decisão tomada (uso interno, não sai do guarda)."""

    achado: Achado
    decisao: DecisaoAchado
    valor_saida: str


def _eh_numero(valor) -> bool:
    return isinstance(valor, (int, float)) and not isinstance(valor, bool)


def tratar(acao: Acao, categoria, valor):
    """Aplica uma ação a um valor. PERMITIR devolve o próprio valor."""
    if acao is Acao.PERMITIR:
        return valor
    if acao is Acao.BLOQUEAR:
        return bloquear(categoria)
    if acao is Acao.REDIGIR:
        return redigir(categoria)
    if acao is Acao.MASCARAR:
        return mascarar_valor(categoria, valor)
    # ARREDONDAR: número continua número; texto tem cada coordenada arredondada.
    return arredondar_localizacao(valor) if _eh_numero(valor) else mascarar_localizacao(str(valor))


def _folhas(valor, caminho: str = "$"):
    """(caminho, texto) de cada folha de texto ou número, no formato de caminho do motor."""
    if isinstance(valor, dict):
        for chave, item in valor.items():
            yield from _folhas(item, caminho_filho(caminho, chave))
    elif isinstance(valor, (list, tuple)):
        for indice, item in enumerate(valor):
            yield from _folhas(item, f"{caminho}[{indice}]")
    elif isinstance(valor, str):
        yield caminho, valor
    elif _eh_numero(valor):
        yield caminho, repr(valor) if isinstance(valor, float) else str(valor)


def _trocar_nas_folhas(valor, trocas: dict[str, list[tuple[str, str]]], caminho: str = "$"):
    """Reconstrói a estrutura trocando trechos nas folhas indicadas (sem alterar a original)."""
    if isinstance(valor, dict):
        return {chave: _trocar_nas_folhas(item, trocas, caminho_filho(caminho, chave)) for chave, item in valor.items()}
    if isinstance(valor, (list, tuple)):
        return type(valor)(_trocar_nas_folhas(item, trocas, f"{caminho}[{i}]") for i, item in enumerate(valor))
    if caminho not in trocas or not (isinstance(valor, str) or _eh_numero(valor)):
        return valor
    texto = valor if isinstance(valor, str) else (repr(valor) if isinstance(valor, float) else str(valor))
    original = texto
    # Trechos mais longos primeiro, para um nome completo não ser cortado por um pedaço dele.
    for trecho, novo in sorted(trocas[caminho], key=lambda t: len(t[0]), reverse=True):
        texto = texto.replace(trecho, novo)
    return texto if texto != original else valor


def _trechos(texto: str, procurado: str) -> list[tuple[int, int]]:
    posicoes, inicio = [], texto.find(procurado)
    while procurado and inicio >= 0:
        posicoes.append((inicio, inicio + len(procurado)))
        inicio = texto.find(procurado, inicio + 1)
    return posicoes


class GuardaTaint:
    """Verifica cada saída contra a política. Uma instância pode servir a vários destinos."""

    def __init__(
        self,
        motor: MotorPII | None = None,
        registro: TaintRegistry | None = None,
        repositorio: RepositorioEventos | None = None,
        modo: Modo = Modo.PROTECAO,
    ) -> None:
        self.motor = motor or MotorPII()
        self.registro = registro if registro is not None else TaintRegistry()
        self.repositorio = repositorio if repositorio is not None else RepositorioEventos()
        self.modo = Modo(modo)

    def registrar_fonte(self, fonte: Fonte, payload, rastro: Rastro | None = None, descricao: str = "") -> list[Achado]:
        """Marca um payload que entrou por uma fonte: registra os valores e anota a FONTE no rastro."""
        achados = self.motor.varrer(payload)
        self.registro.registrar(fonte, achados)
        if rastro is not None:
            rastro.adicionar_fontes(fonte.value, achados, descricao)
        return achados

    def verificar_saida(
        self,
        payload,
        contexto: ContextoSaida,
        fonte: Fonte | None = None,
        cenario: str | None = None,
        rastro: Rastro | None = None,
    ) -> ResultadoGuarda:
        rastro = rastro if rastro is not None else Rastro()
        try:
            return self._verificar(payload, contexto, fonte, cenario, rastro)
        except Exception as erro:  # noqa: BLE001 (falha fechada de propósito)
            return self._falhar_fechado(erro, contexto, fonte, cenario, rastro)

    # ------------------------------------------------------------------

    def _verificar(self, payload, contexto, fonte, cenario, rastro) -> ResultadoGuarda:
        base = bytes(payload).decode("utf-8", errors="replace") if isinstance(payload, (bytes, bytearray)) else payload
        estrutura = MotorPII.ler_json_estruturado(base) if isinstance(base, str) else None
        alvo = estrutura if estrutura is not None else base

        achados_motor = self.motor.varrer(alvo)
        achados = achados_motor + self._achados_do_registro(alvo, achados_motor)
        identificacao = any(
            a.categoria in IDENTIFICACAO_PESSOAL and not esta_redigido(a.valor_original()) for a in achados
        )
        itens = [self._decidir(achado, contexto, identificacao) for achado in achados]

        if self.modo is Modo.PROTECAO:
            payload_seguro = self._aplicar(payload, base, estrutura, alvo, achados_motor, itens, contexto, identificacao)
        else:
            payload_seguro = copy.deepcopy(payload)

        if fonte is not None and not rastro.tem_fonte():
            rastro.adicionar_fonte(fonte.value, "dado de entrada")
        self._anotar_destino(rastro, contexto, itens)
        eventos = self._gerar_eventos(itens, contexto, fonte, cenario, rastro)
        for evento in eventos:
            self.repositorio.adicionar(evento)
        decisoes = [item.decisao for item in itens]
        return ResultadoGuarda(pior_estado(d.estado for d in decisoes), payload_seguro, decisoes, eventos, rastro)

    def _achados_do_registro(self, alvo, achados_motor: list[Achado]) -> list[Achado]:
        """Valores registrados nas folhas de texto que o motor não cobriu."""
        if not len(self.registro):
            return []
        campos_por_nome = {a.caminho for a in achados_motor if a.metodo is Metodo.NOME_CAMPO}
        originais_por_caminho: dict[str, list[str]] = defaultdict(list)
        for achado in achados_motor:
            if achado.metodo is Metodo.VALOR:
                originais_por_caminho[achado.caminho].append(achado.valor_original())

        novos: list[Achado] = []
        for caminho, texto in _folhas(alvo):
            if caminho in campos_por_nome:
                continue  # o campo inteiro já é um achado por nome
            cobertos = [t for original in originais_por_caminho[caminho] for t in _trechos(texto, original)]
            for ocorrencia in self.registro.procurar(texto):
                if any(ocorrencia.inicio < fim and inicio < ocorrencia.fim for inicio, fim in cobertos):
                    continue  # o motor já achou esse trecho
                trecho = ocorrencia.trecho()
                novos.append(
                    Achado(
                        caminho=caminho,
                        categoria=ocorrencia.categoria,
                        metodo=Metodo.VALOR,
                        confianca=Confianca.ALTA,
                        regra_id=TA_REG_01,
                        dv_confere=None,
                        ja_mascarado=False,
                        valor_mascarado=str(mascarar_valor(ocorrencia.categoria, trecho)),
                        _original=ValorProtegido(trecho),
                    )
                )
        return novos

    def _decidir(self, achado: Achado, contexto: ContextoSaida, identificacao: bool) -> _Item:
        acao, regra_id, motivo = decidir(achado, contexto, identificacao)
        original = achado.valor_original()
        valor_saida = tratar(acao, achado.categoria, original)
        if acao is Acao.PERMITIR:
            estado = Estado.PERMITIDO
        elif str(valor_saida) == original:
            # A ação não muda nada: o valor já estava na forma exigida (por exemplo,
            # o CPF já mascarado na resposta ao próprio entregador).
            estado = Estado.PERMITIDO
            motivo += " (já estava na forma exigida)"
        else:
            estado = Estado.NEUTRALIZADO if self.modo is Modo.PROTECAO else Estado.EXPOSTO
        decisao = DecisaoAchado(achado.caminho, achado.categoria, acao, regra_id, estado, motivo)
        valor_mostrado = achado.valor_mascarado if acao is Acao.PERMITIR else str(valor_saida)
        return _Item(achado, decisao, valor_mostrado)

    def _aplicar(self, payload, base, estrutura, alvo, achados_motor, itens, contexto, identificacao):
        """Aplica as ações numa cópia: o motor troca os seus achados; o registro troca os dele."""

        def substituir(achado, valor_atual):
            acao, _, _ = decidir(achado, contexto, identificacao)
            return None if acao is Acao.PERMITIR else tratar(acao, achado.categoria, valor_atual)

        copia, _ = self.motor.transformar(alvo, substituir)

        trocas: dict[str, list[tuple[str, str]]] = defaultdict(list)
        for item in itens[len(achados_motor) :]:  # os achados do registro vêm depois dos do motor
            if item.decisao.acao is not Acao.PERMITIR:
                trocas[item.achado.caminho].append((item.achado.valor_original(), str(item.valor_saida)))
        if trocas:
            copia = _trocar_nas_folhas(copia, dict(trocas))

        if estrutura is not None:  # o payload era JSON em texto: volta como JSON em texto
            return json.dumps(copia, ensure_ascii=False) if copia != estrutura else payload
        if isinstance(base, str):
            return copia
        return copia if copia is not alvo else copy.deepcopy(payload)

    def _anotar_destino(self, rastro: Rastro, contexto: ContextoSaida, itens: list[_Item]) -> None:
        saida = f"{contexto.destino.value} para {contexto.destinatario.value}"
        if not itens:
            rastro.adicionar_destino(f"{saida}: nenhum dado pessoal")
            return
        grupos: dict[tuple, list[_Item]] = defaultdict(list)
        for item in itens:
            grupos[(item.decisao.regra_id, item.decisao.acao, item.decisao.estado)].append(item)
        for (regra_id, acao, estado), grupo in grupos.items():
            valor = grupo[0].valor_saida if len(grupo) == 1 else None
            rastro.adicionar_destino(
                f"{saida}, REGRA {regra_id}, ACAO {acao.value}, {estado.value}",
                [item.decisao.categoria for item in grupo],
                valor,
            )

    def _gerar_eventos(self, itens, contexto, fonte, cenario, rastro) -> list[EventoSeguranca]:
        grupos: dict[tuple, list[DecisaoAchado]] = defaultdict(list)
        for item in itens:
            d = item.decisao
            grupos[(d.categoria, d.estado, d.acao, d.regra_id)].append(d)
        passos = tuple(rastro.para_dict())
        return [
            EventoSeguranca(
                cenario=cenario,
                fonte=fonte,
                destino=contexto.destino,
                destinatario=contexto.destinatario,
                categoria=categoria,
                acao=acao,
                estado=estado,
                regra_id=regra_id,
                modo=self.modo,
                quantidade=len(decisoes),
                caminhos=tuple(dict.fromkeys(d.caminho for d in decisoes)),
                rastro=passos,
            )
            for (categoria, estado, acao, regra_id), decisoes in grupos.items()
        ]

    def _falhar_fechado(self, erro, contexto, fonte, cenario, rastro) -> ResultadoGuarda:
        # Só o tipo do erro: a mensagem de uma exceção pode conter o próprio conteúdo.
        _LOG.error("falha interna na verificação de saída (%s); saída bloqueada", type(erro).__name__)
        eventos: list[EventoSeguranca] = []
        try:
            rastro.adicionar_destino("saída bloqueada inteira, REGRA TA-ERRO-01, ACAO BLOQUEAR (erro interno)")
            evento = EventoSeguranca(
                cenario=cenario,
                fonte=fonte if isinstance(fonte, Fonte) else None,
                destino=contexto.destino,
                destinatario=contexto.destinatario,
                categoria=None,
                acao=Acao.BLOQUEAR,
                estado=Estado.NEUTRALIZADO,
                regra_id=TA_ERRO_01,
                modo=self.modo,
                quantidade=0,
                rastro=tuple(rastro.para_dict()),
            )
            self.repositorio.adicionar(evento)
            eventos.append(evento)
        except Exception:  # noqa: BLE001 (nem o registro do erro pode derrubar a saída)
            pass
        return ResultadoGuarda(Estado.NEUTRALIZADO, MARCADOR_ERRO, [], eventos, rastro)
