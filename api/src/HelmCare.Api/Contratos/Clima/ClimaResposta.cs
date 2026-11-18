using System;

namespace HelmCare.Api.Contratos.Clima;

/// <summary>
/// Corpo do <c>200</c> do <c>GET /api/v1/clima</c> (seção 7 do contrato, 1.17): as condições atuais da
/// cidade pela Open-Meteo, para o cartão "Clima" do app.
/// </summary>
public sealed class ClimaResposta
{
    /// <summary>
    /// Crédito exigido pela licença CC BY 4.0 dos dados.
    /// </summary>
    public const string FonteOpenMeteo = "Open-Meteo";

    /// <summary>
    /// Temperatura, em °C, com 1 casa.
    /// </summary>
    public required double TemperaturaC { get; init; }

    /// <summary>
    /// Umidade relativa, em %, inteira.
    /// </summary>
    public required int UmidadePct { get; init; }

    /// <summary>
    /// Precipitação, em mm/h, com 1 casa.
    /// </summary>
    public required double ChuvaMmHora { get; init; }

    /// <summary>
    /// Índice de calor (fórmula da seção 6.1), em °C, com 1 casa.
    /// </summary>
    public required double IndiceCalorC { get; init; }

    /// <summary>
    /// Verdadeiro quando o <see cref="IndiceCalorC"/> já arredondado é maior ou igual a <c>ALERTA_CALOR_C</c>.
    /// </summary>
    public required bool AlertaCalor { get; init; }

    /// <summary>
    /// Horário da medição da Open-Meteo, em UTC.
    /// </summary>
    public required DateTime AtualizadoEm { get; init; }

    /// <summary>
    /// Crédito da fonte, sempre <c>Open-Meteo</c>.
    /// </summary>
    public string Fonte { get; init; } = FonteOpenMeteo;
}
