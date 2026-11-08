using System.Text.Json.Serialization;

namespace HelmCare.Dominio.Enums;

/// <summary>
/// Estado de uma <c>Ocorrencia</c> de SOS na API, conforme a máquina de estados do CLAUDE.md. <see cref="Encerrada"/> e <see cref="Cancelada"/> são finais.
/// No JSON, no banco e no MQTT usa o nome do contrato (UPPER_SNAKE_CASE).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EstadoOcorrencia>))]
public enum EstadoOcorrencia
{
    /// <summary>
    /// SOS aberto e enviado à primeira camada: voluntários disponíveis a até <c>RAIO_CAMADA_1_KM</c> (2 km).
    /// </summary>
    [JsonStringEnumMemberName("SOS_CAMADA_1")]
    SosCamada1,

    /// <summary>
    /// Ninguém aceitou em <c>TEMPO_ACEITE_CAMADA_1_S</c> (60 s): o SOS foi escalado para toda a rede de voluntários disponíveis.
    /// </summary>
    [JsonStringEnumMemberName("SOS_CAMADA_2")]
    SosCamada2,

    /// <summary>
    /// Um voluntário aceitou o socorro e está a caminho do entregador.
    /// </summary>
    [JsonStringEnumMemberName("SOCORRO_A_CAMINHO")]
    SocorroACaminho,

    /// <summary>
    /// Atendimento concluído (estado final). Pode vir de uma camada de SOS, quando o SAMU chega antes de um aceite.
    /// </summary>
    [JsonStringEnumMemberName("ENCERRADA")]
    Encerrada,

    /// <summary>
    /// SOS cancelado antes do atendimento (estado final).
    /// </summary>
    [JsonStringEnumMemberName("CANCELADA")]
    Cancelada,
}
