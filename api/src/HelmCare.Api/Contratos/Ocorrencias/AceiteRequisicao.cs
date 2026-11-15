using System;
using System.ComponentModel.DataAnnotations;

namespace HelmCare.Api.Contratos.Ocorrencias;

/// <summary>
/// Corpo do <c>POST /api/v1/ocorrencias/{id}/aceites</c>.
/// </summary>
public sealed class AceiteRequisicao
{
    /// <summary>
    /// Voluntário que aceita o chamado.
    /// </summary>
    /// <example>01a1df61-e6b2-7652-8580-2c6d26b7f04b</example>
    // Anulável só para o [Required] acusar o campo ausente (um Guid comum viraria Guid.Empty).
    [Required(ErrorMessage = "O campo voluntarioId é obrigatório.")]
    public Guid? VoluntarioId { get; init; }
}
