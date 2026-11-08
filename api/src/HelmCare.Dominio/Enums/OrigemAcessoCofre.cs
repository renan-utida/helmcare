using System.Text.Json.Serialization;

namespace HelmCare.Dominio.Enums;

/// <summary>
/// Origem de um acesso à ficha médica pelo Cofre, registrada na auditoria de cada <c>AcessoCofre</c>.
/// No JSON, no banco e no MQTT usa o nome do contrato (UPPER_SNAKE_CASE).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OrigemAcessoCofre>))]
public enum OrigemAcessoCofre
{
    /// <summary>
    /// Socorrista autenticado acessou a ficha durante uma ocorrência de SOS ativa do entregador.
    /// </summary>
    [JsonStringEnumMemberName("SOS_ATIVO")]
    SosAtivo,

    /// <summary>
    /// Socorrista autenticado abriu uma emergência manual pelo QR Code, sem SOS ativo.
    /// </summary>
    [JsonStringEnumMemberName("EMERGENCIA_MANUAL")]
    EmergenciaManual,
}
