namespace HelmCare.Api.Integracoes.OpenMeteo.Dtos;

/// <summary>
/// Corpo do <c>GET v1/forecast</c> da Open-Meteo, só com os blocos usados. Tudo anulável: a ausência
/// vira resposta inválida na conferência do cliente.
/// </summary>
public sealed class RespostaOpenMeteo
{
    /// <summary>
    /// Previsão horária (no <c>POST /turnos</c>).
    /// </summary>
    public DadosHorariosOpenMeteo? Hourly { get; init; }

    /// <summary>
    /// Condições atuais (no <c>GET /clima</c>).
    /// </summary>
    public DadosAtuaisOpenMeteo? Current { get; init; }
}
