"""Cenários C1 a C11: cada um reproduz um erro de código num caminho de saída.

Todos usam as funções reais do guarda (filtro de log, saída protegida,
verificar_saida, sanitizadores) com os dados simulados. O resultado esperado
de cada cenário depende do modo: em PROTECAO o vazamento é neutralizado; em
AUDITORIA ele passa e fica marcado como EXPOSTO. C7 é o contraexemplo: o canal
é autorizado e o dado passa nos dois modos.

saida_emitida é o que realmente saiu (a linha de log, o corpo da resposta).
Ela existe só para os testes e para o script de demonstração: não entra em
evento nem em nenhum JSON de resposta, e fica fora do repr.
"""

import io
import logging
from collections.abc import Callable
from dataclasses import dataclass, field

from helmcare_cyber.simulacao import dados_simulados as dados
from helmcare_cyber.taint.filtro_log import instalar_filtro, proteger_print
from helmcare_cyber.taint.guarda import GuardaTaint, ResultadoGuarda
from helmcare_cyber.taint.modelo import ContextoSaida, Destinatario, Destino, Estado, Fonte, Modo, pior_estado
from helmcare_cyber.taint.rastro import Rastro
from helmcare_cyber.taint.sanitizadores import hash_senha

ROTULO_SIMULADO = "Simulado"
ROTULO_REAL_SIMULADO = "Real (reproduzido com token simulado)"
FORMATO_LOG = "%(levelname)s %(name)s: %(message)s"


@dataclass
class Execucao:
    """O que a função de um cenário devolve: os resultados do guarda e o que saiu."""

    resultados: list[ResultadoGuarda]
    saida_emitida: object


@dataclass(frozen=True)
class Cenario:
    id: str
    titulo: str
    rotulo: str
    fonte: Fonte
    destino: Destino
    destinatario: Destinatario
    descricao: str
    executar: Callable[[GuardaTaint], Execucao] = field(repr=False)
    esperado_protecao: Estado = Estado.NEUTRALIZADO
    esperado_auditoria: Estado = Estado.EXPOSTO

    def esperado(self, modo: Modo) -> Estado:
        return self.esperado_protecao if modo is Modo.PROTECAO else self.esperado_auditoria


@dataclass
class ResultadoCenario:
    cenario: Cenario
    modo: Modo
    resultados: tuple[ResultadoGuarda, ...]
    saida_emitida: object = field(repr=False)
    passou: bool = False

    @property
    def resultado(self) -> ResultadoGuarda:
        """O resultado principal (o primeiro; C7 e C10 têm duas saídas)."""
        return self.resultados[0]

    @property
    def decisao_geral(self) -> Estado:
        return pior_estado(r.decisao_geral for r in self.resultados)

    @property
    def eventos(self) -> list:
        return [evento for r in self.resultados for evento in r.eventos]

    def regras_aplicadas(self) -> list[str]:
        return list(dict.fromkeys(regra for r in self.resultados for regra in r.regras_aplicadas()))


def _rastro_com_fonte(guarda: GuardaTaint, fonte: Fonte, payload, descricao: str) -> Rastro:
    rastro = Rastro()
    guarda.registrar_fonte(fonte, payload, rastro, descricao)
    return rastro


def _logger_isolado(nome: str) -> tuple[logging.Logger, io.StringIO, logging.Handler]:
    """Logger próprio do cenário, que escreve numa memória e não propaga para o root.

    O cenário remove o handler e o filtro no fim, então o mesmo nome pode ser
    reutilizado sem acumular handlers.
    """
    logger = logging.getLogger(nome)
    logger.setLevel(logging.INFO)
    logger.propagate = False
    fluxo = io.StringIO()
    handler = logging.StreamHandler(fluxo)
    handler.setFormatter(logging.Formatter(FORMATO_LOG))
    logger.addHandler(handler)
    return logger, fluxo, handler


# ---------------------------------------------------------------------------
# C1 a C11
# ---------------------------------------------------------------------------


