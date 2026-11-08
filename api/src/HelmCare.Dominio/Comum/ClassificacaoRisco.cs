using HelmCare.Dominio.Enums;

namespace HelmCare.Dominio.Comum;

/// <summary>
/// Converte o Índice de Risco do Turno (0 a 100) na <see cref="FaixaRisco"/>, seguindo a
/// seção 8 do contrato. A API usa esta regra para conferir se a faixa devolvida pelo
/// Serviço de Risco é coerente com o índice.
/// </summary>
public static class ClassificacaoRisco
{
    /// <summary>
    /// Devolve a faixa do índice: 0 a 25 <c>BAIXO</c>, 26 a 50 <c>MODERADO</c>,
    /// 51 a 75 <c>ALTO</c> e 76 a 100 <c>MUITO_ALTO</c>.
    /// </summary>
    /// <param name="indice">Índice de risco, de 0 a 100.</param>
    /// <returns>A faixa correspondente.</returns>
    /// <exception cref="ValidacaoDominioException">Quando o índice está fora de 0 a 100.</exception>
    public static FaixaRisco FaixaDe(int indice)
    {
        return Garantir.Intervalo(indice, "indiceRisco", 0, 100) switch
        {
            <= 25 => FaixaRisco.Baixo,
            <= 50 => FaixaRisco.Moderado,
            <= 75 => FaixaRisco.Alto,
            _ => FaixaRisco.MuitoAlto,
        };
    }
}
