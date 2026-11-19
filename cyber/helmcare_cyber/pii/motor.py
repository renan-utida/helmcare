"""Motor de parsing PII: percorre dados estruturados ou texto e devolve os achados.

Entrada aceita: dict e list (aninhados), str com JSON e texto livre. Nada é
alterado na entrada: mascarar() devolve uma cópia nova.

Regra de ouro do módulo: o valor original de um dado pessoal nunca sai do
motor. O Achado guarda o original dentro de um ValorProtegido, que não aparece
no repr, no str nem no JSON, e se recusa a ser serializado por pickle.
"""

import json
import re
import unicodedata
from collections.abc import Callable
from dataclasses import dataclass, field

from helmcare_cyber.pii.categorias import Categoria, Confianca, Metodo
from helmcare_cyber.pii.mascaras import esta_mascarado, mascarar_valor, parece_numero
from helmcare_cyber.pii.regras import (
    ETAPA_DESEMPATE,
    NOMES_DE_CAMPO,
    ORDEM_TEXTO,
    PADRAO_ONZE_DIGITOS,
    REGRA_LOC_APROX_CAMPO,
    REGRA_LOC_EXATA_CAMPO,
    REGRAS_CAMPO,
    Alvo,
    Exigencia,
    Regra,
    desempatar_onze_digitos,
    dobrar,
    normalizar_nome_campo,
    proteger,
    tem_contexto,
)
from helmcare_cyber.pii.validadores import (
    cnh_valida,
    cpf_valido,
    numero_de_casas_decimais,
    somente_digitos,
)

# Inteiros com 10 dígitos ou mais podem ser um CPF ou telefone gravado como
# número. Abaixo disso (telemetria, contadores, segundos), não há o que procurar.
MINIMO_DIGITOS_INTEIRO = 10

# Chaves de dicionário só são varridas quando podem conter um documento
# (muitos dígitos) ou um e-mail. Varrer toda chave faria o léxico de saúde
# marcar nomes de campo como "laudo-medico".
MINIMO_DIGITOS_CHAVE = 8

_CHAVE_SIMPLES = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")


class ValorProtegido:
    """Embrulho do valor original, só para uso interno em memória.

    Não mostra o conteúdo no repr nem no str, não é serializável em JSON (o
    json.dumps levanta TypeError) e bloqueia pickle. copy e deepcopy devolvem o
    mesmo objeto, para dataclasses.asdict continuar funcionando sem expor nada.
    """

    __slots__ = ("_valor",)

    def __init__(self, valor: str) -> None:
        self._valor = valor

    def revelar(self) -> str:
        return self._valor

    def __repr__(self) -> str:
        return "ValorProtegido(<oculto>)"

    __str__ = __repr__

    def __copy__(self):
        return self

    def __deepcopy__(self, memo):
        return self

    def __reduce_ex__(self, protocolo):
        raise TypeError("ValorProtegido não pode ser serializado")


@dataclass(frozen=True)
class Achado:
    """Um dado pessoal encontrado. Só os campos públicos vão para repr e JSON.

    caminho: onde está, no formato $.contatos[0].telefone ($ para texto solto).
    dv_confere: True ou False para CPF e CNH, None para as outras categorias.
    ja_mascarado: o valor já estava numa forma segura (máscara do Contrato,
    [REDIGIDO:...] ou localização aproximada), então não é vazamento.
    """

    caminho: str
    categoria: Categoria
    metodo: Metodo
    confianca: Confianca
    regra_id: str
    dv_confere: bool | None
    ja_mascarado: bool
    valor_mascarado: str
    _original: ValorProtegido = field(
        default_factory=lambda: ValorProtegido(""), repr=False, compare=False
    )

    def valor_original(self) -> str:
        """Registro interno em memória (por exemplo, para o Taint Analysis comparar valores).

        Nunca registre o retorno em log, evento ou resposta.
        """
        return self._original.revelar()

    def para_dict(self) -> dict:
        """Só os campos públicos, com os enums como texto."""
        return {
            "caminho": self.caminho,
            "categoria": self.categoria.value,
            "metodo": self.metodo.value,
            "confianca": self.confianca.value,
            "regraId": self.regra_id,
            "dvConfere": self.dv_confere,
            "jaMascarado": self.ja_mascarado,
            "valorMascarado": self.valor_mascarado,
        }

    def para_json(self) -> str:
        return json.dumps(self.para_dict(), ensure_ascii=False)