def _c1(guarda: GuardaTaint) -> Execucao:
    cadastro = dados.cadastro_entregador()
    rastro = _rastro_com_fonte(guarda, Fonte.CADASTRO_ENTREGADOR, cadastro, "POST /entregadores")
    rastro.adicionar_propagacao('f-string em mensagem de log: logger.info(f"cadastro {dados}")')
    logger, fluxo, handler = _logger_isolado("helmcare.api.cadastro")
    filtro = instalar_filtro(logger, guarda, fonte=Fonte.CADASTRO_ENTREGADOR, cenario="C1", rastro=rastro)
    try:
        logger.info(f"cadastro {cadastro}")
    finally:
        logger.removeFilter(filtro)
        logger.removeHandler(handler)
    return Execucao([filtro.ultimo_resultado], fluxo.getvalue().rstrip("\n"))


def _c2(guarda: GuardaTaint) -> Execucao:
    login = dados.login()
    rastro = _rastro_com_fonte(guarda, Fonte.LOGIN, login, "POST /entregadores/login")
    rastro.adicionar_propagacao('print de depuração: print(f"login recebido: email={email} senha={senha}")')
    fluxo = io.StringIO()
    with proteger_print(guarda, fluxo, fonte=Fonte.LOGIN, cenario="C2", rastro=rastro) as saida:
        print(f"login recebido: email={login['email']} senha={login['senha']}")
    return Execucao([saida.ultimo_resultado], fluxo.getvalue().rstrip("\n"))


def _c3(guarda: GuardaTaint) -> Execucao:
    cadastro = dados.cadastro_entregador()
    rastro = _rastro_com_fonte(guarda, Fonte.CADASTRO_ENTREGADOR, cadastro, "POST /entregadores")
    rastro.adicionar_propagacao("valores recebidos repetidos no detail e no errors do ProblemDetails")
    erro = {
        "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        "title": "Dados inválidos",
        "status": 400,
        "detail": f"O CPF {cadastro['cpf']} não confere com o telefone {cadastro['telefone']}.",
        "errors": {"cpf": [f"CPF {cadastro['cpf']} já cadastrado"]},
    }
    contexto = ContextoSaida(Destino.MENSAGEM_ERRO, Destinatario.PROPRIO_ENTREGADOR)
    resultado = guarda.verificar_saida(erro, contexto, Fonte.CADASTRO_ENTREGADOR, "C3", rastro)
    return Execucao([resultado], resultado.payload_seguro)


def _c4(guarda: GuardaTaint) -> Execucao:
    ficha = dados.ficha_medica()
    rastro = _rastro_com_fonte(guarda, Fonte.FICHA_MEDICA, ficha, "PUT /entregadores/{id}/ficha-medica")
    rastro.adicionar_propagacao("objeto da ficha médica anexado ao item do chamado próximo")
    chamado = dados.item_chamado_proximo() | {"fichaMedica": ficha}
    contexto = ContextoSaida(Destino.CHAMADO_VOLUNTARIO, Destinatario.VOLUNTARIO_ANTES_ACEITE)
    resultado = guarda.verificar_saida(chamado, contexto, Fonte.FICHA_MEDICA, "C4", rastro)
    return Execucao([resultado], resultado.payload_seguro)


def _c5(guarda: GuardaTaint) -> Execucao:
    cadastro = dados.cadastro_entregador()
    rastro = _rastro_com_fonte(guarda, Fonte.CADASTRO_ENTREGADOR, cadastro, "POST /entregadores")
    guarda.registrar_fonte(Fonte.OCORRENCIA_SOS, dados.evento_mqtt_sos(), rastro, "evento SOS do capacete (MQTT)")
    rastro.adicionar_propagacao("nome, telefone e CPF do cadastro copiados para o payload do evento MQTT")
    evento = dados.evento_mqtt_sos() | {
        "nome": cadastro["nome"],
        "telefone": cadastro["telefone"],
        "cpf": cadastro["cpf"],
    }
    contexto = ContextoSaida(Destino.MQTT_PAYLOAD, Destinatario.SISTEMA)
    resultado = guarda.verificar_saida(evento, contexto, Fonte.CADASTRO_ENTREGADOR, "C5", rastro)
    return Execucao([resultado], resultado.payload_seguro)


