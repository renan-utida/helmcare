using HelmCare.Api.Servicos.Risco;
using HelmCare.Dominio.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes da <see cref="CalculadoraIndiceRisco"/> com a fórmula, as faixas e o arredondamento da seção 8.
/// </summary>
[TestClass]
public sealed class CalculadoraIndiceRiscoTests
{
    // Índice de calor que dá exatamente 30 no componente calor: 27 + 0,3 × 14.
    private const double CalorDe30 = 31.2;

    /// <summary>
    /// Garante o exemplo da seção 8: sinistro 80, chuva 70 e calor 30 dão 65, faixa ALTO.
    /// </summary>
    [TestMethod]
    public void Calcular_ExemploDoContrato_Da65Alto()
    {
        // Act
        CalculoIndiceRisco calculo = CalculadoraIndiceRisco.Calcular(80, 7.0, CalorDe30);

        // Assert
        Assert.AreEqual(80, calculo.Sinistro);
        Assert.AreEqual(70, calculo.Chuva);
        Assert.AreEqual(30, calculo.Calor);
        Assert.AreEqual(65, calculo.IndiceRisco);
        Assert.AreEqual(FaixaRisco.Alto, calculo.FaixaRisco);
    }

    /// <summary>
    /// Garante que 0,5 arredonda para cima, no índice (25,5 vira 26) e no componente (12,5 vira 13).
    /// </summary>
    [TestMethod]
    public void Calcular_MeioExato_ArredondaParaCima()
    {
        // Act
        CalculoIndiceRisco indice = CalculadoraIndiceRisco.Calcular(51, 0, 0);
        CalculoIndiceRisco componente = CalculadoraIndiceRisco.Calcular(12.5, 0, 0);

        // Assert
        Assert.AreEqual(26, indice.IndiceRisco);
        Assert.AreEqual(13, componente.Sinistro);
    }

    /// <summary>
    /// Garante os extremos da chuva: 0 mm/h vale 0; 10 mm/h ou mais vale 100.
    /// </summary>
    /// <param name="chuvaMmHora">Chuva prevista.</param>
    /// <param name="esperado">Componente esperado.</param>
    [TestMethod]
    [DataRow(0.0, 0)]
    [DataRow(-1.0, 0)]
    [DataRow(2.5, 25)]
    [DataRow(10.0, 100)]
    [DataRow(35.0, 100)]
    public void Calcular_Chuva_LinearDe0A10(double chuvaMmHora, int esperado)
    {
        // Act
        CalculoIndiceRisco calculo = CalculadoraIndiceRisco.Calcular(0, chuvaMmHora, 0);

        // Assert
        Assert.AreEqual(esperado, calculo.Chuva);
    }

    /// <summary>
    /// Garante os extremos do calor: 27 °C ou menos vale 0; 41 °C ou mais vale 100.
    /// </summary>
    /// <param name="indiceCalorC">Índice de calor previsto.</param>
    /// <param name="esperado">Componente esperado.</param>
    [TestMethod]
    [DataRow(20.0, 0)]
    [DataRow(27.0, 0)]
    [DataRow(34.0, 50)]
    [DataRow(41.0, 100)]
    [DataRow(48.0, 100)]
    public void Calcular_Calor_LinearDe27A41(double indiceCalorC, int esperado)
    {
        // Act
        CalculoIndiceRisco calculo = CalculadoraIndiceRisco.Calcular(0, 0, indiceCalorC);

        // Assert
        Assert.AreEqual(esperado, calculo.Calor);
    }

    /// <summary>
    /// Garante as faixas nos limites 25/26, 50/51 e 75/76 (só o sinistro, com peso 50%).
    /// </summary>
    /// <param name="sinistro">Componente sinistro (o índice é a metade).</param>
    /// <param name="indice">Índice esperado.</param>
    /// <param name="faixa">Faixa esperada.</param>
    [TestMethod]
    [DataRow(50, 25, FaixaRisco.Baixo)]
    [DataRow(52, 26, FaixaRisco.Moderado)]
    [DataRow(100, 50, FaixaRisco.Moderado)]
    public void Calcular_LimitesDasFaixasBaixas_ClassificaCerto(int sinistro, int indice, FaixaRisco faixa)
    {
        // Act
        CalculoIndiceRisco calculo = CalculadoraIndiceRisco.Calcular(sinistro, 0, 0);

        // Assert
        Assert.AreEqual(indice, calculo.IndiceRisco);
        Assert.AreEqual(faixa, calculo.FaixaRisco);
    }

    /// <summary>
    /// Garante as faixas nos limites 50/51, 75/76 e 100, com sinistro em 100 e a chuva completando.
    /// </summary>
    /// <param name="chuvaMmHora">Chuva prevista (cada 1 mm/h soma 2,5 ao índice).</param>
    /// <param name="indice">Índice esperado.</param>
    /// <param name="faixa">Faixa esperada.</param>
    [TestMethod]
    [DataRow(0.4, 51, FaixaRisco.Alto)]
    [DataRow(10.0, 75, FaixaRisco.Alto)]
    public void Calcular_LimitesDasFaixasAltas_ClassificaCerto(double chuvaMmHora, int indice, FaixaRisco faixa)
    {
        // Act
        CalculoIndiceRisco calculo = CalculadoraIndiceRisco.Calcular(100, chuvaMmHora, 0);

        // Assert
        Assert.AreEqual(indice, calculo.IndiceRisco);
        Assert.AreEqual(faixa, calculo.FaixaRisco);
    }

    /// <summary>
    /// Garante o limite 75/76 e o teto 100, com os três componentes.
    /// </summary>
    [TestMethod]
    public void Calcular_AcimaDe75_MuitoAlto()
    {
        // Act: 50 + 25 + 0,25 × 4 = 76; e tudo no máximo dá 100.
        CalculoIndiceRisco limite = CalculadoraIndiceRisco.Calcular(100, 10, 27.56);
        CalculoIndiceRisco teto = CalculadoraIndiceRisco.Calcular(150, 50, 60);

        // Assert
        Assert.AreEqual(4, limite.Calor);
        Assert.AreEqual(76, limite.IndiceRisco);
        Assert.AreEqual(FaixaRisco.MuitoAlto, limite.FaixaRisco);
        Assert.AreEqual(100, teto.Sinistro);
        Assert.AreEqual(100, teto.IndiceRisco);
    }
}
