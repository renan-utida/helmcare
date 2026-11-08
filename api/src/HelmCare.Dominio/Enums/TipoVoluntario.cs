using System.Text.Json.Serialization;

namespace HelmCare.Dominio.Enums;

/// <summary>
/// Tipo de voluntário da rede que recebe e aceita os chamados de SOS. Todo voluntário também é um entregador cadastrado.
/// No JSON, no banco e no MQTT usa o nome do contrato (UPPER_SNAKE_CASE).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TipoVoluntario>))]
public enum TipoVoluntario
{
    /// <summary>
    /// Entregador que se ofereceu para socorrer colegas.
    /// </summary>
    [JsonStringEnumMemberName("ENTREGADOR")]
    Entregador,

    /// <summary>
    /// Vigia de moto de bairro, que costuma estar perto de quem caiu e atua na primeira camada de resposta.
    /// </summary>
    [JsonStringEnumMemberName("VIGIA_MOTO")]
    VigiaMoto,
}
