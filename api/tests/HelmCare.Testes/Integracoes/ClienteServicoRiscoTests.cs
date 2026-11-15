using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Integracoes.ServicoRisco;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Api.Servicos.Risco;
using HelmCare.Dominio.Enums;
using HelmCare.Testes.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Integracoes;

/// <summary>
/// Testes do <see cref="ClienteServicoRisco"/> sem rede: o HttpClient recebe um
/// <see cref="FakeHttpMessageHandler"/>.
/// </summary>
[TestClass]
public sealed class ClienteServicoRiscoTests
{
    /// <summary>
    /// Resposta do exemplo de referência (servico-risco/README.md).
    /// </summary>
    public const string RespostaReferencia = """
        {
          "indiceRisco": 60,
          "faixaRisco": "ALTO",
          "componentes": { "sinistro": 84, "chuva": 70, "calor": 0 },
          "explicacao": "Risco alto: chuva prevista no turno e o seu horário tem histórico de sinistros graves (pico às 19h)."
        }
        """;

    /// <summary>
    /// Explicação do exemplo de referência.
    /// </summary>
    public const string ExplicacaoReferencia =
        "Risco alto: chuva prevista no turno e o seu horário tem histórico de sinistros graves (pico às 19h).";

    /// <summary>
    /// URL base usada nos testes (a mesma do padrão da configuração).
    /// </summary>
    public static readonly Uri UrlBase = new("http://127.0.0.1:8000/risco/v1/");

    /// <summary>
    /// Início do turno do exemplo de referência (quinta, 17h em São Paulo).
    /// </summary>
    public static readonly DateTime InicioReferencia = new(2026, 11, 12, 20, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Fim do turno do exemplo de referência (21h em São Paulo).
    /// </summary>
    public static readonly DateTime FimReferencia = new(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc);

    private const string RespostaTrechos = """
        {
          "periodo": "2019 a 2025",
          "trechos": [
            { "posicao": 1, "logradouro": "SP 270 (RODOVIA RAPOSO TAVARES)", "latitude": -23.58341, "longitude": -46.77771, "quantidadeSinistrosGraves": 46 },
            { "posicao": 2, "logradouro": "SP 270 (RODOVIA RAPOSO TAVARES)", "latitude": -23.58481, "longitude": -46.78738, "quantidadeSinistrosGraves": 38 }
          ]
        }
        """;

    /// <summary>
    /// Cria o HttpClient do teste, com a URL base do serviço e o handler falso.
    /// </summary>
    /// <param name="handler">Handler falso.</param>
    /// <returns>O HttpClient (quem chama descarta).</returns>
    public static HttpClient Http(FakeHttpMessageHandler handler)
    {
        return new HttpClient(handler, disposeHandler: false) { BaseAddress = UrlBase };
    }

    /// <summary>
    /// Garante que o 200 do exemplo de referência vira 60, ALTO, 84/70/0 e a mesma explicação, e
    /// que a requisição é um POST para .../risco/v1/indice-turno com as datas em Z e a previsão.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularIndiceAsync_RespostaDeReferencia_DevolveOResultadoEEnviaOPost()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        using HttpClient http = Http(handler);
        handler.Responder(HttpStatusCode.OK, RespostaReferencia);
        var cliente = new ClienteServicoRisco(http);

        // Act
        ResultadoRisco resultado = await cliente.CalcularIndiceAsync(
            InicioReferencia, FimReferencia, PrevisaoTempoSimulada.Exemplo, CancellationToken.None);

        // Assert
        Assert.AreEqual(new ResultadoRisco(60, FaixaRisco.Alto, 84, 70, 0, ExplicacaoReferencia), resultado);

        RequisicaoRecebidaFake requisicao = handler.Requisicoes[0];
        Assert.AreEqual(HttpMethod.Post, requisicao.Metodo);
        Assert.AreEqual(new Uri("http://127.0.0.1:8000/risco/v1/indice-turno"), requisicao.Endereco);

        using JsonDocument corpo = JsonDocument.Parse(requisicao.Corpo ?? string.Empty);
        JsonElement raiz = corpo.RootElement;
        Assert.AreEqual("2026-11-12T20:00:00Z", raiz.GetProperty("inicio").GetString());
        Assert.AreEqual("2026-11-13T00:00:00Z", raiz.GetProperty("fim").GetString());
        JsonElement previsao = raiz.GetProperty("previsao");
        Assert.AreEqual(7.0, previsao.GetProperty("chuvaMmHora").GetDouble());
        Assert.AreEqual(24.0, previsao.GetProperty("temperaturaC").GetDouble());
        Assert.AreEqual(85.0, previsao.GetProperty("umidadePct").GetDouble());
    }

