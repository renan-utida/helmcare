using System.ComponentModel.DataAnnotations;

namespace HelmCare.Api.Contratos.Voluntarios;

/// <summary>
/// Corpo do <c>PUT /api/v1/voluntarios/{id}/disponibilidade</c>.
/// </summary>
/// <remarks>
/// Aqui só se valida a obrigatoriedade e a faixa. A regra "disponível exige as duas coordenadas"
/// é do domínio (<c>Voluntario.AtualizarDisponibilidade</c>), e "indisponível apaga a posição" também:
/// as coordenadas que vierem junto com <c>disponivel</c> falso são descartadas.
/// </remarks>
public sealed class DisponibilidadeVoluntarioRequisicao
{
    /// <summary>
    /// Se o voluntário passa a receber chamados.
    /// </summary>
    /// <example>true</example>
    // Anulável só para o [Required] acusar o campo ausente (um bool comum viraria falso).
    [Required(ErrorMessage = "O campo disponivel é obrigatório.")]
    public bool? Disponivel { get; init; }

    /// <summary>
    /// Latitude atual, de -90 a 90. Obrigatória quando disponível.
    /// </summary>
    /// <example>-23.5614</example>
    [Range(-90.0, 90.0, ErrorMessage = "O campo latitude deve estar entre -90 e 90.")]
    public double? Latitude { get; init; }

    /// <summary>
    /// Longitude atual, de -180 a 180. Obrigatória quando disponível.
    /// </summary>
    /// <example>-46.6559</example>
    [Range(-180.0, 180.0, ErrorMessage = "O campo longitude deve estar entre -180 e 180.")]
    public double? Longitude { get; init; }
}
