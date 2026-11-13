using System;
using HelmCare.Api.Mensageria;
using HelmCare.Api.Mensageria.Mensagens;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Construtores;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Mensageria;

/// <summary>
/// Testes do <see cref="LeitorMensagemMqtt"/> com os payloads reais da seção 8 do iot/README.md.
/// </summary>
[TestClass]
public sealed class LeitorMensagemMqttTests
{
    private const string TelemetriaReadme = """
        {
          "dispositivo": "HC-0001",
          "ts": "2026-11-11T01:38:30Z",
          "estado": "ARMADO",
          "temperaturaC": 33.4,
          "umidadePct": 58,
          "indiceCalorC": 39.8,
          "presenca": true,
          "velocidadeKmh": 0
        }
        """;

    private const string SosReadme = """
        {
          "dispositivo": "HC-0001",
          "ts": "2026-11-11T01:39:02Z",
          "tipo": "SOS",
          "picoG": 2.54,
          "orientacaoAnormal": true,
          "pilotandoAntes": false,
          "presencaAntes": true,
          "latitude": -23.5641,
          "longitude": -46.6525
        }
        """;

    /// <summary>
    /// Garante a leitura da telemetria de exemplo do README, com o ts em UTC e sem bateriaPct.
    /// </summary>
    [TestMethod]
    public void TentarLerTelemetria_ExemploDoReadme_LeTodosOsCampos()
    {
        // Arrange
        ReadOnlyMemory<byte> payload = CenarioMqtt.Bytes(TelemetriaReadme);

        // Act
        bool valida = LeitorMensagemMqtt.TentarLerTelemetria("HC-0001", payload, out TelemetriaMensagem? telemetria, out string motivo);

        // Assert
        Assert.IsTrue(valida, motivo);
        Assert.IsNotNull(telemetria);
        Assert.AreEqual("HC-0001", telemetria.Dispositivo);
        Assert.AreEqual(new DateTime(2026, 11, 11, 1, 38, 30, DateTimeKind.Utc), telemetria.Ts);
        Assert.AreEqual(DateTimeKind.Utc, telemetria.Ts.Kind);
        Assert.AreEqual(EstadoDispositivo.Armado, telemetria.Estado);
        Assert.AreEqual(33.4, telemetria.TemperaturaC);
        Assert.AreEqual(58.0, telemetria.UmidadePct);
        Assert.AreEqual(39.8, telemetria.IndiceCalorC);
        Assert.IsTrue(telemetria.Presenca);
        Assert.AreEqual(0.0, telemetria.VelocidadeKmh);
        Assert.IsNull(telemetria.BateriaPct);
    }

    /// <summary>
    /// Garante que a telemetria com os campos do DHT22 nulos (sensor ainda sem leitura) é aceita.
    /// </summary>
    [TestMethod]
    public void TentarLerTelemetria_SensorSemLeitura_AceitaComCamposNulos()
    {
        // Arrange
        ReadOnlyMemory<byte> payload = CenarioMqtt.Bytes(
            """{"dispositivo":"HC-0001","ts":"2026-11-11T01:38:30Z","estado":"ARMADO","temperaturaC":null,"umidadePct":null,"indiceCalorC":null,"presenca":true,"velocidadeKmh":0}""");

        // Act
        bool valida = LeitorMensagemMqtt.TentarLerTelemetria("HC-0001", payload, out TelemetriaMensagem? telemetria, out string motivo);

        // Assert
        Assert.IsTrue(valida, motivo);
        Assert.IsNotNull(telemetria);
        Assert.IsNull(telemetria.IndiceCalorC);
        Assert.IsNull(telemetria.TemperaturaC);
    }

    /// <summary>
    /// Garante a leitura do SOS de exemplo do README (teste 4), com o ts em UTC.
    /// </summary>
    [TestMethod]
    public void TentarLerEvento_SosDoReadme_LeTodosOsCampos()
    {
        // Arrange
        ReadOnlyMemory<byte> payload = CenarioMqtt.Bytes(SosReadme);

        // Act
        bool valido = LeitorMensagemMqtt.TentarLerEvento("HC-0001", payload, out EventoMensagem? evento, out string motivo);

        // Assert
        Assert.IsTrue(valido, motivo);
        Assert.IsNotNull(evento);
        Assert.AreEqual(TipoEventoMqtt.Sos, evento.Tipo);
        Assert.AreEqual(new DateTime(2026, 11, 11, 1, 39, 2, DateTimeKind.Utc), evento.Ts);
        Assert.AreEqual(DateTimeKind.Utc, evento.Ts.Kind);
        Assert.AreEqual(2.54, evento.PicoG);
        Assert.IsTrue(evento.OrientacaoAnormal);
        Assert.IsFalse(evento.PilotandoAntes);
        Assert.IsTrue(evento.PresencaAntes);
        Assert.AreEqual(-23.5641, evento.Latitude);
        Assert.AreEqual(-46.6525, evento.Longitude);
    }

