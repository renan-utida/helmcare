using System;
using System.ComponentModel.DataAnnotations;
using HelmCare.Api.Contratos.Validacao;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Contratos.Voluntarios;

/// <summary>
/// Corpo do <c>POST /api/v1/voluntarios</c>: torna um entregador cadastrado voluntário da rede.
/// </summary>
public sealed class CadastrarVoluntarioRequisicao
{
    /// <summary>
    /// Id do entregador que vira voluntário.
    /// </summary>
    /// <example>3f2a9c1e-5b7d-4e8a-9c21-7d4e5f6a8b90</example>
    // Anulável só para o [Required] acusar o campo ausente (um Guid comum viraria Guid.Empty).
    [Required(ErrorMessage = "O campo entregadorId é obrigatório.")]
    public Guid? EntregadorId { get; init; }

    /// <summary>
    /// Tipo de voluntário (<c>TipoVoluntario</c>): <c>ENTREGADOR</c> ou <c>VIGIA_MOTO</c>.
    /// </summary>
    /// <example>ENTREGADOR</example>
    [Required(ErrorMessage = "O campo tipo é obrigatório.")]
    [NomeDoContrato<TipoVoluntario>(ErrorMessage = "O campo tipo deve ser ENTREGADOR ou VIGIA_MOTO.")]
    public string? Tipo { get; init; }
}
