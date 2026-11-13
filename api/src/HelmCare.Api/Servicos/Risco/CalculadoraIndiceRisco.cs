using System;
using HelmCare.Dominio.Comum;

namespace HelmCare.Api.Servicos.Risco;

/// <summary>
/// Fórmula do Índice de Risco do Turno (seção 8 do contrato), como função pura:
/// <c>0,5 × sinistro + 0,25 × chuva + 0,25 × calor</c>, com cada componente normalizado de 0 a 100.
/// </summary>
public static class CalculadoraIndiceRisco
{
    /// <summary>
    /// Chuva, em mm/h, a partir da qual o componente <c>chuva</c> vale 100 (0 mm/h vale 0).
    /// </summary>
    public const double ChuvaMaximaMmHora = 10.0;

    /// <summary>
    /// Índice de calor, em °C, em que o componente <c>calor</c> vale 0.
    /// </summary>
    public const double CalorMinimoC = 27.0;

    /// <summary>
    /// Índice de calor, em °C, a partir do qual o componente <c>calor</c> vale 100.
    /// </summary>
    public const double CalorMaximoC = 41.0;

    /// <summary>
    /// Calcula o índice e a faixa. Cada componente e o índice são arredondados para o inteiro
    /// mais próximo, com 0,5 para cima, e limitados a 0..100. O índice é calculado sobre os
    /// componentes já arredondados, como no exemplo da seção 8 (80, 70 e 30 dão 65).
    /// </summary>
    /// <param name="sinistro">Componente de sinistros já normalizado (0 a 100; fora disso é limitado).</param>
    /// <param name="chuvaMmHora">Chuva prevista, em mm/h.</param>
    /// <param name="indiceCalorC">Índice de calor previsto, em °C.</param>
    /// <returns>O índice, a faixa e os componentes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Quando algum valor não é um número finito.</exception>
    public static CalculoIndiceRisco Calcular(double sinistro, double chuvaMmHora, double indiceCalorC)
    {
        ExigirFinito(sinistro, nameof(sinistro));
        ExigirFinito(chuvaMmHora, nameof(chuvaMmHora));
        ExigirFinito(indiceCalorC, nameof(indiceCalorC));

        int componenteSinistro = Normalizar(sinistro);
        int componenteChuva = Normalizar(chuvaMmHora / ChuvaMaximaMmHora * 100);
        int componenteCalor = Normalizar((indiceCalorC - CalorMinimoC) / (CalorMaximoC - CalorMinimoC) * 100);

        int indice = Normalizar((0.5 * componenteSinistro) + (0.25 * componenteChuva) + (0.25 * componenteCalor));

        return new CalculoIndiceRisco(
            indice,
            ClassificacaoRisco.FaixaDe(indice),
            componenteSinistro,
            componenteChuva,
            componenteCalor);
    }

    // Arredonda com 0,5 para cima (AwayFromZero). O padrão do Math.Round é o arredondamento
    // bancário, que levaria 64,5 para 64. Depois limita a 0..100.
    private static int Normalizar(double valor)
    {
        return (int)Math.Clamp(Math.Round(valor, MidpointRounding.AwayFromZero), 0, 100);
    }

    private static void ExigirFinito(double valor, string parametro)
    {
        if (!double.IsFinite(valor))
        {
            throw new ArgumentOutOfRangeException(parametro, "O valor deve ser um número finito.");
        }
    }
}
