"""Testes do calendário de feriados (lista de metadados_modelo.json e regra fora dela)."""

from datetime import date

import pytest

from app.dominio.calendario import CalendarioFeriados, feriados_pela_regra
from app.dominio.excecoes import ValidacaoDominioError


def test_eh_feriado_20_11_2026_verdadeiro(calendario: CalendarioFeriados) -> None:
    # Act e Assert
    assert calendario.eh_feriado(date(2026, 11, 20)) is True


def test_eh_feriado_12_11_2026_falso(calendario: CalendarioFeriados) -> None:
    # Act e Assert
    assert calendario.eh_feriado(date(2026, 11, 12)) is False


def test_calendario_periodo_da_lista_2019_a_2030(calendario: CalendarioFeriados) -> None:
    # Assert
    assert (calendario.ano_inicial, calendario.ano_final) == (2019, 2030)


def test_feriados_pela_regra_2019_a_2030_igual_a_lista(datas_feriados_lista: set[date]) -> None:
    # Arrange e Act
    pela_regra: set[date] = set()
    for ano in range(2019, 2031):
        pela_regra |= feriados_pela_regra(ano)

    # Assert: a regra reproduz a lista inteira, sem sobrar nem faltar datas.
    assert pela_regra == datas_feriados_lista


@pytest.mark.parametrize(
    ("data", "esperado"),
    [
        (date(2031, 11, 20), True),  # Consciência Negra
        (date(2031, 1, 25), True),  # aniversário de São Paulo
        (date(2031, 2, 24), True),  # Carnaval (segunda): Páscoa de 2031 é 13/04
        (date(2031, 2, 25), True),  # Carnaval (terça)
        (date(2031, 6, 12), True),  # Corpus Christi
        (date(2031, 7, 9), True),  # Revolução Constitucionalista (estadual de SP)
        (date(2031, 11, 12), False),
        (date(2018, 11, 20), True),  # antes do período da lista também vale a regra
    ],
)
def test_eh_feriado_fora_do_periodo_da_lista_aplica_regra(
    calendario: CalendarioFeriados, data: date, esperado: bool
) -> None:
    # Act e Assert
    assert calendario.eh_feriado(data) is esperado


def test_calendario_lista_vazia_lanca_validacao() -> None:
    # Act e Assert
    with pytest.raises(ValidacaoDominioError):
        CalendarioFeriados([])
