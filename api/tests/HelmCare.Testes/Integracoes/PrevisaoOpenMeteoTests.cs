using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Configuracao;
using HelmCare.Api.Contratos.Turnos;
using HelmCare.Api.Integracoes.OpenMeteo;
using HelmCare.Api.Servicos.Risco;
using HelmCare.Api.Servicos.Turnos;
using HelmCare.Dominio.Entidades;
using HelmCare.Testes.Construtores;
using HelmCare.Testes.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Integracoes;

/// <summary>
/// Testes da previsão do turno pela Open-Meteo (seção 7 do contrato, 1.17), sem rede: a agregação das
/// horas, os arredondamentos e as faixas, as quatro causas da reserva, o cancelamento e a previsão
/// simulada forçada.
/// </summary>
[TestClass]
public sealed class PrevisaoOpenMeteoTests
{
    private static readonly DateTime _dia = new(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Garante a agregação: a maior chuva do turno, e a temperatura e a umidade da hora de maior índice
    /// de calor (não a da maior temperatura sozinha).
    /// </summary>
    [TestMethod]
    public void Agregar_Horas_MaiorChuvaEHoraDeMaiorIndiceDeCalor()
    {
        // Arrange: 31 °C seco tem índice menor que 30 °C muito úmido.
        CondicaoTempo[] horas =
        [
            new(_dia.AddHours(18), 25.0, 60, 0.4),
            new(_dia.AddHours(19), 31.0, 20, 2.6),
            new(_dia.AddHours(20), 30.0, 80, 1.1),
        ];

        // Act
        PrevisaoTurno previsao = RegrasPrevisao.Agregar(horas);

        // Assert
        Assert.AreEqual(2.6, previsao.ChuvaMmHora);
        Assert.AreEqual(30.0, previsao.TemperaturaC);
        Assert.AreEqual(80, previsao.UmidadePct);
    }

    /// <summary>
    /// Garante o empate do índice de calor pela primeira hora.
    /// </summary>
    [TestMethod]
    public void Agregar_EmpateNoIndice_FicaAPrimeiraHora()
    {
        // Arrange: mesmo par de temperatura e umidade em duas horas; a chuva diferencia.
        CondicaoTempo[] horas =
        [
            new(_dia.AddHours(18), 28.04, 70.4, 0),
            new(_dia.AddHours(19), 28.04, 70.4, 3),
        ];

        // Act
        PrevisaoTurno previsao = RegrasPrevisao.Agregar(horas);

        // Assert
        Assert.AreEqual(28.0, previsao.TemperaturaC);
        Assert.AreEqual(70, previsao.UmidadePct);
        Assert.AreEqual(3.0, previsao.ChuvaMmHora);
    }

    /// <summary>
    /// Garante os arredondamentos (chuva e temperatura com 1 casa, umidade inteira, 0,5 para cima) e os
    /// limites das faixas da seção 8.
    /// </summary>
    /// <param name="t">Temperatura da Open-Meteo.</param>
    /// <param name="u">Umidade.</param>
    /// <param name="p">Chuva.</param>
    /// <param name="tEsperada">Temperatura esperada.</param>
    /// <param name="uEsperada">Umidade esperada.</param>
    /// <param name="pEsperada">Chuva esperada.</param>
    [TestMethod]
    [DataRow(23.45, 84.5, 7.05, 23.5, 85, 7.1)]
    [DataRow(23.44, 84.4, 7.04, 23.4, 84, 7.0)]
    [DataRow(55.0, 104.0, 250.0, 50.0, 100, 200.0)]
    [DataRow(-15.0, -3.0, -1.0, -10.0, 0, 0.0)]
    public void Agregar_Arredondamentos_EFaixas(double t, double u, double p, double tEsperada, int uEsperada, double pEsperada)
    {
        // Act
        PrevisaoTurno previsao = RegrasPrevisao.Agregar([new CondicaoTempo(_dia, t, u, p)]);

        // Assert
        Assert.AreEqual(tEsperada, previsao.TemperaturaC);
        Assert.AreEqual(uEsperada, previsao.UmidadePct);
        Assert.AreEqual(pEsperada, previsao.ChuvaMmHora);
    }

    /// <summary>
    /// Garante que só as horas cheias que se sobrepõem ao turno contam, num turno que começa e termina
    /// no meio da hora (18:30 a 20:15 UTC usa 18h, 19h e 20h; a chuva forte das 21h fica de fora), e
    /// que o pedido vai com os dias UTC das horas e sem a coordenada no log.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_TurnoNoMeioDaHora_UsaSoAsHorasQueSeSobrepoem()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        handler.Responder(HttpStatusCode.OK, RespostasOpenMeteo.DiasInteiros(_dia, 1, h => h.Hour switch
        {
            17 => (40.0, 90, 50.0),
            18 => (24.0, 60, 0.5),
            19 => (26.0, 70, 1.5),
            20 => (25.0, 65, 0.2),
            21 => (40.0, 90, 80.0),
            _ => (20.0, 50, 0.0),
        }));
        using HttpClient http = RespostasOpenMeteo.Http(handler);
        var previsao = new PrevisaoTempoOpenMeteo(RespostasOpenMeteo.Cliente(http));

        // Act
        PrevisaoTurno resultado = await previsao.ObterAsync(_dia.AddHours(18.5), _dia.AddHours(20.25), CancellationToken.None);

        // Assert
        Assert.AreEqual(1.5, resultado.ChuvaMmHora);
        Assert.AreEqual(26.0, resultado.TemperaturaC);
        Assert.AreEqual(70, resultado.UmidadePct);
        string consulta = handler.Requisicoes.Single().Endereco!.Query;
        Assert.Contains("hourly=temperature_2m,relative_humidity_2m,precipitation", consulta);
        Assert.Contains("start_date=2026-11-18&end_date=2026-11-18", consulta);
        Assert.DoesNotContain("timezone", consulta);
    }

    /// <summary>
    /// Garante que um turno que passa da meia-noite UTC pede os dois dias.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_TurnoQuePassaDaMeiaNoite_PedeOsDoisDias()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        handler.Responder(HttpStatusCode.OK, RespostasOpenMeteo.DiasInteiros(_dia, 2, _ => (22.0, 60, 0.0)));
        using HttpClient http = RespostasOpenMeteo.Http(handler);
        var previsao = new PrevisaoTempoOpenMeteo(RespostasOpenMeteo.Cliente(http));

        // Act
        await previsao.ObterAsync(_dia.AddHours(22), _dia.AddHours(26), CancellationToken.None);

        // Assert
        Assert.Contains("start_date=2026-11-18&end_date=2026-11-19", handler.Requisicoes.Single().Endereco!.Query);
    }

    /// <summary>
    /// Garante as quatro causas da reserva: em cada uma, a previsão simulada (7 mm/h, 24 °C e 85%) e um
    /// aviso no log com o código do motivo e o tempo, sem a coordenada nem o corpo.
    /// </summary>
    /// <param name="caso">Causa simulada.</param>
    /// <param name="motivo">Código esperado no log.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("rede", nameof(MotivoFalhaOpenMeteo.Falha))]
    [DataRow("status500", nameof(MotivoFalhaOpenMeteo.Falha))]
    [DataRow("tempo", nameof(MotivoFalhaOpenMeteo.TempoEsgotado))]
    [DataRow("jsonQuebrado", nameof(MotivoFalhaOpenMeteo.RespostaInvalida))]
    [DataRow("campoAusente", nameof(MotivoFalhaOpenMeteo.RespostaInvalida))]
    [DataRow("valorNulo", nameof(MotivoFalhaOpenMeteo.RespostaInvalida))]
    [DataRow("tamanhosDiferentes", nameof(MotivoFalhaOpenMeteo.RespostaInvalida))]
    [DataRow("horasNaoCobertas", nameof(MotivoFalhaOpenMeteo.HorasNaoCobertas))]
    public async Task ObterAsync_CausaDeReserva_UsaASimuladaEAvisa(string caso, string motivo)
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        TimeSpan tempoLimite = TimeSpan.FromSeconds(3);
        string tempo = "\"time\":[\"2026-11-18T18:00\"]";
        switch (caso)
        {
            case "rede":
                handler.LancarErroDeRede();
                break;
            case "status500":
                handler.Responder(HttpStatusCode.InternalServerError, "{\"error\":true}");
                break;
            case "tempo":
                // O tempo limite real do HttpClient esgota (curto aqui, para o teste não esperar 3 s).
                handler.Responder(HttpStatusCode.OK, RespostasOpenMeteo.DiasInteiros(_dia, 1, _ => (20.0, 50, 0.0)));
                handler.Atrasar(TimeSpan.FromSeconds(5));
                tempoLimite = TimeSpan.FromMilliseconds(100);
                break;
            case "jsonQuebrado":
                handler.Responder(HttpStatusCode.OK, "{\"hourly\":{\"time\":[");
                break;
            case "campoAusente":
                handler.Responder(HttpStatusCode.OK, "{\"hourly\":{" + tempo + ",\"temperature_2m\":[20.0],\"relative_humidity_2m\":[50]}}");
                break;
            case "valorNulo":
                handler.Responder(HttpStatusCode.OK, "{\"hourly\":{" + tempo + ",\"temperature_2m\":[null],\"relative_humidity_2m\":[50],\"precipitation\":[0.0]}}");
                break;
            case "tamanhosDiferentes":
                handler.Responder(HttpStatusCode.OK, "{\"hourly\":{" + tempo + ",\"temperature_2m\":[20.0,21.0],\"relative_humidity_2m\":[50],\"precipitation\":[0.0]}}");
                break;
            default:
                // Só as 18h; o turno vai até as 20h.
                handler.Responder(HttpStatusCode.OK, RespostasOpenMeteo.Horaria([(_dia.AddHours(18), 20.0, 50, 0.0)]));
                break;
        }

        var logger = new FakeLogger<PrevisaoTempoComReserva>();
        using HttpClient http = RespostasOpenMeteo.Http(handler, tempoLimite);
        PrevisaoTempoComReserva reserva = Reserva(http, logger);

        // Act
        PrevisaoTurno previsao = await reserva.ObterAsync(_dia.AddHours(18), _dia.AddHours(20), CancellationToken.None);

        // Assert
        Assert.AreEqual(PrevisaoTempoSimulada.Exemplo, previsao);
        RegistroLogFake aviso = logger.Registros.Single();
        Assert.AreEqual(LogLevel.Warning, aviso.Nivel);
        Assert.Contains($"motivo {motivo}", aviso.Mensagem);
        Assert.Contains(" ms)", aviso.Mensagem);
        Assert.DoesNotContain("-23.5641", aviso.Mensagem);
        Assert.DoesNotContain("-46.6525", aviso.Mensagem);
    }

    /// <summary>
    /// Garante que, com a Open-Meteo no ar, a reserva devolve a previsão real e registra o sucesso.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_OpenMeteoNoAr_UsaAPrevisaoReal()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        handler.Responder(HttpStatusCode.OK, RespostasOpenMeteo.DiasInteiros(_dia, 1, _ => (27.3, 66, 0.8)));
        var logger = new FakeLogger<PrevisaoTempoComReserva>();

        // Act
        using HttpClient http = RespostasOpenMeteo.Http(handler);
        PrevisaoTurno previsao = await Reserva(http, logger).ObterAsync(_dia.AddHours(18), _dia.AddHours(20), CancellationToken.None);

        // Assert
        Assert.AreEqual(new PrevisaoTurno(0.8, 27.3, 66), previsao);
        Assert.AreEqual(LogLevel.Information, logger.Registros.Single().Nivel);
    }

    /// <summary>
    /// Garante que o cancelamento da requisição do cliente não vira reserva: propaga.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ObterAsync_ClienteCancelou_Propaga()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        var logger = new FakeLogger<PrevisaoTempoComReserva>();
        using var cancelado = new CancellationTokenSource();
        await cancelado.CancelAsync();

        // Act e Assert
        using HttpClient http = RespostasOpenMeteo.Http(handler);
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Reserva(http, logger).ObterAsync(_dia.AddHours(18), _dia.AddHours(20), cancelado.Token));
        Assert.IsEmpty(logger.Registros);
    }

    /// <summary>
    /// Garante a previsão simulada forçada: o turno usa a simulada sem nenhuma chamada HTTP, e o turno
    /// do exemplo do contrato continua com o índice 60.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task Registrar_SimuladaForcada_SemChamadaHttpEIndice60()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        var servicos = new ServiceCollection();
        servicos.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        servicos.AddLogging();
        servicos.AddSingleton(TimeProvider.System);
        ConfiguracaoPrevisao.Registrar(servicos, simuladaForcada: true);
        servicos.AddHttpClient<ClienteOpenMeteo>().ConfigurePrimaryHttpMessageHandler(() => handler);
        await using ServiceProvider provedor = servicos.BuildServiceProvider();
        IPrevisaoTempo previsao = provedor.GetRequiredService<IPrevisaoTempo>();

        var entregadores = new FakeEntregadorRepositorio();
        Entregador entregador = CenarioSos.NovoEntregador();
        entregadores.Semear(entregador);
        var turnos = new TurnoServico(
            new FakeTurnoRepositorio(),
            entregadores,
            new FakeDispositivoRepositorio(),
            new FakeOcorrenciaRepositorio(),
            previsao,
            new ServicoRiscoSimulado(),
            new FakePublicadorComandos(),
            new FakeUnidadeDeTrabalho(),
            new FakeAutorizacaoLivre(),
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 19, 0, 0, TimeSpan.Zero)));

        // Act
        RiscoTurnoResposta risco = await turnos.IniciarAsync(
            new IniciarTurnoRequisicao
            {
                EntregadorId = entregador.Id,
                Inicio = new DateTime(2026, 11, 12, 20, 0, 0, DateTimeKind.Utc),
                FimPrevisto = new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc),
                ProtecaoAtiva = false,
            },
            CancellationToken.None);

        // Assert
        Assert.IsInstanceOfType<PrevisaoTempoSimulada>(previsao);
        Assert.AreEqual(60, risco.IndiceRisco);
        Assert.IsEmpty(handler.Requisicoes);
    }

    /// <summary>
    /// Garante que, sem a forçada, o turno usa a previsão com reserva sobre a Open-Meteo.
    /// </summary>
    [TestMethod]
    public void Registrar_SemForcada_UsaAReserva()
    {
        // Arrange
        var servicos = new ServiceCollection();
        servicos.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        servicos.AddLogging();
        servicos.AddSingleton(TimeProvider.System);
        ConfiguracaoPrevisao.Registrar(servicos, simuladaForcada: false);
        using ServiceProvider provedor = servicos.BuildServiceProvider();
        using IServiceScope escopo = provedor.CreateScope();

        // Act
        IPrevisaoTempo previsao = escopo.ServiceProvider.GetRequiredService<IPrevisaoTempo>();

        // Assert
        Assert.IsInstanceOfType<PrevisaoTempoComReserva>(previsao);
    }

    private static PrevisaoTempoComReserva Reserva(HttpClient http, FakeLogger<PrevisaoTempoComReserva> logger)
    {
        return new PrevisaoTempoComReserva(
            new PrevisaoTempoOpenMeteo(RespostasOpenMeteo.Cliente(http)),
            new PrevisaoTempoSimulada(),
            TimeProvider.System,
            logger);
    }
}
