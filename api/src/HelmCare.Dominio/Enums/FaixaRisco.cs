using System.Text.Json.Serialization;

namespace HelmCare.Dominio.Enums;

/// <summary>
/// Faixa do índice de risco (0 a 100) de um turno, calculado pelo Serviço de Risco e guardado em <c>Turno.FaixaRisco</c>.
/// No JSON, no banco e no MQTT usa o nome do contrato (UPPER_SNAKE_CASE).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<FaixaRisco>))]
public enum FaixaRisco
{
    /// <summary>
    /// Índice de risco de 0 a 25.
    /// </summary>
    [JsonStringEnumMemberName("BAIXO")]
    Baixo,

    /// <summary>
    /// Índice de risco de 26 a 50.
    /// </summary>
    [JsonStringEnumMemberName("MODERADO")]
    Moderado,

    /// <summary>
    /// Índice de risco de 51 a 75.
    /// </summary>
    [JsonStringEnumMemberName("ALTO")]
    Alto,

    /// <summary>
    /// Índice de risco de 76 a 100.
    /// </summary>
    [JsonStringEnumMemberName("MUITO_ALTO")]
    MuitoAlto,
}
