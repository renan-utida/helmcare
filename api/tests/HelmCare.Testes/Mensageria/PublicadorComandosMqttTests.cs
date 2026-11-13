using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Mensageria;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Mensageria;

/// <summary>
/// Testes do <see cref="PublicadorComandosMqtt"/> com o cliente MQTT falso.
/// </summary>
[TestClass]
public sealed class PublicadorComandosMqttTests
{
    private readonly FakeClienteMqtt _cliente = new();
    private readonly PublicadorComandosMqtt _publicador;

    /// <summary>
    /// Monta o publicador com o relógio parado em 2026-11-12T20:00:00.789Z.
    /// </summary>
    public PublicadorComandosMqttTests()
    {
        var relogio = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 20, 0, 0, 789, TimeSpan.Zero));
        _publicador = new PublicadorComandosMqtt(
            _cliente,
            relogio,
            Options.Create(new OpcoesMqtt()),
            NullLogger<PublicadorComandosMqtt>.Instance);
    }

    /// <summary>
    /// Garante o tópico de comando, o QoS 1 e o payload do contrato, com o ts do relógio em ISO 8601 UTC.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task PublicarAsync_Armar_PublicaNoTopicoDeComandoEmQos1()
    {
        // Arrange: o publicador vem do construtor.

        // Act
        await _publicador.PublicarAsync("HC-0001", TipoComandoMqtt.Armar, CancellationToken.None);

        // Assert
        PublicacaoFake publicada = _cliente.Publicadas[0];
        Assert.HasCount(1, _cliente.Publicadas);
        Assert.AreEqual("helmcare-rdu/v1/HC-0001/comando", publicada.Topico);
        Assert.AreEqual(QosMqtt.PeloMenosUmaVez, publicada.Qos);
        Assert.AreEqual("""{"tipo":"ARMAR","ts":"2026-11-12T20:00:00Z"}""", publicada.Payload);
    }

    /// <summary>
    /// Garante que um comando com nome composto sai com o nome exato do contrato.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task PublicarAsync_SocorroACaminho_UsaONomeDoContrato()
    {
        // Arrange: o publicador vem do construtor.

        // Act
        await _publicador.PublicarAsync("HC-0001", TipoComandoMqtt.SocorroACaminho, CancellationToken.None);

        // Assert
        Assert.Contains("\"tipo\":\"SOCORRO_A_CAMINHO\"", _cliente.Publicadas[0].Payload);
    }

    /// <summary>
    /// Garante que, sem conexão com o broker, o publicador lança MensageriaIndisponivelException e nada é publicado.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task PublicarAsync_Desconectado_LancaMensageriaIndisponivel()
    {
        // Arrange
        _cliente.EstaConectado = false;

        // Act e Assert
        await Assert.ThrowsExactlyAsync<MensageriaIndisponivelException>(
            () => _publicador.PublicarAsync("HC-0001", TipoComandoMqtt.Armar, CancellationToken.None));
        Assert.IsEmpty(_cliente.Publicadas);
    }
}
