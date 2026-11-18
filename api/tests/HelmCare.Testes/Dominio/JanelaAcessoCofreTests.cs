using System;
using System.Collections.Generic;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Dominio;

/// <summary>
/// Testes da <see cref="JanelaAcessoCofre"/> (seção 7 do contrato, 1.16), por instante: TTL de 60 min,
/// fechamento pelo fim da ocorrência, bloqueio de mais 60 min e a exceção da ocorrência nova.
/// </summary>
[TestClass]
public sealed class JanelaAcessoCofreTests
{
    private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(60);
    private static readonly DateTime _inicio = new(2026, 11, 18, 22, 0, 0, DateTimeKind.Utc);
    private static readonly Guid _entregador = Guid.NewGuid();
    private static readonly Guid _socorrista = Guid.NewGuid();
    private static readonly Guid _ocorrencia = Guid.NewGuid();

    private readonly List<AcessoCofre> _acessos = [];

    /// <summary>
    /// Garante a primeira concessão com SOS ativo: SOS_ATIVO, a ocorrência e expiraEm = agora + TTL.
    /// </summary>
    [TestMethod]
    public void Decidir_PrimeiroAcessoComSosAtivo_AbreJanelaSosAtivo()
    {
        // Act
        DecisaoAcessoCofre decisao = Tentar(_inicio, _ocorrencia);

        // Assert
        Assert.IsTrue(decisao.Concedido);
        Assert.AreEqual(OrigemAcessoCofre.SosAtivo, decisao.Origem);
        Assert.AreEqual(_ocorrencia, decisao.OcorrenciaId);
        Assert.AreEqual(_inicio + _ttl, decisao.ExpiraEm);
    }

    /// <summary>
    /// Garante que, dentro do TTL, o pedido repetido é concedido com o mesmo expiraEm e a mesma origem
    /// (repetir não estica o prazo).
    /// </summary>
    [TestMethod]
    public void Decidir_DentroDoTtl_MesmoExpiraEmEMesmaOrigem()
    {
        // Arrange
        Tentar(_inicio, _ocorrencia);

        // Act
        DecisaoAcessoCofre segunda = Tentar(_inicio.AddMinutes(30), _ocorrencia);
        DecisaoAcessoCofre terceira = Tentar(_inicio.AddMinutes(59), _ocorrencia);

        // Assert
        Assert.IsTrue(segunda.Concedido);
        Assert.IsTrue(terceira.Concedido);
        Assert.AreEqual(_inicio + _ttl, segunda.ExpiraEm);
        Assert.AreEqual(_inicio + _ttl, terceira.ExpiraEm);
        Assert.AreEqual(OrigemAcessoCofre.SosAtivo, terceira.Origem);
    }

    /// <summary>
    /// Garante que a ocorrência encerrada (ou cancelada) fecha a janela SOS_ATIVO antes do TTL: o pedido
    /// seguinte é negado como expirado, com o expiraEm no fechamento e a origem que valeria agora.
    /// </summary>
    [TestMethod]
    public void Decidir_OcorrenciaEncerrada_FechaAJanela()
    {
        // Arrange
        Tentar(_inicio, _ocorrencia);
        DateTime fimDaOcorrencia = _inicio.AddMinutes(10);

        // Act
        DecisaoAcessoCofre decisao = Tentar(_inicio.AddMinutes(15), null, fimDaOcorrencia);

        // Assert
        Assert.IsFalse(decisao.Concedido);
        Assert.AreEqual(MotivoNegacaoCofre.AcessoExpirado, decisao.Motivo);
        Assert.AreEqual(fimDaOcorrencia, decisao.ExpiraEm);
        Assert.AreEqual(OrigemAcessoCofre.EmergenciaManual, decisao.Origem);
        Assert.IsNull(decisao.OcorrenciaId);
    }

