using System;

namespace HelmCare.Api.Servicos.Risco;

/// <summary>
/// Uma hora cheia do turno na versão simulada, como a <c>HoraTurno</c> do Serviço de Risco
/// (<c>horas_turno.py</c>): o início em UTC, o dia e a hora locais de São Paulo, o peso na média
/// e o componente sinistro da tabela.
/// </summary>
/// <param name="InicioUtc">Início da hora cheia, em UTC.</param>
/// <param name="DiaLocal">Dia da semana local da hora.</param>
/// <param name="HoraLocal">Hora local, de 0 a 23.</param>
/// <param name="SegundosNoTurno">Quanto desta hora fica dentro do turno (3600 numa hora inteira).</param>
/// <param name="Sinistro">Componente sinistro da hora, já normalizado e sem arredondar.</param>
public sealed record HoraTurnoSimulada(
    DateTime InicioUtc,
    DayOfWeek DiaLocal,
    int HoraLocal,
    double SegundosNoTurno,
    double Sinistro);