    /// <summary>
    /// Garante que um evento com ts muito no passado (o relógio do Wokwi atrasa) é aceito, sem comparar com a hora do servidor.
    /// </summary>
    [TestMethod]
    public void TentarLerEvento_TsTresHorasAtrasado_Aceita()
    {
        // Arrange
        DateTime atrasado = CenarioMqtt.AgoraServidor.UtcDateTime.AddHours(-3);
        ReadOnlyMemory<byte> payload = CenarioMqtt.Bytes(CenarioMqtt.EventoJson("SOS", atrasado));

        // Act
        bool valido = LeitorMensagemMqtt.TentarLerEvento("HC-0001", payload, out EventoMensagem? evento, out string motivo);

        // Assert
        Assert.IsTrue(valido, motivo);
        Assert.IsNotNull(evento);
        Assert.AreEqual(atrasado, evento.Ts);
    }

    /// <summary>
    /// Garante a recusa de cada tipo de evento inválido, com o motivo citando só o campo.
    /// </summary>
    /// <param name="json">Payload inválido.</param>
    /// <param name="trechoDoMotivo">Parte esperada do motivo.</param>
    [TestMethod]
    [DataRow("{ isto não é json", "JSON válido")]
    [DataRow("[1, 2, 3]", "objeto JSON")]
    [DataRow("""{"dispositivo":"HC-0001","ts":"2026-11-11T01:39:02Z","tipo":"QUEDA","picoG":2.5,"orientacaoAnormal":true,"pilotandoAntes":true,"presencaAntes":true,"latitude":-23.5,"longitude":-46.6}""", "campo tipo")]
    [DataRow("""{"dispositivo":"HC-0002","ts":"2026-11-11T01:39:02Z","tipo":"SOS","picoG":2.5,"orientacaoAnormal":true,"pilotandoAntes":true,"presencaAntes":true,"latitude":-23.5,"longitude":-46.6}""", "diferente do código do tópico")]
    [DataRow("""{"dispositivo":"HC-0001","ts":"2026-11-11T01:39:02Z","tipo":"SOS","picoG":2.5,"orientacaoAnormal":true,"pilotandoAntes":true,"presencaAntes":true,"longitude":-46.6}""", "campo latitude ausente")]
    [DataRow("""{"dispositivo":"HC-0001","ts":"ontem","tipo":"SOS","picoG":2.5,"orientacaoAnormal":true,"pilotandoAntes":true,"presencaAntes":true,"latitude":-23.5,"longitude":-46.6}""", "campo ts")]
    [DataRow("""{"dispositivo":"HC-0001","ts":"2026-11-11T01:39:02Z","tipo":"SOS","picoG":"alto","orientacaoAnormal":true,"pilotandoAntes":true,"presencaAntes":true,"latitude":-23.5,"longitude":-46.6}""", "campo picoG")]
    public void TentarLerEvento_PayloadInvalido_RecusaComMotivo(string json, string trechoDoMotivo)
    {
        // Arrange
        ReadOnlyMemory<byte> payload = CenarioMqtt.Bytes(json);

        // Act
        bool valido = LeitorMensagemMqtt.TentarLerEvento("HC-0001", payload, out EventoMensagem? evento, out string motivo);

        // Assert
        Assert.IsFalse(valido);
        Assert.IsNull(evento);
        Assert.Contains(trechoDoMotivo, motivo);
        Assert.DoesNotContain("-23.5", motivo);
    }

    /// <summary>
    /// Garante a recusa de uma telemetria com estado fora do contrato.
    /// </summary>
    [TestMethod]
    public void TentarLerTelemetria_EstadoDesconhecido_Recusa()
    {
        // Arrange
        ReadOnlyMemory<byte> payload = CenarioMqtt.Bytes(TelemetriaReadme.Replace("\"ARMADO\"", "\"LIGADO\"", StringComparison.Ordinal));

        // Act
        bool valida = LeitorMensagemMqtt.TentarLerTelemetria("HC-0001", payload, out _, out string motivo);

        // Assert
        Assert.IsFalse(valida);
        Assert.Contains("campo estado", motivo);
    }

    /// <summary>
    /// Garante a recusa de uma telemetria de outro capacete publicada no tópico do HC-0001.
    /// </summary>
    [TestMethod]
    public void TentarLerTelemetria_DispositivoDiferenteDoTopico_Recusa()
    {
        // Arrange
        ReadOnlyMemory<byte> payload = CenarioMqtt.Bytes(TelemetriaReadme);

        // Act
        bool valida = LeitorMensagemMqtt.TentarLerTelemetria("HC-0009", payload, out _, out string motivo);

        // Assert
        Assert.IsFalse(valida);
        Assert.Contains("diferente do código do tópico", motivo);
    }
}
