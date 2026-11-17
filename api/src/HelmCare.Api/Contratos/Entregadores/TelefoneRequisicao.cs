using System.ComponentModel.DataAnnotations;
using HelmCare.Api.Contratos.Validacao;

namespace HelmCare.Api.Contratos.Entregadores;

/// <summary>
/// Corpo do <c>PUT /api/v1/entregadores/{id}/telefone</c>.
/// </summary>
public sealed class TelefoneRequisicao
{
    /// <summary>
    /// Telefone novo com DDD, 10 ou 11 dígitos, com ou sem máscara.
    /// </summary>
    /// <example>(11) 98765-4321</example>
    [Required(ErrorMessage = "O campo telefone é obrigatório.")]
    [Digitos(10, 11, ErrorMessage = "O campo telefone deve ter 10 ou 11 dígitos, com DDD.")]
    public string Telefone { get; init; } = string.Empty;
}
