using System;

namespace HelmCare.Api.Contratos.Entregadores;

/// <summary>
/// Corpo do <c>GET /api/v1/entregadores/sessao</c>: o mesmo do login, sem os campos do token. Com ele,
/// o app monta a sessão em qualquer aparelho, mesmo sem o SQLite.
/// </summary>
public sealed class SessaoEntregadorResposta
{
    /// <summary>
    /// Id do entregador do token.
    /// </summary>
    public required Guid EntregadorId { get; init; }

    /// <summary>
    /// Nome do entregador (para o próprio entregador).
    /// </summary>
    public required string Nome { get; init; }

    /// <summary>
    /// Id do voluntário do entregador, ou nulo se ele não é voluntário.
    /// </summary>
    public required Guid? VoluntarioId { get; init; }

    /// <summary>
    /// Código do capacete vinculado, ou nulo.
    /// </summary>
    public required string? CodigoDispositivo { get; init; }

    /// <summary>
    /// Id do turno ativo, ou nulo.
    /// </summary>
    public required Guid? TurnoAtivoId { get; init; }

    /// <summary>
    /// Id da ocorrência do entregador como vítima ainda aberta (<c>SOS_CAMADA_1</c>, <c>SOS_CAMADA_2</c>
    /// ou <c>SOCORRO_A_CAMINHO</c>), ou nulo.
    /// </summary>
    public required Guid? OcorrenciaAtivaId { get; init; }
}
