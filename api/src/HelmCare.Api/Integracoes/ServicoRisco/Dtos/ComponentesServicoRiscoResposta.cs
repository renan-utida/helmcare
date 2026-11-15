namespace HelmCare.Api.Integracoes.ServicoRisco.Dtos;

/// <summary>
/// Campo <c>componentes</c> da resposta do <c>POST /risco/v1/indice-turno</c>.
/// </summary>
internal sealed class ComponentesServicoRiscoResposta
{
    /// <summary>
    /// Componente de sinistros.
    /// </summary>
    public required int Sinistro { get; init; }

    /// <summary>
    /// Componente de chuva.
    /// </summary>
    public required int Chuva { get; init; }

    /// <summary>
    /// Componente de calor.
    /// </summary>
    public required int Calor { get; init; }
}
