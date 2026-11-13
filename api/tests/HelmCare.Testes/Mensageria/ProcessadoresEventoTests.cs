using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Construtores;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Mensageria;

/// <summary>
/// Testes de cada processador de evento, pela tabela "Como a API registra cada evento" (seção 6.2).
/// </summary>
[TestClass]
public sealed class ProcessadoresEventoTests
{
    private static readonly DateTime _ts = CenarioMqtt.TsImpacto;

    private readonly CenarioMqtt _cenario = new();

    /// <summary>
    /// Garante que um impacto com pilotagem ou presença nasce em aberto e deixa o capacete em IMPACTO_DETECTADO.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ImpactoDetectado_ComPresenca_RegistraEmAbertoEEstadoImpactoDetectado()
    {
        // Arrange
        var contexto = _cenario.Contexto(TipoEventoMqtt.ImpactoDetectado, _ts, pilotandoAntes: false, presencaAntes: true);

        // Act
        await _cenario.Processador(TipoEventoMqtt.ImpactoDetectado).ProcessarAsync(contexto, CancellationToken.None);

        // Assert
        EventoImpacto impacto = _cenario.Impactos.Impactos.Single();
        Assert.IsTrue(impacto.EstaEmAberto);
        Assert.AreEqual(_ts, impacto.OcorridoEm);
        Assert.AreEqual(2.54, impacto.PicoG);
        Assert.AreEqual(_cenario.Dispositivo.Id, impacto.DispositivoId);
        Assert.AreEqual(EstadoDispositivo.ImpactoDetectado, _cenario.Dispositivo.EstadoAtual);
        Assert.AreEqual(_ts, _cenario.Dispositivo.EstadoAtualizadoEm);
    }

    /// <summary>
    /// Garante que um impacto sem pilotagem e sem presença nasce IGNORADO e o capacete fica ARMADO.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ImpactoDetectado_SemPilotagemNemPresenca_RegistraIgnoradoEEstadoArmado()
    {
        // Arrange
        var contexto = _cenario.Contexto(TipoEventoMqtt.ImpactoDetectado, _ts, pilotandoAntes: false, presencaAntes: false);

        // Act
        await _cenario.Processador(TipoEventoMqtt.ImpactoDetectado).ProcessarAsync(contexto, CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoImpacto.Ignorado, _cenario.Impactos.Impactos.Single().Resultado);
        Assert.AreEqual(EstadoDispositivo.Armado, _cenario.Dispositivo.EstadoAtual);
    }

    /// <summary>
    /// Garante que a contagem guarda contagemTerminaEm = ts + 15 s no impacto em aberto e deixa o capacete em CONTAGEM.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ContagemIniciada_ComImpactoEmAberto_GuardaFimDaContagem()
    {
        // Arrange
        EventoImpacto impacto = _cenario.SemearImpactoEmAberto();
        DateTime tsContagem = _ts.AddSeconds(10);

        // Act
        await _cenario.Processador(TipoEventoMqtt.ContagemIniciada)
            .ProcessarAsync(_cenario.Contexto(TipoEventoMqtt.ContagemIniciada, tsContagem), CancellationToken.None);

        // Assert
        Assert.AreEqual(tsContagem.AddSeconds(15), impacto.ContagemTerminaEm);
        Assert.IsTrue(impacto.EstaEmAberto);
        Assert.AreEqual(EstadoDispositivo.Contagem, _cenario.Dispositivo.EstadoAtual);
    }

    /// <summary>
    /// Garante que o falso positivo encerra o impacto em aberto com FALSO_POSITIVO.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task FalsoPositivo_ComImpactoEmAberto_MarcaOResultado()
    {
        // Arrange
        EventoImpacto impacto = _cenario.SemearImpactoEmAberto();

        // Act
        await _cenario.Processador(TipoEventoMqtt.FalsoPositivo)
            .ProcessarAsync(_cenario.Contexto(TipoEventoMqtt.FalsoPositivo, _ts.AddSeconds(4)), CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoImpacto.FalsoPositivo, impacto.Resultado);
        Assert.AreEqual(EstadoDispositivo.FalsoPositivo, _cenario.Dispositivo.EstadoAtual);
    }

    /// <summary>
    /// Garante que o cancelamento encerra o impacto em aberto com CANCELADO_PELO_ENTREGADOR.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task Cancelado_ComImpactoEmAberto_MarcaOResultado()
    {
        // Arrange
        EventoImpacto impacto = _cenario.SemearImpactoEmAberto();

        // Act
        await _cenario.Processador(TipoEventoMqtt.Cancelado)
            .ProcessarAsync(_cenario.Contexto(TipoEventoMqtt.Cancelado, _ts.AddSeconds(18)), CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoImpacto.CanceladoPeloEntregador, impacto.Resultado);
        Assert.AreEqual(EstadoDispositivo.Cancelado, _cenario.Dispositivo.EstadoAtual);
    }

    /// <summary>
    /// Garante que o evento só se aplica ao impacto em aberto mais recente, sem mexer em um mais antigo.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task Cancelado_DoisImpactosEmAberto_AplicaNoMaisRecente()
    {
        // Arrange
        EventoImpacto antigo = _cenario.SemearImpactoEmAberto(_ts.AddMinutes(-30));
        EventoImpacto recente = _cenario.SemearImpactoEmAberto(_ts);

        // Act
        await _cenario.Processador(TipoEventoMqtt.Cancelado)
            .ProcessarAsync(_cenario.Contexto(TipoEventoMqtt.Cancelado, _ts.AddSeconds(18)), CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoImpacto.CanceladoPeloEntregador, recente.Resultado);
        Assert.IsTrue(antigo.EstaEmAberto);
    }

