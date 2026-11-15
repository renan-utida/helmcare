namespace HelmCare.Api.Integracoes.ServicoRisco.Dtos;

/// <summary>
/// Campo <c>previsao</c> do <c>POST /risco/v1/indice-turno</c>.
/// </summary>
internal sealed class PrevisaoServicoRiscoRequisicao
{
    /// <summary>
    /// Chuva prevista, em mm/h.
    /// </summary>
    public required double ChuvaMmHora { get; init; }

    /// <summary>
    /// Temperatura prevista, em °C.
    /// </summary>
    public required double TemperaturaC { get; init; }

    /// <summary>
    /// Umidade relativa prevista, em %.
    /// </summary>
    public required double UmidadePct { get; init; }
}
