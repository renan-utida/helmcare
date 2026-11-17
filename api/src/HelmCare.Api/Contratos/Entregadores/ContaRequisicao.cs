using System.ComponentModel.DataAnnotations;
using HelmCare.Api.Contratos.Validacao;

namespace HelmCare.Api.Contratos.Entregadores;

/// <summary>
/// Corpo do <c>POST /api/v1/dev/entregadores/{id}/conta</c> (só em Development): a conta de um
/// entregador cadastrado antes da 1.15, com as mesmas regras de e-mail e senha do cadastro.
/// </summary>
public sealed class ContaRequisicao
{
    /// <summary>
    /// E-mail da conta, até 254 caracteres. Não pode repetir (sem diferenciar maiúsculas).
    /// </summary>
    /// <example>entregador@exemplo.com</example>
    [Required(ErrorMessage = "O campo email é obrigatório.")]
    [EmailAddress(ErrorMessage = "O campo email deve ser um e-mail válido.")]
    [StringLength(CriarEntregadorRequisicao.TamanhoMaximoEmail, ErrorMessage = "O campo email deve ter até 254 caracteres.")]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Senha de 8 a 64 caracteres, com maiúscula, minúscula e dígito.
    /// </summary>
    /// <example>Senha123</example>
    [Required(ErrorMessage = "O campo senha é obrigatório.")]
    [Senha]
    public string Senha { get; init; } = string.Empty;
}
