using System;
using System.Collections.Generic;
using System.Linq;
using HelmCare.Api.Servicos.Risco;

namespace HelmCare.Api.Contratos.TrechosCriticos;

/// <summary>
/// Resposta do <c>GET /api/v1/trechos-criticos</c>, no mesmo formato do Serviço de Risco
/// (exemplo da seção 8 do contrato): o período e os trechos, na ordem recebida.
/// </summary>
public sealed class TrechosCriticosResposta
{
    /// <summary>
    /// Período dos dados de sinistros.
    /// </summary>
    /// <example>2019 a 2025</example>
    public required string Periodo { get; init; }

    /// <summary>
    /// Os trechos, em ordem decrescente de sinistros graves.
    /// </summary>
    public required IReadOnlyList<TrechoCriticoResposta> Trechos { get; init; }

    /// <summary>
    /// Monta a resposta a partir da lista do Serviço de Risco, mantendo a ordem.
    /// </summary>
    /// <param name="trechos">Lista devolvida pelo serviço.</param>
    /// <returns>A resposta da API.</returns>
    public static TrechosCriticosResposta De(ListaTrechosCriticos trechos)
    {
        ArgumentNullException.ThrowIfNull(trechos);

        return new TrechosCriticosResposta
        {
            Periodo = trechos.Periodo,
            Trechos = trechos.Trechos.Select(TrechoCriticoResposta.De).ToList(),
        };
    }
}
