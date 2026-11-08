using System;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Dominio;

/// <summary>
/// Testes da concessão e da validade do <see cref="AcessoCofre"/>.
/// </summary>
[TestClass]
public sealed class AcessoCofreTests
{
    private static readonly DateTime _agora = new(2026, 11, 12, 21, 17, 0, DateTimeKind.Utc);
    private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(60);

    /// <summary>
    /// Garante que o acesso SOS_ATIVO sem ocorrência é rejeitado.
    /// </summary>
    [TestMethod]
    public void Conceder_SosAtivoSemOcorrencia_LancaValidacao()
    {
        // Arrange, Act e Assert
        ValidacaoDominioException excecao = Assert.ThrowsExactly<ValidacaoDominioException>(
            () => AcessoCofre.Conceder(Guid.NewGuid(), null, Guid.NewGuid(), OrigemAcessoCofre.SosAtivo, _agora, _ttl, null));

        Assert.Contains("SOS_ATIVO", excecao.Message);
    }

    /// <summary>
    /// Garante que o acesso EMERGENCIA_MANUAL com ocorrência é rejeitado.
    /// </summary>
    [TestMethod]
    public void Conceder_EmergenciaManualComOcorrencia_LancaValidacao()
    {
        // Arrange, Act e Assert
        ValidacaoDominioException excecao = Assert.ThrowsExactly<ValidacaoDominioException>(
            () => AcessoCofre.Conceder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), OrigemAcessoCofre.EmergenciaManual, _agora, _ttl, null));

        Assert.Contains("EMERGENCIA_MANUAL", excecao.Message);
    }

    /// <summary>
    /// Garante que ttl zero ou negativo é rejeitado.
    /// </summary>
    /// <param name="segundos">Ttl em segundos.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Conceder_TtlNaoPositivo_LancaValidacao(int segundos)
    {
        // Arrange
        TimeSpan ttl = TimeSpan.FromSeconds(segundos);

        // Act e Assert
        Assert.ThrowsExactly<ValidacaoDominioException>(
            () => AcessoCofre.Conceder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), OrigemAcessoCofre.SosAtivo, _agora, ttl, null));
    }

    /// <summary>
    /// Garante que a concessão válida preenche os campos e calcula a expiração.
    /// </summary>
    [TestMethod]
    public void Conceder_SosAtivoValido_PreencheCamposECalculaExpiracao()
    {
        // Arrange
        Guid entregadorId = Guid.NewGuid();
        Guid ocorrenciaId = Guid.NewGuid();
        Guid socorristaId = Guid.NewGuid();

        // Act
        var acesso = AcessoCofre.Conceder(entregadorId, ocorrenciaId, socorristaId, OrigemAcessoCofre.SosAtivo, _agora, _ttl, " 203.0.113.7 ");

        // Assert
        Assert.AreEqual(entregadorId, acesso.EntregadorId);
        Assert.AreEqual(ocorrenciaId, acesso.OcorrenciaId);
        Assert.AreEqual(socorristaId, acesso.SocorristaId);
        Assert.AreEqual(OrigemAcessoCofre.SosAtivo, acesso.Origem);
        Assert.AreEqual(_agora, acesso.AcessadoEm);
        Assert.AreEqual(_agora.AddMinutes(60), acesso.ExpiraEm);
        Assert.AreEqual("203.0.113.7", acesso.Ip);
    }

    /// <summary>
    /// Garante que o acesso vale 1 s antes da expiração e já não vale exatamente em ExpiraEm.
    /// </summary>
    [TestMethod]
    public void EstaValido_AntesENaExpiracao_VerdadeiroDepoisFalso()
    {
        // Arrange
        var acesso = AcessoCofre.Conceder(Guid.NewGuid(), null, Guid.NewGuid(), OrigemAcessoCofre.EmergenciaManual, _agora, _ttl, null);

        // Act
        bool umSegundoAntes = acesso.EstaValido(acesso.ExpiraEm.AddSeconds(-1));
        bool naExpiracao = acesso.EstaValido(acesso.ExpiraEm);

        // Assert
        Assert.IsTrue(umSegundoAntes);
        Assert.IsFalse(naExpiracao);
        Assert.IsNull(acesso.Ip);
    }
}
