using System;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Contratos.Turnos;

/// <summary>
/// Índice de Risco do Turno, no formato do exemplo do <c>POST /turnos</c> da seção 7 do contrato.
/// É a resposta do <c>POST /api/v1/turnos</c> e do <c>GET /api/v1/turnos/{id}/risco</c>.
/// </summary>
public sealed class RiscoTurnoResposta
{
    /// <summary>
    /// Id do turno.
    /// </summary>
    public required Guid TurnoId { get; init; }

    /// <summary>
    /// Índice de Risco do Turno, de 0 a 100.
    /// </summary>
    /// <example>65</example>
    public required int IndiceRisco { get; init; }

    /// <summary>
    /// Faixa do índice.
    /// </summary>
    public required FaixaRisco FaixaRisco { get; init; }

    /// <summary>
    /// Componentes do índice.
    /// </summary>
    public required ComponentesRiscoResposta Componentes { get; init; }

    /// <summary>
    /// Explicação em linguagem simples.
    /// </summary>
    public required string Explicacao { get; init; }

    /// <summary>
    /// Monta a resposta com o risco guardado no turno.
    /// </summary>
    /// <param name="turno">Turno com o risco aplicado.</param>
    /// <returns>A resposta.</returns>
    /// <exception cref="InvalidOperationException">Quando o turno não tem risco (a API sempre aplica o risco antes de gravar).</exception>
    public static RiscoTurnoResposta De(Turno turno)
    {
        ArgumentNullException.ThrowIfNull(turno);

        return new RiscoTurnoResposta
        {
            TurnoId = turno.Id,
            IndiceRisco = turno.IndiceRisco ?? throw SemRisco(),
            FaixaRisco = turno.FaixaRisco ?? throw SemRisco(),
            Componentes = ComponentesDe(turno),
            Explicacao = turno.Explicacao ?? throw SemRisco(),
        };
    }

    /// <summary>
    /// Monta os componentes guardados no turno.
    /// </summary>
    /// <param name="turno">Turno com o risco aplicado.</param>
    /// <returns>Os componentes.</returns>
    /// <exception cref="InvalidOperationException">Quando o turno não tem risco.</exception>
    internal static ComponentesRiscoResposta ComponentesDe(Turno turno)
    {
        return new ComponentesRiscoResposta
        {
            Sinistro = turno.ComponenteSinistro ?? throw SemRisco(),
            Chuva = turno.ComponenteChuva ?? throw SemRisco(),
            Calor = turno.ComponenteCalor ?? throw SemRisco(),
        };
    }

    /// <summary>
    /// Erro para um turno gravado sem o risco, o que a API nunca faz (vira 500).
    /// </summary>
    /// <returns>A exceção.</returns>
    internal static InvalidOperationException SemRisco()
    {
        return new InvalidOperationException("O turno foi gravado sem o Índice de Risco.");
    }
}
