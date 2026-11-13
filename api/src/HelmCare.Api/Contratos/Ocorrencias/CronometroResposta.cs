namespace HelmCare.Api.Contratos.Ocorrencias;

/// <summary>
/// Cronômetro do <c>GET /api/v1/ocorrencias/{id}</c>: tempo desde a queda, comparado com a meta.
/// O contrato não define o formato; estes três campos são decisão da API.
/// </summary>
public sealed class CronometroResposta
{
    /// <summary>
    /// Segundos completos desde a queda até o aceite, o encerramento ou agora, nessa ordem.
    /// </summary>
    /// <example>63</example>
    public required int SegundosDesdeQueda { get; init; }

    /// <summary>
    /// Meta do tempo até o aceite (<c>META_TEMPO_ACEITE_S</c>).
    /// </summary>
    /// <example>120</example>
    public required int MetaSegundos { get; init; }

    /// <summary>
    /// Verdadeiro quando a ocorrência foi aceita ou terminou: o valor não muda mais.
    /// </summary>
    public required bool Parado { get; init; }
}
