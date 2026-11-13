using HelmCare.Dominio.Enums;

namespace HelmCare.Api.Servicos.Risco;

/// <summary>
/// Resultado da <see cref="CalculadoraIndiceRisco"/>: o índice, a faixa e os três componentes,
/// todos inteiros de 0 a 100.
/// </summary>
/// <param name="IndiceRisco">Índice de Risco do Turno.</param>
/// <param name="FaixaRisco">Faixa do índice.</param>
/// <param name="Sinistro">Componente de sinistros.</param>
/// <param name="Chuva">Componente de chuva.</param>
/// <param name="Calor">Componente de calor.</param>
public sealed record CalculoIndiceRisco(int IndiceRisco, FaixaRisco FaixaRisco, int Sinistro, int Chuva, int Calor);
