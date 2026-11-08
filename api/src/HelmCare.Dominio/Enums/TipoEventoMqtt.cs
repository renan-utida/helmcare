using System.Text.Json.Serialization;

namespace HelmCare.Dominio.Enums;

/// <summary>
/// Tipo do evento que o capacete publica no tópico <c>helmcare-rdu/v1/{dispositivo}/evento</c> (seção 6.2 do contrato).
/// No JSON, no banco e no MQTT usa o nome do contrato (UPPER_SNAKE_CASE).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TipoEventoMqtt>))]
public enum TipoEventoMqtt
{
    /// <summary>
    /// O capacete detectou um impacto; a API grava o <c>EventoImpacto</c>.
    /// </summary>
    [JsonStringEnumMemberName("IMPACTO_DETECTADO")]
    ImpactoDetectado,

    /// <summary>
    /// A observação terminou sem falso positivo e começou a contagem de cancelamento.
    /// </summary>
    [JsonStringEnumMemberName("CONTAGEM_INICIADA")]
    ContagemIniciada,

    /// <summary>
    /// O entregador cancelou durante a contagem; o resultado vira <see cref="ResultadoImpacto.CanceladoPeloEntregador"/>.
    /// </summary>
    [JsonStringEnumMemberName("CANCELADO")]
    Cancelado,

    /// <summary>
    /// A velocidade de moto voltou durante a observação; o resultado vira <see cref="ResultadoImpacto.FalsoPositivo"/>.
    /// </summary>
    [JsonStringEnumMemberName("FALSO_POSITIVO")]
    FalsoPositivo,

    /// <summary>
    /// A contagem terminou sem cancelamento; a API abre a <c>Ocorrencia</c> em <see cref="EstadoOcorrencia.SosCamada1"/>.
    /// </summary>
    [JsonStringEnumMemberName("SOS")]
    Sos,
}