    /// <summary>
    /// Garante que o TTL vencido fecha a janela, mesmo com a ocorrência ainda ativa (60 min exatos já fecham).
    /// </summary>
    [TestMethod]
    public void Decidir_TtlVencido_FechaAJanela()
    {
        // Arrange
        Tentar(_inicio, _ocorrencia);

        // Act
        DecisaoAcessoCofre decisao = Tentar(_inicio + _ttl, _ocorrencia);

        // Assert
        Assert.IsFalse(decisao.Concedido);
        Assert.AreEqual(MotivoNegacaoCofre.AcessoExpirado, decisao.Motivo);
        Assert.AreEqual(_inicio + _ttl, decisao.ExpiraEm);
        Assert.AreEqual(OrigemAcessoCofre.SosAtivo, decisao.Origem);
        Assert.AreEqual(_ocorrencia, decisao.OcorrenciaId);
    }

    /// <summary>
    /// Garante que o bloqueio depois do fechamento impede a emergência manual: sem ocorrência ativa,
    /// até fechamento + TTL, o pedido continua negado.
    /// </summary>
    [TestMethod]
    public void Decidir_DentroDoBloqueio_ImpedeAEmergenciaManual()
    {
        // Arrange: a janela fecha com o fim do SOS aos 10 min; o bloqueio vai até 70 min.
        Tentar(_inicio, _ocorrencia);
        DateTime fim = _inicio.AddMinutes(10);

        // Act
        DecisaoAcessoCofre aos69 = Tentar(_inicio.AddMinutes(69), null, fim);

        // Assert
        Assert.IsFalse(aos69.Concedido);
        Assert.AreEqual(MotivoNegacaoCofre.AcessoExpirado, aos69.Motivo);
    }

    /// <summary>
    /// Garante que, depois do bloqueio, uma emergência manual nova abre, com prazo novo e sem ocorrência.
    /// </summary>
    [TestMethod]
    public void Decidir_DepoisDoBloqueio_AbreEmergenciaManualNova()
    {
        // Arrange
        Tentar(_inicio, _ocorrencia);
        DateTime fim = _inicio.AddMinutes(10);
        DateTime depois = _inicio.AddMinutes(70);

        // Act
        DecisaoAcessoCofre decisao = Tentar(depois, null, fim);

        // Assert
        Assert.IsTrue(decisao.Concedido);
        Assert.AreEqual(OrigemAcessoCofre.EmergenciaManual, decisao.Origem);
        Assert.IsNull(decisao.OcorrenciaId);
        Assert.AreEqual(depois + _ttl, decisao.ExpiraEm);
    }

    /// <summary>
    /// Garante que a emergência manual só fecha pelo TTL e que, depois dela, uma ocorrência nova (outro
    /// SOS) abre uma janela nova na hora, mesmo dentro do bloqueio.
    /// </summary>
    [TestMethod]
    public void Decidir_OcorrenciaNovaDentroDoBloqueio_AbreJanelaNaHora()
    {
        // Arrange: emergência manual às 22:00; fecha às 23:00 pelo TTL.
        DecisaoAcessoCofre manual = Tentar(_inicio, null);
        Guid novaOcorrencia = Guid.NewGuid();

        // Act
        DecisaoAcessoCofre aos30 = Tentar(_inicio.AddMinutes(30), null, fimDaOcorrencia: _inicio.AddMinutes(1));
        DecisaoAcessoCofre bloqueado = Tentar(_inicio.AddMinutes(61), null);
        DecisaoAcessoCofre comSosNovo = Tentar(_inicio.AddMinutes(62), novaOcorrencia);

        // Assert
        Assert.AreEqual(OrigemAcessoCofre.EmergenciaManual, manual.Origem);
        Assert.IsTrue(aos30.Concedido, "Na emergência manual, só o TTL fecha a janela.");
        Assert.AreEqual(MotivoNegacaoCofre.AcessoExpirado, bloqueado.Motivo);
        Assert.IsTrue(comSosNovo.Concedido);
        Assert.AreEqual(OrigemAcessoCofre.SosAtivo, comSosNovo.Origem);
        Assert.AreEqual(novaOcorrencia, comSosNovo.OcorrenciaId);
        Assert.AreEqual(_inicio.AddMinutes(62) + _ttl, comSosNovo.ExpiraEm);
    }

