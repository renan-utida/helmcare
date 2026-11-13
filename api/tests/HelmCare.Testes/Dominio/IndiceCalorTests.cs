using System;
using HelmCare.Dominio.Comum;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Dominio;

/// <summary>
/// Testes do <see cref="IndiceCalor"/> com os valores de referência da seção 6.1, medidos no Wokwi.
/// </summary>
[TestClass]
public sealed class IndiceCalorTests
{
    /// <summary>
    /// Garante os dois valores de referência do contrato, com uma casa decimal (como o capacete publica).
    /// </summary>
    /// <param name="temperaturaC">Temperatura, em °C.</param>
    /// <param name="umidadePct">Umidade, em %.</param>
    /// <param name="esperado">Índice de calor de referência, em °C.</param>
    [TestMethod]
    [DataRow(33.4, 58.0, 39.8)]
    [DataRow(23.5, 58.0, 23.4)]
    public void CalcularC_ValoresDeReferencia_BatemComOContrato(double temperaturaC, double umidadePct, double esperado)
    {
        // Act
        double indice = IndiceCalor.CalcularC(temperaturaC, umidadePct);

        // Assert
        Assert.AreEqual(esperado, Math.Round(indice, 1, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Garante que a previsão simulada (24 °C e 85%) fica abaixo de 27 °C, onde o componente calor é zero.
    /// </summary>
    [TestMethod]
    public void CalcularC_PrevisaoSimulada_FicaAbaixoDoInicioDoCalor()
    {
        // Act
        double indice = IndiceCalor.CalcularC(24.0, 85);

        // Assert
        Assert.AreEqual(24.7, Math.Round(indice, 1, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Garante a recusa de valores que não são números finitos.
    /// </summary>
    [TestMethod]
    public void CalcularC_NaoFinito_LancaArgumentOutOfRange()
    {
        // Act e Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => IndiceCalor.CalcularC(double.NaN, 58));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => IndiceCalor.CalcularC(30, double.PositiveInfinity));
    }
}
