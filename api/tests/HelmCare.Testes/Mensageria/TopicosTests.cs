using HelmCare.Api.Mensageria;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Mensageria;

/// <summary>
/// Testes da montagem e da interpretação dos tópicos da seção 6.
/// </summary>
[TestClass]
public sealed class TopicosTests
{
    private const string Raiz = "helmcare-rdu/v1";

    /// <summary>
    /// Garante os dois filtros que a API assina, com o curinga de um nível no código do capacete.
    /// </summary>
    [TestMethod]
    public void Filtros_Raiz_MontamTelemetriaEEvento()
    {
        // Arrange e Act
        string telemetria = Topicos.FiltroTelemetria(Raiz);
        string evento = Topicos.FiltroEvento(Raiz);

        // Assert
        Assert.AreEqual("helmcare-rdu/v1/+/telemetria", telemetria);
        Assert.AreEqual("helmcare-rdu/v1/+/evento", evento);
    }

    /// <summary>
    /// Garante o tópico de comando de um capacete.
    /// </summary>
    [TestMethod]
    public void ComandoDe_Codigo_MontaOTopicoDeComando()
    {
        // Arrange e Act
        string topico = Topicos.ComandoDe(Raiz, "HC-0001");

        // Assert
        Assert.AreEqual("helmcare-rdu/v1/HC-0001/comando", topico);
    }

    /// <summary>
    /// Garante a extração do código e do canal dos dois tópicos assinados.
    /// </summary>
    /// <param name="topico">Tópico recebido.</param>
    /// <param name="canalEsperado">Canal esperado.</param>
    [TestMethod]
    [DataRow("helmcare-rdu/v1/HC-0001/telemetria", CanalMqtt.Telemetria)]
    [DataRow("helmcare-rdu/v1/HC-0001/evento", CanalMqtt.Evento)]
    public void TentarInterpretar_TopicoValido_ExtraiCodigoECanal(string topico, CanalMqtt canalEsperado)
    {
        // Arrange: a entrada vem do DataRow.

        // Act
        bool valido = Topicos.TentarInterpretar(Raiz, topico, out string? codigo, out CanalMqtt canal);

        // Assert
        Assert.IsTrue(valido);
        Assert.AreEqual("HC-0001", codigo);
        Assert.AreEqual(canalEsperado, canal);
    }

    /// <summary>
    /// Garante que tópicos fora do padrão (outra raiz, canal de comando, nível a mais, código vazio ou curinga) são recusados.
    /// </summary>
    /// <param name="topico">Tópico inválido.</param>
    [TestMethod]
    [DataRow("outra-raiz/v1/HC-0001/evento")]
    [DataRow("helmcare-rdu/v1/HC-0001/comando")]
    [DataRow("helmcare-rdu/v1/HC-0001/evento/extra")]
    [DataRow("helmcare-rdu/v1//evento")]
    [DataRow("helmcare-rdu/v1/+/evento")]
    [DataRow("helmcare-rdu/v1/HC-0001")]
    [DataRow(null)]
    public void TentarInterpretar_TopicoInvalido_Recusa(string? topico)
    {
        // Arrange: a entrada vem do DataRow.

        // Act
        bool valido = Topicos.TentarInterpretar(Raiz, topico, out string? codigo, out _);

        // Assert
        Assert.IsFalse(valido);
        Assert.IsNull(codigo);
    }
}
