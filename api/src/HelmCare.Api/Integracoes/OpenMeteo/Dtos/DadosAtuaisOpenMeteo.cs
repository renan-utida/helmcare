using System.Text.Json.Serialization;

namespace HelmCare.Api.Integracoes.OpenMeteo.Dtos;

/// <summary>
/// Bloco <c>current</c> da Open-Meteo: as condições do horário da medição, em GMT.
/// </summary>
public sealed class DadosAtuaisOpenMeteo
{
    /// <summary>
    /// Horário da medição (<c>yyyy-MM-ddTHH:mm</c>, GMT).
    /// </summary>
    [JsonPropertyName("time")]
    public string? Time { get; init; }

    /// <summary>
    /// Temperatura a 2 m, em °C.
    /// </summary>
    [JsonPropertyName("temperature_2m")]
    public double? Temperature2m { get; init; }

    /// <summary>
    /// Umidade relativa a 2 m, em %.
    /// </summary>
    [JsonPropertyName("relative_humidity_2m")]
    public double? RelativeHumidity2m { get; init; }

    /// <summary>
    /// Precipitação, em mm.
    /// </summary>
    [JsonPropertyName("precipitation")]
    public double? Precipitation { get; init; }
}
