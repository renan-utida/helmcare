"""Calendário de feriados usado na variável "feriado" do modelo (seção 8.1 do contrato).

Dentro do período da lista de metadados_modelo.json (2019 a 2030), vale a lista.
Fora dele, vale a mesma regra que gerou a lista no notebook de ML:
- nacionais e estaduais de SP pela biblioteca holidays (Brazil, subdiv="SP");
- municipais da capital: 25/01 (aniversário da cidade), Corpus Christi (Páscoa + 60
  dias) e 20/11 (Consciência Negra);
- Carnaval: segunda = Páscoa menos 48 dias, terça = Páscoa menos 47 dias.
"""

from collections.abc import Iterable
from datetime import date, timedelta

import holidays
from dateutil.easter import easter

from app.dominio.excecoes import ValidacaoDominioError

# Deslocamentos a partir do domingo de Páscoa.
DIAS_CORPUS_CHRISTI = 60
DIAS_SEGUNDA_CARNAVAL = -48
DIAS_TERCA_CARNAVAL = -47


def feriados_pela_regra(ano: int) -> set[date]:
    """Calcula os feriados de um ano pela regra do notebook de ML."""
    datas = set(holidays.Brazil(subdiv="SP", years=ano).keys())
    pascoa = easter(ano)
    datas.add(date(ano, 1, 25))
    datas.add(pascoa + timedelta(days=DIAS_CORPUS_CHRISTI))
    datas.add(date(ano, 11, 20))
    datas.add(pascoa + timedelta(days=DIAS_SEGUNDA_CARNAVAL))
    datas.add(pascoa + timedelta(days=DIAS_TERCA_CARNAVAL))
    return datas


class CalendarioFeriados:
    """Responde se uma data local é feriado.

    Recebe a lista de datas de metadados_modelo.json (quem lê o arquivo é a
    infraestrutura). O período coberto pela lista é deduzido do menor e do maior ano
    presentes nela; para anos fora dele, aplica feriados_pela_regra.
    """

    def __init__(self, datas_lista: Iterable[date]) -> None:
        """Guarda a lista de feriados e o período de anos que ela cobre."""
        self._datas = frozenset(datas_lista)
        if not self._datas:
            raise ValidacaoDominioError("A lista de feriados não pode ser vazia.")
        self.ano_inicial = min(d.year for d in self._datas)
        self.ano_final = max(d.year for d in self._datas)
        # Cache por ano para não recalcular a regra a cada hora do turno.
        self._cache_regra: dict[int, set[date]] = {}

    def eh_feriado(self, data: date) -> bool:
        """True se a data (local, de America/Sao_Paulo) é feriado."""
        if self.ano_inicial <= data.year <= self.ano_final:
            return data in self._datas
        if data.year not in self._cache_regra:
            self._cache_regra[data.year] = feriados_pela_regra(data.year)
        return data in self._cache_regra[data.year]
