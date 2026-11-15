namespace HelmCare.Api.Integracoes.ServicoRisco.Dtos;

/// <summary>
/// Corpo do <c>POST /risco/v1/indice-turno</c> (seção 8 do contrato).
/// </summary>
internal sealed class IndiceTurnoServicoRiscoRequisicao
{
    /// <summary>
    /// Início do turno em ISO 8601 UTC, com <c>Z</c> (o serviço recusa data sem fuso).
    /// </summary>
    public required string Inicio { get; init; }

    /// <summary>
    /// Fim previsto do turno em ISO 8601 UTC, com <c>Z</c>.
    /// </summary>
    public required string Fim { get; init; }

    /// <summary>
    /// Previsão do tempo para o turno inteiro.
    /// </summary>
    public required PrevisaoServicoRiscoRequisicao Previsao { get; init; }
}
