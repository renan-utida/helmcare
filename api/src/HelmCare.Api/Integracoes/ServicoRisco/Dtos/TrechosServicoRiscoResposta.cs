using System.Collections.Generic;

namespace HelmCare.Api.Integracoes.ServicoRisco.Dtos;

/// <summary>
/// Resposta 200 do <c>GET /risco/v1/trechos-criticos</c> (exemplo da seção 8).
/// </summary>
internal sealed class TrechosServicoRiscoResposta
{
    /// <summary>
    /// Período dos dados.
    /// </summary>
    public required string? Periodo { get; init; }

    /// <summary>
    /// Os trechos, em ordem decrescente de sinistros graves.
    /// </summary>
    public required IReadOnlyList<TrechoServicoRiscoResposta?>? Trechos { get; init; }
}
