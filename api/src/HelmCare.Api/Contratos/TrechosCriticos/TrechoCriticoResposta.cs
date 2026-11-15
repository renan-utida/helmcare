using System;
using HelmCare.Api.Servicos.Risco;

namespace HelmCare.Api.Contratos.TrechosCriticos;

/// <summary>
/// Um trecho crítico na resposta do <c>GET /api/v1/trechos-criticos</c> (seção 8 do contrato).
/// </summary>
public sealed class TrechoCriticoResposta
{
    /// <summary>
    /// Posição na lista (1 é o trecho com mais sinistros graves).
    /// </summary>
    /// <example>1</example>
    public required int Posicao { get; init; }

    /// <summary>
    /// Nome do logradouro.
    /// </summary>
    /// <example>SP 270 (RODOVIA RAPOSO TAVARES)</example>
    public required string Logradouro { get; init; }

    /// <summary>
    /// Latitude do trecho, em graus decimais.
    /// </summary>
    /// <example>-23.58341</example>
    public required double Latitude { get; init; }

    /// <summary>
    /// Longitude do trecho, em graus decimais.
    /// </summary>
    /// <example>-46.77771</example>
    public required double Longitude { get; init; }

    /// <summary>
    /// Sinistros graves com moto no período.
    /// </summary>
    /// <example>46</example>
    public required int QuantidadeSinistrosGraves { get; init; }

    /// <summary>
    /// Converte um trecho do Serviço de Risco.
    /// </summary>
    /// <param name="trecho">Trecho devolvido pelo serviço.</param>
    /// <returns>O trecho da resposta.</returns>
    public static TrechoCriticoResposta De(TrechoCritico trecho)
    {
        ArgumentNullException.ThrowIfNull(trecho);

        return new TrechoCriticoResposta
        {
            Posicao = trecho.Posicao,
            Logradouro = trecho.Logradouro,
            Latitude = trecho.Latitude,
            Longitude = trecho.Longitude,
            QuantidadeSinistrosGraves = trecho.QuantidadeSinistrosGraves,
        };
    }
}
