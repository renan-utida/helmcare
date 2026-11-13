using System;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Contratos.Turnos;

/// <summary>
/// Turno devolvido pelo <c>POST /api/v1/turnos/{id}/encerrar</c> (200 com o recurso atualizado,
/// quadro "Códigos padrão" da seção 7). O contrato não traz exemplo deste corpo: são os campos do
/// <c>Turno</c> da seção 3, com o risco no mesmo formato do <c>POST /turnos</c>.
/// </summary>
public sealed class TurnoResposta
{
    /// <summary>
    /// Id do turno.
    /// </summary>
    public required Guid TurnoId { get; init; }

    /// <summary>
    /// Id do entregador.
    /// </summary>
    public required Guid EntregadorId { get; init; }

    /// <summary>
    /// Início do turno, em UTC.
    /// </summary>
    public required DateTime Inicio { get; init; }

    /// <summary>
    /// Fim previsto, em UTC.
    /// </summary>
    public required DateTime FimPrevisto { get; init; }

    /// <summary>
    /// Fim real, em UTC, ou nulo enquanto o turno está ativo.
    /// </summary>
    public required DateTime? Fim { get; init; }

    /// <summary>
    /// Se a proteção do capacete está ligada (falso depois do encerramento).
    /// </summary>
    public required bool ProtecaoAtiva { get; init; }

    /// <summary>
    /// Índice de Risco do Turno, de 0 a 100.
    /// </summary>
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
    /// Explicação do risco.
    /// </summary>
    public required string Explicacao { get; init; }

    /// <summary>
    /// Monta a resposta a partir da entidade.
    /// </summary>
    /// <param name="turno">Turno com o risco aplicado.</param>
    /// <returns>A resposta.</returns>
    /// <exception cref="InvalidOperationException">Quando o turno não tem risco.</exception>
    public static TurnoResposta De(Turno turno)
    {
        ArgumentNullException.ThrowIfNull(turno);

        return new TurnoResposta
        {
            TurnoId = turno.Id,
            EntregadorId = turno.EntregadorId,
            Inicio = turno.Inicio,
            FimPrevisto = turno.FimPrevisto,
            Fim = turno.Fim,
            ProtecaoAtiva = turno.ProtecaoAtiva,
            IndiceRisco = turno.IndiceRisco ?? throw RiscoTurnoResposta.SemRisco(),
            FaixaRisco = turno.FaixaRisco ?? throw RiscoTurnoResposta.SemRisco(),
            Componentes = RiscoTurnoResposta.ComponentesDe(turno),
            Explicacao = turno.Explicacao ?? throw RiscoTurnoResposta.SemRisco(),
        };
    }
}
