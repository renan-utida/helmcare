using System;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Contratos.Turnos;

/// <summary>
/// Estado do alerta de queda, no formato do exemplo do <c>GET /turnos/{id}/alerta</c> da seção 7
/// do contrato. Os campos nulos saem como <c>null</c> no JSON, como no exemplo.
/// </summary>
public sealed class AlertaTurnoResposta
{
    /// <summary>
    /// Último estado recebido do capacete, por evento ou por telemetria.
    /// </summary>
    public required EstadoDispositivo EstadoCapacete { get; init; }

    /// <summary>
    /// Fim da contagem, só em <c>CONTAGEM</c>. Vem do relógio do capacete: o app conta a partir
    /// dele, e a API nunca o compara com a hora do servidor.
    /// </summary>
    public required DateTime? ContagemTerminaEm { get; init; }

    /// <summary>
    /// Ocorrência ativa do entregador, só depois do SOS.
    /// </summary>
    public required Guid? OcorrenciaId { get; init; }

    /// <summary>
    /// Índice de calor da última telemetria, em °C, ou nulo.
    /// </summary>
    /// <example>37.1</example>
    public required double? IndiceCalorC { get; init; }
}
