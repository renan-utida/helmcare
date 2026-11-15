namespace HelmCare.Api.Integracoes.ServicoRisco.Dtos;

/// <summary>
/// Um item de <c>trechos</c> na resposta do <c>GET /risco/v1/trechos-criticos</c>.
/// </summary>
internal sealed class TrechoServicoRiscoResposta
{
    /// <summary>
    /// Posição na lista.
    /// </summary>
    public required int Posicao { get; init; }

    /// <summary>
    /// Nome do logradouro.
    /// </summary>
    public required string? Logradouro { get; init; }

    /// <summary>
    /// Latitude, em graus decimais.
    /// </summary>
    public required double Latitude { get; init; }

    /// <summary>
    /// Longitude, em graus decimais.
    /// </summary>
    public required double Longitude { get; init; }

    /// <summary>
    /// Sinistros graves no período.
    /// </summary>
    public required int QuantidadeSinistrosGraves { get; init; }
}