    /// <summary>
    /// Garante que a fração de segundo vai com no máximo 6 casas (precisão do datetime do Python), em Z.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularIndiceAsync_InicioComFracao_EnviaAteMicrossegundosEmZ()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        using HttpClient http = Http(handler);
        handler.Responder(HttpStatusCode.OK, RespostaReferencia);
        var cliente = new ClienteServicoRisco(http);
        DateTime inicio = InicioReferencia.AddTicks(9_162_925);

        // Act
        await cliente.CalcularIndiceAsync(inicio, FimReferencia, PrevisaoTempoSimulada.Exemplo, CancellationToken.None);

        // Assert
        using JsonDocument corpo = JsonDocument.Parse(handler.Requisicoes[0].Corpo ?? string.Empty);
        Assert.AreEqual("2026-11-12T20:00:00.916292Z", corpo.RootElement.GetProperty("inicio").GetString());
    }

    /// <summary>
    /// Garante que 503 (modelo não carregado) e 500 dão ServicoIndisponivel, com o status.
    /// </summary>
    /// <param name="status">Status da resposta.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(503)]
    [DataRow(500)]
    public async Task CalcularIndiceAsync_Erro5xx_LancaServicoIndisponivel(int status)
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        using HttpClient http = Http(handler);
        handler.Responder((HttpStatusCode)status, """{"title":"Serviço indisponível","status":503}""", "application/problem+json");

        // Act
        ServicoRiscoIndisponivelException excecao = await CalcularEsperandoFalhaAsync(http);

        // Assert
        Assert.AreEqual(MotivoFalhaServicoRisco.ServicoIndisponivel, excecao.Motivo);
        Assert.AreEqual(status, excecao.StatusHttp);
    }

    /// <summary>
    /// Garante que a conexão recusada (HttpRequestException) dá ErroDeRede, sem status.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularIndiceAsync_ConexaoRecusada_LancaErroDeRede()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        using HttpClient http = Http(handler);
        handler.LancarErroDeRede();

        // Act
        ServicoRiscoIndisponivelException excecao = await CalcularEsperandoFalhaAsync(http);

        // Assert
        Assert.AreEqual(MotivoFalhaServicoRisco.ErroDeRede, excecao.Motivo);
        Assert.IsNull(excecao.StatusHttp);
    }

    /// <summary>
    /// Garante que o tempo esgotado do HttpClient (TaskCanceledException sem cancelamento de quem
    /// chamou) dá TempoEsgotado.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularIndiceAsync_TempoEsgotado_LancaTempoEsgotado()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        using HttpClient http = Http(handler);
        handler.LancarTempoEsgotado();

        // Act
        ServicoRiscoIndisponivelException excecao = await CalcularEsperandoFalhaAsync(http);

        // Assert
        Assert.AreEqual(MotivoFalhaServicoRisco.TempoEsgotado, excecao.Motivo);
        Assert.IsNull(excecao.StatusHttp);
    }

    /// <summary>
    /// Garante que o 400 dá ErroDeIntegracao com os nomes dos campos do errors, nunca os valores.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularIndiceAsync_Erro400_LancaErroDeIntegracaoSoComOsNomesDosCampos()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        using HttpClient http = Http(handler);
        handler.Responder(
            HttpStatusCode.BadRequest,
            """{"title":"Requisição inválida","status":400,"errors":{"fim":["O fim do turno deve ser depois do início."]}}""",
            "application/problem+json");

        // Act
        ServicoRiscoIndisponivelException excecao = await CalcularEsperandoFalhaAsync(http);

        // Assert
        Assert.AreEqual(MotivoFalhaServicoRisco.ErroDeIntegracao, excecao.Motivo);
        Assert.AreEqual(400, excecao.StatusHttp);
        Assert.AreEqual("fim", excecao.CamposComErro[0]);
        Assert.HasCount(1, excecao.CamposComErro);
        Assert.DoesNotContain("depois do início", excecao.Message);
    }

    /// <summary>
    /// Garante que JSON inválido, índice fora de 0 a 100, campo faltando e explicação vazia dão RespostaInvalida.
    /// </summary>
    /// <param name="corpo">Corpo da resposta 200.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("{ isto não é json")]
    [DataRow("""{"indiceRisco":150,"faixaRisco":"MUITO_ALTO","componentes":{"sinistro":84,"chuva":70,"calor":0},"explicacao":"Risco muito alto: chuva prevista no turno."}""")]
    [DataRow("""{"indiceRisco":60,"faixaRisco":"ALTO","explicacao":"Risco alto: chuva prevista no turno."}""")]
    [DataRow("""{"indiceRisco":60,"faixaRisco":"ALTO","componentes":{"sinistro":84,"chuva":70,"calor":0},"explicacao":"  "}""")]
    [DataRow("""{"indiceRisco":60,"faixaRisco":"ALTISSIMO","componentes":{"sinistro":84,"chuva":70,"calor":0},"explicacao":"Risco alto."}""")]
    [DataRow("null")]
    public async Task CalcularIndiceAsync_RespostaForaDoFormato_LancaRespostaInvalida(string corpo)
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        using HttpClient http = Http(handler);
        handler.Responder(HttpStatusCode.OK, corpo);

        // Act
        ServicoRiscoIndisponivelException excecao = await CalcularEsperandoFalhaAsync(http);

        // Assert
        Assert.AreEqual(MotivoFalhaServicoRisco.RespostaInvalida, excecao.Motivo);
        Assert.AreEqual(200, excecao.StatusHttp);
    }

    /// <summary>
    /// Garante que o cancelamento de quem chamou sobe como OperationCanceledException, e não como
    /// falha do serviço.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task CalcularIndiceAsync_QuemChamouCancelou_PropagaOperationCanceled()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        using HttpClient http = Http(handler);
        handler.Responder(HttpStatusCode.OK, RespostaReferencia);
        var cliente = new ClienteServicoRisco(http);
        using var cancelamento = new CancellationTokenSource();
        await cancelamento.CancelAsync();

        // Act e Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => cliente.CalcularIndiceAsync(
            InicioReferencia, FimReferencia, PrevisaoTempoSimulada.Exemplo, cancelamento.Token));
    }

    /// <summary>
    /// Garante que o limite vai na query do GET e que os trechos voltam no formato e na ordem da seção 8.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ListarTrechosCriticosAsync_Limite2_EnviaNaQueryEDevolveNaOrdem()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        using HttpClient http = Http(handler);
        handler.Responder(HttpStatusCode.OK, RespostaTrechos);
        var cliente = new ClienteServicoRisco(http);

        // Act
        ListaTrechosCriticos lista = await cliente.ListarTrechosCriticosAsync(2, CancellationToken.None);

        // Assert
        RequisicaoRecebidaFake requisicao = handler.Requisicoes[0];
        Assert.AreEqual(HttpMethod.Get, requisicao.Metodo);
        Assert.AreEqual(new Uri("http://127.0.0.1:8000/risco/v1/trechos-criticos?limite=2"), requisicao.Endereco);
        Assert.AreEqual("2019 a 2025", lista.Periodo);
        Assert.HasCount(2, lista.Trechos);
        Assert.AreEqual(
            new TrechoCritico(1, "SP 270 (RODOVIA RAPOSO TAVARES)", -23.58341, -46.77771, 46),
            lista.Trechos[0]);
        Assert.AreEqual(
            new TrechoCritico(2, "SP 270 (RODOVIA RAPOSO TAVARES)", -23.58481, -46.78738, 38),
            lista.Trechos[1]);
    }

    /// <summary>
    /// Garante que trechos sem período ou com trecho incompleto dão RespostaInvalida.
    /// </summary>
    /// <param name="corpo">Corpo da resposta 200.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow("""{"trechos":[]}""")]
    [DataRow("""{"periodo":"2019 a 2025","trechos":[{"posicao":1,"logradouro":null,"latitude":-23.5,"longitude":-46.7,"quantidadeSinistrosGraves":46}]}""")]
    public async Task ListarTrechosCriticosAsync_RespostaForaDoFormato_LancaRespostaInvalida(string corpo)
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        using HttpClient http = Http(handler);
        handler.Responder(HttpStatusCode.OK, corpo);
        var cliente = new ClienteServicoRisco(http);

        // Act
        ServicoRiscoIndisponivelException excecao = await Assert.ThrowsExactlyAsync<ServicoRiscoIndisponivelException>(
            () => cliente.ListarTrechosCriticosAsync(2, CancellationToken.None));

        // Assert
        Assert.AreEqual(MotivoFalhaServicoRisco.RespostaInvalida, excecao.Motivo);
    }

    private static Task<ServicoRiscoIndisponivelException> CalcularEsperandoFalhaAsync(HttpClient http)
    {
        var cliente = new ClienteServicoRisco(http);
        return Assert.ThrowsExactlyAsync<ServicoRiscoIndisponivelException>(() => cliente.CalcularIndiceAsync(
            InicioReferencia, FimReferencia, PrevisaoTempoSimulada.Exemplo, CancellationToken.None));
    }
}
