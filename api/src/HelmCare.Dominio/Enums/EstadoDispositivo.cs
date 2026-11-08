using System.Text.Json.Serialization;

namespace HelmCare.Dominio.Enums;

/// <summary>
/// Estado da máquina de estados do capacete (ESP32), recebido pela API nos eventos MQTT e guardado em <c>Dispositivo.EstadoAtual</c>.
/// No JSON, no banco e no MQTT usa o nome do contrato (UPPER_SNAKE_CASE).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EstadoDispositivo>))]
public enum EstadoDispositivo
{
    /// <summary>
    /// Fora de turno: o capacete não detecta impactos.
    /// </summary>
    [JsonStringEnumMemberName("DESARMADO")]
    Desarmado,

    /// <summary>
    /// Turno em andamento: o capacete monitora a aceleração e detecta impactos.
    /// </summary>
    [JsonStringEnumMemberName("ARMADO")]
    Armado,

    /// <summary>
    /// A aceleração passou de <c>LIMIAR_IMPACTO_G</c>; o capacete avalia se o entregador estava pilotando ou presente.
    /// </summary>
    [JsonStringEnumMemberName("IMPACTO_DETECTADO")]
    ImpactoDetectado,

    /// <summary>
    /// Janela de observação de 10 s após o impacto: se a velocidade de moto voltar, vira falso positivo.
    /// </summary>
    [JsonStringEnumMemberName("OBSERVACAO")]
    Observacao,

    /// <summary>
    /// Contagem de 15 s para o entregador cancelar o alerta antes do SOS.
    /// </summary>
    [JsonStringEnumMemberName("CONTAGEM")]
    Contagem,

    /// <summary>
    /// A contagem terminou sem cancelamento: o capacete enviou o SOS e o reenvia até a API responder.
    /// </summary>
    [JsonStringEnumMemberName("SOS_ENVIADO")]
    SosEnviado,

    /// <summary>
    /// O entregador cancelou o alerta durante a contagem (botão ou comando <c>CANCELAR</c>).
    /// </summary>
    [JsonStringEnumMemberName("CANCELADO")]
    Cancelado,

    /// <summary>
    /// A velocidade de moto voltou durante a observação: o impacto não era queda.
    /// </summary>
    [JsonStringEnumMemberName("FALSO_POSITIVO")]
    FalsoPositivo,
}
