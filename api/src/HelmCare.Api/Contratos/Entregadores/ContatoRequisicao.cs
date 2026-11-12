using System.ComponentModel.DataAnnotations;
using HelmCare.Api.Contratos.Validacao;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Contratos.Entregadores;

/// <summary>
/// Corpo do <c>POST /api/v1/entregadores/{id}/contatos</c>.
/// </summary>
public sealed class ContatoRequisicao
{
    /// <summary>
    /// Nome do contato, até 120 caracteres.
    /// </summary>
    /// <example>Contato Simulado</example>
    [Required(ErrorMessage = "O campo nome é obrigatório.")]
    [StringLength(ContatoEmergencia.TamanhoMaximoNome, ErrorMessage = "O campo nome deve ter até 120 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    /// <summary>
    /// Telefone com DDD, 10 ou 11 dígitos, com ou sem máscara.
    /// </summary>
    /// <example>(11) 3123-4567</example>
    [Required(ErrorMessage = "O campo telefone é obrigatório.")]
    [Digitos(10, 11, ErrorMessage = "O campo telefone deve ter 10 ou 11 dígitos, com DDD.")]
    public string Telefone { get; init; } = string.Empty;

    /// <summary>
    /// Relação com o entregador (ex.: mãe), até 40 caracteres.
    /// </summary>
    /// <example>Mãe</example>
    [Required(ErrorMessage = "O campo parentesco é obrigatório.")]
    [StringLength(ContatoEmergencia.TamanhoMaximoParentesco, ErrorMessage = "O campo parentesco deve ter até 40 caracteres.")]
    public string Parentesco { get; init; } = string.Empty;
}
