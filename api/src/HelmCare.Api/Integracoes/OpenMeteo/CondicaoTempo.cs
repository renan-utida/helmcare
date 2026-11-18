using System;

namespace HelmCare.Api.Integracoes.OpenMeteo;

/// <summary>
/// Condição do tempo num instante, já lida e conferida da Open-Meteo (uma hora da previsão horária ou
/// as condições atuais). Os valores ainda não foram arredondados nem limitados às faixas.
/// </summary>
/// <param name="InstanteUtc">Início da hora (horária) ou horário da medição (atual), em UTC.</param>
/// <param name="TemperaturaC">Temperatura, em °C.</param>
/// <param name="UmidadePct">Umidade relativa, em %.</param>
/// <param name="ChuvaMmHora">Precipitação, em mm (na hora).</param>
public sealed record CondicaoTempo(DateTime InstanteUtc, double TemperaturaC, double UmidadePct, double ChuvaMmHora);