def _nfkc(texto: str) -> str:
    """NFKC junta formas equivalentes: dígitos de largura total viram 0 a 9, por exemplo."""
    return unicodedata.normalize("NFKC", texto)


def _caminho_filho(caminho: str, chave) -> str:
    texto = str(chave)
    if _CHAVE_SIMPLES.fullmatch(texto):
        return f"{caminho}.{texto}"
    return f"{caminho}[{json.dumps(texto, ensure_ascii=False)}]"


# Nome público, para quem precisa montar caminhos no mesmo formato dos achados.
caminho_filho = _caminho_filho

# substituir(achado, valor_atual) devolve o novo valor, ou None para manter o atual.
Substituidor = Callable[["Achado", object], object]


def _sobrepoe(inicio: int, fim: int, ocupados: list[tuple[int, int]]) -> bool:
    return any(inicio < o_fim and o_inicio < fim for o_inicio, o_fim in ocupados)


def _eh_numero(valor) -> bool:
    return isinstance(valor, (int, float)) and not isinstance(valor, bool)


class MotorPII:
    """Encontra e mascara dados pessoais em payloads do HelmCare."""

    def varrer(self, dado) -> list[Achado]:
        """Devolve os achados de PII do dado, sem alterar nada."""
        _, achados = self._processar_raiz(dado)
        return achados

    def mascarar(self, dado) -> tuple[object, list[Achado]]:
        """Devolve (cópia profunda com a PII mascarada, achados). A entrada não muda."""
        return self._processar_raiz(dado)

    def transformar(self, dado, substituir: Substituidor) -> tuple[object, list[Achado]]:
        """Como mascarar, mas quem decide o novo valor de cada achado é substituir.

        substituir(achado, valor_atual) recebe o valor do campo (achado por nome
        de campo) ou o trecho do texto (achado pelo valor) e devolve o valor que
        fica no lugar, ou None para manter o atual. É o ponto em que o Taint
        Analysis aplica a política sem precisar localizar o dado de novo.
        """
        return self._processar_raiz(dado, substituir)

    # ------------------------------------------------------------------
    # Entrada
    # ------------------------------------------------------------------

    def _processar_raiz(self, dado, substituir: Substituidor | None = None) -> tuple[object, list[Achado]]:
        if isinstance(dado, (bytes, bytearray)):
            dado = bytes(dado).decode("utf-8", errors="replace")
        if isinstance(dado, str):
            estrutura = self._tentar_json(dado)
            if estrutura is not None:
                copia, achados = self._processar(estrutura, "$", None, substituir)
                # Sem mudança, devolve o texto original intacto (mesma formatação).
                mudou = bool(achados) if substituir is None else copia != estrutura
                return (json.dumps(copia, ensure_ascii=False) if mudou else dado), achados
            return self._varrer_texto(dado, "$", substituir)
        return self._processar(dado, "$", None, substituir)

    @staticmethod
    def _tentar_json(texto: str):
        """Só aceita JSON que seja objeto ou lista. JSON inválido vira texto livre."""
        inicio = texto.lstrip()[:1]
        if inicio not in ("{", "["):
            return None
        try:
            estrutura = json.loads(texto)
        except (ValueError, RecursionError):
            return None
        return estrutura if isinstance(estrutura, (dict, list)) else None

    # Nome público: quem precisa ler um payload do mesmo jeito que o motor
    # (para os caminhos dos achados baterem) usa esta função.
    ler_json_estruturado = _tentar_json

    # ------------------------------------------------------------------
    # Dados estruturados
    # ------------------------------------------------------------------

    def _processar(
        self, valor, caminho: str, herdada: Categoria | None, substituir: Substituidor | None = None
    ) -> tuple[object, list[Achado]]:
        """Percorre recursivamente. herdada é a categoria do campo pai (ex.: lista de medicamentos)."""
        if isinstance(valor, dict):
            return self._processar_dict(valor, caminho, herdada, substituir)
        if isinstance(valor, (list, tuple)):
            novos, achados = [], []
            for indice, item in enumerate(valor):
                novo, encontrados = self._processar(item, f"{caminho}[{indice}]", herdada, substituir)
                novos.append(novo)
                achados.extend(encontrados)
            return type(valor)(novos), achados
        if herdada is not None:
            return self._achado_por_campo(valor, caminho, herdada, substituir)
        if isinstance(valor, str):
            return self._varrer_texto(valor, caminho, substituir)
        if isinstance(valor, int) and not isinstance(valor, bool):
            if len(str(abs(valor))) >= MINIMO_DIGITOS_INTEIRO:
                mascarado, achados = self._varrer_texto(str(valor), caminho, substituir)
                return (mascarado if achados and mascarado != str(valor) else valor), achados
        # None, bool, float e inteiros curtos (telemetria) não são PII pelo valor.
        return valor, []

    def _processar_dict(self, valor: dict, caminho: str, herdada: Categoria | None, substituir=None):
        novo: dict = {}
        achados: list[Achado] = []
        for chave, item in valor.items():
            categoria = NOMES_DE_CAMPO.get(normalizar_nome_campo(chave))
            nova_chave = chave
            if categoria is None and isinstance(chave, str) and self._chave_suspeita(chave):
                chave_mascarada, achados_chave = self._varrer_texto(chave, f"{caminho}.<chave>", substituir)
                if achados_chave:
                    nova_chave = chave_mascarada
                    achados.extend(achados_chave)
            filho = _caminho_filho(caminho, nova_chave)
            novo_item, encontrados = self._processar(item, filho, categoria or herdada, substituir)
            novo[nova_chave] = novo_item
            achados.extend(encontrados)
        return novo, achados

    @staticmethod
    def _chave_suspeita(chave: str) -> bool:
        return "@" in chave or len(somente_digitos(_nfkc(chave))) >= MINIMO_DIGITOS_CHAVE

    def _achado_por_campo(self, valor, caminho: str, categoria: Categoria, substituir=None):
        """Marca o valor de um campo com nome conhecido (o nome basta, confiança ALTA)."""
        if valor is None or isinstance(valor, bool):
            return valor, []
        if not _eh_numero(valor) and not isinstance(valor, str):
            return valor, []
        normalizado = _nfkc(valor) if isinstance(valor, str) else valor
        if isinstance(normalizado, str) and not normalizado.strip():
            return valor, []

        regra = REGRAS_CAMPO[categoria]
        if categoria is Categoria.LOCALIZACAO_EXATA:
            categoria, regra = self._classificar_coordenada(normalizado)

        ja_mascarado = esta_mascarado(categoria, normalizado)
        mascarado = normalizado if ja_mascarado else mascarar_valor(categoria, normalizado)

        # O Contrato só exige 11 dígitos para cpf e cnh, então o campo é marcado
        # mesmo com DV errado; dv_confere registra se a conta bate.
        dv_confere = None
        if not ja_mascarado and categoria is Categoria.CPF:
            dv_confere = cpf_valido(normalizado)
        elif not ja_mascarado and categoria is Categoria.CNH:
            dv_confere = cnh_valida(normalizado)

        achado = Achado(
            caminho=caminho,
            categoria=categoria,
            metodo=Metodo.NOME_CAMPO,
            confianca=Confianca.ALTA,
            regra_id=regra.id,
            dv_confere=dv_confere,
            ja_mascarado=ja_mascarado,
            valor_mascarado=str(mascarado),
            _original=ValorProtegido(str(valor)),
        )
        if substituir is not None:
            novo = substituir(achado, valor)
            return (valor if novo is None else novo), [achado]
        return mascarado, [achado]

    @staticmethod
    def _classificar_coordenada(valor) -> tuple[Categoria, Regra]:
        """3 ou mais casas decimais é exata; até 2 é a aproximada do Contrato."""
        if parece_numero(valor) and numero_de_casas_decimais(valor) <= 2:
            return Categoria.LOCALIZACAO_APROXIMADA, REGRA_LOC_APROX_CAMPO
        return Categoria.LOCALIZACAO_EXATA, REGRA_LOC_EXATA_CAMPO

    # ------------------------------------------------------------------
    # Texto livre
    # ------------------------------------------------------------------

    def _varrer_texto(self, original: str, caminho: str, substituir=None) -> tuple[str, list[Achado]]:
        """Aplica ORDEM_TEXTO; quem casa primeiro fica com o trecho.

        Devolve o texto mascarado (normalizado em NFKC) e os achados em ordem
        de posição. Sem achados, devolve o texto original intacto.
        """
        texto = _nfkc(original)
        alvos = {Alvo.TEXTO: texto, Alvo.DOBRADO: dobrar(texto), Alvo.PROTEGIDO: proteger(texto)}
        dobrado = alvos[Alvo.DOBRADO]
        ocupados: list[tuple[int, int]] = []
        encontrados: list[tuple[int, int, Achado]] = []

        def registrar(regra, inicio_total, fim_total, inicio, fim, dv, confianca):
            trecho = texto[inicio:fim]
            ja_mascarado = esta_mascarado(regra.categoria, trecho)
            ocupados.append((inicio_total, fim_total))
            encontrados.append(
                (
                    inicio,
                    fim,
                    Achado(
                        caminho=caminho,
                        categoria=regra.categoria,
                        metodo=Metodo.VALOR,
                        confianca=confianca,
                        regra_id=regra.id,
                        dv_confere=dv,
                        ja_mascarado=ja_mascarado,
                        valor_mascarado=str(trecho if ja_mascarado else mascarar_valor(regra.categoria, trecho)),
                        _original=ValorProtegido(trecho),
                    ),
                )
            )

        for item in ORDEM_TEXTO:
            if item == ETAPA_DESEMPATE:
                for casamento in PADRAO_ONZE_DIGITOS.finditer(alvos[Alvo.PROTEGIDO]):
                    inicio, fim = casamento.span()
                    if _sobrepoe(inicio, fim, ocupados):
                        continue
                    decisao = desempatar_onze_digitos(dobrado, inicio, texto[inicio:fim])
                    if decisao:
                        registrar(decisao.regra, inicio, fim, inicio, fim, decisao.dv_confere, decisao.confianca)
                continue

            regra: Regra = item
            for casamento in regra.padrao.finditer(alvos[regra.alvo]):
                inicio_total, fim_total = casamento.span()
                if _sobrepoe(inicio_total, fim_total, ocupados):
                    continue
                inicio, fim = casamento.span("v") if "v" in regra.padrao.groupindex else (inicio_total, fim_total)
                decisao = self._avaliar(regra, texto[inicio:fim], dobrado, inicio_total)
                if decisao is not None:
                    dv, confianca = decisao
                    registrar(regra, inicio_total, fim_total, inicio, fim, dv, confianca)

        if not encontrados:
            return original, []
        encontrados.sort(key=lambda e: e[0])
        partes, cursor, mudou = [], 0, substituir is None
        for inicio, fim, achado in encontrados:
            partes.append(texto[cursor:inicio])
            if substituir is None:
                partes.append(achado.valor_mascarado)
            else:
                novo = substituir(achado, texto[inicio:fim])
                partes.append(texto[inicio:fim] if novo is None else str(novo))
                mudou = mudou or novo is not None
            cursor = fim
        partes.append(texto[cursor:])
        achados = [achado for _, _, achado in encontrados]
        # Nada substituído: devolve o texto original, sem a normalização NFKC.
        return ("".join(partes) if mudou else original), achados

    @staticmethod
    def _avaliar(regra: Regra, trecho: str, dobrado: str, inicio: int) -> tuple[bool | None, Confianca] | None:
        """Aplica a exigência da regra. Devolve (dv_confere, confiança) ou None se não marca."""
        valido = regra.validador(trecho) if regra.validador else None
        contexto = tem_contexto(dobrado, inicio, regra.contexto) if regra.contexto else False
        exige = regra.exige
        if exige is Exigencia.VALIDADOR and not valido:
            return None
        if exige is Exigencia.CONTEXTO and not contexto:
            return None
        if exige is Exigencia.VALIDADOR_E_CONTEXTO and not (valido and contexto):
            return None
        confianca = regra.confianca
        if exige is Exigencia.VALIDADOR_OU_CONTEXTO:
            if not valido and not contexto:
                return None
            if not valido:
                confianca = Confianca.MEDIA
        # dv_confere só faz sentido para documentos com dígito verificador.
        dv = bool(valido) if regra.categoria in (Categoria.CPF, Categoria.CNH) else None
        return dv, confianca