    /// <summary>
    /// Garante que, sem impacto em aberto, os três eventos de continuação só registram o estado do capacete.
    /// </summary>
    /// <param name="tipo">Tipo do evento.</param>
    /// <param name="estadoEsperado">Estado esperado do capacete.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(TipoEventoMqtt.ContagemIniciada, EstadoDispositivo.Contagem)]
    [DataRow(TipoEventoMqtt.FalsoPositivo, EstadoDispositivo.FalsoPositivo)]
    [DataRow(TipoEventoMqtt.Cancelado, EstadoDispositivo.Cancelado)]
    public async Task Continuacao_SemImpactoEmAberto_SoRegistraOEstado(TipoEventoMqtt tipo, EstadoDispositivo estadoEsperado)
    {
        // Arrange
        EventoImpacto encerrado = _cenario.SemearImpactoEmAberto();
        encerrado.MarcarFalsoPositivo();

        // Act
        await _cenario.Processador(tipo).ProcessarAsync(_cenario.Contexto(tipo, _ts.AddSeconds(20)), CancellationToken.None);

        // Assert
        Assert.HasCount(1, _cenario.Impactos.Impactos);
        Assert.AreEqual(ResultadoImpacto.FalsoPositivo, encerrado.Resultado);
        Assert.IsNull(encerrado.ContagemTerminaEm);
        Assert.AreEqual(estadoEsperado, _cenario.Dispositivo.EstadoAtual);
    }

    /// <summary>
    /// Garante que o SOS com impacto em aberto abre a ocorrência em SOS_CAMADA_1, com o criadaEm do servidor, e manda o impacto para SOS_ENVIADO.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task Sos_ComImpactoEmAberto_AbreOcorrenciaNaCamada1()
    {
        // Arrange
        EventoImpacto impacto = _cenario.SemearImpactoEmAberto();
        DateTime tsSos = _ts.AddSeconds(25);

        // Act
        await _cenario.Processador(TipoEventoMqtt.Sos)
            .ProcessarAsync(_cenario.Contexto(TipoEventoMqtt.Sos, tsSos), CancellationToken.None);

        // Assert
        Ocorrencia ocorrencia = _cenario.Ocorrencias.Ocorrencias.Single();
        Assert.AreEqual(EstadoOcorrencia.SosCamada1, ocorrencia.Estado);
        Assert.AreEqual(Ocorrencia.CamadaProxima, ocorrencia.Camada);
        Assert.AreEqual(_cenario.Dispositivo.EntregadorId, ocorrencia.EntregadorId);
        Assert.AreEqual(impacto.Id, ocorrencia.EventoImpactoId);
        Assert.AreEqual(CenarioMqtt.AgoraServidor.UtcDateTime, ocorrencia.CriadaEm);
        Assert.AreEqual(-23.5641, ocorrencia.Latitude);
        Assert.AreEqual(-46.6525, ocorrencia.Longitude);
        Assert.AreEqual(ResultadoImpacto.SosEnviado, impacto.Resultado);
        Assert.IsFalse(impacto.OcorridoEmEstimado);
        Assert.HasCount(1, _cenario.Impactos.Impactos);
        Assert.AreEqual(EstadoDispositivo.SosEnviado, _cenario.Dispositivo.EstadoAtual);
    }

    /// <summary>
    /// Garante que o SOS sem impacto em aberto cria o impacto estimado (ocorridoEm = ts - 25 s) e abre a ocorrência.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task Sos_SemImpactoEmAberto_CriaImpactoEstimado()
    {
        // Arrange
        DateTime tsSos = _ts.AddSeconds(25);

        // Act
        await _cenario.Processador(TipoEventoMqtt.Sos)
            .ProcessarAsync(_cenario.Contexto(TipoEventoMqtt.Sos, tsSos), CancellationToken.None);

        // Assert
        EventoImpacto impacto = _cenario.Impactos.Impactos.Single();
        Assert.IsTrue(impacto.OcorridoEmEstimado);
        Assert.AreEqual(tsSos.AddSeconds(-25), impacto.OcorridoEm);
        Assert.AreEqual(ResultadoImpacto.SosEnviado, impacto.Resultado);
        Assert.AreEqual(impacto.Id, _cenario.Ocorrencias.Ocorrencias.Single().EventoImpactoId);
    }

    /// <summary>
    /// Garante que o SOS de um entregador com ocorrência ativa não abre outra nem cria impacto.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task Sos_EntregadorComOcorrenciaAtiva_NaoAbreOutra()
    {
        // Arrange
        Ocorrencia ativa = CenarioSos.AbrirOcorrencia(_cenario.Dispositivo.EntregadorId);
        _cenario.Ocorrencias.Semear(ativa);

        // Act
        await _cenario.Processador(TipoEventoMqtt.Sos)
            .ProcessarAsync(_cenario.Contexto(TipoEventoMqtt.Sos, _ts.AddSeconds(25)), CancellationToken.None);

        // Assert
        Assert.AreSame(ativa, _cenario.Ocorrencias.Ocorrencias.Single());
        Assert.IsEmpty(_cenario.Impactos.Impactos);
        Assert.AreEqual(EstadoDispositivo.SosEnviado, _cenario.Dispositivo.EstadoAtual);
    }
}
