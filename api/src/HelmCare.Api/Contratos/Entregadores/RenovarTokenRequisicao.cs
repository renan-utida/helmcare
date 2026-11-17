using System.ComponentModel.DataAnnotations;

namespace HelmCare.Api.Contratos.Entregadores;

/// <summary>
/// Corpo do <c>POST /api/v1/entregadores/renovar-token</c>.
/// </summary>
public sealed class RenovarTokenRequisicao
{
    /// <summary>
    /// Token de renovação recebido no login ou na última renovação.
    /// </summary>
    [Required(ErrorMessage = "O campo refreshToken é obrigatório.")]
    public string RefreshToken { get; init; } = string.Empty;
}
