using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HelmCare.Api.Integracoes.OpenMeteo.Dtos;

/// <summary>
/// Bloco <c>hourly</c> da Open-Meteo: listas paralelas, uma posição por hora, com o horário em GMT
/// (<c>yyyy-MM-ddTHH:mm</c>).
/// </summary>
public sealed class DadosHorariosOpenMeteo
{
    /// <summary>
    /// Horário de cada hora.
    /// </summary>
    [JsonPropertyName("time")]
    public IReadOnlyList<string?>? Time { get; init; }

    /// <summary>
    /// Temperatura a 2 m, em °C.
    /// </summary>
    [JsonPropertyName("temperature_2m")]
    public IReadOnlyList<double?>? Temperature2m { get; init; }

    /// <summary>
    /// Umidade relativa a 2 m, em %.
    /// </summary>
    [JsonPropertyName("relative_humidity_2m")]
    public IReadOnlyList<double?>? RelativeHumidity2m { get; init; }

    /// <summary>
    /// Precipitação da hora, em mm.
    /// </summary>
    [JsonPropertyName("precipitation")]
    public IReadOnlyList<double?>? Precipitation { get; init; }
}