def _c6(guarda: GuardaTaint) -> Execucao:
    ocorrencia = dados.ocorrencia_sos()
    rastro = _rastro_com_fonte(guarda, Fonte.OCORRENCIA_SOS, ocorrencia, "GET /ocorrencias/{id}")
    rastro.adicionar_propagacao("latitude e longitude da ocorrência copiadas para o push sem arredondar")
    push = {
        "titulo": "SOS perto de você",
        "ocorrenciaId": ocorrencia["id"],
        "latitude": ocorrencia["latitude"],
        "longitude": ocorrencia["longitude"],
    }
    contexto = ContextoSaida(Destino.NOTIFICACAO_PUSH, Destinatario.VOLUNTARIO_ANTES_ACEITE)
    resultado = guarda.verificar_saida(push, contexto, Fonte.OCORRENCIA_SOS, "C6", rastro)
    return Execucao([resultado], resultado.payload_seguro)


def _c7(guarda: GuardaTaint) -> Execucao:
    ocorrencia = dados.ocorrencia_sos()
    achados = guarda.registrar_fonte(Fonte.OCORRENCIA_SOS, ocorrencia)

    rastro_voluntario = Rastro().adicionar_fontes(Fonte.OCORRENCIA_SOS.value, achados, "GET /ocorrencias/{id}")
    rastro_voluntario.adicionar_propagacao("corpo do GET /ocorrencias/{id} para o voluntário que aceitou")
    voluntario = guarda.verificar_saida(
        ocorrencia,
        ContextoSaida(Destino.CHAMADO_VOLUNTARIO, Destinatario.VOLUNTARIO_QUE_ACEITOU),
        Fonte.OCORRENCIA_SOS,
        "C7",
        rastro_voluntario,
    )

    rastro_contato = Rastro().adicionar_fontes(Fonte.OCORRENCIA_SOS.value, achados, "GET /ocorrencias/{id}")
    rastro_contato.adicionar_propagacao("aviso ao contato de emergência com a localização da queda")
    aviso = {
        "ocorrenciaId": ocorrencia["id"],
        "mensagem": "Alerta de queda: o entregador acionou o SOS.",
        "latitude": ocorrencia["latitude"],
        "longitude": ocorrencia["longitude"],
    }
    contato = guarda.verificar_saida(
        aviso,
        ContextoSaida(Destino.CONTATO_EMERGENCIA, Destinatario.CONTATO_EMERGENCIA),
        Fonte.OCORRENCIA_SOS,
        "C7",
        rastro_contato,
    )
    return Execucao([voluntario, contato], [voluntario.payload_seguro, contato.payload_seguro])


def _c8(guarda: GuardaTaint) -> Execucao:
    rastro = _rastro_com_fonte(guarda, Fonte.FICHA_MEDICA, dados.ficha_medica(), "PUT /entregadores/{id}/ficha-medica")
    guarda.registrar_fonte(Fonte.CONTATO_EMERGENCIA, dados.contato_emergencia(), rastro, "POST /entregadores/{id}/contatos")
    rastro.adicionar_propagacao("medicamentos incluído por engano na resposta do Cofre")
    resposta = dados.resposta_cofre(incluir_medicamentos=True)
    contexto = ContextoSaida(Destino.COFRE_SOCORRISTA, Destinatario.SOCORRISTA_VERIFICADO_NA_JANELA)
    resultado = guarda.verificar_saida(resposta, contexto, Fonte.FICHA_MEDICA, "C8", rastro)
    return Execucao([resultado], resultado.payload_seguro)


