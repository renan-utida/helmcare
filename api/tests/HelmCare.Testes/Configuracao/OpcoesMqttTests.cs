using System;
using HelmCare.Api.Configuracao;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Configuracao;

/// <summary>
/// Testes das opções de conexão MQTT.
/// </summary>
[TestClass]
public sealed class OpcoesMqttTests
{
    /// <summary>
    /// Garante os valores padrão e que eles passam na validação da subida.
    /// </summary>
    [TestMethod]
    public void Validacao_ValoresPadrao_Aprovados()
    {
        // Arrange
        using ServiceProvider provedor = Provedor(_ => { });

        // Act
        OpcoesMqtt opcoes = provedor.GetRequiredService<IOptions<OpcoesMqtt>>().Value;

        // Assert
        Assert.IsTrue(opcoes.Habilitado);
        Assert.AreEqual("broker.hivemq.com", opcoes.Host);
        Assert.AreEqual(1883, opcoes.Porta);
        Assert.AreEqual("helmcare-rdu-api-dev", opcoes.ClientId);
        Assert.AreEqual("helmcare-rdu/v1", opcoes.RaizTopicos);
        Assert.AreEqual(5, opcoes.IntervaloReconexaoS);
    }

    /// <summary>
    /// Garante que o clientId do capacete é recusado (os dois se derrubariam no broker).
    /// </summary>
    [TestMethod]
    public void Validacao_ClientIdDoCapacete_Reprovado()
    {
        // Arrange
        using ServiceProvider provedor = Provedor(o => o.ClientId = "helmcare-rdu-HC-0001");
        IOptions<OpcoesMqtt> opcoes = provedor.GetRequiredService<IOptions<OpcoesMqtt>>();

        // Act
        OptionsValidationException excecao = Assert.ThrowsExactly<OptionsValidationException>(() => opcoes.Value);

        // Assert
        Assert.Contains("ClientId", excecao.Message);
    }

    /// <summary>
    /// Garante que uma raiz de tópicos com curinga ou barra no fim é recusada.
    /// </summary>
    /// <param name="raiz">Raiz inválida.</param>
    [TestMethod]
    [DataRow("helmcare-rdu/#")]
    [DataRow("helmcare-rdu/+/v1")]
    [DataRow("helmcare-rdu/v1/")]
    public void Validacao_RaizInvalida_Reprovada(string raiz)
    {
        // Arrange
        using ServiceProvider provedor = Provedor(o => o.RaizTopicos = raiz);
        IOptions<OpcoesMqtt> opcoes = provedor.GetRequiredService<IOptions<OpcoesMqtt>>();

        // Act e Assert
        Assert.ThrowsExactly<OptionsValidationException>(() => opcoes.Value);
    }

    private static ServiceProvider Provedor(Action<OpcoesMqtt> configurar)
    {
        var servicos = new ServiceCollection();
        servicos.AddOptions<OpcoesMqtt>()
            .Configure(configurar)
            .ValidateDataAnnotations();
        return servicos.BuildServiceProvider();
    }
}
