using System;
using HelmCare.Api.Integracoes.ServicoRisco;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Configuracao;

/// <summary>
/// Testes das opções do Serviço de Risco.
/// </summary>
[TestClass]
public sealed class OpcoesServicoRiscoTests
{
    /// <summary>
    /// Garante os valores padrão (127.0.0.1 e 3 s) e que eles passam na validação da subida.
    /// </summary>
    [TestMethod]
    public void Validacao_ValoresPadrao_Aprovados()
    {
        // Arrange
        using ServiceProvider provedor = Provedor(_ => { });

        // Act
        OpcoesServicoRisco opcoes = provedor.GetRequiredService<IOptions<OpcoesServicoRisco>>().Value;

        // Assert
        Assert.AreEqual(new Uri("http://127.0.0.1:8000/risco/v1/"), opcoes.UrlBase);
        Assert.AreEqual(3, opcoes.TempoLimiteS);
    }

    /// <summary>
    /// Garante que URL sem a barra final ou relativa é recusada (perderia o /risco/v1).
    /// </summary>
    /// <param name="url">URL inválida.</param>
    [TestMethod]
    [DataRow("http://127.0.0.1:8000/risco/v1")]
    [DataRow("risco/v1/")]
    public void Validacao_UrlSemBarraOuRelativa_Reprovada(string url)
    {
        // Arrange
        using ServiceProvider provedor = Provedor(o => o.UrlBase = new Uri(url, UriKind.RelativeOrAbsolute));
        IOptions<OpcoesServicoRisco> opcoes = provedor.GetRequiredService<IOptions<OpcoesServicoRisco>>();

        // Act
        OptionsValidationException excecao = Assert.ThrowsExactly<OptionsValidationException>(() => opcoes.Value);

        // Assert
        Assert.Contains("UrlBase", excecao.Message);
    }

    /// <summary>
    /// Garante que o tempo limite fora de 1 a 30 s é recusado.
    /// </summary>
    /// <param name="segundos">Tempo limite inválido.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(31)]
    public void Validacao_TempoLimiteForaDaFaixa_Reprovado(int segundos)
    {
        // Arrange
        using ServiceProvider provedor = Provedor(o => o.TempoLimiteS = segundos);
        IOptions<OpcoesServicoRisco> opcoes = provedor.GetRequiredService<IOptions<OpcoesServicoRisco>>();

        // Act e Assert
        Assert.ThrowsExactly<OptionsValidationException>(() => opcoes.Value);
    }

    private static ServiceProvider Provedor(Action<OpcoesServicoRisco> configurar)
    {
        var servicos = new ServiceCollection();
        servicos.AddOptions<OpcoesServicoRisco>()
            .Configure(configurar)
            .ValidateDataAnnotations();
        return servicos.BuildServiceProvider();
    }
}
