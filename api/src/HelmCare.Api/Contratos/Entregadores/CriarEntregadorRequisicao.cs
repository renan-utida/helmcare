using System.ComponentModel.DataAnnotations;
using HelmCare.Api.Contratos.Validacao;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Contratos.Entregadores;

/// <summary>
/// Corpo do <c>POST /api/v1/entregadores</c>. As regras são as de "Validações de cadastro"
/// (seção 3 do contrato); dado fora delas volta 400 com o erro de cada campo.
/// </summary>
public sealed class CriarEntregadorRequisicao
{
    /// <summary>
    /// Tamanho máximo do e-mail (seção 3 do contrato).
    /// </summary>
    public const int TamanhoMaximoEmail = 254;

    /// <summary>
    /// Nome completo, até 120 caracteres.
    /// </summary>
    /// <example>Entregador Simulado</example>
    [Required(ErrorMessage = "O campo nome é obrigatório.")]
    [StringLength(Entregador.TamanhoMaximoNome, ErrorMessage = "O campo nome deve ter até 120 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    /// <summary>
    /// CPF com 11 dígitos, com ou sem máscara. A API guarda só os dígitos.
    /// </summary>
    /// <example>111.444.777-35</example>
    [Required(ErrorMessage = "O campo cpf é obrigatório.")]
    [Digitos(11, ErrorMessage = "O campo cpf deve ter 11 dígitos, com ou sem máscara.")]
    public string Cpf { get; init; } = string.Empty;

    /// <summary>
    /// Número da CNH com 11 dígitos, com ou sem máscara. A API guarda só os dígitos.
    /// </summary>
    /// <example>"12345678900"</example>
    [Required(ErrorMessage = "O campo cnh é obrigatório.")]
    [Digitos(11, ErrorMessage = "O campo cnh deve ter 11 dígitos, com ou sem máscara.")]
    public string Cnh { get; init; } = string.Empty;

    /// <summary>
    /// Telefone com DDD, 10 ou 11 dígitos, com ou sem máscara. A API guarda só os dígitos.
    /// </summary>
    /// <example>(11) 91234-5678</example>
    [Required(ErrorMessage = "O campo telefone é obrigatório.")]
    [Digitos(10, 11, ErrorMessage = "O campo telefone deve ter 10 ou 11 dígitos, com DDD.")]
    public string Telefone { get; init; } = string.Empty;

    /// <summary>
    /// E-mail da conta, até 254 caracteres. Não pode repetir (sem diferenciar maiúsculas).
    /// </summary>
    /// <example>entregador@exemplo.com</example>
    [Required(ErrorMessage = "O campo email é obrigatório.")]
    [EmailAddress(ErrorMessage = "O campo email deve ser um e-mail válido.")]
    [StringLength(TamanhoMaximoEmail, ErrorMessage = "O campo email deve ter até 254 caracteres.")]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Senha de 8 a 64 caracteres, com pelo menos uma letra maiúscula, uma minúscula e um dígito.
    /// Fica só como hash no Identity.
    /// </summary>
    /// <example>Senha123</example>
    [Required(ErrorMessage = "O campo senha é obrigatório.")]
    [Senha]
    public string Senha { get; init; } = string.Empty;
}
