using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Mensageria;
using HelmCare.Api.Servicos.Ocorrencias;
using HelmCare.Dominio.Entidades;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Construtores;
using HelmCare.Testes.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Teste do <see cref="ServicoEscalonamentoSos"/> sem tempo real: o <see cref="FakeTimeProvider"/>
/// dispara o <see cref="PeriodicTimer"/>, e o teste espera o início de cada rodada pelo canal do
/// repositório falso (o tempo limite de 10 s só existe para o teste não travar se algo quebrar).
/// </summary>
[TestClass]
public sealed class ServicoEscalonamentoSosTests
{
    /// <summary>
    /// Garante que o serviço roda uma rodada na subida (escala a ocorrência com 60 s) e, ao avançar o
    /// relógio em um intervalo, roda de novo (escala a que completou 60 s nesse segundo).
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ExecuteAsync_SubidaEUmIntervalo_DuasRodadas()
    {
        // Arrange
        var ocorrencias = new FakeOcorrenciaRepositorio();
        var unidade = new FakeUnidadeDeTrabalho();
        var relogio = new FakeTimeProvider(new DateTimeOffset(CenarioSos.Sos.AddSeconds(60)));
        IOptions<OpcoesHelmCare> opcoes = Options.Create(new OpcoesHelmCare());

        // A com 60 s na subida; B com 59 s na subida e 60 s depois de um intervalo.
        Ocorrencia a = Ocorrencia.Abrir(Guid.NewGuid(), CenarioSos.NovoEventoEmAberto(), CenarioSos.Local, CenarioSos.Sos);
        Ocorrencia b = Ocorrencia.Abrir(Guid.NewGuid(), CenarioSos.NovoEventoEmAberto(), CenarioSos.Local, CenarioSos.Sos.AddSeconds(1));
        ocorrencias.Semear(a);
        ocorrencias.Semear(b);

        var servicos = new ServiceCollection();
        servicos.AddScoped(_ => new EscalonadorOcorrencias(
            ocorrencias, unidade, relogio, opcoes, NullLogger<EscalonadorOcorrencias>.Instance));
        servicos.AddScoped(_ => Encerrador(ocorrencias, unidade, new FakePublicadorComandos(), relogio, opcoes));
        await using ServiceProvider provedor = servicos.BuildServiceProvider();

        using var servico = new ServicoEscalonamentoSos(
            provedor.GetRequiredService<IServiceScopeFactory>(),
            relogio,
            opcoes,
            NullLogger<ServicoEscalonamentoSos>.Instance);
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act e Assert: rodada da subida, sem avançar o relógio.
        await servico.StartAsync(CancellationToken.None);
        Assert.AreEqual(CenarioSos.Sos, await ocorrencias.ConsultasDeVencidas.ReadAsync(limite.Token));

        // Um intervalo depois: a segunda rodada só começa quando a primeira terminou.
        relogio.Advance(TimeSpan.FromSeconds(opcoes.Value.IntervaloEscalonamentoS));
        Assert.AreEqual(CenarioSos.Sos.AddSeconds(1), await ocorrencias.ConsultasDeVencidas.ReadAsync(limite.Token));
        Assert.AreEqual(EstadoOcorrencia.SosCamada2, a.Estado);

        // Parar espera a segunda rodada terminar.
        await servico.StopAsync(CancellationToken.None);
        Assert.AreEqual(EstadoOcorrencia.SosCamada2, b.Estado);
        Assert.AreEqual(2, unidade.VezesSalvo);
    }

    /// <summary>
    /// Garante o encerramento automático no mesmo laço: na rodada da subida, a ocorrência com 120 min
    /// vai para ENCERRADA, com o ENCERRAR publicado, sem nenhuma falha registrada.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ExecuteAsync_OcorrenciaCom120Minutos_EncerraNaRodada()
    {
        // Arrange
        var ocorrencias = new FakeOcorrenciaRepositorio();
        var unidade = new FakeUnidadeDeTrabalho();
        var publicador = new FakePublicadorComandos();
        var relogio = new FakeTimeProvider(new DateTimeOffset(CenarioSos.Sos.AddMinutes(120)));
        IOptions<OpcoesHelmCare> opcoes = Options.Create(new OpcoesHelmCare());
        var logger = new FakeLogger<ServicoEscalonamentoSos>();

        Guid vitima = Guid.NewGuid();
        Ocorrencia antiga = Ocorrencia.Abrir(vitima, CenarioSos.NovoEventoEmAberto(), CenarioSos.Local, CenarioSos.Sos);
        antiga.EscalarParaCamada2(CenarioSos.Sos.AddSeconds(60), CenarioSos.TempoAceiteCamada1);
        ocorrencias.Semear(antiga);
        var dispositivos = new FakeDispositivoRepositorio();
        dispositivos.Semear(new Dispositivo("HC-0001", vitima));

        var servicos = new ServiceCollection();
        servicos.AddScoped(_ => new EscalonadorOcorrencias(
            ocorrencias, unidade, relogio, opcoes, NullLogger<EscalonadorOcorrencias>.Instance));
        servicos.AddScoped(_ => Encerrador(ocorrencias, unidade, publicador, relogio, opcoes, dispositivos));
        await using ServiceProvider provedor = servicos.BuildServiceProvider();

        using var servico = new ServicoEscalonamentoSos(provedor.GetRequiredService<IServiceScopeFactory>(), relogio, opcoes, logger);
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act: a rodada da subida; o StopAsync espera ela terminar.
        await servico.StartAsync(CancellationToken.None);
        await ocorrencias.ConsultasDeVencidas.ReadAsync(limite.Token);
        await servico.StopAsync(CancellationToken.None);

        // Assert
        Assert.AreEqual(EstadoOcorrencia.Encerrada, antiga.Estado);
        Assert.IsTrue(antiga.EncerradaAutomaticamente);
        Assert.AreEqual(TipoComandoMqtt.Encerrar, publicador.Comandos.Single().Tipo);
        Assert.IsEmpty(logger.Registros);
    }

    private static EncerradorAutomaticoOcorrencias Encerrador(
        FakeOcorrenciaRepositorio ocorrencias,
        FakeUnidadeDeTrabalho unidade,
        FakePublicadorComandos publicador,
        FakeTimeProvider relogio,
        IOptions<OpcoesHelmCare> opcoes,
        FakeDispositivoRepositorio? dispositivos = null)
    {
        var comandos = new PublicadorComandosOcorrencia(
            publicador,
            dispositivos ?? new FakeDispositivoRepositorio(),
            NullLogger<PublicadorComandosOcorrencia>.Instance);
        return new EncerradorAutomaticoOcorrencias(
            ocorrencias, unidade, comandos, relogio, opcoes, NullLogger<EncerradorAutomaticoOcorrencias>.Instance);
    }
}
