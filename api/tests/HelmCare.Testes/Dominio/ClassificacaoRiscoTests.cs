using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Dominio;

/// <summary>
/// Testes das faixas de risco da seção 8 do contrato.
/// </summary>
[TestClass]
public sealed class ClassificacaoRiscoTests
{
    /// <summary>
    /// Garante os limites de cada faixa: 0 a 25, 26 a 50, 51 a 75 e 76 a 100.
    /// </summary>
    /// <param name="indice">Índice no limite de uma faixa.</param>
    /// <param name="esperada">Faixa do contrato para o índice.</param>
    [TestMethod]
    [DataRow(0, FaixaRisco.Baixo)]
    [DataRow(25, FaixaRisco.Baixo)]
    [DataRow(26, FaixaRisco.Moderado)]
    [DataRow(50, FaixaRisco.Moderado)]
    [DataRow(51, FaixaRisco.Alto)]
    [DataRow(75, FaixaRisco.Alto)]
    [DataRow(76, FaixaRisco.MuitoAlto)]
    [DataRow(100, FaixaRisco.MuitoAlto)]
    public void FaixaDe_LimitesDasFaixas_DevolveFaixaDoContrato(int indice, FaixaRisco esperada)
    {
        // Arrange: a entrada vem do DataRow.

        // Act
        FaixaRisco faixa = ClassificacaoRisco.FaixaDe(indice);

        // Assert
        Assert.AreEqual(esperada, faixa);
    }

    /// <summary>
    /// Garante que índices fora de 0 a 100 são rejeitados.
    /// </summary>
    /// <param name="indice">Índice fora do intervalo.</param>
    [TestMethod]
    [DataRow(-1)]
    [DataRow(101)]
    public void FaixaDe_ForaDeZeroACem_LancaValidacao(int indice)
    {
        // Arrange: a entrada vem do DataRow.

        // Act
        ValidacaoDominioException excecao = Assert.ThrowsExactly<ValidacaoDominioException>(
            () => ClassificacaoRisco.FaixaDe(indice));

        // Assert
        Assert.Contains("indiceRisco", excecao.Message);
    }
}