def _c9(guarda: GuardaTaint) -> Execucao:
    rastro = _rastro_com_fonte(guarda, Fonte.FICHA_MEDICA, dados.ficha_medica(), "PUT /entregadores/{id}/ficha-medica")
    guarda.registrar_fonte(Fonte.CONTATO_EMERGENCIA, dados.contato_emergencia(), rastro, "POST /entregadores/{id}/contatos")
    rastro.adicionar_propagacao("resposta do Cofre montada sem conferir verificação e janela do socorrista")
    contexto = ContextoSaida(Destino.COFRE_SOCORRISTA, Destinatario.SOCORRISTA_NAO_AUTORIZADO)
    resultado = guarda.verificar_saida(dados.resposta_cofre(), contexto, Fonte.FICHA_MEDICA, "C9", rastro)
    return Execucao([resultado], resultado.payload_seguro)


def _c10(guarda: GuardaTaint) -> Execucao:
    login = dados.login()
    achados = guarda.registrar_fonte(Fonte.CADASTRO_ENTREGADOR, login)
    contexto = ContextoSaida(Destino.PERSISTENCIA, Destinatario.SISTEMA)

    rastro_pura = Rastro().adicionar_fontes(Fonte.CADASTRO_ENTREGADOR.value, achados, "POST /entregadores")
    rastro_pura.adicionar_propagacao("senha recebida gravada como veio, sem hash")
    pura = guarda.verificar_saida(
        {"email": login["email"], "senha": login["senha"]}, contexto, Fonte.CADASTRO_ENTREGADOR, "C10", rastro_pura
    )

    rastro_hash = Rastro().adicionar_fontes(Fonte.CADASTRO_ENTREGADOR.value, achados, "POST /entregadores")
    senha_hash = hash_senha(login["senha"], rastro_hash)
    rastro_hash.adicionar_propagacao("só o hash da senha vai para a gravação")
    com_hash = guarda.verificar_saida(
        {"email": login["email"], "senha": senha_hash}, contexto, Fonte.CADASTRO_ENTREGADOR, "C10", rastro_hash
    )
    return Execucao([pura, com_hash], [pura.payload_seguro, com_hash.payload_seguro])


def _c11(guarda: GuardaTaint) -> Execucao:
    rastro = _rastro_com_fonte(guarda, Fonte.DISPOSITIVO, dados.resposta_dispositivo(), "POST /dispositivos")
    rastro.adicionar_propagacao("endereço do Cofre copiado para a legenda de um print na documentação")
    legenda = (
        "Figura 4: o QR do capacete abre "
        f"http://192.168.0.10:5000/cofre/{dados.QR_TOKEN} no celular do socorrista."
    )
    contexto = ContextoSaida(Destino.DOCUMENTACAO_PUBLICA, Destinatario.ANONIMO)
    resultado = guarda.verificar_saida(legenda, contexto, Fonte.DISPOSITIVO, "C11", rastro)
    return Execucao([resultado], resultado.payload_seguro)


