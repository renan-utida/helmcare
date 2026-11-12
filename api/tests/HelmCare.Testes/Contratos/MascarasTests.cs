using System;
using HelmCare.Api.Contratos;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Contratos;

/// <summary>
/// Testes das máscaras de PII das respostas (seção 9 do contrato).
/// </summary>
[TestClass]
public sealed class MascarasTests
{
    /// <summary>
    /// Garante que o CPF mostra só os 2 últimos dígitos.
    /// </summary>
    [TestMethod]
    public void Cpf_OnzeDigitos_MostraSoOsDoisUltimos()
    {
        // Arrange
        string cpf = "11144477735";

        // Act
        string mascarado = Mascaras.Cpf(cpf);

        // Assert
        Assert.AreEqual("***.***.***-35", mascarado);
    }

    /// <summary>
    /// Garante a máscara do celular (11 dígitos): DDD, cinco asteriscos e os 4 últimos.
    /// </summary>
    [TestMethod]
    public void Telefone_OnzeDigitos_UsaCincoAsteriscos()
    {
        // Arrange
        string telefone = "11912345678";

        // Act
        string mascarado = Mascaras.Telefone(telefone);

        // Assert
        Assert.AreEqual("(11) *****-5678", mascarado);
    }

    /// <summary>
    /// Garante a máscara do fixo (10 dígitos): DDD, quatro asteriscos e os 4 últimos.
    /// </summary>
    [TestMethod]
    public void Telefone_DezDigitos_UsaQuatroAsteriscos()
    {
        // Arrange
        string telefone = "1131234567";

        // Act
        string mascarado = Mascaras.Telefone(telefone);

        // Assert
        Assert.AreEqual("(11) ****-4567", mascarado);
    }

    /// <summary>
    /// Garante que a CNH mostra só os 4 últimos dígitos, depois de sete asteriscos.
    /// </summary>
    [TestMethod]
    public void Cnh_OnzeDigitos_MostraSoOsQuatroUltimos()
    {
        // Arrange
        string cnh = "12345678900";

        // Act
        string mascarado = Mascaras.Cnh(cnh);

        // Assert
        Assert.AreEqual("*******8900", mascarado);
    }

    /// <summary>
    /// Garante que um valor fora do formato é recusado sem repetir o valor na mensagem (PII).
    /// </summary>
    /// <param name="valor">Valor fora do formato.</param>
    [TestMethod]
    [DataRow("1114447773")]
    [DataRow("111.444.777-35")]
    public void Cpf_ForaDoFormato_LancaArgumentExceptionSemOValor(string valor)
    {
        // Arrange: a entrada vem do DataRow.

        // Act
        ArgumentException excecao = Assert.ThrowsExactly<ArgumentException>(() => Mascaras.Cpf(valor));

        // Assert
        Assert.DoesNotContain(valor, excecao.Message);
    }
}
