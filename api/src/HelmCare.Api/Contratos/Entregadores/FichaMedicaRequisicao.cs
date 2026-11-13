using System.ComponentModel.DataAnnotations;
using HelmCare.Dominio.Entidades;

namespace HelmCare.Api.Contratos.Entregadores;

/// <summary>
/// Corpo do <c>PUT /api/v1/entregadores/{id}/ficha-medica</c>. Substitui a ficha inteira:
/// campo opcional não enviado fica vazio.
/// </summary>
public sealed class FichaMedicaRequisicao
{
    /// <summary>
    /// Tipo sanguíneo: <c>A+</c>, <c>A-</c>, <c>B+</c>, <c>B-</c>, <c>AB+</c>, <c>AB-</c>, <c>O+</c> ou <c>O-</c> (maiúsculas).
    /// </summary>
    /// <example>O+</example>
    [Required(ErrorMessage = "O campo tipoSanguineo é obrigatório.")]
    [RegularExpression(
        "^(A|B|AB|O)[+-]$",
        ErrorMessage = "O campo tipoSanguineo deve ser A+, A-, B+, B-, AB+, AB-, O+ ou O-.")]
    public string TipoSanguineo { get; init; } = string.Empty;

    /// <summary>
    /// Alergias conhecidas, opcional, até 500 caracteres.
    /// </summary>
    /// <example>Dipirona</example>
    [StringLength(FichaMedica.TamanhoMaximoTexto, ErrorMessage = "O campo alergias deve ter até 500 caracteres.")]
    public string? Alergias { get; init; }

    /// <summary>
    /// Condições críticas de saúde, opcional, até 500 caracteres.
    /// </summary>
    /// <example>Asma</example>
    [StringLength(FichaMedica.TamanhoMaximoTexto, ErrorMessage = "O campo condicoesCriticas deve ter até 500 caracteres.")]
    public string? CondicoesCriticas { get; init; }

    /// <summary>
    /// Medicamentos de uso contínuo, opcional, até 500 caracteres.
    /// </summary>
    /// <example>Salbutamol</example>
    [StringLength(FichaMedica.TamanhoMaximoTexto, ErrorMessage = "O campo medicamentos deve ter até 500 caracteres.")]
    public string? Medicamentos { get; init; }
}
