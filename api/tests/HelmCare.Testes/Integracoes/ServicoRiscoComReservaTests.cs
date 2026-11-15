using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Integracoes.ServicoRisco;
using HelmCare.Api.Servicos.Risco;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Integracoes;

/// <summary>
/// Testes do <see cref="ServicoRiscoComReserva"/>: o cliente HTTP de verdade com um handler falso e
/// a versão simulada de verdade como reserva.
/// </summary>
[TestClass]
public sealed class ServicoRiscoComReservaTests
{
    // Resposta propositalmente diferente da simulada, para provar que veio do serviço.
    private const string RespostaDoServico = """
        {
          "indiceRisco": 61,
          "faixaRisco": "ALTO",
          "componentes": { "sinistro": 86, "chuva": 70, "calor": 0 },
          "explicacao": "Risco alto: texto vindo do serviço."
        }
        """;

    /// <summary>
    /// Garante que, no sucesso, devolve o resultado do serviço e registra a informação com o tempo.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularAsync_ServicoNoAr_DevolveOResultadoDoServico()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        using HttpClient http = ClienteServicoRiscoTests.Http(handler);
        handler.Responder(HttpStatusCode.OK, RespostaDoServico);
        var logger = new FakeLogger<ServicoRiscoComReserva>();
        ServicoRiscoComReserva servico = Criar(http, logger);

        // Act
        ResultadoRisco resultado = await CalcularReferenciaAsync(servico, CancellationToken.None);

        // Assert
        Assert.AreEqual(new ResultadoRisco(61, FaixaRisco.Alto, 86, 70, 0, "Risco alto: texto vindo do serviço."), resultado);
        RegistroLogFake registro = logger.Registros.Single();
        Assert.AreEqual(LogLevel.Information, registro.Nivel);
        Assert.StartsWith("Índice de Risco calculado pelo Serviço de Risco em ", registro.Mensagem);
    }

    /// <summary>
    /// Garante que cada motivo de falha devolve o resultado da simulada (60 e a explicação do
    /// exemplo de referência) sem lançar, com aviso para as falhas do serviço e erro para as de integração.
    /// </summary>
    /// <param name="caso">Falha simulada no handler.</param>
    /// <param name="motivo">Motivo esperado no log.</param>
    /// <param name="nivel">Nível esperado do log.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("503", "ServicoIndisponivel", LogLevel.Warning)]
    [DataRow("500", "ServicoIndisponivel", LogLevel.Warning)]
    [DataRow("rede", "ErroDeRede", LogLevel.Warning)]
    [DataRow("tempo", "TempoEsgotado", LogLevel.Warning)]
    [DataRow("400", "ErroDeIntegracao", LogLevel.Error)]
    [DataRow("json", "RespostaInvalida", LogLevel.Error)]
    public async Task CalcularAsync_ServicoFalhou_DevolveASimuladaComOLogCerto(string caso, string motivo, LogLevel nivel)
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        using HttpClient http = ClienteServicoRiscoTests.Http(handler);
        ConfigurarFalha(handler, caso);
        var logger = new FakeLogger<ServicoRiscoComReserva>();
        ServicoRiscoComReserva servico = Criar(http, logger);

        // Act
        ResultadoRisco resultado = await CalcularReferenciaAsync(servico, CancellationToken.None);

        // Assert
        Assert.AreEqual(
            new ResultadoRisco(60, FaixaRisco.Alto, 84, 70, 0, ClienteServicoRiscoTests.ExplicacaoReferencia),
            resultado);
        RegistroLogFake registro = logger.Registros.Single();
        Assert.AreEqual(nivel, registro.Nivel);
        Assert.Contains($"motivo {motivo}", registro.Mensagem);
        Assert.Contains("versão simulada", registro.Mensagem);
    }

    /// <summary>
    /// Garante que o log do 400 leva o status e os nomes dos campos, mas não os valores nem o corpo.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularAsync_Erro400_LogComStatusECamposSemValores()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        using HttpClient http = ClienteServicoRiscoTests.Http(handler);
        ConfigurarFalha(handler, "400");
        var logger = new FakeLogger<ServicoRiscoComReserva>();
        ServicoRiscoComReserva servico = Criar(http, logger);

        // Act
        await CalcularReferenciaAsync(servico, CancellationToken.None);

        // Assert
        string mensagem = logger.Registros.Single().Mensagem;
        Assert.Contains("status 400", mensagem);
        Assert.Contains("campos fim", mensagem);
        Assert.DoesNotContain("depois do início", mensagem);
        Assert.DoesNotContain("2026-11-12", mensagem);
    }

    /// <summary>
    /// Garante que o cancelamento de quem chamou propaga e não usa a simulada (nem registra falha).
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularAsync_QuemChamouCancelou_PropagaSemUsarASimulada()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        using HttpClient http = ClienteServicoRiscoTests.Http(handler);
        handler.Responder(HttpStatusCode.OK, RespostaDoServico);
        var logger = new FakeLogger<ServicoRiscoComReserva>();
        ServicoRiscoComReserva servico = Criar(http, logger);
        using var cancelamento = new CancellationTokenSource();
        await cancelamento.CancelAsync();

        // Act e Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => CalcularReferenciaAsync(servico, cancelamento.Token));
        Assert.IsEmpty(logger.Registros);
    }

    private static ServicoRiscoComReserva Criar(HttpClient http, FakeLogger<ServicoRiscoComReserva> logger)
    {
        return new ServicoRiscoComReserva(
            new ClienteServicoRisco(http),
            new ServicoRiscoSimulado(),
            new FakeTimeProvider(),
            logger);
    }

    private static Task<ResultadoRisco> CalcularReferenciaAsync(ServicoRiscoComReserva servico, CancellationToken cancellationToken)
    {
        return servico.CalcularAsync(
            ClienteServicoRiscoTests.InicioReferencia,
            ClienteServicoRiscoTests.FimReferencia,
            PrevisaoTempoSimulada.Exemplo,
            cancellationToken);
    }

    private static void ConfigurarFalha(FakeHttpMessageHandler handler, string caso)
    {
        switch (caso)
        {
            case "rede":
                handler.LancarErroDeRede();
                break;
            case "tempo":
                handler.LancarTempoEsgotado();
                break;
            case "400":
                handler.Responder(
                    HttpStatusCode.BadRequest,
                    """{"status":400,"errors":{"fim":["O fim do turno deve ser depois do início."]}}""",
                    "application/problem+json");
                break;
            case "json":
                handler.Responder(HttpStatusCode.OK, "{ isto não é json");
                break;
            default:
                handler.Responder((HttpStatusCode)int.Parse(caso, CultureInfo.InvariantCulture), "{}");
                break;
        }
    }
}
