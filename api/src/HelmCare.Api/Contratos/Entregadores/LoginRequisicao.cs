using System.ComponentModel.DataAnnotations;

namespace HelmCare.Api.Contratos.Entregadores;

/// <summary>
/// Corpo do <c>POST /api/v1/entregadores/login</c>. A senha não passa pela regra do cadastro: uma
/// senha fora dela simplesmente não confere e dá o mesmo <c>401</c>.
/// </summary>
public sealed class LoginRequisicao
{
    /// <summary>
    /// E-mail da conta.
    /// </summary>
    /// <example>entregador@exemplo.com</example>
    [Required(ErrorMessage = "O campo email é obrigatório.")]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Senha da conta.
    /// </summary>
    /// <example>Senha123</example>
    [Required(ErrorMessage = "O campo senha é obrigatório.")]
    public string Senha { get; init; } = string.Empty;
}
