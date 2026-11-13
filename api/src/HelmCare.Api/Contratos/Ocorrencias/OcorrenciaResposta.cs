using System;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Contratos.Ocorrencias;

/// <summary>
/// Status de uma ocorrência e o cronômetro, devolvidos pelo <c>GET /api/v1/ocorrencias/{id}</c>.
/// Não leva nenhum dado do entregador (seção 9 do contrato).
/// </summary>
public sealed class OcorrenciaResposta
{
    /// <summary>
    /// Id da ocorrência.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Estado da ocorrência.
    /// </summary>
    public required EstadoOcorrencia Estado { get; init; }

    /// <summary>
    /// Camada de voluntários acionada (1 ou 2).
    /// </summary>
    /// <example>1</example>
    public required int Camada { get; init; }

    /// <summary>
    /// Abertura da ocorrência, em UTC.
    /// </summary>
    public required DateTime CriadaEm { get; init; }

    /// <summary>
    /// Aceite, em UTC, ou nulo.
    /// </summary>
    public required DateTime? AceitaEm { get; init; }

    /// <summary>
    /// Encerramento ou cancelamento, em UTC, ou nulo.
    /// </summary>
    public required DateTime? EncerradaEm { get; init; }

    /// <summary>
    /// Indicador de impacto: segundos completos entre a queda e o aceite, ou nulo sem aceite.
    /// </summary>
    public required int? TempoAteAceiteSegundos { get; init; }

    /// <summary>
    /// Latitude: exata para o voluntário que aceitou; nos outros casos, com 2 casas (seção 9).
    /// </summary>
    /// <example>-23.56</example>
    public required double Latitude { get; init; }

    /// <summary>
    /// Longitude: exata para o voluntário que aceitou; nos outros casos, com 2 casas (seção 9).
    /// </summary>
    /// <example>-46.65</example>
    public required double Longitude { get; init; }

    /// <summary>
    /// Diz qual localização veio: verdadeiro para a exata, falso para a aproximada.
    /// </summary>
    public required bool LocalizacaoExata { get; init; }

    /// <summary>
    /// Cronômetro desde a queda.
    /// </summary>
    public required CronometroResposta Cronometro { get; init; }
}
