"""Horas cheias do turno e as variáveis de calendário de cada uma (seção 8.1 do contrato).

O modelo prevê sinistros por hora local (0 a 23). Por isso o turno, que chega em
UTC, é dividido nas horas cheias UTC que se sobrepõem a ele, e cada hora é convertida
para America/Sao_Paulo antes de montar hora, dia_semana, mes e feriado.
"""

from dataclasses import dataclass
from datetime import UTC, datetime, timedelta
from zoneinfo import ZoneInfo

from app.dominio.calendario import CalendarioFeriados
from app.dominio.excecoes import ValidacaoDominioError

FUSO_LOCAL = ZoneInfo("America/Sao_Paulo")
DURACAO_MAXIMA_TURNO = timedelta(hours=24)
UMA_HORA = timedelta(hours=1)

# Ordem das variáveis de entrada do modelo, igual a ordemVariaveis de metadados_modelo.json.
ORDEM_VARIAVEIS: tuple[str, ...] = ("hora", "dia_semana", "mes", "feriado")


@dataclass(frozen=True)
class HoraTurno:
    """Uma hora cheia do turno com as variáveis do modelo e o seu peso na média.

    segundos_no_turno é quanto desta hora fica dentro do turno: 3600 numa hora
    inteira e menos que isso na primeira ou na última hora de um turno quebrado.
    """

    inicio_utc: datetime
    inicio_local: datetime
    hora: int
    dia_semana: int
    mes: int
    feriado: int
    segundos_no_turno: float

    def variaveis_modelo(self) -> tuple[int, int, int, int]:
        """Variáveis de entrada do modelo, na ordem de ORDEM_VARIAVEIS."""
        return (self.hora, self.dia_semana, self.mes, self.feriado)


def exigir_fuso(instante: datetime, nome: str) -> datetime:
    """Confere que o instante tem fuso e o devolve convertido para UTC.

    Data sem fuso é recusada, porque não dá para saber a que hora local ela se refere.
    """
    if instante.tzinfo is None or instante.utcoffset() is None:
        raise ValidacaoDominioError(
            f"O campo {nome} precisa de fuso horário (por exemplo, Z ou -03:00)."
        )
    return instante.astimezone(UTC)


def validar_turno(inicio: datetime, fim: datetime) -> tuple[datetime, datetime]:
    """Valida o turno e devolve o início e o fim em UTC.

    Regras do contrato: os dois com fuso, fim depois do início e no máximo 24 h de
    turno (exatamente 24 h passa).
    """
    inicio_utc = exigir_fuso(inicio, "inicio")
    fim_utc = exigir_fuso(fim, "fim")
    if fim_utc <= inicio_utc:
        raise ValidacaoDominioError("O fim do turno deve ser depois do início.")
    if fim_utc - inicio_utc > DURACAO_MAXIMA_TURNO:
        raise ValidacaoDominioError("O turno pode ter no máximo 24 horas.")
    return inicio_utc, fim_utc


def gerar_horas_turno(
    inicio: datetime, fim: datetime, calendario: CalendarioFeriados
) -> tuple[HoraTurno, ...]:
    """Divide o turno nas horas cheias UTC que se sobrepõem a ele.

    Parte do início arredondado para baixo na hora cheia e avança de hora em hora
    enquanto a hora começar antes do fim. Assim, um fim exatamente na hora cheia não
    inclui a hora seguinte, um turno de 24 h alinhado dá 24 horas e um desalinhado dá 25.
    """
    inicio_utc, fim_utc = validar_turno(inicio, fim)

    horas: list[HoraTurno] = []
    hora_utc = inicio_utc.replace(minute=0, second=0, microsecond=0)
    while hora_utc < fim_utc:
        # Parte da hora que fica dentro do turno.
        sobreposicao = min(fim_utc, hora_utc + UMA_HORA) - max(inicio_utc, hora_utc)
        local = hora_utc.astimezone(FUSO_LOCAL)
        horas.append(
            HoraTurno(
                inicio_utc=hora_utc,
                inicio_local=local,
                hora=local.hour,
                dia_semana=local.weekday(),
                mes=local.month,
                feriado=1 if calendario.eh_feriado(local.date()) else 0,
                segundos_no_turno=sobreposicao.total_seconds(),
            )
        )
        hora_utc += UMA_HORA
    return tuple(horas)
