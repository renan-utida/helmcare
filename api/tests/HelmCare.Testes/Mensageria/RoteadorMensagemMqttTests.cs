using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Mensageria;
using HelmCare.Api.Mensageria.Processadores;
using HelmCare.Api.Persistencia.Repositorios;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Construtores;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace HelmCare.Testes.Mensageria;

/// <summary>
/// Testes do <see cref="RoteadorMensagemMqtt"/>: idempotência, descarte e gravação única.
/// </summary>
[TestClass]
public sealed class RoteadorMensagemMqttTests
{
    private static readonly DateTime _ts = CenarioMqtt.TsImpacto;

    private readonly CenarioMqtt _cenario = new();

    /// <summary>
    /// Garante que o mesmo SOS recebido duas vezes (reenvio com o mesmo ts) gera uma única ocorrência e um único registro.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ProcessarAsync_SosRepetidoComMesmoTs_UmaOcorrenciaSo()
    {
        // Arrange
        RoteadorMensagemMqtt roteador = _cenario.Roteador();
        MensagemMqttRecebida sos = CenarioMqtt.Evento("SOS", _ts);

        // Act
        ResultadoRoteamento primeiro = await roteador.ProcessarAsync(sos, CancellationToken.None);
        ResultadoRoteamento segundo = await roteador.ProcessarAsync(sos, CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Processada, primeiro);
        Assert.AreEqual(ResultadoRoteamento.Duplicada, segundo);
        Assert.HasCount(1, _cenario.Ocorrencias.Ocorrencias);
        Assert.HasCount(1, _cenario.Processados.Registros);
        Assert.AreEqual(1, _cenario.Unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante o fluxo do teste 4 do capacete: impacto, contagem e SOS viram uma ocorrência em SOS_CAMADA_1,
    /// mesmo com o ts 3 horas antes da hora do servidor (relógio do Wokwi).
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ProcessarAsync_FluxoDoTeste4ComTsAtrasado_AbreOcorrencia()
    {
        // Arrange
        RoteadorMensagemMqtt roteador = _cenario.Roteador();
        Assert.AreEqual(3, (CenarioMqtt.AgoraServidor.UtcDateTime - _ts).TotalHours);

        // Act
        ResultadoRoteamento impacto = await roteador.ProcessarAsync(CenarioMqtt.Evento("IMPACTO_DETECTADO", _ts, false, true), CancellationToken.None);
        ResultadoRoteamento contagem = await roteador.ProcessarAsync(CenarioMqtt.Evento("CONTAGEM_INICIADA", _ts.AddSeconds(10), false, true), CancellationToken.None);
        ResultadoRoteamento sos = await roteador.ProcessarAsync(CenarioMqtt.Evento("SOS", _ts.AddSeconds(25), false, true), CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Processada, impacto);
        Assert.AreEqual(ResultadoRoteamento.Processada, contagem);
        Assert.AreEqual(ResultadoRoteamento.Processada, sos);
        EventoImpacto registrado = _cenario.Impactos.Impactos.Single();
        Assert.AreEqual(ResultadoImpacto.SosEnviado, registrado.Resultado);
        Assert.AreEqual(_ts, registrado.OcorridoEm);
        Assert.AreEqual(EstadoOcorrencia.SosCamada1, _cenario.Ocorrencias.Ocorrencias.Single().Estado);
        Assert.HasCount(3, _cenario.Processados.Registros);
        Assert.AreEqual(3, _cenario.Unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que a mensagem de um capacete não cadastrado é descartada sem gravar nada.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ProcessarAsync_CapaceteDesconhecido_DescartaSemGravar()
    {
        // Arrange
        RoteadorMensagemMqtt roteador = _cenario.Roteador();
        string json = CenarioMqtt.EventoJson("SOS", _ts).Replace("HC-0001", "HC-0999", StringComparison.Ordinal);
        var mensagem = new MensagemMqttRecebida("helmcare-rdu/v1/HC-0999/evento", CenarioMqtt.Bytes(json));

        // Act
        ResultadoRoteamento resultado = await roteador.ProcessarAsync(mensagem, CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Descartada, resultado);
        Assert.IsEmpty(_cenario.Ocorrencias.Ocorrencias);
        Assert.IsEmpty(_cenario.Impactos.Impactos);
        Assert.IsEmpty(_cenario.Processados.Registros);
        Assert.AreEqual(0, _cenario.Unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que uma mensagem inválida (tópico ou payload) é descartada sem lançar exceção.
    /// </summary>
    /// <param name="topico">Tópico recebido.</param>
    /// <param name="payload">Payload recebido.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("helmcare-rdu/v1/HC-0001/evento", "{ quebrado")]
    [DataRow("helmcare-rdu/v1/HC-0001/telemetria", "{}")]
    [DataRow("helmcare-rdu/v1/HC-0001/outro", "{}")]
    public async Task ProcessarAsync_MensagemInvalida_DescartaSemLancar(string topico, string payload)
    {
        // Arrange
        RoteadorMensagemMqtt roteador = _cenario.Roteador();

        // Act
        ResultadoRoteamento resultado = await roteador.ProcessarAsync(
            new MensagemMqttRecebida(topico, CenarioMqtt.Bytes(payload)),
            CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Descartada, resultado);
        Assert.AreEqual(0, _cenario.Unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que a telemetria aplica o estado e o índice de calor no capacete.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ProcessarAsync_Telemetria_RegistraEstadoECalor()
    {
        // Arrange
        RoteadorMensagemMqtt roteador = _cenario.Roteador();

        // Act
        ResultadoRoteamento resultado = await roteador.ProcessarAsync(CenarioMqtt.Telemetria("ARMADO", _ts), CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Processada, resultado);
        Assert.AreEqual(EstadoDispositivo.Armado, _cenario.Dispositivo.EstadoAtual);
        Assert.AreEqual(39.8, _cenario.Dispositivo.UltimoIndiceCalorC);
        Assert.AreEqual(_ts, _cenario.Dispositivo.UltimoIndiceCalorEm);
        Assert.IsEmpty(_cenario.Processados.Registros);
        Assert.AreEqual(1, _cenario.Unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que uma telemetria com ts mais antigo que o último estado e o último calor é ignorada (MQTT não garante a ordem).
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ProcessarAsync_TelemetriaAtrasada_IgnoraSemMudarOEstado()
    {
        // Arrange
        RoteadorMensagemMqtt roteador = _cenario.Roteador();
        await roteador.ProcessarAsync(CenarioMqtt.Telemetria("SOS_ENVIADO", _ts), CancellationToken.None);

        // Act
        ResultadoRoteamento resultado = await roteador.ProcessarAsync(
            CenarioMqtt.Telemetria("ARMADO", _ts.AddSeconds(-10)),
            CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Ignorada, resultado);
        Assert.AreEqual(EstadoDispositivo.SosEnviado, _cenario.Dispositivo.EstadoAtual);
        Assert.AreEqual(_ts, _cenario.Dispositivo.EstadoAtualizadoEm);
        Assert.AreEqual(1, _cenario.Unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que a violação do índice único na gravação (duas cópias ao mesmo tempo) é tratada como duplicata.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ProcessarAsync_IndiceUnicoNaGravacao_TrataComoDuplicata()
    {
        // Arrange
        RoteadorMensagemMqtt roteador = _cenario.Roteador();
        _cenario.Unidade.FalhaAoSalvar = () => new DbUpdateException(
            "duplicado",
            new PostgresException("duplicado", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation));

        // Act
        ResultadoRoteamento resultado = await roteador.ProcessarAsync(CenarioMqtt.Evento("SOS", _ts), CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Duplicada, resultado);
    }

    /// <summary>
    /// Garante que uma falha inesperada do banco vira resultado Falhou, sem lançar para o assinante.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ProcessarAsync_BancoFora_FalhaSemLancar()
    {
        // Arrange
        RoteadorMensagemMqtt roteador = _cenario.Roteador();
        _cenario.Unidade.FalhaAoSalvar = () => new TimeoutException("banco fora");

        // Act
        ResultadoRoteamento resultado = await roteador.ProcessarAsync(CenarioMqtt.Evento("SOS", _ts), CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Falhou, resultado);
    }

    /// <summary>
    /// Garante que um evento recusado pelo domínio (contagem repetida com outro ts) é descartado sem gravar.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ProcessarAsync_EventoRecusadoPeloDominio_DescartaSemGravar()
    {
        // Arrange
        RoteadorMensagemMqtt roteador = _cenario.Roteador();
        _cenario.SemearImpactoEmAberto();
        await roteador.ProcessarAsync(CenarioMqtt.Evento("CONTAGEM_INICIADA", _ts.AddSeconds(10)), CancellationToken.None);

        // Act
        ResultadoRoteamento resultado = await roteador.ProcessarAsync(
            CenarioMqtt.Evento("CONTAGEM_INICIADA", _ts.AddSeconds(11)),
            CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Descartada, resultado);
        Assert.HasCount(1, _cenario.Processados.Registros);
        Assert.AreEqual(1, _cenario.Unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante que a montagem falha, citando o tipo, quando algum dos 5 tipos fica sem processador.
    /// </summary>
    [TestMethod]
    public void MontarProcessadores_FaltandoOSos_FalhaComMensagemClara()
    {
        // Arrange
        IProcessadorEvento[] semSos = _cenario.Processadores().Where(p => p.Tipo != TipoEventoMqtt.Sos).ToArray();

        // Act
        InvalidOperationException excecao = Assert.ThrowsExactly<InvalidOperationException>(
            () => RoteadorMensagemMqtt.MontarProcessadores(semSos));

        // Assert
        Assert.Contains("SOS", excecao.Message);
    }

    /// <summary>
    /// Garante que a montagem falha quando um tipo tem dois processadores.
    /// </summary>
    [TestMethod]
    public void MontarProcessadores_TipoRepetido_FalhaComMensagemClara()
    {
        // Arrange
        IProcessadorEvento[] processadores = [.. _cenario.Processadores(), _cenario.Processador(TipoEventoMqtt.Cancelado)];

        // Act
        InvalidOperationException excecao = Assert.ThrowsExactly<InvalidOperationException>(
            () => RoteadorMensagemMqtt.MontarProcessadores(processadores));

        // Assert
        Assert.Contains("CANCELADO", excecao.Message);
    }

    /// <summary>
    /// Garante que o filtro de ocorrência ativa usado no banco concorda com Ocorrencia.EstaAtiva em todo estado.
    /// </summary>
    /// <param name="estado">Estado da ocorrência.</param>
    [TestMethod]
    [DataRow(EstadoOcorrencia.SosCamada1)]
    [DataRow(EstadoOcorrencia.SosCamada2)]
    [DataRow(EstadoOcorrencia.SocorroACaminho)]
    [DataRow(EstadoOcorrencia.Encerrada)]
    [DataRow(EstadoOcorrencia.Cancelada)]
    public void FiltroAtiva_CadaEstado_ConcordaComEstaAtiva(EstadoOcorrencia estado)
    {
        // Arrange
        Ocorrencia ocorrencia = CenarioSos.OcorrenciaEm(estado);
        Func<Ocorrencia, bool> filtro = OcorrenciaRepositorio.Ativa.Compile();

        // Act
        bool ativaNoBanco = filtro(ocorrencia);

        // Assert
        Assert.AreEqual(ocorrencia.EstaAtiva, ativaNoBanco);
    }
}
