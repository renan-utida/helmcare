using System;
using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Contratos.Ocorrencias;

/// <summary>
/// Um chamado do <c>GET /api/v1/ocorrencias/proximas?voluntarioId=</c>: uma ocorrência que o
/// voluntário pode atender. Não leva nenhum dado do entregador, e a localização é sempre a
/// aproximada, porque ninguém aceitou ainda (seção 9 do contrato).
/// </summary>
public sealed class OcorrenciaProximaResposta
{
    /// <summary>
    /// Id da ocorrência.
    /// </summary>
    public required Guid OcorrenciaId { get; init; }

    /// <summary>
    /// Estado da ocorrência (<c>SOS_CAMADA_1</c> ou <c>SOS_CAMADA_2</c>).
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
    /// Latitude aproximada (2 casas).
    /// </summary>
    /// <example>-23.56</example>
    public required double Latitude { get; init; }

    /// <summary>
    /// Longitude aproximada (2 casas).
    /// </summary>
    /// <example>-46.65</example>
    public required double Longitude { get; init; }

    /// <summary>
    /// Distância, em km com 1 casa, da posição do voluntário até a localização aproximada (e não
    /// até a exata, para a distância não revelar a posição; pode diferir da real em até cerca de 0,7 km).
    /// </summary>
    /// <example>0.6</example>
    public required double DistanciaKm { get; init; }

    /// <summary>
    /// Cronômetro desde a queda, no mesmo formato do <c>GET /ocorrencias/{id}</c>.
    /// </summary>
    public required CronometroResposta Cronometro { get; init; }
}
