using System.Text.Json.Serialization;

namespace HelmCare.Dominio.Enums;

/// <summary>
/// Motivo de uma tentativa negada de acesso ao Cofre (seção 4 do contrato, 1.16), gravado no
/// <c>motivo</c> do <c>AcessoCofre</c>. Nulo quando o acesso é concedido. No JSON e no banco usa o
/// nome do contrato (UPPER_SNAKE_CASE).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MotivoNegacaoCofre>))]
public enum MotivoNegacaoCofre
{
    /// <summary>
    /// O socorrista está autenticado, mas o cadastro dele ainda não foi verificado.
    /// </summary>
    [JsonStringEnumMemberName("SOCORRISTA_NAO_VERIFICADO")]
    SocorristaNaoVerificado,

    /// <summary>
    /// A janela de acesso do socorrista para esse entregador já fechou (TTL vencido ou fim da
    /// ocorrência) e ainda está dentro do bloqueio.
    /// </summary>
    [JsonStringEnumMemberName("ACESSO_EXPIRADO")]
    AcessoExpirado,
}
