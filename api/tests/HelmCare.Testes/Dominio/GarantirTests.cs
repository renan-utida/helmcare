using System;
using HelmCare.Dominio.Comum;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Dominio;

/// <summary>
/// Testes das validações compartilhadas de <see cref="Garantir"/>.
/// </summary>
[TestClass]
public sealed class GarantirTests
{
    /// <summary>
    /// Garante que texto obrigatório nulo, vazio ou só com espaços é rejeitado, citando o campo.
    /// </summary>
    /// <param name="valor">Texto inválido.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void TextoObrigatorio_VazioOuSoEspacos_LancaValidacao(string? valor)
    {
        // Arrange: a entrada vem do DataRow.

        // Act
        ValidacaoDominioException excecao = Assert.ThrowsExactly<ValidacaoDominioException>(
            () => Garantir.TextoObrigatorio(valor, "nome", 120));

        // Assert
        Assert.Contains("nome", excecao.Message);
    }

    /// <summary>
    /// Garante que o texto obrigatório volta sem os espaços das pontas.
    /// </summary>
    [TestMethod]
    public void TextoObrigatorio_ComEspacosNasPontas_DevolveComTrim()
    {
        // Arrange
        string valor = "  Maria Silva  ";

        // Act
        string resultado = Garantir.TextoObrigatorio(valor, "nome", 120);

        // Assert
        Assert.AreEqual("Maria Silva", resultado);
    }

    /// <summary>
    /// Garante que o tamanho máximo vale depois do Trim: no limite passa, acima é rejeitado.
    /// </summary>
    [TestMethod]
    public void TextoObrigatorio_AcimaDoTamanhoMaximo_LancaValidacao()
    {
        // Arrange
        string noLimite = $" {new string('a', 5)} ";
        string acima = new('a', 6);

        // Act
        string aceito = Garantir.TextoObrigatorio(noLimite, "nome", 5);

        // Assert
        Assert.AreEqual(5, aceito.Length);
        Assert.ThrowsExactly<ValidacaoDominioException>(() => Garantir.TextoObrigatorio(acima, "nome", 5));
    }

    /// <summary>
    /// Garante que texto opcional nulo vira string vazia e que o tamanho máximo continua valendo.
    /// </summary>
    [TestMethod]
    public void TextoOpcional_Nulo_DevolveVazioERespeitaTamanho()
    {
        // Arrange
        string? nulo = null;

        // Act
        string resultado = Garantir.TextoOpcional(nulo, "alergias", 10);

        // Assert
        Assert.AreEqual(string.Empty, resultado);
        Assert.AreEqual("pólen", Garantir.TextoOpcional(" pólen ", "alergias", 10));
        Assert.ThrowsExactly<ValidacaoDominioException>(() => Garantir.TextoOpcional(new string('a', 11), "alergias", 10));
    }

    /// <summary>
    /// Garante que a máscara do CPF é removida e só os dígitos são devolvidos.
    /// </summary>
    [TestMethod]
    public void SoDigitos_CpfComMascara_DevolveSoDigitos()
    {
        // Arrange
        string cpf = "123.456.789-12";

        // Act
        string resultado = Garantir.SoDigitos(cpf, "cpf", 11);

        // Assert
        Assert.AreEqual("12345678912", resultado);
    }

    /// <summary>
    /// Garante que uma quantidade de dígitos fora das aceitas é rejeitada, citando o campo.
    /// </summary>
    /// <param name="valor">Texto com a quantidade errada de dígitos.</param>
    [TestMethod]
    [DataRow("123456789")]
    [DataRow("(11) 9 8765-43210")]
    [DataRow(null)]
    public void SoDigitos_TamanhoErrado_LancaValidacao(string? valor)
    {
        // Arrange: a entrada vem do DataRow.

        // Act
        ValidacaoDominioException excecao = Assert.ThrowsExactly<ValidacaoDominioException>(
            () => Garantir.SoDigitos(valor, "telefone", 10, 11));

        // Assert
        Assert.Contains("telefone", excecao.Message);
        Assert.Contains("10 ou 11", excecao.Message);
    }

    /// <summary>
    /// Garante que instantes com Kind Local ou Unspecified são rejeitados.
    /// </summary>
    /// <param name="kind">Kind diferente de UTC.</param>
    [TestMethod]
    [DataRow(DateTimeKind.Local)]
    [DataRow(DateTimeKind.Unspecified)]
    public void Utc_KindDiferenteDeUtc_LancaValidacao(DateTimeKind kind)
    {
        // Arrange
        var instante = new DateTime(2026, 11, 12, 20, 0, 0, kind);

        // Act
        ValidacaoDominioException excecao = Assert.ThrowsExactly<ValidacaoDominioException>(
            () => Garantir.Utc(instante, "inicio"));

        // Assert
        Assert.Contains("inicio", excecao.Message);
    }

    /// <summary>
    /// Garante que um instante UTC é aceito e devolvido sem alteração.
    /// </summary>
    [TestMethod]
    public void Utc_KindUtc_DevolveOMesmoInstante()
    {
        // Arrange
        var instante = new DateTime(2026, 11, 12, 20, 0, 0, DateTimeKind.Utc);

        // Act
        DateTime resultado = Garantir.Utc(instante, "inicio");

        // Assert
        Assert.AreEqual(instante, resultado);
    }
}
