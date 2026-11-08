using System;
using System.Linq;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Dominio;

/// <summary>
/// Testes do código, do QR token e do registro de estado do <see cref="Dispositivo"/>.
/// </summary>
[TestClass]
public sealed class DispositivoTests
{
    private static readonly DateTime _ts = new(2026, 11, 12, 21, 16, 2, DateTimeKind.Utc);

    /// <summary>
    /// Garante que códigos fora do formato HC-0000 são rejeitados.
    /// </summary>
    /// <param name="codigo">Código inválido.</param>
    [TestMethod]
    [DataRow("HC-1")]
    [DataRow("hc-0001")]
    [DataRow("XX-0001")]
    public void Construtor_CodigoInvalido_LancaValidacao(string codigo)
    {
        // Arrange: a entrada vem do DataRow.

        // Act
        ValidacaoDominioException excecao = Assert.ThrowsExactly<ValidacaoDominioException>(
            () => new Dispositivo(codigo, Guid.NewGuid()));

        // Assert
        Assert.Contains("codigo", excecao.Message);
    }

    /// <summary>
    /// Garante que o dispositivo nasce desarmado e sem estado recebido.
    /// </summary>
    [TestMethod]
    public void Construtor_CodigoValido_ComecaDesarmado()
    {
        // Arrange e Act
        var dispositivo = new Dispositivo("HC-0001", Guid.NewGuid());

        // Assert
        Assert.AreEqual("HC-0001", dispositivo.Codigo);
        Assert.AreEqual(EstadoDispositivo.Desarmado, dispositivo.EstadoAtual);
        Assert.IsNull(dispositivo.EstadoAtualizadoEm);
    }

    /// <summary>
    /// Garante que cada dispositivo recebe um QR token diferente, com 43 ou mais caracteres Base64Url.
    /// </summary>
    [TestMethod]
    public void Construtor_DoisDispositivos_GeraQrTokensDiferentesEmBase64Url()
    {
        // Arrange
        Guid entregadorId = Guid.NewGuid();

        // Act
        var primeiro = new Dispositivo("HC-0001", entregadorId);
        var segundo = new Dispositivo("HC-0002", entregadorId);

        // Assert
        Assert.AreNotEqual(primeiro.QrToken, segundo.QrToken);

        foreach (string token in new[] { primeiro.QrToken, segundo.QrToken })
        {
            Assert.IsGreaterThanOrEqualTo(43, token.Length);
            Assert.IsTrue(token.All(EhCaractereBase64Url), $"Token com caractere fora do Base64Url: {token}");
        }
    }

    /// <summary>
    /// Garante que uma mensagem mais antiga que o último estado é ignorada sem alterar nada.
    /// </summary>
    [TestMethod]
    public void RegistrarEstado_TsMaisAntigo_IgnoraEDevolveFalse()
    {
        // Arrange
        var dispositivo = new Dispositivo("HC-0001", Guid.NewGuid());
        dispositivo.RegistrarEstado(EstadoDispositivo.Contagem, _ts);

        // Act
        bool aplicado = dispositivo.RegistrarEstado(EstadoDispositivo.Observacao, _ts.AddSeconds(-1));

        // Assert
        Assert.IsFalse(aplicado);
        Assert.AreEqual(EstadoDispositivo.Contagem, dispositivo.EstadoAtual);
        Assert.AreEqual(_ts, dispositivo.EstadoAtualizadoEm);
    }

    /// <summary>
    /// Garante que mensagens com ts igual ou mais novo são aplicadas.
    /// </summary>
    /// <param name="segundosDepois">Diferença em segundos para o último ts.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void RegistrarEstado_TsIgualOuMaisNovo_AplicaEDevolveTrue(int segundosDepois)
    {
        // Arrange
        var dispositivo = new Dispositivo("HC-0001", Guid.NewGuid());
        dispositivo.RegistrarEstado(EstadoDispositivo.Contagem, _ts);
        DateTime novoTs = _ts.AddSeconds(segundosDepois);

        // Act
        bool aplicado = dispositivo.RegistrarEstado(EstadoDispositivo.SosEnviado, novoTs);

        // Assert
        Assert.IsTrue(aplicado);
        Assert.AreEqual(EstadoDispositivo.SosEnviado, dispositivo.EstadoAtual);
        Assert.AreEqual(novoTs, dispositivo.EstadoAtualizadoEm);
    }

    /// <summary>
    /// Garante que ts com Kind diferente de UTC é rejeitado.
    /// </summary>
    /// <param name="kind">Kind diferente de UTC.</param>
    [TestMethod]
    [DataRow(DateTimeKind.Local)]
    [DataRow(DateTimeKind.Unspecified)]
    public void RegistrarEstado_TsSemUtc_LancaValidacao(DateTimeKind kind)
    {
        // Arrange
        var dispositivo = new Dispositivo("HC-0001", Guid.NewGuid());
        var ts = new DateTime(2026, 11, 12, 21, 16, 2, kind);

        // Act
        ValidacaoDominioException excecao = Assert.ThrowsExactly<ValidacaoDominioException>(
            () => dispositivo.RegistrarEstado(EstadoDispositivo.Armado, ts));

        // Assert
        Assert.Contains("ts", excecao.Message);
        Assert.AreEqual(EstadoDispositivo.Desarmado, dispositivo.EstadoAtual);
    }

    /// <summary>
    /// Garante que o índice de calor segue a mesma regra de ordem do estado.
    /// </summary>
    [TestMethod]
    public void RegistrarIndiceCalor_TsMaisAntigo_IgnoraEDevolveFalse()
    {
        // Arrange
        var dispositivo = new Dispositivo("HC-0001", Guid.NewGuid());
        dispositivo.RegistrarIndiceCalor(33.5, _ts);

        // Act
        bool aplicado = dispositivo.RegistrarIndiceCalor(29.0, _ts.AddSeconds(-10));

        // Assert
        Assert.IsFalse(aplicado);
        Assert.AreEqual(33.5, dispositivo.UltimoIndiceCalorC);
        Assert.AreEqual(_ts, dispositivo.UltimoIndiceCalorEm);
    }

    private static bool EhCaractereBase64Url(char caractere)
    {
        return char.IsAsciiLetterOrDigit(caractere) || caractere == '-' || caractere == '_';
    }
}
