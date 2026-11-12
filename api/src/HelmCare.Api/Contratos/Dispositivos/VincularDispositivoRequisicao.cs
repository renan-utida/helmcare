using System;
using System.ComponentModel.DataAnnotations;

namespace HelmCare.Api.Contratos.Dispositivos;

/// <summary>
/// Corpo do <c>POST /api/v1/dispositivos</c>: vincula um capacete a um entregador.
/// </summary>
public sealed class VincularDispositivoRequisicao
{
    /// <summary>
    /// Código do capacete, no formato <c>HC-0000</c>.
    /// </summary>
    /// <example>HC-0001</example>
    [Required(ErrorMessage = "O campo codigo é obrigatório.")]
    [RegularExpression("^HC-[0-9]{4}$", ErrorMessage = "O campo codigo deve estar no formato HC-0000.")]
    public string Codigo { get; init; } = string.Empty;

    /// <summary>
    /// Id do entregador dono do capacete.
    /// </summary>
    /// <example>3f2a9c1e-5b7d-4e8a-9c21-7d4e5f6a8b90</example>
    // Anulável só para o [Required] acusar o campo ausente (um Guid comum viraria Guid.Empty).
    [Required(ErrorMessage = "O campo entregadorId é obrigatório.")]
    public Guid? EntregadorId { get; init; }
}
