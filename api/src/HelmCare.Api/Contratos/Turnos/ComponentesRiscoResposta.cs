namespace HelmCare.Api.Contratos.Turnos;

/// <summary>
/// Os três componentes do Índice de Risco do Turno, de 0 a 100 (seção 8 do contrato).
/// </summary>
public sealed class ComponentesRiscoResposta
{
    /// <summary>
    /// Histórico de sinistros graves com moto no horário do turno (peso 50%).
    /// </summary>
    /// <example>80</example>
    public required int Sinistro { get; init; }

    /// <summary>
    /// Exposição direta à chuva prevista (peso 25%).
    /// </summary>
    /// <example>70</example>
    public required int Chuva { get; init; }

    /// <summary>
    /// Índice de calor previsto (peso 25%).
    /// </summary>
    /// <example>30</example>
    public required int Calor { get; init; }
}