    /// <summary>
    /// Garante que a janela de um socorrista não afeta a de outro: com a janela do primeiro fechada e
    /// bloqueada, o segundo abre a dele.
    /// </summary>
    [TestMethod]
    public void Decidir_OutroSocorrista_TemAPropriaJanela()
    {
        // Arrange: o primeiro socorrista acessou e a janela dele fechou pelo TTL.
        Tentar(_inicio, _ocorrencia);
        var outro = Guid.NewGuid();
        DateTime agora = _inicio.AddMinutes(61);

        // Act
        JanelaAcessoCofre? janelaDoOutro = JanelaAcessoCofre.DoPar(_acessos.FindAll(a => a.SocorristaId == outro), null, _ttl);
        DecisaoAcessoCofre doOutro = JanelaAcessoCofre.Decidir(janelaDoOutro, true, _ocorrencia, agora, _ttl);
        DecisaoAcessoCofre doPrimeiro = Tentar(agora, _ocorrencia);

        // Assert
        Assert.IsNull(janelaDoOutro);
        Assert.IsTrue(doOutro.Concedido);
        Assert.AreEqual(agora + _ttl, doOutro.ExpiraEm);
        Assert.IsFalse(doPrimeiro.Concedido);
    }

    /// <summary>
    /// Garante que o socorrista não verificado é negado com o expiraEm igual ao instante, a origem e a
    /// ocorrência que valeriam agora, e que a negação não abre janela.
    /// </summary>
    [TestMethod]
    public void Decidir_NaoVerificado_NegaSemAbrirJanela()
    {
        // Act
        DecisaoAcessoCofre decisao = Tentar(_inicio, _ocorrencia, verificado: false);
        JanelaAcessoCofre? janela = JanelaAcessoCofre.DoPar(_acessos, null, _ttl);

        // Assert
        Assert.IsFalse(decisao.Concedido);
        Assert.AreEqual(MotivoNegacaoCofre.SocorristaNaoVerificado, decisao.Motivo);
        Assert.AreEqual(_inicio, decisao.ExpiraEm);
        Assert.AreEqual(OrigemAcessoCofre.SosAtivo, decisao.Origem);
        Assert.AreEqual(_ocorrencia, decisao.OcorrenciaId);
        Assert.IsNull(janela);
    }

    /// <summary>
    /// Garante que o registro recusa um expiraEm que não combina com o resultado (concedido já vencido,
    /// ou negado no futuro).
    /// </summary>
    [TestMethod]
    public void Registrar_ExpiraEmIncoerente_Lanca()
    {
        // Arrange
        var concedidoVencido = DecisaoAcessoCofre.Conceder(OrigemAcessoCofre.EmergenciaManual, null, _inicio);
        var negadoNoFuturo = DecisaoAcessoCofre.Negar(MotivoNegacaoCofre.AcessoExpirado, OrigemAcessoCofre.EmergenciaManual, null, _inicio.AddMinutes(1));

        // Act e Assert
        Assert.ThrowsExactly<ValidacaoDominioException>(() => AcessoCofre.Registrar(_entregador, _socorrista, concedidoVencido, _inicio, null));
        Assert.ThrowsExactly<ValidacaoDominioException>(() => AcessoCofre.Registrar(_entregador, _socorrista, negadoNoFuturo, _inicio, null));
    }

    // Simula uma tentativa do socorrista de referência: monta a janela com os acessos já gravados,
    // decide e grava o resultado, como o serviço faz.
    private DecisaoAcessoCofre Tentar(DateTime agora, Guid? ocorrenciaAtiva, DateTime? fimDaOcorrencia = null, bool verificado = true)
    {
        JanelaAcessoCofre? janela = JanelaAcessoCofre.DoPar(_acessos.FindAll(a => a.SocorristaId == _socorrista), fimDaOcorrencia, _ttl);
        DecisaoAcessoCofre decisao = JanelaAcessoCofre.Decidir(janela, verificado, ocorrenciaAtiva, agora, _ttl);
        _acessos.Add(AcessoCofre.Registrar(_entregador, _socorrista, decisao, agora, "203.0.113.7"));
        return decisao;
    }
}
