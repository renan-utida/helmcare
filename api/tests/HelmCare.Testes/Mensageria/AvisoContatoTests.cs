using System;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Mensageria;
using HelmCare.Api.Servicos.Ocorrencias;
using HelmCare.Dominio.Entidades;
using HelmCare.Testes.Construtores;
using HelmCare.Testes.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace HelmCare.Testes.Mensageria;

/// <summary>
/// Testes do aviso simulado ao contato de emergência: o roteador só avisa depois de gravar uma
/// ocorrência nova, e o log do <see cref="NotificadorContatoSimulado"/> não leva dado pessoal.
/// </summary>
[TestClass]
public sealed class AvisoContatoTests
{
    private static readonly DateTime _ts = CenarioMqtt.TsImpacto;

    private readonly CenarioMqtt _cenario = new();

    /// <summary>
    /// Garante um aviso, depois da gravação, com o id da ocorrência aberta e a quantidade de contatos.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ProcessarAsync_SosAbreOcorrencia_AvisaUmaVezDepoisDeGravar()
    {
        // Arrange
        _cenario.Entregador.AdicionarContato("Contato Simulado", "11912345678", "Mãe");
        _cenario.Entregador.AdicionarContato("Outro Contato", "1131234567", "Irmão");
        RoteadorMensagemMqtt roteador = _cenario.Roteador();

        // Act
        ResultadoRoteamento resultado = await roteador.ProcessarAsync(CenarioMqtt.Evento("SOS", _ts), CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Processada, resultado);
        Ocorrencia ocorrencia = _cenario.Ocorrencias.Ocorrencias[0];
        Assert.AreEqual(new AvisoContatoFake(ocorrencia.Id, 2, GravacoesAntes: 1), _cenario.Notificador.Avisos[0]);
        Assert.HasCount(1, _cenario.Notificador.Avisos);
    }

    /// <summary>
    /// Garante que o reenvio do mesmo SOS (duplicata) não avisa de novo.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ProcessarAsync_SosDuplicado_NaoAvisaDeNovo()
    {
        // Arrange
        RoteadorMensagemMqtt roteador = _cenario.Roteador();
        await roteador.ProcessarAsync(CenarioMqtt.Evento("SOS", _ts), CancellationToken.None);

        // Act
        ResultadoRoteamento resultado = await roteador.ProcessarAsync(CenarioMqtt.Evento("SOS", _ts), CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Duplicada, resultado);
        Assert.HasCount(1, _cenario.Notificador.Avisos);
    }

    /// <summary>
    /// Garante que, se a gravação falha (banco fora ou cópia gravada ao mesmo tempo), ninguém é avisado.
    /// </summary>
    /// <param name="falha">Tipo de falha na gravação.</param>
    /// <param name="esperado">Resultado esperado.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("banco", ResultadoRoteamento.Falhou)]
    [DataRow("unico", ResultadoRoteamento.Duplicada)]
    public async Task ProcessarAsync_GravacaoFalha_NaoAvisa(string falha, ResultadoRoteamento esperado)
    {
        // Arrange
        RoteadorMensagemMqtt roteador = _cenario.Roteador();
        _cenario.Unidade.FalhaAoSalvar = falha == "banco"
            ? () => new TimeoutException("banco fora")
            : () => new DbUpdateException(
                "duplicado",
                new PostgresException("duplicado", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation));

        // Act
        ResultadoRoteamento resultado = await roteador.ProcessarAsync(CenarioMqtt.Evento("SOS", _ts), CancellationToken.None);

        // Assert
        Assert.AreEqual(esperado, resultado);
        Assert.IsEmpty(_cenario.Notificador.Avisos);
    }

    /// <summary>
    /// Garante que um SOS de quem já tem ocorrência ativa (nenhuma ocorrência abre) não avisa.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ProcessarAsync_EntregadorComOcorrenciaAtiva_NaoAvisa()
    {
        // Arrange
        _cenario.Ocorrencias.Semear(CenarioSos.AbrirOcorrencia(_cenario.Entregador.Id));
        RoteadorMensagemMqtt roteador = _cenario.Roteador();

        // Act
        ResultadoRoteamento resultado = await roteador.ProcessarAsync(CenarioMqtt.Evento("SOS", _ts), CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Processada, resultado);
        Assert.HasCount(1, _cenario.Ocorrencias.Ocorrencias);
        Assert.IsEmpty(_cenario.Notificador.Avisos);
    }

    /// <summary>
    /// Garante que os outros eventos (sem ocorrência) não avisam.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ProcessarAsync_ImpactoDetectado_NaoAvisa()
    {
        // Arrange
        RoteadorMensagemMqtt roteador = _cenario.Roteador();

        // Act
        ResultadoRoteamento resultado = await roteador.ProcessarAsync(CenarioMqtt.Evento("IMPACTO_DETECTADO", _ts), CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Processada, resultado);
        Assert.IsEmpty(_cenario.Notificador.Avisos);
    }

    /// <summary>
    /// Garante que uma falha do aviso não desfaz nada: a mensagem continua processada e a ocorrência gravada.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ProcessarAsync_AvisoFalha_OcorrenciaContinuaGravada()
    {
        // Arrange
        _cenario.Notificador.Falhar = true;
        RoteadorMensagemMqtt roteador = _cenario.Roteador();

        // Act
        ResultadoRoteamento resultado = await roteador.ProcessarAsync(CenarioMqtt.Evento("SOS", _ts), CancellationToken.None);

        // Assert
        Assert.AreEqual(ResultadoRoteamento.Processada, resultado);
        Assert.HasCount(1, _cenario.Ocorrencias.Ocorrencias);
        Assert.AreEqual(1, _cenario.Unidade.VezesSalvo);
        Assert.HasCount(1, _cenario.Notificador.Avisos);
    }

    /// <summary>
    /// Garante o texto exato do log simulado: só o id e a quantidade, sem nome, telefone nem coordenadas.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task NotificarAberturaAsync_Simulado_SoIdEQuantidadeNoLog()
    {
        // Arrange
        var logger = new FakeLogger<NotificadorContatoSimulado>();
        var notificador = new NotificadorContatoSimulado(logger);
        var ocorrenciaId = Guid.Parse("01a1d222-1dc9-7fbe-85f0-59599125ba4f");

        // Act
        await notificador.NotificarAberturaAsync(ocorrenciaId, 2, CancellationToken.None);

        // Assert
        RegistroLogFake registro = logger.Registros[0];
        Assert.HasCount(1, logger.Registros);
        Assert.AreEqual(LogLevel.Information, registro.Nivel);
        Assert.AreEqual(
            "Aviso simulado ao contato de emergência da ocorrência 01a1d222-1dc9-7fbe-85f0-59599125ba4f (2 contato(s)).",
            registro.Mensagem);
        Assert.DoesNotContain("-23.", registro.Mensagem);
        Assert.DoesNotContain("-46.", registro.Mensagem);
    }
}
