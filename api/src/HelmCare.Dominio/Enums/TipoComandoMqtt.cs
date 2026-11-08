using System.Text.Json.Serialization;

namespace HelmCare.Dominio.Enums;

/// <summary>
/// Tipo do comando que a API publica no tópico <c>helmcare-rdu/v1/{dispositivo}/comando</c> (seção 6.3 do contrato).
/// No JSON, no banco e no MQTT usa o nome do contrato (UPPER_SNAKE_CASE).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TipoComandoMqtt>))]
public enum TipoComandoMqtt
{
    /// <summary>
    /// Leva o capacete de <c>DESARMADO</c> para <c>ARMADO</c>.
    /// </summary>
    [JsonStringEnumMemberName("ARMAR")]
    Armar,

    /// <summary>
    /// Leva o capacete para <c>DESARMADO</c>; pela v1.3, o capacete só aceita em <c>ARMADO</c>.
    /// </summary>
    [JsonStringEnumMemberName("DESARMAR")]
    Desarmar,

    /// <summary>
    /// Cancela o alerta; só vale em <c>CONTAGEM</c> e é ignorado fora dela.
    /// </summary>
    [JsonStringEnumMemberName("CANCELAR")]
    Cancelar,

    /// <summary>
    /// Em <c>SOS_ENVIADO</c>: mostra a mensagem de socorro a caminho no OLED e para o reenvio do SOS.
    /// </summary>
    [JsonStringEnumMemberName("SOCORRO_A_CAMINHO")]
    SocorroACaminho,

    /// <summary>
    /// Em <c>SOS_ENVIADO</c>: para o reenvio do SOS e o capacete volta para <c>ARMADO</c>, porque o turno continua.
    /// </summary>
    [JsonStringEnumMemberName("ENCERRAR")]
    Encerrar,
}
