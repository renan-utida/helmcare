using System;
using HelmCare.Api.Configuracao;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Configuracao;

/// <summary>
/// Testes das opções da seção 5 do contrato.
/// </summary>
[TestClass]
public sealed class OpcoesHelmCareTests
{
    /// <summary>
    /// Garante que os valores padrão são os da seção 5 do contrato.
    /// </summary>
    [TestMethod]
    public void Construtor_SemConfiguracao_UsaOsValoresDaSecao5()
    {
        // Arrange e Act
        var opcoes = new OpcoesHelmCare();

        // Assert
        Assert.AreEqual(10, opcoes.JanelaObservacaoS);
        Assert.AreEqual(15, opcoes.ContagemCancelamentoS);
        Assert.AreEqual(2.0, opcoes.RaioCamada1Km);
        Assert.AreEqual(60, opcoes.TempoAceiteCamada1S);
        Assert.AreEqual(60, opcoes.TtlAcessoCofreMin);
        Assert.AreEqual(120, opcoes.MetaTempoAceiteS);
        Assert.AreEqual(32.0, opcoes.AlertaCalorC);
        Assert.AreEqual(10, opcoes.IntervaloTelemetriaS);
    }

    /// <summary>
    /// Garante que os valores padrão passam na mesma validação que roda na subida da API.
    /// </summary>
    [TestMethod]
    public void Validacao_ValoresPadrao_Aprovados()
    {
        // Arrange
        using ServiceProvider provedor = Provedor(_ => { });

        // Act
        OpcoesHelmCare opcoes = provedor.GetRequiredService<IOptions<OpcoesHelmCare>>().Value;

        // Assert
        Assert.AreEqual(2.0, opcoes.RaioCamada1Km);
    }

    /// <summary>
    /// Garante que um valor fora da faixa é reprovado, citando o campo.
    /// </summary>
    [TestMethod]
    public void Validacao_RaioForaDaFaixa_Reprovado()
    {
        // Arrange
        using ServiceProvider provedor = Provedor(o => o.RaioCamada1Km = 0);
        IOptions<OpcoesHelmCare> opcoes = provedor.GetRequiredService<IOptions<OpcoesHelmCare>>();

        // Act
        OptionsValidationException excecao = Assert.ThrowsExactly<OptionsValidationException>(() => opcoes.Value);

        // Assert
        Assert.Contains(nameof(OpcoesHelmCare.RaioCamada1Km), excecao.Message);
    }

    // Mesma montagem do Program.cs (ValidateDataAnnotations), sem a fonte de configuração.
    private static ServiceProvider Provedor(Action<OpcoesHelmCare> configurar)
    {
        var servicos = new ServiceCollection();
        servicos.AddOptions<OpcoesHelmCare>()
            .Configure(configurar)
            .ValidateDataAnnotations();
        return servicos.BuildServiceProvider();
    }
}