CENARIOS: tuple[Cenario, ...] = (
    Cenario(
        "C1",
        "Cadastro em log por f-string",
        ROTULO_SIMULADO,
        Fonte.CADASTRO_ENTREGADOR,
        Destino.LOG,
        Destinatario.SISTEMA,
        'logger.info(f"cadastro {dados}") com o dict inteiro do cadastro, filtro de log instalado.',
        _c1,
    ),
    Cenario(
        "C2",
        "Senha impressa no log de login",
        ROTULO_SIMULADO,
        Fonte.LOGIN,
        Destino.LOG,
        Destinatario.SISTEMA,
        "print de depuração com e-mail e senha, com a saída padrão protegida.",
        _c2,
    ),
    Cenario(
        "C3",
        "Erro de validação repete CPF e telefone",
        ROTULO_SIMULADO,
        Fonte.CADASTRO_ENTREGADOR,
        Destino.MENSAGEM_ERRO,
        Destinatario.PROPRIO_ENTREGADOR,
        "O ProblemDetails do 400 repete os valores recebidos no detail e no errors.",
        _c3,
    ),
    Cenario(
        "C4",
        "Ficha médica no chamado ao voluntário",
        ROTULO_SIMULADO,
        Fonte.FICHA_MEDICA,
        Destino.CHAMADO_VOLUNTARIO,
        Destinatario.VOLUNTARIO_ANTES_ACEITE,
        "A ficha médica vai junto com o item do chamado para um voluntário que ainda não aceitou.",
        _c4,
    ),
    Cenario(
        "C5",
        "Identificação pessoal no payload MQTT",
        ROTULO_SIMULADO,
        Fonte.CADASTRO_ENTREGADOR,
        Destino.MQTT_PAYLOAD,
        Destinatario.SISTEMA,
        "Nome, telefone e CPF entram no evento SOS, junto com a coordenada.",
        _c5,
    ),
    Cenario(
        "C6",
        "Push com localização exata antes do aceite",
        ROTULO_SIMULADO,
        Fonte.OCORRENCIA_SOS,
        Destino.NOTIFICACAO_PUSH,
        Destinatario.VOLUNTARIO_ANTES_ACEITE,
        "A notificação ao voluntário leva a coordenada com 4 casas em vez de 2.",
        _c6,
    ),
    Cenario(
        "C7",
        "Localização exata nos canais autorizados",
        ROTULO_SIMULADO,
        Fonte.OCORRENCIA_SOS,
        Destino.CHAMADO_VOLUNTARIO,
        Destinatario.VOLUNTARIO_QUE_ACEITOU,
        "Coordenada exata para o voluntário que aceitou e para o contato de emergência: uso legítimo.",
        _c7,
        esperado_protecao=Estado.PERMITIDO,
        esperado_auditoria=Estado.PERMITIDO,
    ),
    Cenario(
        "C8",
        "Cofre com medicamentos por engano",
        ROTULO_SIMULADO,
        Fonte.FICHA_MEDICA,
        Destino.COFRE_SOCORRISTA,
        Destinatario.SOCORRISTA_VERIFICADO_NA_JANELA,
        "Socorrista verificado na janela recebe a ficha mínima e os contatos, mais medicamentos por engano.",
        _c8,
    ),
    Cenario(
        "C9",
        "Cofre para socorrista não autorizado",
        ROTULO_SIMULADO,
        Fonte.FICHA_MEDICA,
        Destino.COFRE_SOCORRISTA,
        Destinatario.SOCORRISTA_NAO_AUTORIZADO,
        "A resposta do Cofre sai para um socorrista não verificado ou fora da janela.",
        _c9,
    ),
    Cenario(
        "C10",
        "Senha em texto puro na persistência",
        ROTULO_SIMULADO,
        Fonte.CADASTRO_ENTREGADOR,
        Destino.PERSISTENCIA,
        Destinatario.SISTEMA,
        "A senha vai para a gravação como veio; a mesma gravação com hash_senha é permitida.",
        _c10,
    ),
    Cenario(
        "C11",
        "qrToken em documentação pública",
        ROTULO_REAL_SIMULADO,
        Fonte.DISPOSITIVO,
        Destino.DOCUMENTACAO_PUBLICA,
        Destinatario.ANONIMO,
        "A legenda de um print mostra o endereço /cofre/{qrToken}. O token aqui é inventado.",
        _c11,
    ),
)

CENARIOS_POR_ID: dict[str, Cenario] = {cenario.id: cenario for cenario in CENARIOS}


def executar_cenario(cenario: Cenario | str, guarda: GuardaTaint) -> ResultadoCenario:
    """Roda um cenário com o registro de taint limpo, para um não contaminar o outro."""
    cenario = CENARIOS_POR_ID[cenario.upper()] if isinstance(cenario, str) else cenario
    guarda.registro.limpar()
    execucao = cenario.executar(guarda)
    resultado = ResultadoCenario(cenario, guarda.modo, tuple(execucao.resultados), execucao.saida_emitida)
    resultado.passou = resultado.decisao_geral is cenario.esperado(guarda.modo)
    return resultado


def executar_todos(guarda: GuardaTaint, ids: list[str] | None = None) -> list[ResultadoCenario]:
    escolhidos = [CENARIOS_POR_ID[i.upper()] for i in ids] if ids else list(CENARIOS)
    return [executar_cenario(cenario, guarda) for cenario in escolhidos]
