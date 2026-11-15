using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HelmCare.Api.Contratos.TrechosCriticos;
using HelmCare.Api.Integracoes.ServicoRisco;
using HelmCare.Api.Servicos.Comum;
using HelmCare.Api.Servicos.TrechosCriticos;
using HelmCare.Dominio.Comum;
using HelmCare.Testes.Fakes;
using HelmCare.Testes.Integracoes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HelmCare.Testes.Servicos;

/// <summary>
/// Testes do <see cref="TrechosCriticosServico"/>, com o cliente HTTP de verdade e um handler falso.
/// </summary>
[TestClass]
public sealed class TrechosCriticosServicoTests
{
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
    /// Garante que a lista volta no formato da resposta da API, na ordem recebida.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ListarAsync_ServicoNoAr_DevolveNaOrdemRecebida()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        using HttpClient http = ClienteServicoRiscoTests.Http(handler);
        handler.Responder(HttpStatusCode.OK, RespostaTrechos);
        var logger = new FakeLogger<TrechosCriticosServico>();
        TrechosCriticosServico servico = Criar(http, logger);

        // Act
        TrechosCriticosResposta resposta = await servico.ListarAsync(2, CancellationToken.None);

        // Assert
        Assert.AreEqual("2019 a 2025", resposta.Periodo);
        Assert.AreEqual("1 2", string.Join(' ', resposta.Trechos.Select(t => t.Posicao)));
        Assert.AreEqual(46, resposta.Trechos[0].QuantidadeSinistrosGraves);
        Assert.AreEqual(-46.78738, resposta.Trechos[1].Longitude);
        Assert.AreEqual(LogLevel.Information, logger.Registros.Single().Nivel);
    }

    /// <summary>
    /// Garante o 400 para limite fora de 1 a 50, sem chamar o serviço.
    /// </summary>
    /// <param name="limite">Limite inválido.</param>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    [DataRow(0)]
    [DataRow(51)]
    public async Task ListarAsync_LimiteForaDaFaixa_Lanca400SemChamarOServico(int limite)
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        using HttpClient http = ClienteServicoRiscoTests.Http(handler);
        TrechosCriticosServico servico = Criar(http, new FakeLogger<TrechosCriticosServico>());

        // Act
        ValidacaoDominioException excecao = await Assert.ThrowsExactlyAsync<ValidacaoDominioException>(
            () => servico.ListarAsync(limite, CancellationToken.None));

        // Assert
        Assert.Contains("entre 1 e 50", excecao.Message);
        Assert.IsEmpty(handler.Requisicoes);
    }

    /// <summary>
    /// Garante que a falha do serviço sobe (vira 503 no tratador), com aviso no log e sem versão simulada.
    /// </summary>
    /// <returns>A tarefa do teste.</returns>
    [TestMethod]
    public async Task ListarAsync_ServicoFora_PropagaComAviso()
    {
        // Arrange
        using var handler = new FakeHttpMessageHandler();
        using HttpClient http = ClienteServicoRiscoTests.Http(handler);
        handler.LancarErroDeRede();
        var logger = new FakeLogger<TrechosCriticosServico>();
        TrechosCriticosServico servico = Criar(http, logger);

        // Act
        ServicoRiscoIndisponivelException excecao = await Assert.ThrowsExactlyAsync<ServicoRiscoIndisponivelException>(
            () => servico.ListarAsync(2, CancellationToken.None));

        // Assert
        Assert.AreEqual(MotivoFalhaServicoRisco.ErroDeRede, excecao.Motivo);
        RegistroLogFake registro = logger.Registros.Single();
        Assert.AreEqual(LogLevel.Warning, registro.Nivel);
        Assert.Contains("motivo ErroDeRede", registro.Mensagem);
    }

    private static TrechosCriticosServico Criar(HttpClient http, FakeLogger<TrechosCriticosServico> logger)
    {
        return new TrechosCriticosServico(new ClienteServicoRisco(http), new FakeTimeProvider(), logger);
    }
}
