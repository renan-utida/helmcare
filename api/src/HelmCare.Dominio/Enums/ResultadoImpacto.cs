using System.Text.Json.Serialization;

namespace HelmCare.Dominio.Enums;

/// <summary>
/// Desfecho de um <c>EventoImpacto</c> registrado pela API a partir dos eventos MQTT. Fica vazio enquanto o impacto está em observação ou contagem.
/// No JSON, no banco e no MQTT usa o nome do contrato (UPPER_SNAKE_CASE).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ResultadoImpacto>))]
public enum ResultadoImpacto
{
    /// <summary>
    /// Impacto descartado na hora: o entregador não estava pilotando nem presente (capacete solto).
    /// </summary>
    [JsonStringEnumMemberName("IGNORADO")]
    Ignorado,

    /// <summary>
    /// A velocidade de moto voltou durante a observação: não houve queda.
    /// </summary>
    [JsonStringEnumMemberName("FALSO_POSITIVO")]
    FalsoPositivo,

    /// <summary>
    /// O entregador cancelou o alerta durante a contagem.
    /// </summary>
    [JsonStringEnumMemberName("CANCELADO_PELO_ENTREGADOR")]
    CanceladoPeloEntregador,

    /// <summary>
    /// A contagem terminou sem cancelamento e o SOS gerou uma <c>Ocorrencia</c>.
    /// </summary>
    [JsonStringEnumMemberName("SOS_ENVIADO")]
    SosEnviado,
}
