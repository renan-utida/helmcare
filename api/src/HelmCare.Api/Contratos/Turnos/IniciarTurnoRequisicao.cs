using System;
using System.ComponentModel.DataAnnotations;

namespace HelmCare.Api.Contratos.Turnos;

/// <summary>
/// Corpo do <c>POST /api/v1/turnos</c> (exemplo da seção 7 do contrato).
/// </summary>
/// <remarks>
/// As datas aceitam qualquer fuso (<c>Z</c> ou <c>-03:00</c>) e são convertidas para UTC; sem fuso,
/// valem como UTC. Os campos são anuláveis só para o <c>[Required]</c> acusar o campo ausente.
/// </remarks>
public sealed class IniciarTurnoRequisicao
{
    /// <summary>
    /// Id do entregador.
    /// </summary>
    /// <example>3f2a9c1e-5b7d-4e8a-9c21-7d4e5f6a8b90</example>
    [Required(ErrorMessage = "O campo entregadorId é obrigatório.")]
    public Guid? EntregadorId { get; init; }

    /// <summary>
    /// Início do turno.
    /// </summary>
    /// <example>2026-11-12T20:00:00Z</example>
    [Required(ErrorMessage = "O campo inicio é obrigatório.")]
    public DateTime? Inicio { get; init; }

    /// <summary>
    /// Fim previsto do turno, depois do início e a até 24 h dele.
    /// </summary>
    /// <example>2026-11-13T00:00:00Z</example>
    [Required(ErrorMessage = "O campo fimPrevisto é obrigatório.")]
    public DateTime? FimPrevisto { get; init; }

    /// <summary>
    /// Se o capacete deve ser armado (opcional, padrão verdadeiro). Verdadeiro exige capacete vinculado.
    /// </summary>
    /// <example>true</example>
    public bool ProtecaoAtiva { get; init; } = true;
}
